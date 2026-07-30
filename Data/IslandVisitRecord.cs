namespace CrescentAtlas.Data;

public sealed record IslandVisitRecord
{
    public int SchemaVersion { get; init; } = 1;

    public required string VisitId { get; init; }

    public uint TerritoryId { get; init; }

    public string TerritoryName { get; init; } = string.Empty;

    public required DateTimeOffset EnteredAtUtc { get; init; }

    public DateTimeOffset? ExitedAtUtc { get; init; }

    public required DateTimeOffset LastSeenAtUtc { get; init; }

    public DateTimeOffset? EstimatedContentEndUtc { get; init; }

    public required string IslandKey { get; init; }

    public string InstancePointer { get; init; } = string.Empty;

    public string ExitReason { get; init; } = string.Empty;
}
