using System.Numerics;
using CrescentAtlas.Contracts;
using CrescentAtlas.Collection;
using CrescentAtlas.Data;
using CrescentAtlas.Events;
using CrescentAtlas.Notifications;
using CrescentAtlas.Runtime;

var origin = new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero);
Assert(
    AtlasDetectionRanges.TreasureCandidateCheckRadius == 70.0f,
    "the debug range matches the live treasure candidate check radius");
Assert(
    ConfirmedCarrotObjects.IsKnownDataId(2010139),
    "the Fortune Carrot EventObj is recognized without user configuration");
Assert(
    !ConfirmedCarrotObjects.IsKnownDataId(2007457),
    "the Knowledge Crystal EventObj is not misclassified as a carrot");
Assert(
    ConfirmedPotTargetObservations.EventObjectDataIds.SetEquals([2014741u, 2014742u, 2014743u]),
    "gold, silver, and bronze Magical Elixir coffers are recognized");
Assert(
    ConfirmedPotTargetObservations.RequiresActiveElixirStatus(2014742),
    "silver EventObj classification is gated by the active Elixir status");
Assert(
    MagicalElixirStatusMatcher.IsMatch("マジカルエリクサー"),
    "the Japanese Magical Elixir status name is recognized");
Assert(
    MagicalElixirStatusMatcher.IsMatch("Magical Elixir"),
    "the English Magical Elixir status name is recognized");
Assert(ConfirmedCarrotSpots.NorthHorn.Count == 8, "eight confirmed fixed carrot spots are bundled");
var carrotHistoryLine =
    """{"observedAtUtc":"2026-07-30T06:45:43.1067071+00:00","kind":"carrot-candidate","territoryId":1346,"dataId":2010139,"x":-560.9,"y":50.74249,"z":-447}""";
Assert(
    CarrotSpotHistoryReader.TryParseLine(
        carrotHistoryLine,
        ConfirmedCarrotObjects.FortuneCarrotDataId,
        out var restoredCarrotSpot),
    "confirmed carrot candidates from older logs restore as fixed spots");
Assert(restoredCarrotSpot.TerritoryId == 1346, "restored carrot keeps its territory");
Assert(
    restoredCarrotSpot.Position == new Vector3(-560.9f, 50.74249f, -447.0f),
    "restored carrot keeps its fixed position");
Assert(
    !CarrotSpotHistoryReader.TryParseLine(
        carrotHistoryLine.Replace("2010139", "2007457", StringComparison.Ordinal),
        ConfirmedCarrotObjects.FortuneCarrotDataId,
        out _),
    "unrelated EventObj candidates never become fixed carrot spots");
var nearestLiveCarrot = AtlasMarkerSelector.FindNearestActiveCarrot(
    [
        new AtlasMarker(
            "fixed-carrot",
            AtlasMarkerKind.Carrot,
            "Carrot spot",
            new Vector3(1, 0, 0),
            origin,
            false,
            1346),
        new AtlasMarker(
            "far-live-carrot",
            AtlasMarkerKind.Carrot,
            "Carrot",
            new Vector3(40, 0, 0),
            origin,
            true,
            1346),
        new AtlasMarker(
            "near-live-carrot",
            AtlasMarkerKind.Carrot,
            "Carrot",
            new Vector3(10, 0, 0),
            origin,
            true,
            1346),
    ],
    Vector3.Zero,
    120.0f);
Assert(
    nearestLiveCarrot?.Key == "near-live-carrot",
    "carrot guidance selects the nearest loaded carrot and ignores fixed spots");
Assert(
    AtlasMarkerSelector.FindNearestActiveCarrot(
        [
            new AtlasMarker(
                "fixed-only",
                AtlasMarkerKind.Carrot,
                "Carrot spot",
                Vector3.Zero,
                origin,
                false,
                1346),
        ],
        Vector3.Zero,
        120.0f) is null,
    "carrot guidance is absent until a real carrot is loaded");
var bronzeTreasure = new AtlasMarker(
    "bronze",
    AtlasMarkerKind.TreasureCandidate,
    "Bronze treasure",
    Vector3.Zero,
    origin,
    false,
    1346,
    TreasureType: "bronze");
