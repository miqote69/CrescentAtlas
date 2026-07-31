using CrescentAtlas.Contracts;

namespace CrescentAtlas.Runtime;

public static class AtlasMarkerSelector
{
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
