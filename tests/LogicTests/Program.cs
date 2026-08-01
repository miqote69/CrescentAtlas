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
    "the live treasure candidate check radius remains 70 yalms");
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
    MagicalElixirMapMarkerClassifier.ResolveTargetDataId(
        objectiveId: 0,
        levelObjectId: 2014743) == 2014743,
    "borrowed AgentMap markers resolve a bronze Elixir target through the Level object");
Assert(
    MagicalElixirMapMarkerClassifier.ResolveTargetDataId(
        objectiveId: 2014742,
        levelObjectId: 0) == 2014742,
    "borrowed AgentMap markers resolve a silver Elixir target through the objective");
Assert(
    MagicalElixirMapMarkerClassifier.ResolveTargetDataId(
        objectiveId: 123,
        levelObjectId: 456) == 0,
    "unrelated AgentMap markers are ignored");
Assert(
    MagicalElixirDirectionResolver.TryParse(
        "\u5b9d\u306e\u6c17\u914d\u306f\u5317\u6771\u65b9\u5411\u304b\u3089\u611f\u3058\u308b\u3002",
        out var japaneseDirection)
    && japaneseDirection == CompassDirection.NorthEast,
    "Japanese diagonal Magical Elixir direction messages are parsed");
Assert(
    MagicalElixirDirectionResolver.TryParse(
        "The treasure is in the south-west direction.",
        out var englishDirection)
    && englishDirection == CompassDirection.SouthWest,
    "English diagonal Magical Elixir direction messages are parsed");
Assert(
    MagicalElixirDirectionResolver.TryParse(
        "Far, far to the northwest.",
        out var conciseEnglishDirection)
    && conciseEnglishDirection == CompassDirection.NorthWest,
    "the concise English in-game distance and direction message is parsed");
Assert(
    !MagicalElixirDirectionResolver.TryParse("Travel north to continue.", out _),
    "ordinary directional chat is not treated as an Elixir hint");
Assert(
    MagicalElixirDirectionResolver.TryParse(
        "財宝の気配を、北方向のとても遠くから感じているようだ",
        out _,
        out var veryFarBand)
    && veryFarBand == MagicalElixirDistanceBand.VeryFar,
    "Japanese very-far Magical Elixir messages retain their distance band");
Assert(
    MagicalElixirDirectionResolver.TryParse(
        "財宝の気配を、東方向の近くから感じているようだ。",
        out _,
        out var nearBand)
    && nearBand == MagicalElixirDistanceBand.Near,
    "Japanese nearby Magical Elixir messages retain their distance band");
Assert(
    MagicalElixirDirectionResolver.TryParse(
        "財宝の気配を、北東方向のとても近くから感じているようだ！",
        out _,
        out var veryNearBand)
    && veryNearBand == MagicalElixirDistanceBand.VeryNear,
    "Japanese very-near Magical Elixir messages retain their narrow distance band");
Assert(
    MagicalElixirDirectionResolver.BearingDegrees(Vector3.Zero, new Vector3(0, 0, -10)) == 0.0f,
    "negative world Z is map north");
Assert(
    MagicalElixirDirectionResolver.BearingDegrees(Vector3.Zero, new Vector3(10, 0, 0)) == 90.0f,
    "positive world X is map east");
var directionSpots = new[]
{
    new ConfirmedPotTargetObservation(1346, 2014741, "North", new Vector3(0, 0, -100), origin),
    new ConfirmedPotTargetObservation(1346, 2014742, "East", new Vector3(100, 0, 0), origin),
    new ConfirmedPotTargetObservation(1346, 2014743, "South", new Vector3(0, 0, 100), origin),
};
var northCandidates = MagicalElixirDirectionResolver.Resolve(
    1346,
    directionSpots,
    [new MagicalElixirDirectionHint(CompassDirection.North, Vector3.Zero, origin, "north")]);
Assert(
    northCandidates.Count == 1 && northCandidates[0].Spot.Name == "North",
    "a direction hint eliminates fixed targets outside its cone");
