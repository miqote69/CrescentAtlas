namespace CrescentAtlas.Runtime;

public static class PotPredictionDisplayPolicy
{
    public static bool ShouldShow(
        bool configuredVisible,
        bool isSurfaceMap,
        bool hasPrediction,
        bool hasActivePotFate)
        => configuredVisible
           && isSurfaceMap
           && hasPrediction
           && !hasActivePotFate;
}
