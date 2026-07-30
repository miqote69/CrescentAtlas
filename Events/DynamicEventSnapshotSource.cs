using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;

namespace CrescentAtlas.Events;

/// <summary>
/// Best-effort, read-only view of the client dynamic-event container.
/// Any unavailable pointer, incompatible layout, or transient exception fails closed.
/// </summary>
public sealed unsafe class DynamicEventSnapshotSource : ICriticalEncounterSnapshotSource
{
    public bool TryRead(out IReadOnlyList<CriticalEncounterSnapshot> encounters)
    {
        encounters = Array.Empty<CriticalEncounterSnapshot>();

        try
        {
            var container = DynamicEventContainer.GetInstance();
            if (container is null)
                return false;

            var result = new List<CriticalEncounterSnapshot>();
            foreach (ref readonly var dynamicEvent in container->Events)
            {
                if (dynamicEvent.DynamicEventId == 0 ||
                    dynamicEvent.State == DynamicEventState.Inactive)
                {
                    continue;
                }

                result.Add(new CriticalEncounterSnapshot(
                    dynamicEvent.DynamicEventId,
                    dynamicEvent.Name.ToString(),
                    dynamicEvent.MapMarker.Position,
                    dynamicEvent.State.ToString(),
                    dynamicEvent.SecondsLeft,
                    dynamicEvent.Progress,
                    dynamicEvent.Participants,
                    dynamicEvent.MapMarker.IconId));
            }

            encounters = result;
            return true;
        }
        catch
        {
            encounters = Array.Empty<CriticalEncounterSnapshot>();
            return false;
        }
    }
}
