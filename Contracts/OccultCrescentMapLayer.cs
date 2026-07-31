namespace CrescentAtlas.Contracts;

public enum OccultCrescentMapLayer
{
    Surface,
    Subterranean,
}

public static class OccultCrescentMapLayerPolicy
{
    public static OccultCrescentMapLayer Resolve(uint currentMapId, uint surfaceMapId)
        => currentMapId != 0
           && surfaceMapId != 0
           && currentMapId != surfaceMapId
            ? OccultCrescentMapLayer.Subterranean
            : OccultCrescentMapLayer.Surface;

    public static bool IsMarkerVisible(OccultCrescentMapLayer layer, AtlasMarker marker)
        => layer == OccultCrescentMapLayer.Surface
           || marker.Kind is AtlasMarkerKind.ActiveTreasure
               or AtlasMarkerKind.PotTarget
               or AtlasMarkerKind.Aetheryte
               or AtlasMarkerKind.Player
           || marker.Kind == AtlasMarkerKind.Carrot && marker.IsActive;
}
