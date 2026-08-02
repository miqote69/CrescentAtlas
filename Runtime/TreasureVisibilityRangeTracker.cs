using CrescentAtlas.Contracts;

namespace CrescentAtlas.Runtime;

/// <summary>
/// Expands the absence-check radius only when the current island visit has
/// demonstrated that treasure objects are loaded farther away.
/// </summary>
public sealed class TreasureVisibilityRangeTracker
{
    private readonly Dictionary<OccultCrescentMapLayer, float> farthestObservedDistances = [];

    public float Observe(
        OccultCrescentMapLayer mapLayer,
        Vector3 playerPosition,
        IReadOnlyCollection<AtlasMarker> visibleTreasures)
    {
        foreach (var treasure in visibleTreasures)
        {
            var distance = HorizontalDistance(playerPosition, treasure.Position);
            if (!float.IsFinite(distance))
                continue;

            if (!farthestObservedDistances.TryGetValue(mapLayer, out var previous)
                || distance > previous)
            {
                farthestObservedDistances[mapLayer] = distance;
            }
        }

        return GetCheckRadius(mapLayer);
    }

    public float GetCheckRadius(OccultCrescentMapLayer mapLayer)
    {
        if (!farthestObservedDistances.TryGetValue(mapLayer, out var evidenceDistance))
            return AtlasDetectionRanges.TreasureCandidateCheckRadius;

        return Math.Clamp(
            evidenceDistance - AtlasDetectionRanges.TreasureVisibilitySafetyMargin,
            AtlasDetectionRanges.TreasureCandidateCheckRadius,
            AtlasDetectionRanges.MaximumTreasureCandidateCheckRadius);
    }

    public float? GetFarthestObservedDistance(OccultCrescentMapLayer mapLayer)
        => farthestObservedDistances.TryGetValue(mapLayer, out var distance)
            ? distance
            : null;

    public void Reset()
        => farthestObservedDistances.Clear();

    public static float HorizontalDistance(Vector3 left, Vector3 right)
    {
        var x = left.X - right.X;
        var z = left.Z - right.Z;
        return MathF.Sqrt((x * x) + (z * z));
    }
}
