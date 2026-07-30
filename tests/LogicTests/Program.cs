using System.Numerics;
using CrescentAtlas.Contracts;
using CrescentAtlas.Collection;
using CrescentAtlas.Events;
using CrescentAtlas.Notifications;
using CrescentAtlas.Runtime;

var origin = new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero);
var firstPosition = new Vector3(10, 0, 20);
var secondPosition = new Vector3(40, 0, 50);
var tracker = new PotPredictionTracker();

var provisional = tracker.Observe(new PotObservation("instance-a", origin, 100, firstPosition));
Assert(provisional.Confidence == PotPredictionConfidence.Provisional, "one observation is provisional");
Assert(provisional.NextOccurrenceUtc == origin.AddMinutes(30), "default interval is 30 minutes");

var confirmed = tracker.Observe(new PotObservation(
    "instance-a",
    origin.AddMinutes(42),
    200,
    secondPosition));
Assert(confirmed.Confidence == PotPredictionConfidence.Confirmed, "two observations confirm interval");
Assert(confirmed.EstimatedInterval == TimeSpan.FromMinutes(42), "measured interval is retained");
Assert(confirmed.NextOccurrenceUtc == origin.AddMinutes(84), "next time uses measured interval");
Assert(confirmed.PredictedEventId == 100, "alternating event predicts preceding event");
Assert(confirmed.PredictedPosition == firstPosition, "alternating location predicts preceding location");

var advanced = tracker.GetUpcomingPrediction("instance-a", origin.AddMinutes(90));
Assert(advanced.NextOccurrenceUtc == origin.AddMinutes(126), "elapsed prediction advances to the next future occurrence");
Assert(advanced.PredictedEventId == 200, "advanced prediction alternates to the latest event");
Assert(advanced.PredictedPosition == secondPosition, "advanced prediction alternates to the latest location");

var isolated = tracker.GetPrediction("instance-b");
Assert(isolated.Confidence == PotPredictionConfidence.Unknown, "instances remain isolated");

var missedTracker = new PotPredictionTracker();
missedTracker.Observe(new PotObservation("missed", origin, 100, firstPosition));
missedTracker.Observe(new PotObservation("missed", origin.AddMinutes(30), 200, secondPosition));
var afterMissedSpawns = missedTracker.Observe(
    new PotObservation("missed", origin.AddMinutes(120), 100, firstPosition));
Assert(afterMissedSpawns.EstimatedInterval == TimeSpan.FromMinutes(30), "missed spawns retain base interval");
Assert(afterMissedSpawns.NextOccurrenceUtc == origin.AddMinutes(150), "prediction continues from latest observation");
Assert(afterMissedSpawns.PredictedEventId == 200, "prediction selects the other alternating event");

var beforeReloadDuplicate = missedTracker.GetObservations("missed").Count;
missedTracker.Observe(new PotObservation(
    "missed",
    origin.AddMinutes(140),
    100,
    firstPosition));
Assert(
    missedTracker.GetObservations("missed").Count == beforeReloadDuplicate,
    "same active pot after reload is ignored");

var historyLine =
    """{"observedAtUtc":"2026-07-30T08:01:15.9085312+00:00","kind":"FateStarted","eventId":2072,"x":233,"y":7.729229,"z":-470,"properties":{"instanceKey":"territory-1346"}}""";
Assert(
    PotObservationHistoryReader.TryParseLine(
        historyLine,
        new HashSet<uint> { 2072, 2073 },
        "fallback",
        out var restoredPot),
    "pot history line parses");
Assert(restoredPot.InstanceKey == "territory-1346", "history restores instance key");
Assert(restoredPot.EventId == 2072, "history restores event id");
Assert(restoredPot.Position == new Vector3(233, 7.729229f, -470), "history restores position");

var atlas = new MutableAtlasDataSource();
atlas.SetContext(1346, "North Horn", Vector3.Zero, 0.0f);
atlas.ReplaceSource(
    AtlasMarkerKind.TreasureCandidate,
    [
        new AtlasMarker(
            "treasure:a",
            AtlasMarkerKind.TreasureCandidate,
            "Treasure",
            Vector3.Zero,
            origin,
            true,
            1346),
    ]);
