namespace CrescentAtlas.Data;

public sealed record ConfirmedPotChestObservation(
    uint TerritoryId,
    uint DataId,
    string Name,
    Vector3 Position,
    DateTimeOffset ObservedAtUtc);

public static class ConfirmedPotChestObservations
{
    public static IReadOnlyList<ConfirmedPotChestObservation> NorthHorn { get; } =
    [
        new(
            1346,
            2014741,
            "Golden treasure coffer",
            new Vector3(-747.4032f, 28.970308f, -492.1095f),
            new DateTimeOffset(2026, 7, 30, 3, 51, 33, TimeSpan.Zero)),
    ];
}
