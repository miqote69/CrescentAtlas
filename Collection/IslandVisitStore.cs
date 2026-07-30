using System.IO;
using System.Text.Json;
using CrescentAtlas.Data;
using CrescentAtlas.Runtime;

namespace CrescentAtlas.Collection;

/// <summary>
/// Persists one local record for each entry into and exit from Occult Crescent.
/// The projected content end time acts as a best-effort island fingerprint:
/// returning to the same live island normally preserves that countdown, while
/// a different island normally has a different projected end.
/// </summary>
public sealed class IslandVisitStore : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly object syncRoot = new();
    private readonly List<IslandVisitRecord> visits = [];
    private bool dirty;
    private bool disposed;

    public IslandVisitStore(string collectionDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionDirectory);
        OutputPath = Path.Combine(Path.GetFullPath(collectionDirectory), "island-visits.json");
        Load();
    }

    public string OutputPath { get; }

    public IslandVisitRecord? ActiveVisit { get; private set; }

    public IReadOnlyList<IslandVisitRecord> GetVisitsDescending()
    {
        lock (syncRoot)
        {
            return visits
                .OrderByDescending(static visit => visit.EnteredAtUtc)
                .ToArray();
        }
    }

    public IslandVisitRecord StartOrResume(
        uint territoryId,
        string territoryName,
        DateTimeOffset now,
        OccultCrescentInstanceSnapshot instance)
    {
        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var normalizedNow = now.ToUniversalTime();
            var projectedEnd = ProjectContentEnd(normalizedNow, instance.ContentTimeLeftSeconds);
            var islandKey = BuildIslandKey(territoryId, projectedEnd, normalizedNow);
            var existing = visits
                .Where(static visit => visit.ExitedAtUtc is null)
                .OrderByDescending(static visit => visit.EnteredAtUtc)
                .FirstOrDefault();

            if (existing is not null
                && IsSameLiveIsland(existing, territoryId, projectedEnd, normalizedNow))
            {
                ActiveVisit = existing with
                {
                    TerritoryName = Prefer(territoryName, existing.TerritoryName),
                    LastSeenAtUtc = normalizedNow,
                    EstimatedContentEndUtc = projectedEnd ?? existing.EstimatedContentEndUtc,
                    IslandKey = projectedEnd is null ? existing.IslandKey : islandKey,
                    InstancePointer = Prefer(instance.InstancePointer, existing.InstancePointer),
                };
                Replace(ActiveVisit);
                dirty = true;
                return ActiveVisit;
            }

            if (existing is not null)
            {
                Replace(existing with
                {
                    ExitedAtUtc = existing.LastSeenAtUtc,
                    ExitReason = "instance-changed-or-plugin-gap",
                });
            }

            ActiveVisit = new IslandVisitRecord
            {
                VisitId = $"{normalizedNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}",
                TerritoryId = territoryId,
                TerritoryName = territoryName.Trim(),
                EnteredAtUtc = normalizedNow,
                LastSeenAtUtc = normalizedNow,
                EstimatedContentEndUtc = projectedEnd,
                IslandKey = islandKey,
                InstancePointer = instance.InstancePointer,
            };
            visits.Add(ActiveVisit);
            dirty = true;
            return ActiveVisit;
        }
    }

    public void Touch(DateTimeOffset now, OccultCrescentInstanceSnapshot instance)
    {
        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (ActiveVisit is null)
                return;

            var normalizedNow = now.ToUniversalTime();
            var projectedEnd = ProjectContentEnd(normalizedNow, instance.ContentTimeLeftSeconds);
            ActiveVisit = ActiveVisit with
            {
                LastSeenAtUtc = normalizedNow,
                EstimatedContentEndUtc = projectedEnd ?? ActiveVisit.EstimatedContentEndUtc,
                IslandKey = projectedEnd is null
                    ? ActiveVisit.IslandKey
                    : BuildIslandKey(ActiveVisit.TerritoryId, projectedEnd, normalizedNow),
                InstancePointer = Prefer(instance.InstancePointer, ActiveVisit.InstancePointer),
            };
            Replace(ActiveVisit);
            dirty = true;
        }
    }

    public void EndVisit(DateTimeOffset now, string reason = "left-content")
    {
        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (ActiveVisit is null)
                return;

            var normalizedNow = now.ToUniversalTime();
            ActiveVisit = ActiveVisit with
            {
                LastSeenAtUtc = normalizedNow,
                ExitedAtUtc = normalizedNow,
                ExitReason = reason,
            };
            Replace(ActiveVisit);
            ActiveVisit = null;
            dirty = true;
        }
    }

    public bool CloseUnfinishedVisitsAtLastSeen(string reason)
    {
        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var changed = false;
            for (var index = 0; index < visits.Count; index++)
            {
                var visit = visits[index];
                if (visit.ExitedAtUtc is not null)
                    continue;

                visits[index] = visit with
                {
                    ExitedAtUtc = visit.LastSeenAtUtc,
                    ExitReason = reason,
                };
                changed = true;
            }

            if (changed)
                dirty = true;
            return changed;
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

            FlushCore();
            disposed = true;
        }
    }

    internal static DateTimeOffset? ProjectContentEnd(
        DateTimeOffset observedAtUtc,
        float? contentTimeLeftSeconds)
    {
        if (contentTimeLeftSeconds is not { } seconds
            || !float.IsFinite(seconds)
            || seconds <= 0.0f
            || seconds > TimeSpan.FromHours(12).TotalSeconds)
        {
            return null;
        }

        return observedAtUtc.ToUniversalTime().AddSeconds(seconds);
    }

    internal static string BuildIslandKey(
        uint territoryId,
        DateTimeOffset? projectedEndUtc,
        DateTimeOffset enteredAtUtc)
    {
        var identityTime = projectedEndUtc ?? enteredAtUtc;
        var roundedTicks = (identityTime.UtcTicks / TimeSpan.TicksPerMinute) * TimeSpan.TicksPerMinute;
        var rounded = new DateTimeOffset(roundedTicks, TimeSpan.Zero);
        var source = projectedEndUtc is null ? "visit" : "expires";
        return $"territory-{territoryId}:{source}-{rounded:yyyyMMddTHHmm}Z";
    }

    private static bool IsSameLiveIsland(
        IslandVisitRecord existing,
        uint territoryId,
        DateTimeOffset? projectedEndUtc,
        DateTimeOffset nowUtc)
    {
        if (existing.TerritoryId != territoryId)
            return false;
        if (existing.EstimatedContentEndUtc is not { } existingEnd || projectedEndUtc is not { } currentEnd)
            return nowUtc - existing.LastSeenAtUtc <= TimeSpan.FromMinutes(5);

        return Math.Abs((existingEnd - currentEnd).TotalMinutes) <= 2.0;
    }

    private void Load()
    {
        if (!File.Exists(OutputPath))
            return;

        try
        {
            using var stream = new FileStream(
                OutputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var loaded = JsonSerializer.Deserialize<List<IslandVisitRecord>>(stream, JsonOptions);
            if (loaded is null)
                return;

            visits.AddRange(loaded);
        }
        catch (IOException)
        {
            // Preserve the prior file and start a recoverable in-memory history.
        }
        catch (JsonException)
        {
            // Preserve malformed history for manual recovery.
        }
    }

    private void FlushCore()
    {
        if (!dirty)
            return;

        var directory = Path.GetDirectoryName(OutputPath)
                        ?? throw new InvalidOperationException("Visit history path has no directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory,
            $".island-visits.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 8 * 1024,
                       FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(
                    stream,
                    visits.OrderByDescending(static visit => visit.EnteredAtUtc).ToArray(),
                    JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, OutputPath, overwrite: true);
            dirty = false;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private void Replace(IslandVisitRecord updated)
    {
        var index = visits.FindIndex(visit =>
            StringComparer.Ordinal.Equals(visit.VisitId, updated.VisitId));
        if (index >= 0)
            visits[index] = updated;
    }

    private static string Prefer(string candidate, string fallback)
        => string.IsNullOrWhiteSpace(candidate) ? fallback : candidate;
}
