using Dalamud.Plugin.Services;

namespace CrescentAtlas.Events;

public interface IFateSnapshotSource
{
    bool TryRead(out IReadOnlyList<FateSnapshot> fates);
}

/// <summary>
/// Reads only the public Dalamud IFateTable surface.
/// Invalid entries and transient read failures are omitted.
/// </summary>
public sealed class DalamudFateSnapshotSource(IFateTable fateTable) : IFateSnapshotSource
{
    public bool TryRead(out IReadOnlyList<FateSnapshot> fates)
    {
        fates = Array.Empty<FateSnapshot>();

        try
        {
            var result = new List<FateSnapshot>();
            foreach (var fate in fateTable)
            {
                try
                {
                    if (!fateTable.IsValid(fate) || fate.FateId == 0)
                        continue;

                    result.Add(new FateSnapshot(
                        fate.FateId,
                        fate.Name.ToString(),
                        fate.Position,
                        fate.Progress,
                        fate.State.ToString(),
                        fate.TimeRemaining));
                }
                catch
                {
                    // A FATE may disappear while IFateTable is being enumerated.
                }
            }

            fates = result;
            return true;
        }
        catch
        {
            fates = Array.Empty<FateSnapshot>();
            return false;
        }
    }
}
