using System.Globalization;

namespace CrescentAtlas.Data;

internal static class ObservationIdentity
{
    private const int CoordinateDecimals = 2;

    public static string AggregateKey(ObservationRecord observation)
        => string.Join(
            "|",
            observation.TerritoryId.ToString(CultureInfo.InvariantCulture),
            observation.Source,
            observation.Kind.Trim(),
            observation.DataId.ToString(CultureInfo.InvariantCulture),
            observation.EventId.ToString(CultureInfo.InvariantCulture),
            observation.Key.Trim(),
            Coordinate(observation.X),
            Coordinate(observation.Y),
            Coordinate(observation.Z));

    public static string SessionFingerprint(ObservationRecord observation)
        => $"{AggregateKey(observation)}|{(observation.IsActive ? 1 : 0)}";

    public static string PositionKey(uint territoryId, string kind, uint dataId, uint eventId, Vector3 position)
        => string.Join(
            ":",
            territoryId.ToString(CultureInfo.InvariantCulture),
            kind,
            dataId.ToString(CultureInfo.InvariantCulture),
            eventId.ToString(CultureInfo.InvariantCulture),
            Coordinate(position.X),
            Coordinate(position.Y),
            Coordinate(position.Z));

    private static string Coordinate(float value)
        => MathF.Round(value, CoordinateDecimals).ToString("F2", CultureInfo.InvariantCulture);
}
