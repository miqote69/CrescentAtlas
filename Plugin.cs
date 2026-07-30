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
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);

    [PluginService] private static IDalamudPluginInterface PluginInterface { get; set; } = null!;
    [PluginService] private static ICommandManager CommandManager { get; set; } = null!;
    [PluginService] private static IFramework Framework { get; set; } = null!;
    [PluginService] private static IClientState ClientState { get; set; } = null!;
    [PluginService] private static IDataManager DataManager { get; set; } = null!;
    [PluginService] private static IObjectTable ObjectTable { get; set; } = null!;
    [PluginService] private static IFateTable FateTable { get; set; } = null!;
    [PluginService] private static IGameGui GameGui { get; set; } = null!;
    [PluginService] private static IChatGui ChatGui { get; set; } = null!;
    [PluginService] private static IPluginLog Log { get; set; } = null!;

    private readonly Configuration configuration;
    private readonly ObservationStore observationStore;
    private readonly MutableAtlasDataSource atlasData = new();
    private readonly OccultCrescentContext crescentContext;
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
    private DateTimeOffset nextPollUtc;
    private DateTimeOffset nextFlushUtc;
    private DateTimeOffset nextLayoutScanUtc;
    private uint scannedTerritoryId;
    private bool wasActive;

    public Plugin()
    {
        configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        observationStore = new ObservationStore(PluginInterface);
        crescentContext = new OccultCrescentContext(ClientState, DataManager, Log);
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
                IncludeUnclassifiedEventObjects = true,
            });
        fateSource = new DalamudFateSnapshotSource(FateTable);
        fateDetector = new FateEventDetector(fateSource, observationStore.SessionId);
        encounterDetector = new CriticalEncounterDetector(encounterSource, observationStore.SessionId);

        atlasWindow = new AtlasWindow(atlasData, configuration);
        treasureLineOverlay = new NearbyTreasureLineOverlay(GameGui, atlasData);
        windowSystem.AddWindow(atlasWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Crescent Atlas: map, collection and status controls.",
        });
        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.Draw += treasureLineOverlay.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMap;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleMap;
        Framework.Update += OnFrameworkUpdate;

        nextPollUtc = DateTimeOffset.UtcNow;
        nextFlushUtc = DateTimeOffset.UtcNow + FlushInterval;
        ChatGui.Print("[Crescent Atlas] Loaded. /catlas toggles the display-only map.");
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleMap;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMap;
        PluginInterface.UiBuilder.Draw -= treasureLineOverlay.Draw;
        CommandManager.RemoveHandler(CommandName);
        windowSystem.RemoveAllWindows();
        atlasWindow.Dispose();
        observationStore.Dispose();
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
                ChatGui.Print(
                    $"[Crescent Atlas] active={OccultCrescentContext.IsActive()}, territory={ClientState.TerritoryType}, " +
                    $"observations={observationStore.SessionObservationCount}, collection={configuration.CollectionEnabled}");
                break;
            default:
                ChatGui.Print("[Crescent Atlas] /catlas [map|collect on|collect off|click|flush|folder|status]");
                break;
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
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
        }
    }

    private void Poll(DateTimeOffset now)
    {
        var active = OccultCrescentContext.IsActive();
        if (!active)
        {
            if (wasActive)
                ResetTerritoryState();
            wasActive = false;
            atlasData.SetContext(ClientState.TerritoryType, crescentContext.TerritoryName, ObjectTable.LocalPlayer?.Position);
            return;
        }

        var entering = !wasActive;
        wasActive = true;
        var territoryId = crescentContext.TerritoryId;
        var territoryName = crescentContext.TerritoryName;
        var instanceKey = $"territory-{territoryId}";
        atlasData.SetContext(territoryId, territoryName, ObjectTable.LocalPlayer?.Position);

        if (scannedTerritoryId != territoryId && now >= nextLayoutScanUtc)
        {
            var candidates = layoutScanner.Scan(
                observationStore.SessionId,
                territoryId,
                territoryName,
                now,
                out var candidateObservations);
            atlasData.ReplaceSource(AtlasMarkerKind.TreasureCandidate, candidates);
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
        var carrots = carrotCandidates
            .Where(marker => !marker.Key.Contains("carrot-candidate", StringComparison.Ordinal))
            .ToArray();
        atlasData.ReplaceSource(AtlasMarkerKind.ActiveTreasure, treasures);
        atlasData.ReplaceSource(AtlasMarkerKind.Carrot, carrots);
        NotifyNewObjects(treasures, carrots);

        PollFates(territoryId, territoryName, instanceKey, now, entering);
        PollCriticalEncounters(territoryId, territoryName, instanceKey, now, entering);

        if (now >= nextFlushUtc)
        {
            if (configuration.CollectionEnabled)
                observationStore.Flush();
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
            EventId: fate.FateId)).ToArray();
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
                EventId: encounter.EventId));
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

    private void NotifyNewObjects(IReadOnlyList<AtlasMarker> treasures, IReadOnlyList<AtlasMarker> carrots)
    {
        var treasureKeys = treasures.Select(marker => marker.Key).ToHashSet(StringComparer.Ordinal);
        var carrotKeys = carrots.Select(marker => marker.Key).ToHashSet(StringComparer.Ordinal);

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

        previousTreasureKeys = treasureKeys;
        previousCarrotKeys = carrotKeys;
    }

    private bool IsPotFate(FateSnapshot fate) => IsPotFate(fate.FateId, fate.Name);

    private bool IsPotFate(uint eventId, string name)
        => configuration.ConfirmedPotFateIds.Contains(eventId)
           || name.Contains("magic pot", StringComparison.OrdinalIgnoreCase)
           || name.Contains("マジックポット", StringComparison.Ordinal);

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
        nextLayoutScanUtc = DateTimeOffset.MinValue;
        previousTreasureKeys.Clear();
        previousCarrotKeys.Clear();
        fateDetector.Reset();
        potPredictionTracker.ResetAll();
        atlasData.SetContext(0, string.Empty, null);
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