var lateTrackingHints = new MagicalElixirDirectionHint[]
{
    new(CompassDirection.NorthWest, new(226.58598f, 8.557167f, -477.65237f), origin, "very far northwest", MagicalElixirDistanceBand.VeryFar),
    new(CompassDirection.NorthWest, new(198.52986f, 19.448032f, -527.9485f), origin.AddSeconds(6), "very far northwest", MagicalElixirDistanceBand.VeryFar),
    new(CompassDirection.NorthWest, new(178.68251f, 31.544199f, -603.8502f), origin.AddSeconds(11), "very far northwest", MagicalElixirDistanceBand.VeryFar),
    new(CompassDirection.West, new(119.52219f, 29.592543f, -654.2998f), origin.AddSeconds(17), "very far west", MagicalElixirDistanceBand.VeryFar),
    new(CompassDirection.West, new(51.803513f, 38.486988f, -686.2103f), origin.AddSeconds(22), "very far west", MagicalElixirDistanceBand.VeryFar),
    new(CompassDirection.West, new(-35.207733f, 50.338135f, -702.7713f), origin.AddSeconds(28), "far west", MagicalElixirDistanceBand.Far),
    new(CompassDirection.West, new(-89.26941f, 61.491196f, -741.0671f), origin.AddSeconds(33), "far west", MagicalElixirDistanceBand.Far),
    new(CompassDirection.NorthWest, new(-152.3785f, 59.89325f, -733.92346f), origin.AddSeconds(38), "near northwest", MagicalElixirDistanceBand.Near),
    new(CompassDirection.NorthEast, new(-209.12477f, 57.767963f, -741.13544f), origin.AddSeconds(44), "near northeast", MagicalElixirDistanceBand.Near),
};
Assert(
    !MagicalElixirDirectionResolver.IsTargetFromCurrentSearch(
        origin.AddSeconds(62),
        [new MagicalElixirDirectionHint(
            CompassDirection.West,
            new Vector3(948.15063f, 63.372074f, -568.507f),
            origin.AddSeconds(79),
            "second search",
            MagicalElixirDistanceBand.VeryFar)],
        TimeSpan.FromMilliseconds(500)),
    "the first coffer cannot finish a later chained Elixir search while it remains loaded");
Assert(
    MagicalElixirDirectionResolver.IsTargetFromCurrentSearch(
        origin.AddSeconds(82),
        [new MagicalElixirDirectionHint(
            CompassDirection.West,
            new Vector3(948.15063f, 63.372074f, -568.507f),
            origin.AddSeconds(79),
            "second search",
            MagicalElixirDistanceBand.VeryFar)],
        TimeSpan.FromMilliseconds(500)),
    "a coffer discovered after the second search begins can complete that leg");
var firstChainedGoal = new Vector3(948.5978f, 63.594563f, -567.0099f);
var firstChainedFinalHint = new MagicalElixirDirectionHint(
    CompassDirection.North,
    new Vector3(945.9019f, 62.54354f, -548.00775f),
    origin.AddSeconds(57),
    "first chained goal",
    MagicalElixirDistanceBand.VeryNear);
Assert(
    MagicalElixirDirectionResolver.IsCompletionTarget(
        firstChainedGoal,
        origin.AddSeconds(62),
        [firstChainedFinalHint],
        TimeSpan.FromMilliseconds(500)),
    "the first coffer completes its leg without ending the chained Elixir session");
var incidentalSecondLegCoffer = new Vector3(32.4f, 56.835186f, -777.3f);
var incidentalSecondLegHint = new MagicalElixirDirectionHint(
    CompassDirection.West,
    new Vector3(60.44817f, 53.80692f, -787.5975f),
    origin.AddSeconds(206),
    "incidental coffer",
    MagicalElixirDistanceBand.VeryFar);
