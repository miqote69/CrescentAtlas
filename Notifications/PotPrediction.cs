namespace CrescentAtlas.Notifications;

public enum PotPredictionConfidence
{
    Unknown,
    Provisional,
    Confirmed,
}

public sealed record PotObservation(
    string InstanceKey,
    DateTimeOffset ObservedAtUtc,
    uint EventId,
    Vector3 Position);

public sealed record PotPrediction(
    string InstanceKey,
    PotPredictionConfidence Confidence,
    DateTimeOffset? NextOccurrenceUtc,
    TimeSpan? EstimatedInterval,
    uint? PredictedEventId,
    Vector3? PredictedPosition,
    int ObservationCount)
{
    public static PotPrediction Unknown(string instanceKey) =>
        new(
            instanceKey,
            PotPredictionConfidence.Unknown,
            null,
            null,
            null,
            null,
            0);
}
