using CrescentAtlas.Contracts;

namespace CrescentAtlas.Runtime;

/// <summary>
/// Retains loaded treasure markers for the current island visit after they leave the object table.
/// A retained marker is removed only after the player is close enough for absence to be meaningful.
/// </summary>
public sealed class DetectedTreasureTracker
{
    private readonly Dictionary<string, TrackedTreasure> tracked = new(StringComparer.Ordinal);
    private string instanceKey = string.Empty;

    public IReadOnlyList<AtlasMarker> Observe(
        string currentInstanceKey,
        OccultCrescentMapLayer mapLayer,
        IReadOnlyCollection<AtlasMarker> liveTreasures)
    {
        EnsureInstance(currentInstanceKey);
        foreach (var treasure in liveTreasures)
            tracked[treasure.Key] = new TrackedTreasure(mapLayer, treasure);

        return GetMarkers(currentInstanceKey, mapLayer);
    }

    public void RemoveConfirmedAbsentNearby(
        string currentInstanceKey,
        OccultCrescentMapLayer mapLayer,
        Vector3 playerPosition,
        float visibilityRadius,
        IReadOnlyCollection<AtlasMarker> liveTreasures,
        float objectMatchRadius)
    {
        EnsureInstance(currentInstanceKey);
        var visibilityRadiusSquared = visibilityRadius * visibilityRadius;
        var objectMatchRadiusSquared = objectMatchRadius * objectMatchRadius;

        foreach (var key in tracked
                     .Where(pair =>
                         pair.Value.MapLayer == mapLayer
                         && HorizontalDistanceSquared(playerPosition, pair.Value.Marker.Position)
                         <= visibilityRadiusSquared
                         && !liveTreasures.Any(live =>
                             HorizontalDistanceSquared(live.Position, pair.Value.Marker.Position)
                             <= objectMatchRadiusSquared))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            tracked.Remove(key);
        }
    }

    public IReadOnlyList<AtlasMarker> GetMarkers(
        string currentInstanceKey,
        OccultCrescentMapLayer mapLayer)
    {
        EnsureInstance(currentInstanceKey);
        return tracked.Values
            .Where(entry => entry.MapLayer == mapLayer)
            .Select(entry => entry.Marker)
            .OrderBy(marker => marker.Key, StringComparer.Ordinal)
            .ToArray();
    }

    public void Reset()
    {
        tracked.Clear();
        instanceKey = string.Empty;
    }

    private void EnsureInstance(string currentInstanceKey)
    {
        if (StringComparer.Ordinal.Equals(instanceKey, currentInstanceKey))
            return;

        tracked.Clear();
        instanceKey = currentInstanceKey;
    }

    private static float HorizontalDistanceSquared(Vector3 left, Vector3 right)
        => ((left.X - right.X) * (left.X - right.X))
           + ((left.Z - right.Z) * (left.Z - right.Z));

    private sealed record TrackedTreasure(
        OccultCrescentMapLayer MapLayer,
        AtlasMarker Marker);
}
