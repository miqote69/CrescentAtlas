using CrescentAtlas.Contracts;

namespace CrescentAtlas.Runtime;

public static class FieldNotificationPolicy
{
    public static bool ShouldEmit(
        OccultCrescentMapLayer mapLayer,
        bool hasForkedTowerTreasureEvidence = false)
        => mapLayer != OccultCrescentMapLayer.ForkedTower
           && !hasForkedTowerTreasureEvidence;
}