var silverTreasure = bronzeTreasure with
{
    Key = "silver",
    Label = "Silver treasure",
    TreasureType = "silver",
};
Assert(
    !AtlasMarkerSelector.IsTreasureVisible(bronzeTreasure, false, true),
    "bronze treasure guides are hidden with bronze map markers");
Assert(
    AtlasMarkerSelector.IsTreasureVisible(silverTreasure, false, true),
    "silver treasure guides remain visible when only bronze markers are hidden");
Assert(
    !AtlasMarkerSelector.IsTreasureVisible(silverTreasure, true, false),
    "silver treasure guides are hidden with silver map markers");
Assert(
    TreasureLayerClassifier.IsSurfaceCandidate(
        new Vector3(-928.6488f, -11.245972f, -744.9607f)),
    "the newly observed low-elevation surface coffer is retained");
Assert(
    TreasureLayerClassifier.IsSurfaceCandidate(
        new Vector3(-876.0f, -48.85687f, -903.0f)),
    "confirmed low North Horn surface positions remain on the surface map");
Assert(
    !TreasureLayerClassifier.IsSurfaceCandidate(
        new Vector3(-287.76996f, -92.02722f, 125.65808f)),
    "confirmed subterranean treasure positions stay off the surface map");
Assert(
    TreasureLayerClassifier.IsSubterraneanCandidate(
        new Vector3(-287.76996f, -92.02722f, 125.65808f)),
    "confirmed subterranean treasure positions appear on the subterranean map");
Assert(
    TreasureLayerClassifier.IsCandidateForLayer(
        OccultCrescentMapLayer.Subterranean,
        new Vector3(-287.76996f, -92.02722f, 125.65808f)),
    "subterranean scanning accepts only the underground layer candidate");
Assert(
    !TreasureLayerClassifier.IsCandidateForLayer(
        OccultCrescentMapLayer.Subterranean,
        new Vector3(-876.0f, -48.85687f, -903.0f)),
    "surface treasure positions stay off the subterranean map");
Assert(
    !TreasureLayerClassifier.IsSurfaceCandidate(
        new Vector3(float.NaN, 0.0f, 0.0f)),
    "invalid layout positions are rejected");
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
var newIslandPrediction = tracker.GetUpcomingPrediction("island-visit:new", origin);
Assert(
    newIslandPrediction.Confidence == PotPredictionConfidence.Unknown,
    "a new island has no prediction before its first live observation");
var firstNewIslandObservation = tracker.Observe(new PotObservation(
    "island-visit:new",
    origin.AddMinutes(5),
    100,
    firstPosition));
Assert(
    firstNewIslandObservation.Confidence == PotPredictionConfidence.Provisional,
    "a new island unlocks a provisional prediction after one live observation");

var fixedLocationTracker = new PotPredictionTracker(
    knownEventPositions: new Dictionary<uint, Vector3>
    {
        [100] = firstPosition,
        [200] = secondPosition,
    });
var fixedLocationPrediction = fixedLocationTracker.Observe(
    new PotObservation("fixed-location", origin, 100, firstPosition));
Assert(
    fixedLocationPrediction.PredictedEventId == 200,
    "first island observation predicts the opposite known Magic Pot event");
Assert(
    fixedLocationPrediction.PredictedPosition == secondPosition,
    "first island observation predicts the opposite fixed location");
var fixedLocationAfterMiss = fixedLocationTracker.GetUpcomingPrediction(
    "fixed-location",
    origin.AddMinutes(31));
Assert(
    fixedLocationAfterMiss.NextOccurrenceUtc == origin.AddMinutes(60),
    "a missed provisional occurrence advances the prediction time");
Assert(
    fixedLocationAfterMiss.PredictedEventId == 100,
    "a missed provisional occurrence advances the alternating location");
Assert(
    fixedLocationAfterMiss.PredictedPosition == firstPosition,
    "a missed provisional occurrence returns to the observed fixed location");

var advanceNotification = new PotAdvanceNotificationTracker();
var advanceOccurrence = origin.AddMinutes(30);
Assert(
    !advanceNotification.ShouldNotify(
        "instance-a",
        advanceOccurrence,
        origin.AddMinutes(26).AddSeconds(59),
        TimeSpan.FromMinutes(3)),
    "three-minute notification does not fire early");
Assert(
    advanceNotification.ShouldNotify(
        "instance-a",
        advanceOccurrence,
        origin.AddMinutes(27),
        TimeSpan.FromMinutes(3)),
    "three-minute notification fires when the lead window begins");
