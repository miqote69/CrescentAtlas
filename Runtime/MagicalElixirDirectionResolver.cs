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

public enum MagicalElixirDistanceBand
{
    Unknown,
    VeryNear,
    Near,
    Far,
    VeryFar,
}

public sealed record MagicalElixirDirectionHint(
    CompassDirection Direction,
    Vector3 PlayerPosition,
    DateTimeOffset ObservedAtUtc,
    string Message,
    MagicalElixirDistanceBand DistanceBand = MagicalElixirDistanceBand.Unknown);

public sealed record MagicalElixirDirectionCandidate(
    ConfirmedPotTargetObservation Spot,
    float MeanAngularErrorDegrees);

public sealed record MagicalElixirLocationEstimate(
    Vector3 Position,
    float MeanAngularErrorDegrees,
    float MaximumAngularErrorDegrees,
    float UncertaintyRadiusYalms,
    bool IsReliable);

public static class MagicalElixirDirectionResolver
{
    public const float DefaultHalfWidthDegrees = 35.0f;

    public static bool TryParse(string? message, out CompassDirection direction)
        => TryParse(message, out direction, out _);

    public static bool TryParse(
        string? message,
        out CompassDirection direction,
        out MagicalElixirDistanceBand distanceBand)
    {
        direction = default;
        distanceBand = MagicalElixirDistanceBand.Unknown;
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

        distanceBand = ParseDistanceBand(text, normalized);

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

    public static MagicalElixirLocationEstimate? EstimateUnknownLocation(
        IReadOnlyCollection<MagicalElixirDirectionHint> hints)
    {
        if (hints.Count == 0)
            return null;

        var ordered = ReduceCorrelatedHints(hints);
        if (ordered.Length == 1)
        {
            var hint = ordered[0];
            var radians = DirectionDegrees(hint.Direction) * MathF.PI / 180.0f;
            var distance = NominalDistance(hint.DistanceBand);
            var initialPosition = hint.PlayerPosition + new Vector3(
                MathF.Sin(radians) * distance,
                0.0f,
                -MathF.Cos(radians) * distance);
            return new MagicalElixirLocationEstimate(
                initialPosition,
                0.0f,
                0.0f,
                UncertaintyRadius(hint.DistanceBand),
                false);
        }

        const float searchMargin = 500.0f;
        var minimumX = ordered.Min(hint => hint.PlayerPosition.X) - searchMargin;
        var maximumX = ordered.Max(hint => hint.PlayerPosition.X) + searchMargin;
        var minimumZ = ordered.Min(hint => hint.PlayerPosition.Z) - searchMargin;
        var maximumZ = ordered.Max(hint => hint.PlayerPosition.Z) + searchMargin;

        var best = FindBestGridPoint(
            ordered,
            minimumX,
            maximumX,
            minimumZ,
            maximumZ,
            20.0f);
        best = FindBestGridPoint(
            ordered,
            best.X - 30.0f,
            best.X + 30.0f,
            best.Y - 30.0f,
            best.Y + 30.0f,
            5.0f);
        best = FindBestGridPoint(
            ordered,
            best.X - 6.0f,
            best.X + 6.0f,
            best.Y - 6.0f,
            best.Y + 6.0f,
            1.0f);

        var position = new Vector3(best.X, ordered[^1].PlayerPosition.Y, best.Y);
        var errors = ordered
            .Select(hint => AngularErrorDegrees(
                DirectionDegrees(hint.Direction),
                BearingDegrees(hint.PlayerPosition, position)))
            .ToArray();
        return new MagicalElixirLocationEstimate(
            position,
            errors.Average(),
            errors.Max(),
            UncertaintyRadius(ordered[^1].DistanceBand),
            HasReliableFix(ordered));
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

    private static MagicalElixirDistanceBand ParseDistanceBand(string text, string normalized)
    {
        if (text.Contains("\u3068\u3066\u3082\u9060\u304f", StringComparison.Ordinal)
            || normalized.Contains("far, far", StringComparison.Ordinal)
            || normalized.Contains("very far", StringComparison.Ordinal))
        {
            return MagicalElixirDistanceBand.VeryFar;
        }

        if (text.Contains("\u3068\u3066\u3082\u8fd1\u304f", StringComparison.Ordinal)
            || normalized.Contains("very near", StringComparison.Ordinal)
            || normalized.Contains("very close", StringComparison.Ordinal))
        {
            return MagicalElixirDistanceBand.VeryNear;
        }

        if (text.Contains("\u8fd1\u304f", StringComparison.Ordinal)
            || normalized.Contains("near", StringComparison.Ordinal)
            || normalized.Contains("close", StringComparison.Ordinal))
        {
            return MagicalElixirDistanceBand.Near;
        }

        if (text.Contains("\u9060\u304f", StringComparison.Ordinal)
            || ContainsWord(normalized, "far"))
        {
            return MagicalElixirDistanceBand.Far;
        }

        return MagicalElixirDistanceBand.Unknown;
    }

    private static Vector2 FindBestGridPoint(
        IReadOnlyCollection<MagicalElixirDirectionHint> hints,
        float minimumX,
        float maximumX,
        float minimumZ,
        float maximumZ,
        float step)
    {
        var best = new Vector2(minimumX, minimumZ);
        var bestScore = float.PositiveInfinity;
        for (var x = minimumX; x <= maximumX + step * 0.5f; x += step)
        {
            for (var z = minimumZ; z <= maximumZ + step * 0.5f; z += step)
            {
                var candidate = new Vector3(x, 0.0f, z);
                var score = hints.Sum(hint => EstimateScore(hint, candidate));
                if (score >= bestScore)
                    continue;

                bestScore = score;
                best = new Vector2(x, z);
            }
        }

        return best;
    }

    private static float EstimateScore(
        MagicalElixirDirectionHint hint,
        Vector3 candidate)
    {
        var angularError = AngularErrorDegrees(
            DirectionDegrees(hint.Direction),
            BearingDegrees(hint.PlayerPosition, candidate));
        const float directionSectorHalfWidth = 22.5f;
        var sectorExcess = MathF.Max(0.0f, angularError - directionSectorHalfWidth);
        var angularScore = MathF.Pow(sectorExcess / 8.0f, 2.0f)
                           + (0.04f * MathF.Pow(angularError / directionSectorHalfWidth, 2.0f));

        var distance = Vector2.Distance(
            new Vector2(hint.PlayerPosition.X, hint.PlayerPosition.Z),
            new Vector2(candidate.X, candidate.Z));
        var distanceScore = hint.DistanceBand switch
        {
            MagicalElixirDistanceBand.VeryNear when distance > 25.0f
                => MathF.Pow((distance - 25.0f) / 12.0f, 2.0f),
            MagicalElixirDistanceBand.Near when distance < 20.0f
                => MathF.Pow((20.0f - distance) / 15.0f, 2.0f),
            MagicalElixirDistanceBand.Near when distance > 100.0f
                => MathF.Pow((distance - 100.0f) / 40.0f, 2.0f),
            MagicalElixirDistanceBand.Far when distance < 100.0f
                => MathF.Pow((100.0f - distance) / 40.0f, 2.0f),
            MagicalElixirDistanceBand.Far when distance > 220.0f
                => MathF.Pow((distance - 220.0f) / 60.0f, 2.0f),
            MagicalElixirDistanceBand.VeryFar when distance < 200.0f
                => MathF.Pow((200.0f - distance) / 50.0f, 2.0f),
            _ => 0.0f,
        };
        return angularScore + distanceScore;
    }

    private static float NominalDistance(MagicalElixirDistanceBand distanceBand)
        => distanceBand switch
        {
            MagicalElixirDistanceBand.VeryNear => 12.0f,
            MagicalElixirDistanceBand.Near => 60.0f,
            MagicalElixirDistanceBand.Far => 150.0f,
            MagicalElixirDistanceBand.VeryFar => 300.0f,
            _ => 150.0f,
        };

    private static MagicalElixirDirectionHint[] ReduceCorrelatedHints(
        IReadOnlyCollection<MagicalElixirDirectionHint> hints)
    {
        var ordered = hints.OrderBy(hint => hint.ObservedAtUtc).ToArray();
        var reduced = new List<MagicalElixirDirectionHint>(ordered.Length);
        foreach (var hint in ordered)
        {
            if (reduced.Count > 0
                && reduced[^1].Direction == hint.Direction
                && reduced[^1].DistanceBand == hint.DistanceBand)
            {
                reduced[^1] = hint;
            }
            else
            {
                reduced.Add(hint);
            }
        }

        return reduced.ToArray();
    }

    private static bool HasReliableFix(IReadOnlyList<MagicalElixirDirectionHint> hints)
    {
        var latestBand = hints[^1].DistanceBand;
        if (latestBand == MagicalElixirDistanceBand.VeryNear)
            return true;
        if (latestBand != MagicalElixirDistanceBand.Near)
            return false;

        var axes = hints
            .Select(hint => DirectionDegrees(hint.Direction) % 180.0f)
            .Distinct()
            .ToArray();
        return axes.Any(left => axes.Any(right =>
        {
            var difference = MathF.Abs(left - right);
            return MathF.Min(difference, 180.0f - difference) >= 22.5f;
        }));
    }

    private static float UncertaintyRadius(MagicalElixirDistanceBand distanceBand)
        => distanceBand switch
        {
            MagicalElixirDistanceBand.VeryNear => 25.0f,
            MagicalElixirDistanceBand.Near => 70.0f,
            MagicalElixirDistanceBand.Far => 140.0f,
            MagicalElixirDistanceBand.VeryFar => 600.0f,
            _ => 250.0f,
        };

    private static string SpotLocationKey(ConfirmedPotTargetObservation spot)
        => FormattableString.Invariant(
            $"{spot.TerritoryId}:{spot.Position.X:F1}:{spot.Position.Y:F1}:{spot.Position.Z:F1}");
}
