namespace CrescentAtlas.Data;

public sealed record ConfirmedPotTargetObservation(
    uint TerritoryId,
    uint DataId,
    string Name,
    Vector3 Position,
    DateTimeOffset ObservedAtUtc);

public static class ConfirmedPotTargetObservations
{
    public static IReadOnlySet<uint> EventObjectDataIds { get; } =
        new HashSet<uint>
        {
            2014741, // Gold coffer
            2014742, // Silver coffer
            2014743, // Bronze coffer
        };

    public static IReadOnlyList<ConfirmedPotTargetObservation> NorthHorn { get; } =
    [
        new(
            1346,
            2014741,
            "Magic Pot target",
            new Vector3(-747.4032f, 28.970308f, -492.1095f),
            new DateTimeOffset(2026, 7, 30, 3, 51, 33, TimeSpan.Zero)),
        new(
            1346,
            2014741,
            "Magic Pot target",
            new Vector3(-656.9f, 23.036425f, -799.3f),
            new DateTimeOffset(2026, 7, 30, 7, 8, 2, TimeSpan.Zero)),
        new(
            1346,
            2014741,
            "Magic Pot target",
            new Vector3(593.0f, 39.622505f, 34.0f),
            new DateTimeOffset(2026, 7, 30, 9, 5, 36, TimeSpan.Zero)),
    ];
}
