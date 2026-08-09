namespace CrescentAtlas.Contracts;

public enum OccultCrescentMapLayer
{
    Surface,
    Subterranean,
    ForkedTower,
}

public static class OccultCrescentMapLayerPolicy
{
    public const uint ForkedTowerFirstMapId = 1178;
    public const uint ForkedTowerLastMapId = 1191;

    public static OccultCrescentMapLayer Resolve(uint currentMapId, uint surfaceMapId)
        => IsForkedTowerMap(currentMapId)
            ? OccultCrescentMapLayer.ForkedTower
            : currentMapId != 0
           && surfaceMapId != 0
           && currentMapId != surfaceMapId
            ? OccultCrescentMapLayer.Subterranean
            : OccultCrescentMapLayer.Surface;

    public static bool IsForkedTowerMap(uint mapId)
        => mapId is >= ForkedTowerFirstMapId and <= ForkedTowerLastMapId;

    public static bool IsMarkerVisible(OccultCrescentMapLayer layer, AtlasMarker marker)
    {
        if (layer == OccultCrescentMapLayer.Surface)
            return true;

        if (layer == OccultCrescentMapLayer.ForkedTower)
        {
            return marker.Kind is AtlasMarkerKind.ActiveTreasure
                or AtlasMarkerKind.TreasureCandidate
                or AtlasMarkerKind.Player;
        }

        return marker.Kind is AtlasMarkerKind.ActiveTreasure
                   or AtlasMarkerKind.TreasureCandidate
                   or AtlasMarkerKind.PotTarget
                   or AtlasMarkerKind.Aetheryte
                   or AtlasMarkerKind.Player
               || marker.Kind == AtlasMarkerKind.Carrot && marker.IsActive;
    }
}
