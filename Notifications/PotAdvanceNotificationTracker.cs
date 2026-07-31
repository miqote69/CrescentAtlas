namespace CrescentAtlas.Notifications;

public sealed class PotAdvanceNotificationTracker
{
    private readonly Dictionary<string, DateTimeOffset> notifiedOccurrences =
        new(StringComparer.Ordinal);

    public bool ShouldNotify(
        string instanceKey,
        DateTimeOffset nextOccurrenceUtc,
        DateTimeOffset now,
        TimeSpan leadTime)
    {
        var remaining = nextOccurrenceUtc - now;
        if (leadTime <= TimeSpan.Zero
            || remaining <= TimeSpan.Zero
            || remaining > leadTime)
        {
            return false;
        }

        if (notifiedOccurrences.TryGetValue(instanceKey, out var notified)
            && notified == nextOccurrenceUtc)
        {
            return false;
        }

        notifiedOccurrences[instanceKey] = nextOccurrenceUtc;
        return true;
    }

    public void Reset(string instanceKey) => notifiedOccurrences.Remove(instanceKey);

    public void ResetAll() => notifiedOccurrences.Clear();
}
