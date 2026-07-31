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
    private readonly IReadOnlyDictionary<uint, Vector3> knownEventPositions;

    public PotPredictionTracker(
        TimeSpan? provisionalInterval = null,
        TimeSpan? duplicateWindow = null,
        IReadOnlyDictionary<uint, Vector3>? knownEventPositions = null)
    {
        this.provisionalInterval =
            provisionalInterval ?? PotPredictionCalculator.DefaultInterval;
        this.duplicateWindow = duplicateWindow ?? TimeSpan.FromMinutes(25);
        this.knownEventPositions = knownEventPositions is null
            ? new Dictionary<uint, Vector3>()
            : new Dictionary<uint, Vector3>(knownEventPositions);
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

        return CalculateForInstance(observation.InstanceKey, instanceObservations);
    }

    public PotPrediction GetPrediction(string instanceKey) =>
        observations.TryGetValue(instanceKey, out var instanceObservations)
            ? CalculateForInstance(instanceKey, instanceObservations)
            : PotPrediction.Unknown(instanceKey);

    public PotPrediction GetUpcomingPrediction(string instanceKey, DateTimeOffset now)
    {
        if (!observations.TryGetValue(instanceKey, out var instanceObservations))
            return PotPrediction.Unknown(instanceKey);

        var prediction = CalculateForInstance(instanceKey, instanceObservations);
        if (prediction.NextOccurrenceUtc is not { } next
            || prediction.EstimatedInterval is not { } interval
            || interval <= TimeSpan.Zero)
        {
            return prediction;
        }

        var ordered = instanceObservations
            .OrderBy(item => item.ObservedAtUtc)
            .ToArray();
        var advances = 0;
        while (next <= now && advances < 10000)
        {
            next += interval;
            advances++;
        }

        var latest = ordered[^1];
        var alternate = FindAlternateObservation(ordered, latest);
        var predicted = advances % 2 == 0
            ? alternate ?? latest
            : latest;
        return prediction with
        {
            NextOccurrenceUtc = next,
            PredictedEventId = predicted.EventId,
            PredictedPosition = predicted.Position,
        };
    }

    public IReadOnlyList<PotObservation> GetObservations(string instanceKey) =>
        observations.TryGetValue(instanceKey, out var instanceObservations)
            ? instanceObservations.ToArray()
            : Array.Empty<PotObservation>();

    private PotPrediction CalculateForInstance(
        string instanceKey,
        IReadOnlyList<PotObservation> instanceObservations)
    {
        var prediction = PotPredictionCalculator.Calculate(
            instanceKey,
            instanceObservations,
            provisionalInterval);
        if (instanceObservations.Count != 1)
            return prediction;

        var latest = instanceObservations[0];
        var alternate = FindKnownAlternate(latest);
        return alternate is null
            ? prediction
            : prediction with
            {
                PredictedEventId = alternate.EventId,
                PredictedPosition = alternate.Position,
            };
    }

    private PotObservation? FindAlternateObservation(
        IReadOnlyList<PotObservation> ordered,
        PotObservation latest)
    {
        var observedAlternate = ordered
            .Take(ordered.Count - 1)
            .LastOrDefault(item => item.EventId != latest.EventId);
        return observedAlternate ?? FindKnownAlternate(latest);
    }

    private PotObservation? FindKnownAlternate(PotObservation latest)
    {
        foreach (var known in knownEventPositions.OrderBy(item => item.Key))
        {
            if (known.Key == latest.EventId)
                continue;

            return new PotObservation(
                latest.InstanceKey,
                latest.ObservedAtUtc,
                known.Key,
                known.Value);
        }

        return null;
    }

    public void Reset(string instanceKey) => observations.Remove(instanceKey);

    public void ResetAll() => observations.Clear();
}
