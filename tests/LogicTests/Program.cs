using System.Numerics;
using CrescentAtlas.Contracts;
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

Console.WriteLine("CrescentAtlas logic smoke tests: PASS");
return;

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException($"FAILED: {message}");
}
