namespace CrescentAtlas.Data;

public sealed record ObservationSnapshot
{
    public int SchemaVersion { get; init; } = 1;

    public DateTimeOffset UpdatedAtUtc { get; init; }

    public IReadOnlyList<ObservationAggregate> Observations { get; init; } = [];
}

public sealed record ObservationAggregate
{
    public required string AggregateKey { get; init; }

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

    public DateTimeOffset FirstObservedAtUtc { get; init; }

    public DateTimeOffset LastObservedAtUtc { get; init; }

    public int SeenCount { get; init; }

    public string LastSessionId { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string>? Properties { get; init; }
}
