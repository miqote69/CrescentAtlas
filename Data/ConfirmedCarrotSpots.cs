namespace CrescentAtlas.Data;

public sealed record ConfirmedCarrotSpot(
    uint TerritoryId,
    uint DataId,
    Vector3 Position,
    DateTimeOffset ConfirmedAtUtc);

public static class ConfirmedCarrotSpots
{
    public static IReadOnlyList<ConfirmedCarrotSpot> NorthHorn { get; } =
    [
        new(1346, ConfirmedCarrotObjects.FortuneCarrotDataId,
            new Vector3(960.0f, 97.05797f, -879.0f),
            new DateTimeOffset(2026, 7, 30, 4, 4, 12, TimeSpan.Zero)),
        new(1346, ConfirmedCarrotObjects.FortuneCarrotDataId,
            new Vector3(-500.0f, 48.000004f, -867.6f),
            new DateTimeOffset(2026, 7, 30, 5, 37, 12, TimeSpan.Zero)),
        new(1346, ConfirmedCarrotObjects.FortuneCarrotDataId,
            new Vector3(-808.0f, 6.3495464f, -879.0f),
            new DateTimeOffset(2026, 7, 30, 5, 39, 0, TimeSpan.Zero)),
        new(1346, ConfirmedCarrotObjects.FortuneCarrotDataId,
            new Vector3(-560.9f, 50.74249f, -447.0f),
            new DateTimeOffset(2026, 7, 30, 6, 45, 43, TimeSpan.Zero)),
        new(1346, ConfirmedCarrotObjects.FortuneCarrotDataId,
            new Vector3(756.858f, 68.92707f, -79.33746f),
            new DateTimeOffset(2026, 7, 30, 10, 57, 44, TimeSpan.Zero)),
        new(1346, ConfirmedCarrotObjects.FortuneCarrotDataId,
            new Vector3(-604.0f, 160.05638f, 939.1f),
            new DateTimeOffset(2026, 7, 30, 13, 9, 19, TimeSpan.Zero)),
        new(1346, ConfirmedCarrotObjects.FortuneCarrotDataId,
            new Vector3(-814.6948f, 5.6813054f, -561.0853f),
            new DateTimeOffset(2026, 7, 30, 13, 14, 39, TimeSpan.Zero)),
        new(1346, ConfirmedCarrotObjects.FortuneCarrotDataId,
            new Vector3(882.1526f, 53.999996f, 115.9092f),
            new DateTimeOffset(2026, 7, 31, 0, 3, 59, TimeSpan.Zero)),
        new(1346, ConfirmedCarrotObjects.FortuneCarrotDataId,
            new Vector3(-847.9f, 114.0f, 196.6f),
            new DateTimeOffset(2026, 8, 1, 6, 26, 51, TimeSpan.Zero)),
    ];
}
