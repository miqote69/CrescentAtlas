namespace CrescentAtlas.Data;

public enum ObservationSource
{
    Layout,
    ObjectTable,
    FateTable,
    DynamicEvent,
    Manual,
}

public sealed record ObservationRecord
{
    public int SchemaVersion { get; init; } = 1;

    public required string SessionId { get; init; }

    public required DateTimeOffset ObservedAtUtc { get; init; }

    public required ObservationSource Source { get; init; }

    public required string Kind { get; init; }

    public required string Key { get; init; }

    public uint TerritoryId { get; init; }

    public string TerritoryName { get; init; } = string.Empty;

    public uint DataId { get; init; }

    public uint EventId { get; init; }

    public string Name { get; init; } = string.Empty;

    public float X { get; init; }

    public float Y { get; init; }

    public float Z { get; init; }

    public bool IsActive { get; init; }

    public IReadOnlyDictionary<string, string>? Properties { get; init; }
}