Assert(
    !MagicalElixirDirectionResolver.IsCompletionTarget(
        incidentalSecondLegCoffer,
        origin.AddSeconds(211),
        [incidentalSecondLegHint],
        TimeSpan.FromMilliseconds(500)),
    "an unrelated coffer encountered during the second leg does not end the search");
var secondChainedGoal = new Vector3(-449.6f, 45.6567f, -967.0001f);
var secondChainedFinalHint = new MagicalElixirDirectionHint(
    CompassDirection.NorthWest,
    new Vector3(-426.973f, 45.93657f, -951.5103f),
    origin.AddSeconds(244),
    "second chained goal",
    MagicalElixirDistanceBand.Near);
Assert(
    MagicalElixirDirectionResolver.IsCompletionTarget(
        secondChainedGoal,
        origin.AddSeconds(250),
        [secondChainedFinalHint],
        TimeSpan.FromMilliseconds(500)),
    "the second coffer independently completes the chained Elixir search");
var unrelatedVisibleTargets = new[]
{
    new ConfirmedPotTargetObservation(1346, 2014742, "Unrelated silver", new Vector3(-86.0f, 60.596237f, -737.0f), origin),
    new ConfirmedPotTargetObservation(1346, 2014743, "Unrelated bronze", new Vector3(-251.781f, 65.949005f, -864.3828f), origin),
};
Assert(
    MagicalElixirDirectionResolver.Resolve(1346, unrelatedVisibleTargets, lateTrackingHints).Count == 0,
    "unrelated visible Elixir coffers do not terminate a direction search");
var actualLateTarget = new Vector3(-190.0f, 61.75258f, -763.0f);
Assert(
    MagicalElixirDirectionResolver.IsConsistentWithHints(actualLateTarget, lateTrackingHints),
    "the actual late-search coffer matches the complete direction and distance history");
var bundledLateCandidates = MagicalElixirDirectionResolver.Resolve(
    1346,
    ConfirmedPotTargetObservations.NorthHorn,
    lateTrackingHints);
Assert(
    bundledLateCandidates.Any(candidate =>
        Vector3.DistanceSquared(candidate.Spot.Position, actualLateTarget) < 0.01f),
    "the bundled goal database resolves a previously observed Elixir route to its fixed destination");
var unknownTarget = new Vector3(151.9998f, 61.106945f, -842.0175f);
Assert(
    MagicalElixirDirectionResolver.EstimateUnknownLocation([]) is null,
    "an empty Elixir hint sequence produces no estimate without indexing an element");
var unknownEstimate = MagicalElixirDirectionResolver.EstimateUnknownLocation(
[
    new(CompassDirection.North, new Vector3(244.62822f, 7.037754f, -458.32602f), origin, "very far north", MagicalElixirDistanceBand.VeryFar),
    new(CompassDirection.North, new Vector3(257.5812f, 18.660856f, -510.5899f), origin.AddSeconds(6), "very far north", MagicalElixirDistanceBand.VeryFar),
    new(CompassDirection.NorthWest, new Vector3(280.42145f, 37.77446f, -599.05554f), origin.AddSeconds(13), "very far northwest", MagicalElixirDistanceBand.VeryFar),
    new(CompassDirection.NorthWest, new Vector3(347.96204f, 60.0f, -728.62823f), origin.AddSeconds(23), "very far northwest", MagicalElixirDistanceBand.VeryFar),
    new(CompassDirection.West, new Vector3(336.87128f, 60.460533f, -787.6997f), origin.AddSeconds(33), "far west", MagicalElixirDistanceBand.Far),
    new(CompassDirection.NorthWest, new Vector3(227.80069f, 34.798172f, -733.4489f), origin.AddSeconds(42), "far northwest", MagicalElixirDistanceBand.Far),
    new(CompassDirection.North, new Vector3(154.15448f, 37.563305f, -737.44556f), origin.AddSeconds(47), "far north", MagicalElixirDistanceBand.Far),
    new(CompassDirection.NorthEast, new Vector3(32.487816f, 46.657104f, -724.7657f), origin.AddSeconds(56), "far northeast", MagicalElixirDistanceBand.Far),
    new(CompassDirection.NorthEast, new Vector3(32.39474f, 58.756954f, -788.25323f), origin.AddSeconds(61), "far northeast", MagicalElixirDistanceBand.Far),
    new(CompassDirection.East, new Vector3(63.746452f, 61.39292f, -844.623f), origin.AddSeconds(67), "near east", MagicalElixirDistanceBand.Near),
    new(CompassDirection.South, new Vector3(142.7786f, 61.0f, -870.2388f), origin.AddSeconds(76), "near south", MagicalElixirDistanceBand.Near),
]);
Assert(
    unknownEstimate is not null
    && Vector2.Distance(
        new Vector2(unknownEstimate.Position.X, unknownEstimate.Position.Z),
        new Vector2(unknownTarget.X, unknownTarget.Z)) <= 35.0f
    && unknownEstimate.MaximumAngularErrorDegrees <= MagicalElixirDirectionResolver.DefaultHalfWidthDegrees,
    "an unregistered Elixir target remains visible and converges near the observed destination");
