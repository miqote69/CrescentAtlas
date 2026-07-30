namespace CrescentAtlas.Events;

/// <summary>
/// Stable, public-API-only representation of a FATE used by the differ.
/// Keeping this type independent of Dalamud makes the detection logic testable offline.
/// </summary>
public sealed record FateSnapshot(
    ushort FateId,
    string Name,
    Vector3 Position,
    byte Progress,
    string State,
    long TimeRemainingSeconds);
