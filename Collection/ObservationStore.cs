using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrescentAtlas.Contracts;
using CrescentAtlas.Data;
using Dalamud.Plugin;

namespace CrescentAtlas.Collection;

/// <summary>
/// Persists locally observed field data. No network or external-process access is performed.
/// </summary>
public sealed class ObservationStore : IObservationSink, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonOptions)
    {
        WriteIndented = true,
    };

    private readonly object syncRoot = new();
    private readonly HashSet<string> sessionFingerprints = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ObservationAggregate> aggregates = new(StringComparer.Ordinal);
    private readonly FileStream sessionStream;
    private readonly StreamWriter sessionWriter;
    private bool disposed;

    public ObservationStore(IDalamudPluginInterface pluginInterface, string directoryName = "collection")
        : this(Path.Combine(pluginInterface.ConfigDirectory.FullName, directoryName))
    {
    }

    /// <summary>
    /// Path-based overload intended for deterministic tests and local tooling.
    /// </summary>
    public ObservationStore(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        OutputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(OutputDirectory);

        var sessionsDirectory = Path.Combine(OutputDirectory, "sessions");
        Directory.CreateDirectory(sessionsDirectory);

        SessionId = $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}";
        SessionFilePath = Path.Combine(sessionsDirectory, $"{SessionId}.jsonl");
        SnapshotFilePath = Path.Combine(OutputDirectory, "snapshot.json");

        LoadSnapshot();

        sessionStream = new FileStream(
            SessionFilePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 16 * 1024,
            FileOptions.SequentialScan);
        sessionWriter = new StreamWriter(sessionStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = false,
            NewLine = "\n",
        };
    }

    public string SessionId { get; }

    public string OutputDirectory { get; }

    public string SessionFilePath { get; }

    public string SnapshotFilePath { get; }

    public int SessionObservationCount
    {
        get
        {
            lock (syncRoot)
                return sessionFingerprints.Count;
        }
    }

    public void Record(ObservationRecord observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            var normalized = observation with
            {
                SessionId = SessionId,
                ObservedAtUtc = observation.ObservedAtUtc == default
                    ? DateTimeOffset.UtcNow
                    : observation.ObservedAtUtc.ToUniversalTime(),
                Kind = observation.Kind.Trim(),
                Key = observation.Key.Trim(),
                TerritoryName = observation.TerritoryName.Trim(),
                Name = observation.Name.Trim(),
            };

            var fingerprint = ObservationIdentity.SessionFingerprint(normalized);
            if (!sessionFingerprints.Add(fingerprint))
                return;

            var line = JsonSerializer.Serialize(normalized, JsonOptions);
            sessionWriter.WriteLine(line);
            MergeAggregate(normalized);
        }
    }

    public void Flush()
    {
        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            FlushCore();
        }
    }

    public void Dispose()
    {
        lock (syncRoot)
        {
            if (disposed)
                return;

            try
            {
                FlushCore();
            }
            finally
            {
                disposed = true;
                sessionWriter.Dispose();
                sessionStream.Dispose();
            }
        }
    }

    private void FlushCore()
    {
        sessionWriter.Flush();
        sessionStream.Flush(flushToDisk: true);

        var snapshot = new ObservationSnapshot
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Observations = aggregates.Values
                .OrderBy(static item => item.TerritoryId)
                .ThenBy(static item => item.Kind, StringComparer.Ordinal)
                .ThenBy(static item => item.AggregateKey, StringComparer.Ordinal)
                .ToArray(),
        };

        WriteSnapshotAtomically(snapshot);
    }

    private void LoadSnapshot()
    {
        if (!File.Exists(SnapshotFilePath))
            return;

        try
        {
            using var stream = new FileStream(
                SnapshotFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var snapshot = JsonSerializer.Deserialize<ObservationSnapshot>(stream, JsonOptions);
            if (snapshot is null)
                return;

            foreach (var observation in snapshot.Observations)
                aggregates[observation.AggregateKey] = observation;
        }
        catch (IOException)
        {
            // A damaged or externally locked prior snapshot must not prevent a new collection session.
        }
        catch (JsonException)
        {
            // Preserve the unreadable file for manual recovery; the next flush safely replaces it.
        }
    }

    private void MergeAggregate(ObservationRecord observation)
    {
        var aggregateKey = ObservationIdentity.AggregateKey(observation);
        if (aggregates.TryGetValue(aggregateKey, out var existing))
        {
            aggregates[aggregateKey] = existing with
            {
                TerritoryName = Prefer(observation.TerritoryName, existing.TerritoryName),
                Name = Prefer(observation.Name, existing.Name),
                IsActive = observation.IsActive,
                LastObservedAtUtc = observation.ObservedAtUtc,
                SeenCount = checked(existing.SeenCount + 1),
                LastSessionId = SessionId,
                Properties = observation.Properties ?? existing.Properties,
            };
            return;
        }

        aggregates[aggregateKey] = new ObservationAggregate
        {
            AggregateKey = aggregateKey,
            Source = observation.Source,
            Kind = observation.Kind,
            Key = observation.Key,
            TerritoryId = observation.TerritoryId,
            TerritoryName = observation.TerritoryName,
            DataId = observation.DataId,
            EventId = observation.EventId,
            Name = observation.Name,
            X = observation.X,
            Y = observation.Y,
            Z = observation.Z,
            IsActive = observation.IsActive,
            FirstObservedAtUtc = observation.ObservedAtUtc,
            LastObservedAtUtc = observation.ObservedAtUtc,
            SeenCount = 1,
            LastSessionId = SessionId,
            Properties = observation.Properties,
        };
    }

    private void WriteSnapshotAtomically(ObservationSnapshot snapshot)
    {
        var temporaryPath = Path.Combine(
            OutputDirectory,
            $".snapshot.{SessionId}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 16 * 1024,
                       FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, snapshot, SnapshotJsonOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, SnapshotFilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static string Prefer(string candidate, string fallback)
        => string.IsNullOrWhiteSpace(candidate) ? fallback : candidate;
}
