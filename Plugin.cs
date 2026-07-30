using CrescentAtlas.Collection;
using CrescentAtlas.Contracts;
using CrescentAtlas.Data;
using CrescentAtlas.Events;
using CrescentAtlas.Notifications;
using CrescentAtlas.Overlays;
using CrescentAtlas.Runtime;
using CrescentAtlas.Windows;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace CrescentAtlas;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/catlas";
    private const string NorthHornInstanceKey = "territory-1346";
    private const float TreasureCandidateCheckRadius = 70.0f;
    private const float TreasureCandidateObjectMatchRadius = 12.0f;
    private static readonly HashSet<uint> MagicPotEventIds = [2072, 2073];
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);
    private static readonly PotObservation[] LearnedNorthHornPotObservations =
    [
        new(
            NorthHornInstanceKey,
            new DateTimeOffset(2026, 7, 30, 3, 16, 5, 206, TimeSpan.Zero),
            2072,
            new Vector3(233.0f, 7.729229f, -470.0f)),
        new(
            NorthHornInstanceKey,
            new DateTimeOffset(2026, 7, 30, 3, 46, 9, 584, TimeSpan.Zero),
            2073,
            new Vector3(-505.2822f, 53.14409f, 244.041f)),
    ];

    [PluginService] private static IDalamudPluginInterface PluginInterface { get; set; } = null!;
    [PluginService] private static ICommandManager CommandManager { get; set; } = null!;
    [PluginService] private static IFramework Framework { get; set; } = null!;
    [PluginService] private static IClientState ClientState { get; set; } = null!;
    [PluginService] private static IDataManager DataManager { get; set; } = null!;
    [PluginService] private static IObjectTable ObjectTable { get; set; } = null!;
    [PluginService] private static IFateTable FateTable { get; set; } = null!;
    [PluginService] private static IGameGui GameGui { get; set; } = null!;
    [PluginService] private static ITextureProvider TextureProvider { get; set; } = null!;
    [PluginService] private static IChatGui ChatGui { get; set; } = null!;
    [PluginService] private static IPluginLog Log { get; set; } = null!;

    private readonly Configuration configuration;
    private readonly ObservationStore observationStore;
    private readonly IslandVisitStore islandVisitStore;
    private readonly HashSet<uint> silverTreasureDataIds = [];
    private readonly HashSet<uint> potTargetDataIds = [];
    private readonly MutableAtlasDataSource atlasData = new();
    private readonly OccultCrescentContext crescentContext;
    private readonly AetheryteMarkerProvider aetheryteMarkerProvider;
    private readonly LayoutTreasureCandidateScanner layoutScanner;
    private readonly ObjectTableCollector objectCollector;
    private readonly DalamudFateSnapshotSource fateSource;
    private readonly DynamicEventSnapshotSource encounterSource = new();
    private readonly FateEventDetector fateDetector;
    private readonly CriticalEncounterDetector encounterDetector;
    private readonly PotPredictionTracker potPredictionTracker = new();
    private readonly WindowSystem windowSystem = new("CrescentAtlas");
    private readonly AtlasWindow atlasWindow;
    private readonly NearbyTreasureLineOverlay treasureLineOverlay;
    private HashSet<string> previousTreasureKeys = new(StringComparer.Ordinal);
    private HashSet<string> previousCarrotKeys = new(StringComparer.Ordinal);
    private HashSet<string> previousPotTargetKeys = new(StringComparer.Ordinal);
    private DateTimeOffset nextPollUtc;
    private DateTimeOffset nextFlushUtc;
    private DateTimeOffset nextLayoutScanUtc;
    private uint scannedTerritoryId;
    private uint scannedAetheryteMapId;
    private bool wasActive;
    private bool firstUpdateLogged;
    private bool firstWindowDrawLogged;
    private bool firstOverlayDrawLogged;

    public Plugin()
    {
        BootstrapDiagnostics.Initialize(PluginInterface);
        BootstrapDiagnostics.Write("constructor entered");
        try
        {
            configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            BootstrapDiagnostics.Write($"configuration loaded; version={configuration.Version}");
            if (configuration.Version < 2)
            {
                // Existing builds defaulted to click-through, which also prevented
                // ImGui's window border from receiving resize drags.
                configuration.MapClickThrough = false;
                configuration.Version = 2;
                SaveConfiguration();
            }

            SeedLearnedPotObservations();
            BootstrapDiagnostics.Write("Magic Pot seed initialized");
            silverTreasureDataIds.UnionWith(
                ConfirmedSilverTreasureSpots.NorthHorn.Select(spot => spot.DataId));
            silverTreasureDataIds.UnionWith(ConfirmedSilverTreasureSpots.EventObjectDataIds);
            potTargetDataIds.UnionWith(
                ConfirmedPotTargetObservations.NorthHorn.Select(observation => observation.DataId));
            BootstrapDiagnostics.Write(
                $"confirmed treasure data initialized; silver={silverTreasureDataIds.Count}; " +
                $"potTarget={potTargetDataIds.Count}");
            observationStore = new ObservationStore(PluginInterface);
            BootstrapDiagnostics.Write($"observation store initialized; session={observationStore.SessionId}");
            islandVisitStore = new IslandVisitStore(observationStore.OutputDirectory);
            BootstrapDiagnostics.Write($"island visit store initialized; path={islandVisitStore.OutputPath}");
            RestorePotObservations();
            crescentContext = new OccultCrescentContext(ClientState, DataManager, Log);
            aetheryteMarkerProvider = new AetheryteMarkerProvider(DataManager);
            layoutScanner = new LayoutTreasureCandidateScanner(DataManager, Log);
            objectCollector = new ObjectTableCollector(
                ObjectTable,
                new ConditionalObservationSink(
                    observationStore,
                    () => configuration.CollectionEnabled),
                new ObjectTableCollectionOptions
                {
                    CarrotDataIds = configuration.ConfirmedCarrotDataIds,
                    CarrotEventIds = configuration.ConfirmedCarrotEventIds,
                    SilverTreasureDataIds = silverTreasureDataIds,
                    PotTargetDataIds = potTargetDataIds,
                    IncludeUnclassifiedEventObjects = true,
                });
            BootstrapDiagnostics.Write("collectors initialized");
            fateSource = new DalamudFateSnapshotSource(FateTable);
            fateDetector = new FateEventDetector(fateSource, observationStore.SessionId);
            encounterDetector = new CriticalEncounterDetector(encounterSource, observationStore.SessionId);
            BootstrapDiagnostics.Write("event detectors initialized");

            atlasWindow = new AtlasWindow(
                atlasData,
                configuration,
                DataManager,
                ClientState,
                TextureProvider,
                islandVisitStore.GetVisitsDescending,
                SaveConfiguration);
            treasureLineOverlay = new NearbyTreasureLineOverlay(GameGui, atlasData);
            windowSystem.AddWindow(atlasWindow);
            BootstrapDiagnostics.Write("atlas window and overlay initialized");

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Crescent Atlas: map, collection and status controls.",
            });
            PluginInterface.UiBuilder.Draw += DrawWindowsWithDiagnostics;
            PluginInterface.UiBuilder.Draw += DrawOverlayWithDiagnostics;
            PluginInterface.UiBuilder.OpenMainUi += ToggleMap;
            PluginInterface.UiBuilder.OpenConfigUi += ToggleMap;
            Framework.Update += OnFrameworkUpdate;
            BootstrapDiagnostics.Write("Dalamud callbacks registered");

            nextPollUtc = DateTimeOffset.UtcNow;
            nextFlushUtc = DateTimeOffset.UtcNow + FlushInterval;
            ChatGui.Print("[Crescent Atlas] Loaded. /catlas toggles the display-only map.");
            BootstrapDiagnostics.Write("constructor completed successfully");
        }
        catch (Exception ex)
        {
            BootstrapDiagnostics.WriteException("constructor", ex);
            throw;
        }
    }

    public void Dispose()
    {
        BootstrapDiagnostics.Write("dispose entered");
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleMap;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMap;
        PluginInterface.UiBuilder.Draw -= DrawOverlayWithDiagnostics;
        PluginInterface.UiBuilder.Draw -= DrawWindowsWithDiagnostics;
        CommandManager.RemoveHandler(CommandName);
        windowSystem.RemoveAllWindows();
        atlasWindow.Dispose();
        islandVisitStore.Dispose();
        observationStore.Dispose();
        BootstrapDiagnostics.Write("dispose completed");
    }

    private void DrawWindowsWithDiagnostics()
    {
        try
        {
            if (!firstWindowDrawLogged)
            {
                firstWindowDrawLogged = true;
                BootstrapDiagnostics.Write("first atlas window draw entered");
            }
            windowSystem.Draw();
        }
        catch (Exception ex)
        {
            BootstrapDiagnostics.WriteException("atlas window draw", ex);
            throw;
        }
    }

    private void DrawOverlayWithDiagnostics()
    {
        try
        {
            if (!firstOverlayDrawLogged)
            {
                firstOverlayDrawLogged = true;
                BootstrapDiagnostics.Write("first treasure overlay draw entered");
            }
            treasureLineOverlay.Draw();
        }
        catch (Exception ex)
        {
            BootstrapDiagnostics.WriteException("treasure overlay draw", ex);
            throw;
        }
    }

    private void ToggleMap()
    {
        configuration.MapVisible = !configuration.MapVisible;
        atlasWindow.IsOpen = configuration.MapVisible;
        SaveConfiguration();
    }

    private void OnCommand(string command, string arguments)
    {
        var parts = arguments.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var action = parts.FirstOrDefault()?.ToLowerInvariant() ?? "map";

        switch (action)
        {
            case "map":
                ToggleMap();
                break;
            case "collect":
                configuration.CollectionEnabled = ParseToggle(parts.Skip(1).FirstOrDefault(), configuration.CollectionEnabled);
                if (configuration.CollectionEnabled)
                    scannedTerritoryId = 0;
                SaveConfiguration();
                ChatGui.Print($"[Crescent Atlas] Collection {(configuration.CollectionEnabled ? "enabled" : "disabled")}.");
                break;
            case "click":
                configuration.MapClickThrough = !configuration.MapClickThrough;
                SaveConfiguration();
                ChatGui.Print($"[Crescent Atlas] Map click-through {(configuration.MapClickThrough ? "enabled" : "disabled")}.");
                break;
            case "flush":
                observationStore.Flush();
                ChatGui.Print($"[Crescent Atlas] Saved {observationStore.SessionObservationCount} observations.");
                break;
            case "folder":
                ChatGui.Print($"[Crescent Atlas] Collection folder: {observationStore.OutputDirectory}");
                break;
            case "status":
                var activeVisit = islandVisitStore.ActiveVisit;
                ChatGui.Print(
                    $"[Crescent Atlas] active={OccultCrescentContext.IsActive()}, territory={ClientState.TerritoryType}, " +
                    $"observations={observationStore.SessionObservationCount}, collection={configuration.CollectionEnabled}");
                ChatGui.Print(
                    $"[Crescent Atlas] visit={activeVisit?.VisitId ?? "none"}, " +
                    $"island={activeVisit?.IslandKey ?? "unknown"}");
                ChatGui.Print($"[Crescent Atlas] {atlasWindow.MapDiagnostic}");
                ChatGui.Print($"[Crescent Atlas] Diagnostic log: {BootstrapDiagnostics.LogPath}");
                break;
            case "log":
                ChatGui.Print($"[Crescent Atlas] Diagnostic log: {BootstrapDiagnostics.LogPath}");
                break;
            default:
                ChatGui.Print("[Crescent Atlas] /catlas [map|collect on|collect off|click|flush|folder|status|log]");
                break;
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!firstUpdateLogged)
        {
            firstUpdateLogged = true;
            BootstrapDiagnostics.Write("first framework update entered");
        }
        // Keep display-only guides tied to the live per-frame position. The
        // heavier scanners remain throttled by PollInterval below.
        var localPlayer = ObjectTable.LocalPlayer;
        atlasData.SetPlayerState(localPlayer?.Position, localPlayer?.Rotation);

        var now = DateTimeOffset.UtcNow;
        if (now < nextPollUtc)
            return;

        nextPollUtc = now + PollInterval;
        try
        {
            Poll(now);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Crescent Atlas update failed.");
            BootstrapDiagnostics.WriteException("framework update", ex);
        }
    }

    private void Poll(DateTimeOffset now)
    {
        var localPlayer = ObjectTable.LocalPlayer;
        var active = OccultCrescentContext.IsActive();
        if (!active)
        {
            if (wasActive)
            {
                islandVisitStore.EndVisit(now);
                islandVisitStore.Flush();
                ResetTerritoryState();
            }
            else if (ClientState.TerritoryType != 1346
                     && islandVisitStore.CloseUnfinishedVisitsAtLastSeen("not-in-content-on-start"))
            {
                islandVisitStore.Flush();
            }
            wasActive = false;
            atlasData.SetContext(
                ClientState.TerritoryType,
                crescentContext.TerritoryName,
                localPlayer?.Position,
                localPlayer?.Rotation);
            return;
        }

        var entering = !wasActive;
        wasActive = true;
        var territoryId = crescentContext.TerritoryId;
        var territoryName = crescentContext.TerritoryName;
        var instanceKey = $"territory-{territoryId}";
        var instanceSnapshot = OccultCrescentContext.ReadInstanceSnapshot();
        if (entering)
        {
            var visit = islandVisitStore.StartOrResume(
                territoryId,
                territoryName,
                now,
                instanceSnapshot);
            islandVisitStore.Flush();
            BootstrapDiagnostics.Write(
                $"Occult Crescent visit entered; visit={visit.VisitId}; island={visit.IslandKey}; " +
                $"instance={visit.InstancePointer}");
        }
        else
        {
            islandVisitStore.Touch(now, instanceSnapshot);
        }
        atlasData.SetContext(territoryId, territoryName, localPlayer?.Position, localPlayer?.Rotation);

        var mapId = ClientState.MapId;
        if (scannedAetheryteMapId != mapId
            && aetheryteMarkerProvider.TryRead(territoryId, mapId, now, out var aetherytes))
        {
            atlasData.ReplaceSource(AtlasMarkerKind.Aetheryte, aetherytes);
            scannedAetheryteMapId = mapId;
        }

        if (scannedTerritoryId != territoryId && now >= nextLayoutScanUtc)
        {
            var candidates = layoutScanner.Scan(
                observationStore.SessionId,
                territoryId,
                territoryName,
                now,
                out var candidateObservations);
            atlasData.ReplaceSource(AtlasMarkerKind.TreasureCandidate, candidates);
            foreach (var candidate in candidates.Where(candidate =>
                         candidate.TreasureType.Equals("silver", StringComparison.OrdinalIgnoreCase)))
                silverTreasureDataIds.Add(candidate.DataId);
            if (configuration.CollectionEnabled)
                RecordAll(candidateObservations);
            if (candidates.Count > 0)
                scannedTerritoryId = territoryId;
            else
                nextLayoutScanUtc = now + TimeSpan.FromSeconds(5);
        }

        var objectMarkers = objectCollector.Collect(territoryId, territoryName, now);
        var treasures = objectMarkers.Where(marker => marker.Kind == AtlasMarkerKind.ActiveTreasure).ToArray();
        var carrotCandidates = objectMarkers.Where(marker => marker.Kind == AtlasMarkerKind.Carrot).ToArray();
        var potTargets = objectMarkers.Where(marker => marker.Kind == AtlasMarkerKind.PotTarget).ToArray();
        var carrots = carrotCandidates
            .Where(marker => !marker.Key.Contains("carrot-candidate", StringComparison.Ordinal))
            .ToArray();
        atlasData.ReplaceSource(AtlasMarkerKind.ActiveTreasure, treasures);
        atlasData.ReplaceSource(AtlasMarkerKind.Carrot, carrots);
        atlasData.ReplaceSource(AtlasMarkerKind.PotTarget, potTargets);
        if (localPlayer is not null)
            atlasData.MarkAbsentNearbyTreasureCandidatesChecked(
                localPlayer.Position,
                TreasureCandidateCheckRadius,
                treasures,
                TreasureCandidateObjectMatchRadius);
        NotifyNewObjects(treasures, carrots, potTargets);

        PollFates(territoryId, territoryName, instanceKey, now, entering);
        UpdatePotPrediction(instanceKey, now);
        PollCriticalEncounters(territoryId, territoryName, instanceKey, now, entering);

        if (now >= nextFlushUtc)
        {
            if (configuration.CollectionEnabled)
                observationStore.Flush();
            islandVisitStore.Flush();
            nextFlushUtc = now + FlushInterval;
        }
    }

    private void PollFates(
        uint territoryId,
        string territoryName,
        string instanceKey,
        DateTimeOffset now,
        bool emitInitialSnapshot)
    {
        if (!fateSource.TryRead(out var current))
            return;
        var markers = current.Select(fate => new AtlasMarker(
            $"fate:{territoryId}:{fate.FateId}",
            IsPotFate(fate) ? AtlasMarkerKind.PotFate : AtlasMarkerKind.Fate,
            fate.Name,
            fate.Position,
            now,
            true,
            territoryId,
            EventId: fate.FateId,
            IconId: ResolveFateMapIcon(fate.FateId),
            Progress: fate.Progress,
            TimeRemainingSeconds: fate.TimeRemainingSeconds,
            EventState: fate.State)).ToArray();
        atlasData.ReplaceSource(AtlasMarkerKind.Fate, markers.Where(marker => marker.Kind == AtlasMarkerKind.Fate));
        atlasData.ReplaceSource(AtlasMarkerKind.PotFate, markers.Where(marker => marker.Kind == AtlasMarkerKind.PotFate));

        var batch = fateDetector.Poll(
            territoryId,
            territoryName,
            instanceKey,
            now,
            emitInitialSnapshot);
        if (configuration.CollectionEnabled)
            RecordAll(batch.Observations);

        foreach (var observation in batch.Observations)
        {
            if (configuration.FateNotificationsEnabled)
                ChatGui.Print($"[Crescent Atlas] FATE: {observation.Name}");

            if (!configuration.PotNotificationsEnabled || !IsPotFate(observation.EventId, observation.Name))
                continue;

            var prediction = potPredictionTracker.Observe(new PotObservation(
                instanceKey,
                observation.ObservedAtUtc,
                observation.EventId,
                new Vector3(observation.X, observation.Y, observation.Z)));
            if (configuration.CollectionEnabled)
                observationStore.Flush();
            PrintPotPrediction(prediction);
        }
    }

    private void PollCriticalEncounters(
        uint territoryId,
        string territoryName,
        string instanceKey,
        DateTimeOffset now,
        bool emitInitialSnapshot)
    {
        if (encounterSource.TryRead(out var current))
        {
            var markers = current.Select(encounter => new AtlasMarker(
                $"ce:{territoryId}:{encounter.EventId}",
                AtlasMarkerKind.CriticalEncounter,
                encounter.Name,
                encounter.Position,
                now,
                true,
                territoryId,
                EventId: encounter.EventId,
                IconId: encounter.IconId,
                Progress: encounter.Progress,
                TimeRemainingSeconds: encounter.SecondsLeft,
                EventState: encounter.State));
            atlasData.ReplaceSource(AtlasMarkerKind.CriticalEncounter, markers);
        }

        var batch = encounterDetector.Poll(
            territoryId,
            territoryName,
            instanceKey,
            now,
            emitInitialSnapshot);
        if (configuration.CollectionEnabled)
            RecordAll(batch.Observations);
        if (configuration.CriticalEncounterNotificationsEnabled)
        {
            foreach (var observation in batch.Observations)
                ChatGui.Print($"[Crescent Atlas] Critical Encounter: {observation.Name}");
        }
    }

    private static uint ResolveFateMapIcon(ushort fateId)
    {
        try
        {
            if (!DataManager.GetExcelSheet<Lumina.Excel.Sheets.Fate>().TryGetRow(fateId, out var row))
                return 0;
            return row.MapIcon != 0 ? row.MapIcon : row.Icon;
        }
        catch
        {
            return 0;
        }
    }

    private void NotifyNewObjects(
        IReadOnlyList<AtlasMarker> treasures,
        IReadOnlyList<AtlasMarker> carrots,
        IReadOnlyList<AtlasMarker> potTargets)
    {
        var treasureKeys = treasures.Select(marker => marker.Key).ToHashSet(StringComparer.Ordinal);
        var carrotKeys = carrots.Select(marker => marker.Key).ToHashSet(StringComparer.Ordinal);
        var potTargetKeys = potTargets.Select(marker => marker.Key).ToHashSet(StringComparer.Ordinal);

        if (configuration.TreasureNotificationsEnabled)
        {
            foreach (var marker in treasures.Where(marker => !previousTreasureKeys.Contains(marker.Key)))
                ChatGui.Print($"[Crescent Atlas] Treasure loaded: {marker.Label}");
        }

        if (configuration.CarrotNotificationsEnabled)
        {
            foreach (var marker in carrots.Where(marker => !previousCarrotKeys.Contains(marker.Key)))
                ChatGui.Print($"[Crescent Atlas] EventObj candidate: {marker.Label}");
        }

        if (configuration.PotNotificationsEnabled)
        {
            foreach (var marker in potTargets.Where(marker => !previousPotTargetKeys.Contains(marker.Key)))
                ChatGui.Print($"[Crescent Atlas] Magic Pot target: {marker.Label}");
        }

        previousTreasureKeys = treasureKeys;
        previousCarrotKeys = carrotKeys;
        previousPotTargetKeys = potTargetKeys;
    }

    private bool IsPotFate(FateSnapshot fate) => IsPotFate(fate.FateId, fate.Name);

    private bool IsPotFate(uint eventId, string name)
        => configuration.ConfirmedPotFateIds.Contains(eventId)
           || eventId is 2072 or 2073
           || name.Contains("magic pot", StringComparison.OrdinalIgnoreCase)
           || name.Contains("マジックポット", StringComparison.Ordinal);

    private void SeedLearnedPotObservations()
    {
        foreach (var observation in LearnedNorthHornPotObservations)
            potPredictionTracker.Observe(observation);
    }

    private void RestorePotObservations()
    {
        var restored = PotObservationHistoryReader.Load(
            observationStore.OutputDirectory,
            MagicPotEventIds,
            NorthHornInstanceKey);
        foreach (var observation in restored)
            potPredictionTracker.Observe(observation);

        BootstrapDiagnostics.Write(
            $"Magic Pot history restored; source={restored.Count}; " +
            $"accepted={potPredictionTracker.GetObservations(NorthHornInstanceKey).Count}");
    }

    private void UpdatePotPrediction(string instanceKey, DateTimeOffset now)
    {
        if (!StringComparer.Ordinal.Equals(instanceKey, NorthHornInstanceKey))
        {
            atlasData.SetPotPrediction(null);
            return;
        }

        var prediction = potPredictionTracker.GetUpcomingPrediction(instanceKey, now);
        if (prediction.NextOccurrenceUtc is not { } next
            || prediction.EstimatedInterval is not { } interval
            || prediction.PredictedEventId is not { } eventId
            || prediction.PredictedPosition is not { } position)
        {
            atlasData.SetPotPrediction(null);
            return;
        }

        atlasData.SetPotPrediction(new AtlasPotPrediction(
            next,
            interval,
            eventId,
            position,
            ResolveFateMapIcon((ushort)eventId),
            prediction.ObservationCount,
            prediction.Confidence == PotPredictionConfidence.Confirmed));
    }

    private void PrintPotPrediction(PotPrediction prediction)
    {
        if (prediction.NextOccurrenceUtc is not { } next)
            return;

        var location = prediction.PredictedPosition is { } position
            ? $"X={position.X:F1}, Z={position.Z:F1}"
            : "location unknown";
        ChatGui.Print(
            $"[Crescent Atlas] Magic Pot next estimate: {next.ToLocalTime():HH:mm:ss} / {location} / " +
            $"{prediction.Confidence} ({prediction.ObservationCount} observations)");
    }

    private void RecordAll(IEnumerable<ObservationRecord> observations)
    {
        foreach (var observation in observations)
            observationStore.Record(observation);
    }

    private void ResetTerritoryState()
    {
        scannedTerritoryId = 0;
        scannedAetheryteMapId = 0;
        nextLayoutScanUtc = DateTimeOffset.MinValue;
        previousTreasureKeys.Clear();
        previousCarrotKeys.Clear();
        previousPotTargetKeys.Clear();
        fateDetector.Reset();
        atlasData.SetPotPrediction(null);
        atlasData.SetContext(0, string.Empty, null, null);
    }

    private static bool ParseToggle(string? value, bool current)
        => value?.ToLowerInvariant() switch
        {
            "on" => true,
            "off" => false,
            _ => !current,
        };

    private void SaveConfiguration()
        => PluginInterface.SavePluginConfig(configuration);

    private sealed class ConditionalObservationSink(
        IObservationSink inner,
        Func<bool> enabled) : IObservationSink
    {
        public string SessionId => inner.SessionId;

        public string OutputDirectory => inner.OutputDirectory;

        public void Record(ObservationRecord observation)
        {
            if (enabled())
                inner.Record(observation);
        }

        public void Flush()
        {
            if (enabled())
                inner.Flush();
        }
    }
}
