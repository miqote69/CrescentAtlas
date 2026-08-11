namespace CrescentAtlas.Runtime;

public static class TreasureCofferTypeClassifier
{
    public const uint BronzeCofferSgbId = 1596;
    public const uint SilverCofferSgbId = 1597;
    public const uint ForkedTowerFinalGoldCofferDataId = 1999;

    public static string ResolveFromSgbId(uint sgbId)
        => sgbId switch
        {
            BronzeCofferSgbId => "bronze",
            SilverCofferSgbId => "silver",
            _ => string.Empty,
        };

    public static string Resolve(uint treasureDataId, uint sgbId)
        => treasureDataId == ForkedTowerFinalGoldCofferDataId
            ? "gold"
            : ResolveFromSgbId(sgbId);

    public static string ResolveActive(uint treasureDataId, bool isConfirmedSilver)
        => treasureDataId == ForkedTowerFinalGoldCofferDataId
            ? "gold"
            : isConfirmedSilver
                ? "silver"
                : string.Empty;
}
