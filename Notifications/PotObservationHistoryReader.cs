using System.IO;
using System.Text.Json;

namespace CrescentAtlas.Notifications;

public static class PotObservationHistoryReader
{
    public static IReadOnlyList<PotObservation> Load(
        string collectionDirectory,
        IReadOnlySet<uint> eventIds,
        string fallbackInstanceKey)
    {
        var sessionsDirectory = Path.Combine(collectionDirectory, "sessions");
        if (!Directory.Exists(sessionsDirectory))
            return Array.Empty<PotObservation>();

        var observations = new List<PotObservation>();
        foreach (var path in Directory.EnumerateFiles(
                     sessionsDirectory,
                     "*.jsonl",
                     SearchOption.TopDirectoryOnly))
        {
            try
            {
                foreach (var line in File.ReadLines(path))
                {
                    if (TryParseLine(line, eventIds, fallbackInstanceKey, out var observation))
                        observations.Add(observation);
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

        return observations
            .OrderBy(item => item.ObservedAtUtc)
            .ToArray();
    }

    public static bool TryParseLine(
        string line,
        IReadOnlySet<uint> eventIds,
        string fallbackInstanceKey,
        out PotObservation observation)
    {
        observation = null!;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("kind", out var kind)
                || !StringComparer.Ordinal.Equals(kind.GetString(), "FateStarted")
                || !root.TryGetProperty("eventId", out var eventIdElement)
                || !eventIdElement.TryGetUInt32(out var eventId)
                || !eventIds.Contains(eventId)
                || !root.TryGetProperty("observedAtUtc", out var observedAtElement)
                || !observedAtElement.TryGetDateTimeOffset(out var observedAtUtc)
                || !TryGetSingle(root, "x", out var x)
                || !TryGetSingle(root, "y", out var y)
                || !TryGetSingle(root, "z", out var z))
            {
                return false;
            }

            var instanceKey = fallbackInstanceKey;
            if (root.TryGetProperty("properties", out var properties)
                && properties.ValueKind == JsonValueKind.Object
                && properties.TryGetProperty("instanceKey", out var instanceKeyElement)
                && !string.IsNullOrWhiteSpace(instanceKeyElement.GetString()))
            {
                instanceKey = instanceKeyElement.GetString()!;
            }

            observation = new PotObservation(
                instanceKey,
                observedAtUtc,
                eventId,
                new Vector3(x, y, z));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetSingle(JsonElement root, string propertyName, out float value)
    {
        value = 0.0f;
        return root.TryGetProperty(propertyName, out var element)
               && element.TryGetSingle(out value)
               && float.IsFinite(value);
    }
}
