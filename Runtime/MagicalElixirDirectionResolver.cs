using CrescentAtlas.Data;

namespace CrescentAtlas.Runtime;

public enum CompassDirection
{
    North,
    NorthEast,
    East,
    SouthEast,
    South,
    SouthWest,
    West,
    NorthWest,
}

public sealed record MagicalElixirDirectionHint(
    CompassDirection Direction,
    Vector3 PlayerPosition,
    DateTimeOffset ObservedAtUtc,
    string Message);

public sealed record MagicalElixirDirectionCandidate(
    ConfirmedPotTargetObservation Spot,
    float MeanAngularErrorDegrees);

public static class MagicalElixirDirectionResolver
{
    public const float DefaultHalfWidthDegrees = 35.0f;

    public static bool TryParse(string? message, out CompassDirection direction)
    {
        direction = default;
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var text = message.Trim();
        var normalized = text.ToLowerInvariant()
            .Replace("north-east", "northeast", StringComparison.Ordinal)
            .Replace("north west", "northwest", StringComparison.Ordinal)
            .Replace("north-west", "northwest", StringComparison.Ordinal)
            .Replace("south-east", "southeast", StringComparison.Ordinal)
            .Replace("south east", "southeast", StringComparison.Ordinal)
            .Replace("south-west", "southwest", StringComparison.Ordinal)
            .Replace("south west", "southwest", StringComparison.Ordinal);

        var looksLikeDirectionHint = text.Contains("\u65b9\u89d2", StringComparison.Ordinal)
                                     || text.Contains("\u65b9\u5411", StringComparison.Ordinal)
                                     || text.Contains("\u53cd\u5fdc", StringComparison.Ordinal)
                                     || text.Contains("\u6c17\u914d", StringComparison.Ordinal)
                                     || text.Contains("\u793a\u3057", StringComparison.Ordinal)
                                     || text.Contains("\u611f\u3058", StringComparison.Ordinal)
                                     || text.Contains("\u9065\u304b", StringComparison.Ordinal)
                                     || text.Contains("\u306f\u308b\u304b", StringComparison.Ordinal)
                                     || text.Contains("\u9060\u304f", StringComparison.Ordinal)
                                     || text.Contains("\u3059\u3050", StringComparison.Ordinal)
                                     || text.Contains("\u76f4\u3050", StringComparison.Ordinal)
                                     || text.Contains("\u9593\u8fd1", StringComparison.Ordinal)
                                     || text.Contains("\u8fd1\u304f", StringComparison.Ordinal)
                                     || normalized.Contains("direction", StringComparison.Ordinal)
                                     || normalized.Contains("treasure", StringComparison.Ordinal)
                                     || normalized.Contains("elixir", StringComparison.Ordinal)
                                     || normalized.StartsWith("far ", StringComparison.Ordinal)
                                     || normalized.StartsWith("far,", StringComparison.Ordinal)
                                     || normalized.StartsWith("immediately ", StringComparison.Ordinal);
        if (!looksLikeDirectionHint)
            return false;

        if (text.Contains("\u5317\u6771", StringComparison.Ordinal)
            || normalized.Contains("northeast", StringComparison.Ordinal))
        {
            direction = CompassDirection.NorthEast;
            return true;
        }

        if (text.Contains("\u5357\u6771", StringComparison.Ordinal)
            || normalized.Contains("southeast", StringComparison.Ordinal))
        {
            direction = CompassDirection.SouthEast;
            return true;
        }

        if (text.Contains("\u5357\u897f", StringComparison.Ordinal)
            || normalized.Contains("southwest", StringComparison.Ordinal))
        {
            direction = CompassDirection.SouthWest;
            return true;
        }

        if (text.Contains("\u5317\u897f", StringComparison.Ordinal)
            || normalized.Contains("northwest", StringComparison.Ordinal))
        {
            direction = CompassDirection.NorthWest;
            return true;
        }

        if (text.Contains('\u5317') || ContainsWord(normalized, "north"))
        {
            direction = CompassDirection.North;
            return true;
        }

        if (text.Contains('\u6771') || ContainsWord(normalized, "east"))
        {
            direction = CompassDirection.East;
            return true;
        }

        if (text.Contains('\u5357') || ContainsWord(normalized, "south"))
        {
            direction = CompassDirection.South;
            return true;
        }

        if (text.Contains('\u897f') || ContainsWord(normalized, "west"))
        {
            direction = CompassDirection.West;
            return true;
        }

        return false;
    }