Assert(
    !advanceNotification.ShouldNotify(
        "instance-a",
        advanceOccurrence,
        origin.AddMinutes(28),
        TimeSpan.FromMinutes(3)),
    "three-minute notification fires only once per occurrence");
Assert(
    advanceNotification.ShouldNotify(
        "instance-b",
        advanceOccurrence,
        origin.AddMinutes(28),
        TimeSpan.FromMinutes(3)),
    "three-minute notification state is isolated per island instance");
Assert(
    !advanceNotification.ShouldNotify(
        "instance-a",
        advanceOccurrence,
        advanceOccurrence,
        TimeSpan.FromMinutes(3)),
    "three-minute notification does not fire after the predicted occurrence");

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
atlas.SetContext(
    true,
    1346,
    1135,
    OccultCrescentMapLayer.Surface,
    "North Horn",
    Vector3.Zero,
    0.0f);
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

var restoredAtlas = new MutableAtlasDataSource();
restoredAtlas.SetContext(
    true,
    1346,
    1135,
    OccultCrescentMapLayer.Surface,
    "North Horn",
    Vector3.Zero,
    0.0f);
restoredAtlas.ReplaceSource(
    AtlasMarkerKind.TreasureCandidate,
    [
        new AtlasMarker(
            "treasure:persisted",
            AtlasMarkerKind.TreasureCandidate,
            "Persisted treasure",
            Vector3.Zero,
            origin,
            true,
            1346),
    ]);
restoredAtlas.RestoreTreasureChecks(new HashSet<string> { "treasure:persisted" });
Assert(
    restoredAtlas.GetMarkers().Single().IsChecked,
    "persisted treasure check is restored after a plugin reload");

atlas.ResetTreasureChecks();
Assert(!atlas.GetMarkers().Single().IsChecked, "reset clears treasure checks");
atlas.MarkAbsentNearbyTreasureCandidatesChecked(Vector3.Zero, 10.0f, [], 2.0f);
Assert(!atlas.GetMarkers().Single().IsChecked, "reset spot stays unchecked until player leaves");
atlas.MarkAbsentNearbyTreasureCandidatesChecked(new Vector3(20, 0, 0), 10.0f, [], 2.0f);
atlas.MarkAbsentNearbyTreasureCandidatesChecked(Vector3.Zero, 10.0f, [], 2.0f);
Assert(atlas.GetMarkers().Single().IsChecked, "spot can be checked again after revisiting");
Assert(atlas.IsInOccultCrescent, "active Crescent context is exposed to the map");
Assert(
    OccultCrescentMapLayerPolicy.Resolve(1135, 1135) == OccultCrescentMapLayer.Surface,
    "the territory default map is the surface layer");
Assert(
    OccultCrescentMapLayerPolicy.Resolve(1136, 1135) == OccultCrescentMapLayer.Subterranean,
    "a different map row in the same territory is the subterranean layer");
Assert(
    !OccultCrescentMapLayerPolicy.IsMarkerVisible(
        OccultCrescentMapLayer.Subterranean,
        new AtlasMarker(
            "surface-treasure",
            AtlasMarkerKind.TreasureCandidate,
            "Treasure",
            Vector3.Zero,
            origin,
            false,
            1346)),
    "surface treasure candidates are hidden underground");
Assert(
    !OccultCrescentMapLayerPolicy.IsMarkerVisible(
        OccultCrescentMapLayer.Subterranean,
        new AtlasMarker(
            "surface-fate",
            AtlasMarkerKind.Fate,
            "FATE",
            Vector3.Zero,
            origin,
            true,
            1346)),
    "surface FATE markers are hidden underground");
Assert(
    !OccultCrescentMapLayerPolicy.IsMarkerVisible(
        OccultCrescentMapLayer.Subterranean,
        new AtlasMarker(
            "surface-ce",
            AtlasMarkerKind.CriticalEncounter,
            "CE",
            Vector3.Zero,
            origin,
            true,
            1346)),
    "surface CE markers are hidden underground");
Assert(
    OccultCrescentMapLayerPolicy.IsMarkerVisible(
        OccultCrescentMapLayer.Subterranean,
        new AtlasMarker(
            "underground-treasure",
            AtlasMarkerKind.ActiveTreasure,
            "Treasure",
            Vector3.Zero,
            origin,
            true,
            1346)),
    "loaded underground treasure remains visible");
