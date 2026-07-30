namespace CrescentAtlas.Events;

public sealed record CriticalEncounterSnapshot(
    ushort EventId,
    string Name,
    Vector3 Position,
    string State,
    long SecondsLeft,
    byte Progress,
    byte Participants,
    uint IconId = 0);

public interface ICriticalEncounterSnapshotSource
{
    bool TryRead(out IReadOnlyList<CriticalEncounterSnapshot> encounters);
}