    public static IReadOnlyList<MagicalElixirDirectionCandidate> Resolve(
        uint territoryId,
        IReadOnlyCollection<ConfirmedPotTargetObservation> knownSpots,
        IReadOnlyCollection<MagicalElixirDirectionHint> hints,
        int maximumCandidates = 3,
        float halfWidthDegrees = DefaultHalfWidthDegrees)
    {
        if (maximumCandidates <= 0 || hints.Count == 0)
            return Array.Empty<MagicalElixirDirectionCandidate>();

        return knownSpots
            .Where(spot => spot.TerritoryId == territoryId)
            .GroupBy(SpotLocationKey, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(spot => spot.ObservedAtUtc).First())
            .Select(spot => new
            {
                Spot = spot,
                Errors = hints
                    .Select(hint => AngularErrorDegrees(
                        DirectionDegrees(hint.Direction),
                        BearingDegrees(hint.PlayerPosition, spot.Position)))
                    .ToArray(),
            })
            .Where(candidate => candidate.Errors.All(error => error <= halfWidthDegrees))
            .Select(candidate => new MagicalElixirDirectionCandidate(
                candidate.Spot,
                candidate.Errors.Average()))
            .OrderBy(candidate => candidate.MeanAngularErrorDegrees)
            .ThenBy(candidate => Vector3.DistanceSquared(
                hints.Last().PlayerPosition,
                candidate.Spot.Position))
            .Take(maximumCandidates)
            .ToArray();
    }

    public static float BearingDegrees(Vector3 from, Vector3 to)
    {
        var deltaX = to.X - from.X;
        var deltaZ = to.Z - from.Z;
        var degrees = MathF.Atan2(deltaX, -deltaZ) * 180.0f / MathF.PI;
        return NormalizeDegrees(degrees);
    }

    public static float DirectionDegrees(CompassDirection direction)
        => direction switch
        {
            CompassDirection.North => 0.0f,
            CompassDirection.NorthEast => 45.0f,
            CompassDirection.East => 90.0f,
            CompassDirection.SouthEast => 135.0f,
            CompassDirection.South => 180.0f,
            CompassDirection.SouthWest => 225.0f,
            CompassDirection.West => 270.0f,
            CompassDirection.NorthWest => 315.0f,
            _ => 0.0f,
        };

    private static float AngularErrorDegrees(float left, float right)
    {
        var difference = MathF.Abs(NormalizeDegrees(left) - NormalizeDegrees(right));
        return MathF.Min(difference, 360.0f - difference);
    }

    private static float NormalizeDegrees(float degrees)
    {
        var normalized = degrees % 360.0f;
        return normalized < 0.0f ? normalized + 360.0f : normalized;
    }

    private static bool ContainsWord(string text, string word)
    {
        var index = text.IndexOf(word, StringComparison.Ordinal);
        while (index >= 0)
        {
            var before = index == 0 || !char.IsLetter(text[index - 1]);
            var afterIndex = index + word.Length;
            var after = afterIndex == text.Length || !char.IsLetter(text[afterIndex]);
            if (before && after)
                return true;
            index = text.IndexOf(word, index + 1, StringComparison.Ordinal);
        }

        return false;
    }

    private static string SpotLocationKey(ConfirmedPotTargetObservation spot)
        => FormattableString.Invariant(
            $"{spot.TerritoryId}:{spot.Position.X:F1}:{spot.Position.Y:F1}:{spot.Position.Z:F1}");
}
