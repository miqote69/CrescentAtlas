using CrescentAtlas.Contracts;

namespace CrescentAtlas.Runtime;

public static class AtlasMarkerSelector
{
    public static bool IsTreasureVisible(
        AtlasMarker marker,
        bool showBronzeTreasure,
        bool showSilverTreasure)
    {
        if (marker.Kind is not (AtlasMarkerKind.TreasureCandidate or AtlasMarkerKind.ActiveTreasure))
            return false;

        return marker.TreasureType.Equals("silver", StringComparison.OrdinalIgnoreCase)
            ? showSilverTreasure
            : marker.TreasureType.Equals("gold", StringComparison.OrdinalIgnoreCase)
                || showBronzeTreasure;
    }

    public static AtlasMarker? FindNearestActiveCarrot(
        IReadOnlyList<AtlasMarker> markers,
        Vector3 playerPosition,
        float maximumDistance)
    {
        var maximumDistanceSquared = maximumDistance * maximumDistance;
        return markers
            .Where(marker =>
                marker.Kind == AtlasMarkerKind.Carrot
                && marker.IsActive
                && Vector3.DistanceSquared(playerPosition, marker.Position) <= maximumDistanceSquared)
            .MinBy(marker => Vector3.DistanceSquared(playerPosition, marker.Position));
    }
}
