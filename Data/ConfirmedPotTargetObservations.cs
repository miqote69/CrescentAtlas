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

    // The destination is fixed independently of which coffer grade appears there.
    // DataId remains zero until a live coffer is observed at the destination.
    public static IReadOnlyList<ConfirmedPotTargetObservation> NorthHorn { get; } =
    [
        Goal(-960.0f, 48.0f, -425.8f),
        Goal(-853.493f, 58.0f, -323.8983f),
        Goal(-839.9977f, 160.0f, 740.0f),
        Goal(-809.0f, 6.3495464f, -879.0f),
        Goal(-747.4032f, 28.970308f, -492.1095f),
        Goal(-656.9f, 23.036425f, -799.3f),
        Goal(-628.4385f, 49.07533f, -449.5009f),
        Goal(-603.0f, 32.0f, -869.0f),
        Goal(-586.3f, 47.81013f, -715.2f),
        Goal(-536.1014f, 87.01824f, 149.8447f),
        Goal(-498.7f, 11.051006f, 128.9f),
        Goal(-487.8f, 48.000015f, -953.2f),
        Goal(-251.781f, 65.949005f, -864.3828f),
        Goal(-223.8233f, 10.891144f, -353.9438f),
        Goal(-190.0f, 61.75258f, -763.0f),
        Goal(-184.5137f, 71.1816f, 667.8036f),
        Goal(-127.0f, 71.47446f, 808.4f),
        Goal(-113.4943f, 5.0879984f, -74.15943f),
        Goal(-86.0f, 60.596237f, -737.0f),
        Goal(1.768392f, 71.555756f, -872.2798f),
        Goal(11.98766f, 68.15505f, 795.707f),
        Goal(47.6f, 3.8843424f, -218.3f),
        Goal(93.4f, 3.7155468f, -114.3f),
        Goal(151.9998f, 61.106945f, -842.0175f),
        Goal(190.3622f, 3.880325f, -204.7095f),
        Goal(321.198f, 59.85f, -889.8872f),
        Goal(385.0f, 33.0f, -177.0f),
        Goal(546.56f, 36.120197f, 143.3104f),
        Goal(593.0f, 39.622505f, 34.0f),
        Goal(714.698f, 69.24771f, 262.6901f),
        Goal(810.8979f, 78.39757f, -278.8099f),
        Goal(830.0979f, 77.75924f, -148.9099f),
        Goal(909.0f, 97.05797f, -961.8f),
        Goal(928.8978f, 74.0003f, -332.8099f),
        Goal(939.2178f, 80.269966f, -273.1175f),
    ];

    private static ConfirmedPotTargetObservation Goal(float x, float y, float z) =>
        new(
            1346,
            0,
            "Magical Elixir goal",
            new Vector3(x, y, z),
            DateTimeOffset.UnixEpoch);
}
