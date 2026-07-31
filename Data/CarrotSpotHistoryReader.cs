using System.Globalization;
using System.IO;
using System.Text.Json;

namespace CrescentAtlas.Data;

public static class CarrotSpotHistoryReader
{
    public static IReadOnlyList<ConfirmedCarrotSpot> Load(
        string collectionDirectory,
        uint carrotDataId)
    {
        var sessionsDirectory = Path.Combine(collectionDirectory, "sessions");
        if (!Directory.Exists(sessionsDirectory))
            return Array.Empty<ConfirmedCarrotSpot>();

        var spots = new List<ConfirmedCarrotSpot>();
        foreach (var path in Directory.EnumerateFiles(
                     sessionsDirectory,
                     "*.jsonl",
                     SearchOption.TopDirectoryOnly))
        {
            try
            {
                foreach (var line in File.ReadLines(path))
                {
                    if (TryParseLine(line, carrotDataId, out var spot))
                        spots.Add(spot);
                }
            }
            catch (IOException)
            {
                // A live session may be rotating or temporarily locked.
            }
            catch (UnauthorizedAccessException)
            {
                // History restoration is best effort and must not block loading.
            }
        }

        return spots
            .GroupBy(SpotKey, StringComparer.Ordinal)
            .Select(group => group.OrderBy(spot => spot.ConfirmedAtUtc).First())
            .OrderBy(spot => spot.ConfirmedAtUtc)
            .ToArray();
    }

    public static bool TryParseLine(
        string line,
        uint carrotDataId,
        out ConfirmedCarrotSpot spot)
    {
        spot = null!;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("kind", out var kindElement)
                || kindElement.GetString() is not ("carrot" or "carrot-candidate")
                || !root.TryGetProperty("dataId", out var dataIdElement)
                || !dataIdElement.TryGetUInt32(out var dataId)
                || dataId != carrotDataId
                || !root.TryGetProperty("territoryId", out var territoryIdElement)
                || !territoryIdElement.TryGetUInt32(out var territoryId)
                || !root.TryGetProperty("observedAtUtc", out var observedAtElement)
                || !observedAtElement.TryGetDateTimeOffset(out var observedAtUtc)
                || !TryGetSingle(root, "x", out var x)
                || !TryGetSingle(root, "y", out var y)
                || !TryGetSingle(root, "z", out var z))
            {
                return false;
            }

            spot = new ConfirmedCarrotSpot(
                territoryId,
                dataId,
                new Vector3(x, y, z),
                observedAtUtc);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string SpotKey(ConfirmedCarrotSpot spot)
        => FormattableString.Invariant(
            $"{spot.TerritoryId}:{spot.DataId}:{spot.Position.X:F1}:{spot.Position.Y:F1}:{spot.Position.Z:F1}");

    private static bool TryGetSingle(JsonElement root, string propertyName, out float value)
    {
        value = 0.0f;
        return root.TryGetProperty(propertyName, out var element)
               && element.TryGetSingle(out value)
               && float.IsFinite(value);
    }
}
