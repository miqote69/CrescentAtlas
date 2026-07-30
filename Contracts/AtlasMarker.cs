namespace CrescentAtlas.Contracts;

public enum AtlasMarkerKind
{
    TreasureCandidate,
    ActiveTreasure,
    Carrot,
    Fate,
    CriticalEncounter,
    PotFate,
    PotPrediction,
    PotChest,
    Player,
}

public sealed record AtlasMarker(
    string Key,
    AtlasMarkerKind Kind,
    string Label,
    Vector3 Position,
    DateTimeOffset ObservedAtUtc,
    bool IsActive,
    uint TerritoryId,
    uint DataId = 0,
    uint EventId = 0);
