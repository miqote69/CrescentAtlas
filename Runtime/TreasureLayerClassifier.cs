using CrescentAtlas.Contracts;

namespace CrescentAtlas.Runtime;

public static class TreasureLayerClassifier
{
    // Collected North Horn surface objects reach Y=-48.86, while confirmed
    // subterranean treasure objects begin at Y=-92.03. Keep a conservative
    // gap so low surface coffers are retained without leaking underground
    // layout candidates onto the surface map.
    public const float MinimumSurfaceElevation = -70.0f;
    // Confirmed North Horn subterranean coffers occupy Y=-92 through Y=-162.
    // Forked Tower coffers use a much deeper coordinate band and are handled
    // separately below.
    public const float MinimumSubterraneanElevation = -250.0f;
    // Forked Tower rooms are separate map rows inside territory 1346. Confirmed
    // tower coffers occupy Y=-674 through Y=-980, so keep them separate from
    // the North Horn subterranean layer instead of treating them as dummies.
    public const float MinimumForkedTowerElevation = -1100.0f;

    public static bool IsSurfaceCandidate(Vector3 position)
        => IsValid(position) && position.Y > MinimumSurfaceElevation;

    public static bool IsSubterraneanCandidate(Vector3 position)
        => IsValid(position)
           && position.Y <= MinimumSurfaceElevation
           && position.Y > MinimumSubterraneanElevation;

    public static bool IsForkedTowerCandidate(Vector3 position)
        => IsValid(position)
           && position.Y <= MinimumSubterraneanElevation
           && position.Y > MinimumForkedTowerElevation;

    public static bool IsCandidateForLayer(
        OccultCrescentMapLayer layer,
        Vector3 position)
        => layer switch
        {
            OccultCrescentMapLayer.Subterranean => IsSubterraneanCandidate(position),
            OccultCrescentMapLayer.ForkedTower => IsForkedTowerCandidate(position),
            _ => IsSurfaceCandidate(position),
        };

    public static bool IsCandidateForMap(
        OccultCrescentMapLayer layer,
        uint mapId,
        uint treasureRowId,
        Vector3 position)
        => IsCandidateForLayer(layer, position)
           && (layer != OccultCrescentMapLayer.ForkedTower
               || ForkedTowerTreasureFloorPolicy.IsCandidateForMap(mapId, treasureRowId));

    private static bool IsValid(Vector3 position)
        => float.IsFinite(position.X)
           && float.IsFinite(position.Y)
           && float.IsFinite(position.Z);
}
