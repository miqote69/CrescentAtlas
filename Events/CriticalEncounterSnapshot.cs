namespace CrescentAtlas.Events;

public sealed record CriticalEncounterSnapshot(
    ushort EventId,
    string Name,
    Vector3 Position,
    string State,
    uint SecondsLeft,
    byte Progress,
    byte Participants);

public interface ICriticalEncounterSnapshotSource
{
    bool TryRead(out IReadOnlyList<CriticalEncounterSnapshot> encounters);
}
