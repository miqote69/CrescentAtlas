namespace CrescentAtlas.Notifications;

/// <summary>
/// Small state holder around the pure calculator. State is isolated by
/// instance key and never crosses an instance transition.
/// </summary>
public sealed class PotPredictionTracker
{
    private readonly Dictionary<string, List<PotObservation>> observations =
        new(StringComparer.Ordinal);
    private readonly TimeSpan provisionalInterval;
    private readonly TimeSpan duplicateWindow;

    public PotPredictionTracker(
        TimeSpan? provisionalInterval = null,
        TimeSpan? duplicateWindow = null)
    {
        this.provisionalInterval =
            provisionalInterval ?? PotPredictionCalculator.DefaultInterval;
        this.duplicateWindow = duplicateWindow ?? TimeSpan.FromSeconds(30);
    }

    public PotPrediction Observe(PotObservation observation)
    {
        if (!observations.TryGetValue(observation.InstanceKey, out var instanceObservations))
        {
            instanceObservations = [];
            observations.Add(observation.InstanceKey, instanceObservations);
        }

        var duplicate = instanceObservations.Any(existing =>
            existing.EventId == observation.EventId &&
            Vector3.DistanceSquared(existing.Position, observation.Position) < 0.01f &&
            (existing.ObservedAtUtc - observation.ObservedAtUtc).Duration() <= duplicateWindow);

        if (!duplicate)
            instanceObservations.Add(observation);

        return PotPredictionCalculator.Calculate(
            observation.InstanceKey,
            instanceObservations,
            provisionalInterval);
    }

    public PotPrediction GetPrediction(string instanceKey) =>
        observations.TryGetValue(instanceKey, out var instanceObservations)
            ? PotPredictionCalculator.Calculate(
                instanceKey,
                instanceObservations,
                provisionalInterval)
            : PotPrediction.Unknown(instanceKey);

    public IReadOnlyList<PotObservation> GetObservations(string instanceKey) =>
        observations.TryGetValue(instanceKey, out var instanceObservations)
            ? instanceObservations.ToArray()
            : Array.Empty<PotObservation>();

    public void Reset(string instanceKey) => observations.Remove(instanceKey);

    public void ResetAll() => observations.Clear();
}
