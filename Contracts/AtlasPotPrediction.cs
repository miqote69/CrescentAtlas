namespace CrescentAtlas.Contracts;

public sealed record AtlasPotPrediction(
    DateTimeOffset NextOccurrenceUtc,
    TimeSpan EstimatedInterval,
    uint PredictedEventId,
    Vector3 PredictedPosition,
    uint IconId,
    int ObservationCount,
    bool IsConfirmed);