Assert(
    !OccultCrescentMapLayerPolicy.IsMarkerVisible(
        OccultCrescentMapLayer.Subterranean,
        new AtlasMarker(
            "surface-carrot-spot",
            AtlasMarkerKind.Carrot,
            "Carrot spot",
            Vector3.Zero,
            origin,
            false,
            1346)),
    "fixed surface carrot spots are hidden underground");
Assert(
    OccultCrescentMapLayerPolicy.IsMarkerVisible(
        OccultCrescentMapLayer.Subterranean,
        new AtlasMarker(
            "loaded-underground-carrot",
            AtlasMarkerKind.Carrot,
            "Carrot",
            Vector3.Zero,
            origin,
            true,
            1346)),
    "a loaded underground carrot remains visible");
atlas.SetContext(
    true,
    1346,
    1136,
    OccultCrescentMapLayer.Subterranean,
    "North Horn",
    Vector3.Zero,
    0.0f);
Assert(atlas.GetMarkers().Count == 0, "changing map layers clears stale surface markers");
Assert(atlas.MapLayer == OccultCrescentMapLayer.Subterranean, "subterranean layer is exposed to the map");
atlas.SetContext(
    false,
    999,
    0,
    OccultCrescentMapLayer.Surface,
    "Outside",
    Vector3.Zero,
    0.0f);
Assert(!atlas.IsInOccultCrescent, "outside-area context is exposed to the map");
Assert(atlas.GetMarkers().Count == 0, "leaving Crescent clears stale map markers");

Assert(
    DynamicEventTimeResolver.Resolve(
        "Warmup",
        1_000,
        1_092,
        0,
        180,
        120,
        999) == 92,
    "CE start timestamp is the common source of truth for every waiting CE");
Assert(
    DynamicEventTimeResolver.Resolve(
        "Register",
        1_000,
        1_104,
        294,
        180,
        120,
        null) == 104,
    "CE start countdown does not reuse the event-dependent seconds-left field");
Assert(
    DynamicEventTimeResolver.Resolve(
        "Register",
        1_000,
        1_080,
        0,
        180,
        120,
        null) == 80,
    "CE start timestamp does not add registration or warmup durations");
Assert(
    DynamicEventTimeResolver.Resolve(
        "Warmup",
        1_001,
        1_000,
        0,
        180,
        120,
        null) == 0,
    "CE warmup is shown as zero after its scheduled start");
Assert(
    DynamicEventNameMatcher.IsMatch(
        "エルムギガース",
        "求道の人造人間「エルムギガース」"),
    "CE standard UI short enemy name matches full dynamic-event name");
Assert(
    DynamicEventNameMatcher.IsMatch(
        "求道の人造人間 エルムギガース",
        "求道の人造人間「エルムギガース」"),
    "CE name matching tolerates UI punctuation differences");
Assert(
    !DynamicEventNameMatcher.IsMatch(
        "エルムギガース",
        "自然の歌い手「イアムベー」"),
    "different CE names do not match");
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
    string firstVisitId;
    using (var visits = new IslandVisitStore(visitRoot))
    {
        var started = visits.StartOrResume(1346, "North Horn", entry, firstInstance);
        firstVisitId = started.VisitId;
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
        Assert(resumed.VisitId == firstVisitId, "same live island retains its pot prediction scope");
        resumedStore.EndVisit(entry.AddMinutes(12));
        resumedStore.Flush();
    }

    using var reloadedStore = new IslandVisitStore(visitRoot);
    var completed = reloadedStore.GetVisitsDescending().Single();
    Assert(completed.ExitedAtUtc == entry.AddMinutes(12), "exit time persists");

    var changedRoot = Path.Combine(visitRoot, "changed");
    using var changedStore = new IslandVisitStore(changedRoot);
    var previousIsland = changedStore.StartOrResume(1346, "North Horn", entry, firstInstance);
    var differentIsland = changedStore.StartOrResume(
        1346,
        "North Horn",
        entry.AddMinutes(1),
        new OccultCrescentInstanceSnapshot("0xDEF", 3_600));
    Assert(
        differentIsland.VisitId != previousIsland.VisitId,
        "a different island receives a fresh pot prediction scope");

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
