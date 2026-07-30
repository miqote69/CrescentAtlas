using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

            var displayedEvents = ReadDisplayedEvents();
            var result = new List<CriticalEncounterSnapshot>();
            foreach (ref readonly var dynamicEvent in container->Events)
            {
                if (dynamicEvent.DynamicEventId == 0 ||
                    dynamicEvent.State == DynamicEventState.Inactive)
                {
                    continue;
                }

                var name = dynamicEvent.Name.ToString();
                var displayedTime = FindDisplayedTime(
                    displayedEvents,
                    dynamicEvent.DynamicEventId,
                    name);
                var timing = ReadTiming(in dynamicEvent);
                result.Add(new CriticalEncounterSnapshot(
                    dynamicEvent.DynamicEventId,
                    name,
                    dynamicEvent.MapMarker.Position,
                    dynamicEvent.State.ToString(),
                    DynamicEventTimeResolver.Resolve(
                        dynamicEvent.State.ToString(),
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        timing.StartTimestamp,
                        timing.SecondsLeft,
                        timing.SecondsRegistrationTime,
                        timing.SecondsWarmupTime,
                        displayedTime),
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

    private static List<DisplayedDynamicEvent> ReadDisplayedEvents()
    {
        var result = new List<DisplayedDynamicEvent>();
        var agent = AgentMycBattleAreaInfo.Instance();
        if (agent is null || agent->MycDynamicEventData is null)
            return result;

        var data = agent->MycDynamicEventData;
        var count = Math.Min(data->Count, (byte)3);
        for (var index = 0; index < count; index++)
        {
            ref readonly var displayedEvent = ref data->Array[index];
            var name = displayedEvent.Name.ToString();
            if (displayedEvent.Id != 0 || !string.IsNullOrWhiteSpace(name))
            {
                result.Add(new DisplayedDynamicEvent(
                    displayedEvent.Id,
                    name,
                    displayedEvent.TimeLeft));
            }
        }

        return result;
    }

    private static long? FindDisplayedTime(
        IReadOnlyList<DisplayedDynamicEvent> displayedEvents,
        ushort dynamicEventId,
        string name)
    {
        var byId = displayedEvents.FirstOrDefault(displayedEvent =>
            displayedEvent.Id == dynamicEventId);
        if (byId is not null)
            return byId.TimeLeft;

        var byName = displayedEvents.FirstOrDefault(displayedEvent =>
            !string.IsNullOrWhiteSpace(name)
            && DynamicEventNameMatcher.IsMatch(displayedEvent.Name, name));
        return byName?.TimeLeft;
    }

    private static DynamicEventTiming ReadTiming(in DynamicEvent dynamicEvent)
    {
        ref var mutableEvent = ref Unsafe.AsRef(in dynamicEvent);
        ref var timing = ref Unsafe.As<DynamicEvent, DynamicEventTimingOverlay>(ref mutableEvent);
        return new DynamicEventTiming(
            timing.StartTimestamp,
            timing.SecondsLeft,
            timing.SecondsRegistrationTime,
            timing.SecondsWarmupTime);
    }

    private sealed record DisplayedDynamicEvent(
        ushort Id,
        string Name,
        long TimeLeft);

    private readonly record struct DynamicEventTiming(
        int StartTimestamp,
        uint SecondsLeft,
        uint SecondsRegistrationTime,
        uint SecondsWarmupTime);

    [StructLayout(LayoutKind.Explicit, Size = 0x74)]
    private struct DynamicEventTimingOverlay
    {
        [FieldOffset(0x60)] public int StartTimestamp;
        [FieldOffset(0x64)] public uint SecondsLeft;
        [FieldOffset(0x6C)] public uint SecondsRegistrationTime;
        [FieldOffset(0x70)] public uint SecondsWarmupTime;
    }
}