atlas.MarkAbsentNearbyTreasureCandidatesChecked(Vector3.Zero, 10.0f, [], 2.0f);
Assert(atlas.GetMarkers().Single().IsChecked, "nearby absent treasure is checked");

atlas.ResetTreasureChecks();
Assert(!atlas.GetMarkers().Single().IsChecked, "reset clears treasure checks");
atlas.MarkAbsentNearbyTreasureCandidatesChecked(Vector3.Zero, 10.0f, [], 2.0f);
Assert(!atlas.GetMarkers().Single().IsChecked, "reset spot stays unchecked until player leaves");
atlas.MarkAbsentNearbyTreasureCandidatesChecked(new Vector3(20, 0, 0), 10.0f, [], 2.0f);
atlas.MarkAbsentNearbyTreasureCandidatesChecked(Vector3.Zero, 10.0f, [], 2.0f);
Assert(atlas.GetMarkers().Single().IsChecked, "spot can be checked again after revisiting");

Assert(
    DynamicEventTimeResolver.Resolve(
        "Warmup",
        1_000,
        900,
        0,
        180,
        120,
        92) == 92,
    "CE standard UI countdown takes precedence");
Assert(
    DynamicEventTimeResolver.Resolve(
        "Warmup",
        1_000,
        900,
        51,
        180,
        120,
        null) == 51,
    "CE native seconds-left is used when UI data is unavailable");
Assert(
    DynamicEventTimeResolver.Resolve(
        "Register",
        1_000,
        900,
        0,
        180,
        120,
        null) == 200,
    "CE battle start countdown is derived from registration and warmup timing");
Assert(
    DynamicEventTimeResolver.Resolve(
        "Battle",
        1_000,
        900,
        0,
        180,
        120,
        null) == -1,
    "CE active battle does not reuse the start countdown formula");

var visitRoot = Path.Combine(
    Path.GetTempPath(),
    $"CrescentAtlas-visit-tests-{Guid.NewGuid():N}");
try
{
    var entry = origin.AddHours(1);
    var firstInstance = new OccultCrescentInstanceSnapshot("0xABC", 10_800);
    using (var visits = new IslandVisitStore(visitRoot))
    {
        var started = visits.StartOrResume(1346, "North Horn", entry, firstInstance);
        Assert(started.ExitedAtUtc is null, "new visit starts active");
        Assert(started.IslandKey.Contains("expires-", StringComparison.Ordinal), "countdown forms island key");
        visits.Touch(entry.AddMinutes(1), new OccultCrescentInstanceSnapshot("0xABC", 10_740));
        visits.Flush();
    }

    using (var resumedStore = new IslandVisitStore(visitRoot))
    {
        var resumed = resumedStore.StartOrResume(
            1346,
            "North Horn",
            entry.AddMinutes(2),
            new OccultCrescentInstanceSnapshot("0xDEF", 10_680));
        Assert(resumedStore.GetVisitsDescending().Count == 1, "plugin reload resumes same live island visit");
        Assert(resumed.EnteredAtUtc == entry, "resumed visit retains original entry time");
        resumedStore.EndVisit(entry.AddMinutes(12));
        resumedStore.Flush();
    }

    using var reloadedStore = new IslandVisitStore(visitRoot);
    var completed = reloadedStore.GetVisitsDescending().Single();
    Assert(completed.ExitedAtUtc == entry.AddMinutes(12), "exit time persists");

    var orphanRoot = Path.Combine(visitRoot, "orphan");
    using var orphanStore = new IslandVisitStore(orphanRoot);
    var orphan = orphanStore.StartOrResume(1346, "North Horn", entry, firstInstance);
    Assert(
        orphanStore.CloseUnfinishedVisitsAtLastSeen("not-in-content-on-start"),
        "unfinished visit closes after next startup outside content");
    Assert(
        orphanStore.GetVisitsDescending().Single().ExitedAtUtc == orphan.LastSeenAtUtc,
        "unfinished visit uses last observed time as exit");
}
finally
{
    if (Directory.Exists(visitRoot))
        Directory.Delete(visitRoot, recursive: true);
}

Console.WriteLine("CrescentAtlas logic smoke tests: PASS");
return;

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException($"FAILED: {message}");
}