var secondUnknownTarget = new Vector3(47.6f, 3.8843424f, -218.3f);
var secondUnknownHints = new MagicalElixirDirectionHint[]
{
    new(CompassDirection.NorthEast, new(-496.2308f, 52.755604f, 224.48422f), origin, "very far northeast", MagicalElixirDistanceBand.VeryFar),
    new(CompassDirection.SouthWest, new(330.03757f, 38.29322f, -581.82806f), origin.AddSeconds(26), "very far southwest", MagicalElixirDistanceBand.VeryFar),
    new(CompassDirection.SouthWest, new(265.8566f, 31.211512f, -567.1656f), origin.AddSeconds(34), "very far southwest", MagicalElixirDistanceBand.VeryFar),
    new(CompassDirection.SouthWest, new(210.27594f, 17.239101f, -518.3449f), origin.AddSeconds(39), "very far southwest", MagicalElixirDistanceBand.VeryFar),
    new(CompassDirection.NorthEast, new(-551.4197f, 66.67442f, 578.6547f), origin.AddSeconds(65), "very far northeast", MagicalElixirDistanceBand.VeryFar),
    new(CompassDirection.NorthEast, new(-541.237f, 57.77471f, 511.3516f), origin.AddSeconds(70), "very far northeast", MagicalElixirDistanceBand.VeryFar),
    new(CompassDirection.NorthEast, new(-505.36682f, 43.401787f, 444.83197f), origin.AddSeconds(76), "very far northeast", MagicalElixirDistanceBand.VeryFar),
    new(CompassDirection.NorthEast, new(-475.8879f, 29.896624f, 375.64084f), origin.AddSeconds(81), "very far northeast", MagicalElixirDistanceBand.VeryFar),
    new(CompassDirection.NorthEast, new(-453.32617f, 16.80894f, 304.99564f), origin.AddSeconds(86), "very far northeast", MagicalElixirDistanceBand.VeryFar),
    new(CompassDirection.NorthEast, new(-439.4996f, 5.028363f, 227.2316f), origin.AddSeconds(91), "very far northeast", MagicalElixirDistanceBand.VeryFar),
    new(CompassDirection.North, new(-16.773022f, 2.1012855f, -43.08181f), origin.AddSeconds(114), "far north", MagicalElixirDistanceBand.Far),
    new(CompassDirection.NorthEast, new(-76.78556f, 3.8826878f, -88.23583f), origin.AddSeconds(123), "far northeast", MagicalElixirDistanceBand.Far),
    new(CompassDirection.NorthEast, new(-31.53759f, 2.998665f, -148.12483f), origin.AddSeconds(128), "far northeast", MagicalElixirDistanceBand.Far),
    new(CompassDirection.NorthEast, new(14.1212635f, 3.3294985f, -176.82266f), origin.AddSeconds(135), "near northeast", MagicalElixirDistanceBand.Near),
    new(CompassDirection.NorthEast, new(40.63087f, 3.6832187f, -206.76562f), origin.AddSeconds(140), "very near northeast", MagicalElixirDistanceBand.VeryNear),
};
var earlySearchEstimate = MagicalElixirDirectionResolver.EstimateUnknownLocation(
    secondUnknownHints.Take(4).ToArray());
