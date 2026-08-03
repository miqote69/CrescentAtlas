namespace CrescentAtlas.Events;

public interface IFateSnapshotSource
{
    bool TryRead(out IReadOnlyList<FateSnapshot> fates);
}
