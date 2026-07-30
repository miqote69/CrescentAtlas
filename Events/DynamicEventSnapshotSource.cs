using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

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

            var displayTimes = ReadDisplayedEventTimes();
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
                    displayTimes.TryGetValue(dynamicEvent.DynamicEventId, out var displayedTime)
                        ? displayedTime
                        : dynamicEvent.SecondsLeft > 0
                            ? dynamicEvent.SecondsLeft
                            : -1,
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

    private static Dictionary<ushort, long> ReadDisplayedEventTimes()
    {
        var result = new Dictionary<ushort, long>();
        var agent = AgentMycBattleAreaInfo.Instance();
        if (agent is null || agent->MycDynamicEventData is null)
            return result;

        var data = agent->MycDynamicEventData;
        var count = Math.Min(data->Count, (byte)3);
        for (var index = 0; index < count; index++)
        {
            ref readonly var displayedEvent = ref data->Array[index];
            if (displayedEvent.Id != 0)
                result[displayedEvent.Id] = displayedEvent.TimeLeft;
        }

        return result;
    }
}
