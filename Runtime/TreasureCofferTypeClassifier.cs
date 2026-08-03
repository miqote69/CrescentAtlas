namespace CrescentAtlas.Runtime;

public static class TreasureCofferTypeClassifier
{
    public const uint BronzeCofferSgbId = 1596;
    public const uint SilverCofferSgbId = 1597;

    public static string ResolveFromSgbId(uint sgbId)
        => sgbId switch
        {
            BronzeCofferSgbId => "bronze",
            SilverCofferSgbId => "silver",
            _ => string.Empty,
        };
}
