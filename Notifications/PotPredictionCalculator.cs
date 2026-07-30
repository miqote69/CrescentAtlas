namespace CrescentAtlas.Notifications;

/// <summary>
/// Pure pot-timing logic. It has no Dalamud dependency and can be exercised
/// by an offline unit test with deterministic timestamps and positions.
/// </summary>
public static class PotPredictionCalculator
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan MinimumPlausibleInterval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaximumPlausibleInterval = TimeSpan.FromHours(3);

    public static PotPrediction Calculate(
        string instanceKey,
        IEnumerable<PotObservation> observations,
        TimeSpan? provisionalInterval = null)
    {
        var ordered = observations
            .Where(item => StringComparer.Ordinal.Equals(item.InstanceKey, instanceKey))
            .OrderBy(item => item.ObservedAtUtc)
            .ToArray();

        if (ordered.Length == 0)
            return PotPrediction.Unknown(instanceKey);

        var fallback = IsPlausible(provisionalInterval ?? DefaultInterval)
            ? provisionalInterval ?? DefaultInterval
            : DefaultInterval;
        var latest = ordered[^1];

        if (ordered.Length == 1)
        {
            return new PotPrediction(
                instanceKey,
                PotPredictionConfidence.Provisional,
                latest.ObservedAtUtc + fallback,
                fallback,
                latest.EventId,
                latest.Position,
                1);
        }

        var previous = ordered[^2];
        var measured = latest.ObservedAtUtc - previous.ObservedAtUtc;
        if (!IsPlausible(measured))
        {
            return new PotPrediction(
                instanceKey,
                PotPredictionConfidence.Provisional,
                latest.ObservedAtUtc + fallback,
                fallback,
                latest.EventId,
                latest.Position,
                ordered.Length);
        }

        // With two alternating observations, the best evidence for the next
        // location/event is the preceding one. More observations keep applying
        // the same two-step cycle without any hard-coded North Horn data.
        return new PotPrediction(
            instanceKey,
            PotPredictionConfidence.Confirmed,
            latest.ObservedAtUtc + measured,
            measured,
            previous.EventId,
            previous.Position,
            ordered.Length);
    }

    private static bool IsPlausible(TimeSpan interval) =>
        interval >= MinimumPlausibleInterval &&
        interval <= MaximumPlausibleInterval;
}
