using CrescentAtlas.Collection;
using CrescentAtlas.Contracts;
using CrescentAtlas.Data;
using CrescentAtlas.Events;
using CrescentAtlas.Notifications;
using CrescentAtlas.Overlays;
using CrescentAtlas.Runtime;
using CrescentAtlas.Windows;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace CrescentAtlas;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/catlas";
    private const string LegacyNorthHornInstanceKey = "territory-1346";
    private const string JapanesePotAlertFileName = "CrescentAtlas.PotAlert.ja.wav";
    private const string JapanesePotAppearedFileName = "CrescentAtlas.PotAppeared.ja.wav";
    private const float TreasureCandidateObjectMatchRadius = 12.0f;
    private const float CarrotSpotMatchRadius = 5.0f;
    private const ushort SilverTreasureChatColor = 37;
    private const ushort BronzeTreasureChatColor = 500;
    private static readonly HashSet<uint> MagicPotEventIds = [2072, 2073];
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PotAdvanceNotificationLeadTime = TimeSpan.FromMinutes(3);
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
    private readonly List<ConfirmedCarrotSpot> knownCarrotSpots = [];
    private readonly MutableAtlasDataSource atlasData = new();
    private readonly OccultCrescentContext crescentContext;
    private readonly AetheryteMarkerProvider aetheryteMarkerProvider;
    private readonly LayoutTreasureCandidateScanner layoutScanner;
    private readonly AgentMapPotTargetSource agentMapPotTargetSource;
    private readonly ObjectTableCollector objectCollector;
    private readonly DalamudFateSnapshotSource fateSource;
    private readonly DynamicEventSnapshotSource encounterSource = new();
    private readonly FateEventDetector fateDetector;
    private readonly CriticalEncounterDetector encounterDetector;
    private readonly PotPredictionTracker potPredictionTracker =
        new(knownEventPositions: ConfirmedMagicPotLocations.NorthHorn);
    private readonly PotAdvanceNotificationTracker potAdvanceNotificationTracker = new();
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
            configuration.CheckedTreasureKeys ??= [];
            configuration.ConfirmedCarrotDataIds ??= [];
            configuration.ConfirmedCarrotEventIds ??= [];
            BootstrapDiagnostics.Write($"configuration loaded; version={configuration.Version}");
            var configurationChanged = false;
            if (configuration.Version < 2)
            {
                // Existing builds defaulted to click-through, which also prevented
                // ImGui's window border from receiving resize drags.
                configuration.MapClickThrough = false;
                configuration.Version = 2;
                configurationChanged = true;
            }
            if (configuration.Version < 3)
            {
                configuration.Version = 3;
                configurationChanged = true;
            }
            if (configuration.ConfirmedCarrotDataIds.Add(ConfirmedCarrotObjects.FortuneCarrotDataId))
                configurationChanged = true;
            if (configurationChanged)
                SaveConfiguration();

            silverTreasureDataIds.UnionWith(
                ConfirmedSilverTreasureSpots.NorthHorn.Select(spot => spot.DataId));
            silverTreasureDataIds.UnionWith(ConfirmedSilverTreasureSpots.EventObjectDataIds);
            potTargetDataIds.UnionWith(
                ConfirmedPotTargetObservations.NorthHorn.Select(observation => observation.DataId));
            potTargetDataIds.UnionWith(ConfirmedPotTargetObservations.EventObjectDataIds);
            BootstrapDiagnostics.Write(
                $"confirmed treasure data initialized; silver={silverTreasureDataIds.Count}; " +
                $"potTarget={potTargetDataIds.Count}");
            observationStore = new ObservationStore(PluginInterface);
            BootstrapDiagnostics.Write($"observation store initialized; session={observationStore.SessionId}");
            RestoreCarrotSpots();
            islandVisitStore = new IslandVisitStore(observationStore.OutputDirectory);
            BootstrapDiagnostics.Write($"island visit store initialized; path={islandVisitStore.OutputPath}");
            RestorePotObservations();
            crescentContext = new OccultCrescentContext(ClientState, DataManager, Log);
            aetheryteMarkerProvider = new AetheryteMarkerProvider(DataManager);
            layoutScanner = new LayoutTreasureCandidateScanner(DataManager, Log);
            agentMapPotTargetSource = new AgentMapPotTargetSource(DataManager, Log);
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
                ResetTreasureChecks,
                SaveConfiguration,
                PlayChatSoundEffect,
                PlayJapanesePotAdvanceVoice,
                PlayJapanesePotAppearedVoice);
            treasureLineOverlay = new NearbyTreasureLineOverlay(GameGui, atlasData, configuration);
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
            ChatGui.Print(T(
                "[Crescent Atlas] Loaded. /catlas toggles the display-only map.",
                "[Crescent Atlas] 読み込み完了。/catlas で表示専用マップを切り替えます。"));
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
                ChatGui.Print(configuration.Language == UiLanguage.Japanese
                    ? $"[Crescent Atlas] データ収集を{(configuration.CollectionEnabled ? "有効" : "無効")}にしました。"
                    : $"[Crescent Atlas] Collection {(configuration.CollectionEnabled ? "enabled" : "disabled")}.");
                break;
            case "click":
                configuration.MapClickThrough = !configuration.MapClickThrough;
                SaveConfiguration();
                ChatGui.Print(configuration.Language == UiLanguage.Japanese
                    ? $"[Crescent Atlas] マップのクリック透過を{(configuration.MapClickThrough ? "有効" : "無効")}にしました。"
                    : $"[Crescent Atlas] Map click-through {(configuration.MapClickThrough ? "enabled" : "disabled")}.");
                break;
            case "flush":
                observationStore.Flush();
                ChatGui.Print(configuration.Language == UiLanguage.Japanese
                    ? $"[Crescent Atlas] {observationStore.SessionObservationCount}件の観測データを保存しました。"
                    : $"[Crescent Atlas] Saved {observationStore.SessionObservationCount} observations.");
                break;
            case "folder":
                ChatGui.Print(T(
                    $"[Crescent Atlas] Collection folder: {observationStore.OutputDirectory}",
                    $"[Crescent Atlas] 収集フォルダー: {observationStore.OutputDirectory}"));
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
                ChatGui.Print(T(
                    $"[Crescent Atlas] Diagnostic log: {BootstrapDiagnostics.LogPath}",
                    $"[Crescent Atlas] 診断ログ: {BootstrapDiagnostics.LogPath}"));
                break;
            case "log":
                ChatGui.Print(T(
                    $"[Crescent Atlas] Diagnostic log: {BootstrapDiagnostics.LogPath}",
                    $"[Crescent Atlas] 診断ログ: {BootstrapDiagnostics.LogPath}"));
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
                false,
                ClientState.TerritoryType,
                ClientState.MapId,
                OccultCrescentMapLayer.Surface,
                crescentContext.TerritoryName,
                localPlayer?.Position,
                localPlayer?.Rotation);
            return;
        }

        var entering = !wasActive;
        wasActive = true;
        var territoryId = crescentContext.TerritoryId;
        var territoryName = crescentContext.TerritoryName;
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
        var instanceKey = islandVisitStore.ActiveVisit is { } activeVisit
            ? $"island-visit:{activeVisit.VisitId}"
            : $"territory-{territoryId}:unidentified";
        if (islandVisitStore.ActiveVisit is { } treasureCheckVisit)
            SynchronizeTreasureCheckVisit(treasureCheckVisit.VisitId);
        var mapId = ClientState.MapId;
        var surfaceMapId = ResolveSurfaceMapId(territoryId);
        var mapLayer = OccultCrescentMapLayerPolicy.Resolve(mapId, surfaceMapId);
        var mapChanged = atlasData.MapId != mapId || atlasData.MapLayer != mapLayer;
        atlasData.SetContext(
            true,
            territoryId,
            mapId,
            mapLayer,
            territoryName,
            localPlayer?.Position,
            localPlayer?.Rotation);
        if (mapChanged)
        {
            scannedTerritoryId = 0;
            scannedAetheryteMapId = 0;
            nextLayoutScanUtc = DateTimeOffset.MinValue;
            BootstrapDiagnostics.Write(
                $"Map layer changed; territory={territoryId}; map={mapId}; " +
                $"surfaceMap={surfaceMapId}; layer={mapLayer}");
        }

        if (scannedAetheryteMapId != mapId
            && aetheryteMarkerProvider.TryRead(territoryId, mapId, now, out var aetherytes))
        {
            atlasData.ReplaceSource(AtlasMarkerKind.Aetheryte, aetherytes);
            scannedAetheryteMapId = mapId;
        }

        if (scannedTerritoryId != territoryId
            && now >= nextLayoutScanUtc)
        {
            var candidates = layoutScanner.Scan(
                observationStore.SessionId,
                territoryId,
                territoryName,
                mapLayer,
                now,
                out var candidateObservations);
            atlasData.ReplaceSource(AtlasMarkerKind.TreasureCandidate, candidates);
            atlasData.RestoreTreasureChecks(configuration.CheckedTreasureKeys);
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
        var loadedPotTargets = objectMarkers.Where(marker => marker.Kind == AtlasMarkerKind.PotTarget).ToArray();
        var mappedPotTargets = agentMapPotTargetSource.Scan(
            observationStore.SessionId,
            territoryId,
            territoryName,
            mapId,
            now,
            out var mappedPotTargetObservations);
        if (configuration.CollectionEnabled)
            RecordAll(mappedPotTargetObservations);
        var potTargets = MergePotTargets(loadedPotTargets, mappedPotTargets);
        atlasData.SetMagicalElixirState(potTargets.Length > 0);
        var liveCarrots = carrotCandidates
            .Where(marker => !marker.Key.Contains("carrot-candidate", StringComparison.Ordinal))
            .ToArray();
        RememberCarrotSpots(liveCarrots);
        var carrots = BuildCarrotMarkers(territoryId, liveCarrots);
        atlasData.ReplaceSource(AtlasMarkerKind.ActiveTreasure, treasures);
        atlasData.ReplaceSource(AtlasMarkerKind.Carrot, carrots);
        atlasData.ReplaceSource(AtlasMarkerKind.PotTarget, potTargets);
        if (localPlayer is not null)
        {
            atlasData.MarkAbsentNearbyTreasureCandidatesChecked(
                localPlayer.Position,
                AtlasDetectionRanges.TreasureCandidateCheckRadius,
                treasures,
                TreasureCandidateObjectMatchRadius);
            PersistTreasureChecks();
        }
        NotifyNewObjects(treasures, liveCarrots, potTargets);

        PollFates(territoryId, territoryName, instanceKey, now, entering);
        if (mapLayer == OccultCrescentMapLayer.Surface)
            UpdatePotPrediction(instanceKey, now);
        else
            atlasData.SetPotPrediction(null);
        PollCriticalEncounters(territoryId, territoryName, instanceKey, now, entering);

        if (now >= nextFlushUtc)
        {
            if (configuration.CollectionEnabled)
                observationStore.Flush();
            islandVisitStore.Flush();
            nextFlushUtc = now + FlushInterval;
        }
    }

    private static AtlasMarker[] MergePotTargets(
        IReadOnlyList<AtlasMarker> loadedTargets,
        IReadOnlyList<AtlasMarker> mappedTargets)
        => loadedTargets
            .Concat(mappedTargets)
            .GroupBy(marker => FormattableString.Invariant(
                $"{marker.DataId}:{marker.Position.X:F1}:{marker.Position.Y:F1}:{marker.Position.Z:F1}"),
                StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

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
                ChatGui.Print(configuration.Language == UiLanguage.Japanese
                    ? $"[Crescent Atlas] FATE発生: {observation.Name}"
                    : $"[Crescent Atlas] FATE: {observation.Name}");

            if (!IsPotFate(observation.EventId, observation.Name))
                continue;

            if (configuration.PotSoundEnabled)
                PlayPotAppearanceAlertSound();

            if (!configuration.PotNotificationsEnabled)
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
                ChatGui.Print(configuration.Language == UiLanguage.Japanese
                    ? $"[Crescent Atlas] クリティカルエンカウント発生: {observation.Name}"
                    : $"[Crescent Atlas] Critical Encounter: {observation.Name}");
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

    private static uint ResolveSurfaceMapId(uint territoryId)
    {
        try
        {
            if (DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()
                .TryGetRow(territoryId, out var territory))
            {
                return territory.Map.RowId;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to resolve the surface map for territory {TerritoryId}.", territoryId);
        }

        return 0;
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
                PrintTreasureDetected(marker);
        }

        if (configuration.CarrotNotificationsEnabled)
        {
            foreach (var marker in carrots.Where(marker => !previousCarrotKeys.Contains(marker.Key)))
                ChatGui.Print(configuration.Language == UiLanguage.Japanese
                    ? $"[Crescent Atlas] にんじん候補を検知: {marker.Label}"
                    : $"[Crescent Atlas] Carrot candidate: {marker.Label}");
        }

        if (configuration.PotNotificationsEnabled)
        {
            foreach (var marker in potTargets.Where(marker => !previousPotTargetKeys.Contains(marker.Key)))
                ChatGui.Print(configuration.Language == UiLanguage.Japanese
                    ? $"[Crescent Atlas] マジカルエリクサー目標を検知: {marker.Label}"
                    : $"[Crescent Atlas] Magical Elixir target: {marker.Label}");
        }

        previousTreasureKeys = treasureKeys;
        previousCarrotKeys = carrotKeys;
        previousPotTargetKeys = potTargetKeys;
    }

    private void PrintTreasureDetected(AtlasMarker marker)
    {
        var isSilver = marker.TreasureType.Equals(
            "silver",
            StringComparison.OrdinalIgnoreCase);
        var typeLabel = configuration.Language == UiLanguage.Japanese
            ? isSilver ? "銀" : "銅"
            : isSilver ? "Silver" : "Bronze";
        var prefix = configuration.Language == UiLanguage.Japanese
            ? "[Crescent Atlas] 宝箱を検知: "
            : "[Crescent Atlas] Treasure loaded: ";
        var color = isSilver
            ? SilverTreasureChatColor
            : BronzeTreasureChatColor;

        ChatGui.Print(
            new SeStringBuilder()
                .AddText(prefix)
                .AddUiForeground(typeLabel, color)
                .Build());
    }

    private bool IsPotFate(FateSnapshot fate) => IsPotFate(fate.FateId, fate.Name);

    private bool IsPotFate(uint eventId, string name)
        => configuration.ConfirmedPotFateIds.Contains(eventId)
           || eventId is 2072 or 2073
           || name.Contains("magic pot", StringComparison.OrdinalIgnoreCase)
           || name.Contains("マジックポット", StringComparison.Ordinal);

    private void RestorePotObservations()
    {
        var restored = PotObservationHistoryReader.Load(
            observationStore.OutputDirectory,
            MagicPotEventIds,
            LegacyNorthHornInstanceKey);
        foreach (var observation in restored)
            potPredictionTracker.Observe(observation);

        BootstrapDiagnostics.Write(
            $"Magic Pot history restored; source={restored.Count}; " +
            $"legacy={potPredictionTracker.GetObservations(LegacyNorthHornInstanceKey).Count}");
    }

    private void RestoreCarrotSpots()
    {
        knownCarrotSpots.AddRange(ConfirmedCarrotSpots.NorthHorn);
        foreach (var restored in CarrotSpotHistoryReader.Load(
                     observationStore.OutputDirectory,
                     ConfirmedCarrotObjects.FortuneCarrotDataId))
        {
            RememberCarrotSpot(restored);
        }

        BootstrapDiagnostics.Write(
            $"Carrot spot history restored; spots={knownCarrotSpots.Count}");
    }

    private void RememberCarrotSpots(IEnumerable<AtlasMarker> carrots)
    {
        foreach (var carrot in carrots.Where(marker =>
                     marker.DataId == ConfirmedCarrotObjects.FortuneCarrotDataId))
        {
            RememberCarrotSpot(new ConfirmedCarrotSpot(
                carrot.TerritoryId,
                carrot.DataId,
                carrot.Position,
                carrot.ObservedAtUtc));
        }
    }

    private void RememberCarrotSpot(ConfirmedCarrotSpot spot)
    {
        var matchRadiusSquared = CarrotSpotMatchRadius * CarrotSpotMatchRadius;
        if (knownCarrotSpots.Any(existing =>
                existing.TerritoryId == spot.TerritoryId
                && Vector3.DistanceSquared(existing.Position, spot.Position) <= matchRadiusSquared))
        {
            return;
        }

        knownCarrotSpots.Add(spot);
    }

    private IReadOnlyList<AtlasMarker> BuildCarrotMarkers(
        uint territoryId,
        IReadOnlyCollection<AtlasMarker> liveCarrots)
    {
        var matchRadiusSquared = CarrotSpotMatchRadius * CarrotSpotMatchRadius;
        var fixedSpots = knownCarrotSpots
            .Where(spot =>
                spot.TerritoryId == territoryId
                && !liveCarrots.Any(carrot =>
                    Vector3.DistanceSquared(carrot.Position, spot.Position) <= matchRadiusSquared))
            .Select(spot => new AtlasMarker(
                $"carrot-spot:{CarrotSpotHistoryReader.SpotKey(spot)}",
                AtlasMarkerKind.Carrot,
                "Carrot spot",
                spot.Position,
                spot.ConfirmedAtUtc,
                IsActive: false,
                spot.TerritoryId,
                spot.DataId));

        return liveCarrots
            .Concat(fixedSpots)
            .OrderByDescending(marker => marker.IsActive)
            .ThenBy(marker => marker.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private void UpdatePotPrediction(string instanceKey, DateTimeOffset now)
    {
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

        NotifyUpcomingPot(instanceKey, next, now);
    }

    private void NotifyUpcomingPot(
        string instanceKey,
        DateTimeOffset nextOccurrenceUtc,
        DateTimeOffset now)
    {
        if (!configuration.PotNotificationsEnabled
            || !configuration.PotThreeMinuteNotificationEnabled
            || !potAdvanceNotificationTracker.ShouldNotify(
                instanceKey,
                nextOccurrenceUtc,
                now,
                PotAdvanceNotificationLeadTime))
        {
            return;
        }

        ChatGui.Print(configuration.Language == UiLanguage.Japanese
            ? $"[Crescent Atlas] マジックポット出現予想の3分前です（予想時間 {nextOccurrenceUtc.ToLocalTime():HH:mm:ss}）。"
            : $"[Crescent Atlas] Magic Pot is predicted in 3 minutes (estimated time {nextOccurrenceUtc.ToLocalTime():HH:mm:ss}).");

        if (configuration.PotSoundEnabled)
            PlayPotAdvanceAlertSound();
    }

    private void PrintPotPrediction(PotPrediction prediction)
    {
        if (prediction.NextOccurrenceUtc is not { } next)
            return;

        var confidence = configuration.Language == UiLanguage.Japanese
            ? prediction.Confidence switch
            {
                PotPredictionConfidence.Provisional => "暫定",
                PotPredictionConfidence.Confirmed => "確定",
                _ => "不明",
            }
            : prediction.Confidence.ToString();
        ChatGui.Print(configuration.Language == UiLanguage.Japanese
            ? $"[Crescent Atlas] マジックポット次回予想: {next.ToLocalTime():HH:mm:ss} / " +
              $"{confidence}（観測{prediction.ObservationCount}回）"
            : $"[Crescent Atlas] Magic Pot next estimate: {next.ToLocalTime():HH:mm:ss} / " +
              $"{confidence} ({prediction.ObservationCount} observations)");
    }

    private void RecordAll(IEnumerable<ObservationRecord> observations)
    {
        foreach (var observation in observations)
            observationStore.Record(observation);
    }

    private void SynchronizeTreasureCheckVisit(string visitId)
    {
        if (StringComparer.Ordinal.Equals(configuration.TreasureCheckVisitId, visitId))
            return;

        configuration.TreasureCheckVisitId = visitId;
        configuration.CheckedTreasureKeys.Clear();
        SaveConfiguration();
    }

    private void PersistTreasureChecks()
    {
        var changed = false;
        foreach (var marker in atlasData.GetMarkers().Where(marker =>
                     marker.Kind == AtlasMarkerKind.TreasureCandidate
                     && marker.IsChecked))
        {
            changed |= configuration.CheckedTreasureKeys.Add(marker.Key);
        }

        if (changed)
            SaveConfiguration();
    }

    private void ResetTreasureChecks()
    {
        atlasData.ResetTreasureChecks();
        configuration.CheckedTreasureKeys.Clear();
        SaveConfiguration();
    }

    private void ResetTerritoryState()
    {
        scannedTerritoryId = 0;
        scannedAetheryteMapId = 0;
        nextLayoutScanUtc = DateTimeOffset.MinValue;
        previousTreasureKeys.Clear();
        previousCarrotKeys.Clear();
        previousPotTargetKeys.Clear();
        potAdvanceNotificationTracker.ResetAll();
        fateDetector.Reset();
        atlasData.SetPotPrediction(null);
        atlasData.SetMagicalElixirState(false);
        atlasData.SetContext(
            false,
            0,
            0,
            OccultCrescentMapLayer.Surface,
            string.Empty,
            null,
            null);
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

    private string T(string english, string japanese)
        => configuration.Language == UiLanguage.Japanese ? japanese : english;

    private static unsafe void PlayChatSoundEffect(uint effectId)
    {
        try
        {
            UIGlobals.PlayChatSoundEffect(Math.Clamp(effectId, 1u, 16u));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to play chat sound effect {EffectId}", effectId);
        }
    }

    private void PlayPotAdvanceAlertSound()
    {
        if (configuration.PotThreeMinuteSoundMode == PotThreeMinuteSoundMode.JapaneseVocalSynth)
        {
            PlayJapanesePotAdvanceVoice();
            return;
        }

        PlayChatSoundEffect(configuration.PotSoundEffect);
    }

    private void PlayJapanesePotAdvanceVoice()
        => PlayJapaneseVoiceFile(
            JapanesePotAlertFileName,
            "Japanese Magic Pot advance voice");

    private void PlayPotAppearanceAlertSound()
    {
        if (configuration.PotAppearanceSoundMode == PotThreeMinuteSoundMode.JapaneseVocalSynth)
        {
            PlayJapanesePotAppearedVoice();
            return;
        }

        PlayChatSoundEffect(configuration.PotSoundEffect);
    }

    private void PlayJapanesePotAppearedVoice()
        => PlayJapaneseVoiceFile(
            JapanesePotAppearedFileName,
            "Japanese Magic Pot appearance voice");

    private void PlayJapaneseVoiceFile(string fileName, string description)
    {
        try
        {
            var assemblyDirectory = PluginInterface.AssemblyLocation.DirectoryName;
            var path = System.IO.Path.Combine(
                assemblyDirectory ?? string.Empty,
                fileName);
            if (NotificationAudioPlayer.TryPlayFile(path))
                return;

            Log.Warning("{Description} is unavailable at {Path}.", description, path);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to play {Description}.", description);
        }

        PlayChatSoundEffect(configuration.PotSoundEffect);
    }

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
