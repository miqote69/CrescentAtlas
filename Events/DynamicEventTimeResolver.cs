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
        if (displayedTimeLeft is >= 0)
            return displayedTimeLeft.Value;

        if (secondsLeft > 0)
            return secondsLeft;

        if (state is not ("Register" or "Warmup") || startTimestamp <= 0)
            return -1;

        var battleStartUnixSeconds = (long)startTimestamp
                                     + secondsRegistrationTime
                                     + secondsWarmupTime;
        var countdown = battleStartUnixSeconds - nowUnixSeconds;
        return countdown is >= 0 and <= MaximumPlausibleCountdownSeconds
            ? countdown
            : -1;
    }
}
