namespace CrescentAtlas.Data;

public sealed record ConfirmedSilverTreasureSpot(
    uint TerritoryId,
    uint DataId,
    Vector3 Position,
    DateTimeOffset ConfirmedAtUtc);

public static class ConfirmedSilverTreasureSpots
{
    public static IReadOnlySet<uint> EventObjectDataIds { get; } =
        new HashSet<uint> { 2014742 };

    public static IReadOnlyList<ConfirmedSilverTreasureSpot> NorthHorn { get; } =
    [
        new(1346, 2006, new Vector3(383.29138f, 32.97461f, -175.67712f),
            new DateTimeOffset(2026, 7, 30, 5, 23, 7, TimeSpan.Zero)),
        new(1346, 2007, new Vector3(-2.3347168f, 66.666626f, -814.9081f),
            new DateTimeOffset(2026, 7, 30, 5, 33, 47, TimeSpan.Zero)),
        new(1346, 2010, new Vector3(634.7904f, 60.501953f, -831.81506f),
            new DateTimeOffset(2026, 7, 30, 5, 17, 42, TimeSpan.Zero)),
    ];
}
