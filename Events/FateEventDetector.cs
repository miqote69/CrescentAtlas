using CrescentAtlas.Contracts;
using CrescentAtlas.Data;

namespace CrescentAtlas.Events;

/// <summary>
/// Detects FATE IDs newly present in a territory/instance snapshot.
/// </summary>
public sealed class FateEventDetector
{
    private readonly IFateSnapshotSource source;
    private readonly string sessionId;
    private HashSet<ushort> previousIds = [];
    private string? previousScope;

    public FateEventDetector(IFateSnapshotSource source, string sessionId)
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
        var scope = $"{territoryId}:{instanceKey}";
        // Preserve the last good baseline on a transient read failure.
        if (!source.TryRead(out var current))
            return EventDetectionBatch.Empty;

        var currentIds = current.Select(fate => fate.FateId).ToHashSet();
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
        foreach (var fate in current.Where(fate => newIds.Contains(fate.FateId)))
        {
            var key = $"fate:{territoryId}:{instanceKey}:{fate.FateId}";
            markers.Add(new AtlasMarker(
                key,
                AtlasMarkerKind.Fate,
                fate.Name,
                fate.Position,
                observedAtUtc,
                true,
                territoryId,
                EventId: fate.FateId));

            observations.Add(new ObservationRecord
            {
                SessionId = sessionId,
                ObservedAtUtc = observedAtUtc,
                Source = ObservationSource.FateTable,
                Kind = "FateStarted",
                Key = key,
                TerritoryId = territoryId,
                TerritoryName = territoryName,
                EventId = fate.FateId,
                Name = fate.Name,
                X = fate.Position.X,
                Y = fate.Position.Y,
                Z = fate.Position.Z,
                IsActive = true,
                Properties = new Dictionary<string, string>
                {
                    ["instanceKey"] = instanceKey,
                    ["progress"] = fate.Progress.ToString(),
                    ["state"] = fate.State,
                    ["timeRemainingSeconds"] = fate.TimeRemainingSeconds.ToString(),
                },
            });
        }

        return new EventDetectionBatch(markers, observations);
    }

    public void Reset()
    {
        previousIds.Clear();
        previousScope = null;
    }
}
