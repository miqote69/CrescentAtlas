using CrescentAtlas.Contracts;
using CrescentAtlas.Data;

namespace CrescentAtlas.Events;

public sealed class CriticalEncounterDetector
{
    private readonly ICriticalEncounterSnapshotSource source;
    private readonly string sessionId;
    private HashSet<ushort> previousIds = [];
    private string? previousScope;

    public CriticalEncounterDetector(
        ICriticalEncounterSnapshotSource source,
        string sessionId)
    {
        this.source = source;
        this.sessionId = sessionId;
    }

    public EventDetectionBatch Poll(
        uint territoryId,
        string territoryName,
        string instanceKey,
        DateTimeOffset observedAtUtc,
        bool emitInitialSnapshot = false)
    {
        // A failed read must not clear the baseline, otherwise recovery would
        // incorrectly announce every still-active encounter as new.
        if (!source.TryRead(out var current))
            return EventDetectionBatch.Empty;

        var scope = $"{territoryId}:{instanceKey}";
        var currentIds = current.Select(encounter => encounter.EventId).ToHashSet();
        var scopeChanged = !StringComparer.Ordinal.Equals(previousScope, scope);
        var newIds = scopeChanged && !emitInitialSnapshot
            ? new HashSet<ushort>()
            : currentIds.Except(previousIds).ToHashSet();

        previousScope = scope;
        previousIds = currentIds;

        if (newIds.Count == 0)
            return EventDetectionBatch.Empty;

        var markers = new List<AtlasMarker>(newIds.Count);
        var observations = new List<ObservationRecord>(newIds.Count);
        foreach (var encounter in current.Where(item => newIds.Contains(item.EventId)))
        {
            var key = $"ce:{territoryId}:{instanceKey}:{encounter.EventId}";
            markers.Add(new AtlasMarker(
                key,
                AtlasMarkerKind.CriticalEncounter,
                encounter.Name,
                encounter.Position,
                observedAtUtc,
                true,
                territoryId,
                EventId: encounter.EventId));

            observations.Add(new ObservationRecord
            {
                SessionId = sessionId,
                ObservedAtUtc = observedAtUtc,
                Source = ObservationSource.DynamicEvent,
                Kind = "CriticalEncounterStarted",
                Key = key,
                TerritoryId = territoryId,
                TerritoryName = territoryName,
                EventId = encounter.EventId,
                Name = encounter.Name,
                X = encounter.Position.X,
                Y = encounter.Position.Y,
                Z = encounter.Position.Z,
                IsActive = true,
                Properties = new Dictionary<string, string>
                {
                    ["instanceKey"] = instanceKey,
                    ["state"] = encounter.State,
                    ["secondsLeft"] = encounter.SecondsLeft.ToString(),
                    ["progress"] = encounter.Progress.ToString(),
                    ["participants"] = encounter.Participants.ToString(),
                },
            });
        }

        return new EventDetectionBatch(markers, observations);
    }
}
