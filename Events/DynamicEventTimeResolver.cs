namespace CrescentAtlas.Events;

public static class DynamicEventTimeResolver
{
    private const long MaximumPlausibleCountdownSeconds = 24 * 60 * 60;

    public static long Resolve(
        string state,
        long nowUnixSeconds,
        int startTimestamp,
        uint secondsLeft,
        uint secondsRegistrationTime,
        uint secondsWarmupTime,
        long? displayedTimeLeft)
    {
        // StartTimestamp is the absolute Unix time at which this CE begins.
        // It is shared by every DynamicEvent and already includes the
        // registration/warmup phases; adding those durations again shifts the
        // countdown by an event-dependent amount.
        if (state is "Register" or "Warmup" && startTimestamp > 0)
        {
            var countdown = (long)startTimestamp - nowUnixSeconds;
            if (countdown is >= 0 and <= MaximumPlausibleCountdownSeconds)
                return countdown;

            // Warmup means the scheduled start has been reached but the battle
            // state has not switched yet.
            if (state == "Warmup" && countdown < 0)
                return 0;
        }

        if (displayedTimeLeft is >= 0)
            return displayedTimeLeft.Value;

        if (secondsLeft > 0)
            return secondsLeft;

        return -1;
    }
}