Assert(
    earlySearchEstimate is { IsReliable: false, UncertaintyRadiusYalms: >= 600.0f },
    "collinear very-far hints remain a broad search area instead of a precise destination");
var farSearchEstimate = MagicalElixirDirectionResolver.EstimateUnknownLocation(
    secondUnknownHints.Take(13).ToArray());
Assert(
    farSearchEstimate is { IsReliable: false, UncertaintyRadiusYalms: >= 140.0f },
    "far-only hints remain explicitly uncertain");
var nearFixEstimate = MagicalElixirDirectionResolver.EstimateUnknownLocation(
    secondUnknownHints.Take(14).ToArray());
Assert(
    nearFixEstimate is { IsReliable: true }
    && Vector2.Distance(
        new(nearFixEstimate.Position.X, nearFixEstimate.Position.Z),
        new(secondUnknownTarget.X, secondUnknownTarget.Z)) <= 70.0f,
    "a near hint with angular diversity enables a bounded destination fix");
var veryNearFixEstimate = MagicalElixirDirectionResolver.EstimateUnknownLocation(secondUnknownHints);
Assert(
    veryNearFixEstimate is { IsReliable: true, UncertaintyRadiusYalms: <= 25.0f }
    && Vector2.Distance(
        new(veryNearFixEstimate.Position.X, veryNearFixEstimate.Position.Z),
        new(secondUnknownTarget.X, secondUnknownTarget.Z)) <= 25.0f,
    "a very-near hint produces a tight final destination fix");
Assert(
    !PotPredictionDisplayPolicy.ShouldShow(true, true, true, hasActivePotFate: true),
    "the next Magic Pot prediction is hidden while a Pot FATE is active");
Assert(
    PotPredictionDisplayPolicy.ShouldShow(true, true, true, hasActivePotFate: false),
    "the next Magic Pot prediction returns after the active Pot FATE ends");
Assert(
    !PotPredictionDisplayPolicy.ShouldShow(false, true, true, hasActivePotFate: false),
    "the user prediction visibility setting remains authoritative");
var potTargetHistoryLine =
    """{"observedAtUtc":"2026-07-30T07:08:02Z","kind":"pot-target-goal","territoryId":1346,"dataId":2014742,"name":"Silver target","x":12.5,"y":-4,"z":-88.25}""";
Assert(
    PotTargetHistoryReader.TryParseLine(
        potTargetHistoryLine,
        ConfirmedPotTargetObservations.EventObjectDataIds,
        out var restoredPotTarget)
    && restoredPotTarget.DataId == 2014742
    && restoredPotTarget.Position == new Vector3(12.5f, -4.0f, -88.25f),
    "verified historical Magical Elixir goal coordinates are restored");
Assert(
    !PotTargetHistoryReader.TryParseLine(
        potTargetHistoryLine.Replace("pot-target-goal", "pot-target", StringComparison.Ordinal),
        ConfirmedPotTargetObservations.EventObjectDataIds,
        out _),
    "unverified visible coffers are not restored as Elixir goals");
Assert(
    ConfirmedPotTargetObservations.NorthHorn.Count == 37,
    "all 37 confirmed physical Magical Elixir goal locations are bundled");
Assert(
    ConfirmedPotTargetObservations.NorthHorn.All(spot =>
        spot.TerritoryId == 1346
        && spot.DataId == 0
        && float.IsFinite(spot.Position.X)
        && float.IsFinite(spot.Position.Y)
        && float.IsFinite(spot.Position.Z)),
    "bundled Elixir goals are generic finite North Horn destinations");
Assert(
    ConfirmedPotTargetObservations.NorthHorn.Any(spot =>
        Vector3.DistanceSquared(spot.Position, new Vector3(-190.0f, 61.75258f, -763.0f)) < 0.01f),
    "the latest confirmed Elixir goal is included in the bundled set");
Assert(ConfirmedCarrotSpots.NorthHorn.Count == 9, "nine confirmed fixed carrot spots are bundled");
Assert(
    ConfirmedCarrotSpots.NorthHorn.Any(spot =>
        Vector3.DistanceSquared(spot.Position, new Vector3(-847.9f, 114.0f, 196.6f)) < 0.01f),
    "the latest confirmed carrot location is bundled");
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
    !TreasureLayerClassifier.IsCandidateForLayer(
        OccultCrescentMapLayer.Subterranean,
        new Vector3(100.0f, -700.0f, 395.0f)),
    "deep staging treasures stay off the playable subterranean map");
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

var restoredFixedLocationTracker = new PotPredictionTracker(
    knownEventPositions: ConfirmedMagicPotLocations.NorthHorn);
restoredFixedLocationTracker.Observe(new PotObservation(
    "restored-fixed-location",
    origin,
    2072,
    ConfirmedMagicPotLocations.NorthHorn[2072]));
restoredFixedLocationTracker.Observe(new PotObservation(
    "restored-fixed-location",
    origin.AddMinutes(30),
    2073,
    Vector3.Zero));
var restoredFixedLocationPrediction = restoredFixedLocationTracker.Observe(new PotObservation(
    "restored-fixed-location",
    origin.AddMinutes(60),
    2072,
    ConfirmedMagicPotLocations.NorthHorn[2072]));
Assert(
    restoredFixedLocationPrediction.PredictedEventId == 2073,
    "restored alternating observations still predict the opposite Magic Pot event");
Assert(
    restoredFixedLocationPrediction.PredictedPosition == ConfirmedMagicPotLocations.NorthHorn[2073],
    "a zero position restored from history is replaced with the fixed Magic Pot location");
Assert(
    restoredFixedLocationTracker.GetObservations("restored-fixed-location")[1].Position ==
        ConfirmedMagicPotLocations.NorthHorn[2073],
    "the corrected fixed position is retained in tracker state");

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
Assert(
    !new PotAdvanceNotificationTracker().ShouldNotify(
        "instance-a",
        advanceOccurrence,
        origin.AddMinutes(29),
        TimeSpan.FromMinutes(3),
        TimeSpan.FromMinutes(1)),
    "three-minute notification does not fire inside the one-minute alert window");

var oneMinuteNotification = new PotAdvanceNotificationTracker();
Assert(
    !oneMinuteNotification.ShouldNotify(
        "instance-a",
        advanceOccurrence,
        origin.AddMinutes(28).AddSeconds(59),
        TimeSpan.FromMinutes(1)),
    "one-minute notification does not fire early");
Assert(
    oneMinuteNotification.ShouldNotify(
        "instance-a",
        advanceOccurrence,
        origin.AddMinutes(29),
        TimeSpan.FromMinutes(1)),
    "one-minute notification fires independently after the three-minute alert");
Assert(
    !oneMinuteNotification.ShouldNotify(
        "instance-a",
        advanceOccurrence,
        origin.AddMinutes(29).AddSeconds(30),
        TimeSpan.FromMinutes(1)),
    "one-minute notification fires only once per occurrence");

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
    OccultCrescentMapLayerPolicy.IsMarkerVisible(
        OccultCrescentMapLayer.Subterranean,
        new AtlasMarker(
            "layer-filtered-treasure",
            AtlasMarkerKind.TreasureCandidate,
            "Treasure",
            Vector3.Zero,
            origin,
            false,
            1346)),
    "treasure candidates already selected by the active layout layer remain visible");
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
