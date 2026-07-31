using CrescentAtlas.Contracts;

namespace CrescentAtlas.Runtime;

public sealed class MutableAtlasDataSource : IAtlasDataSource
{
    private readonly object sync = new();
    private readonly Dictionary<string, AtlasMarker> markers = new(StringComparer.Ordinal);
    private readonly HashSet<string> resetBlockedTreasureKeys = new(StringComparer.Ordinal);

    public bool IsInOccultCrescent { get; private set; }

    public uint TerritoryId { get; private set; }

    public uint MapId { get; private set; }

    public OccultCrescentMapLayer MapLayer { get; private set; }

    public string TerritoryName { get; private set; } = string.Empty;

    public Vector3? PlayerPosition { get; private set; }

    public float? PlayerRotation { get; private set; }

    public AtlasPotPrediction? PotPrediction { get; private set; }

    public bool IsMagicalElixirActive { get; private set; }

    public uint MagicalElixirStatusId { get; private set; }

    public IReadOnlyList<AtlasMarker> GetMarkers()
    {
        lock (sync)
            return markers.Values.ToArray();
    }

    public void SetContext(
        bool isInOccultCrescent,
        uint territoryId,
        uint mapId,
        OccultCrescentMapLayer mapLayer,
        string territoryName,
        Vector3? playerPosition,
        float? playerRotation)
    {
        lock (sync)
        {
            if (IsInOccultCrescent != isInOccultCrescent
                || TerritoryId != territoryId
                || MapId != mapId
                || MapLayer != mapLayer)
            {
                markers.Clear();
                resetBlockedTreasureKeys.Clear();
                PotPrediction = null;
                IsMagicalElixirActive = false;
                MagicalElixirStatusId = 0;
            }

            IsInOccultCrescent = isInOccultCrescent;
            TerritoryId = territoryId;
            MapId = mapId;
            MapLayer = mapLayer;
            TerritoryName = territoryName;
            PlayerPosition = playerPosition;
            PlayerRotation = playerRotation;
        }
    }

    public void SetPlayerState(Vector3? playerPosition, float? playerRotation)
    {
        lock (sync)
        {
            PlayerPosition = playerPosition;
            PlayerRotation = playerRotation;
        }
    }

    public void SetPotPrediction(AtlasPotPrediction? prediction)
    {
        lock (sync)
            PotPrediction = prediction;
    }

    public void SetMagicalElixirState(bool isActive, uint statusId)
    {
        lock (sync)
        {
            IsMagicalElixirActive = isActive;
            MagicalElixirStatusId = isActive ? statusId : 0;
        }
    }

    public void MarkAbsentNearbyTreasureCandidatesChecked(
        Vector3 playerPosition,
        float visibilityRadius,
        IReadOnlyCollection<AtlasMarker> visibleTreasures,
        float objectMatchRadius)
    {
        var visibilityRadiusSquared = visibilityRadius * visibilityRadius;
        var objectMatchRadiusSquared = objectMatchRadius * objectMatchRadius;
        lock (sync)
        {
            resetBlockedTreasureKeys.RemoveWhere(key =>
                !markers.TryGetValue(key, out var marker)
                || HorizontalDistanceSquared(playerPosition, marker.Position) > visibilityRadiusSquared);

            foreach (var key in markers
                         .Where(pair =>
                             pair.Value.Kind == AtlasMarkerKind.TreasureCandidate
                             && !pair.Value.IsChecked
                             && !resetBlockedTreasureKeys.Contains(pair.Key)
                             && HorizontalDistanceSquared(playerPosition, pair.Value.Position) <= visibilityRadiusSquared
                             && !visibleTreasures.Any(treasure =>
                                 HorizontalDistanceSquared(treasure.Position, pair.Value.Position)
                                 <= objectMatchRadiusSquared))
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                markers[key] = markers[key] with { IsChecked = true };
            }
        }
    }

    public void ResetTreasureChecks()
    {
        lock (sync)
        {
            foreach (var key in markers
                         .Where(pair => pair.Value.Kind == AtlasMarkerKind.TreasureCandidate)
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                markers[key] = markers[key] with { IsChecked = false };
                resetBlockedTreasureKeys.Add(key);
            }
        }
    }

    public void RestoreTreasureChecks(IReadOnlySet<string> checkedKeys)
    {
        lock (sync)
        {
            foreach (var key in markers
                         .Where(pair =>
                             pair.Value.Kind == AtlasMarkerKind.TreasureCandidate
                             && checkedKeys.Contains(pair.Key))
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                markers[key] = markers[key] with { IsChecked = true };
            }
        }
    }

    private static float HorizontalDistanceSquared(Vector3 left, Vector3 right)
        => ((left.X - right.X) * (left.X - right.X))
           + ((left.Z - right.Z) * (left.Z - right.Z));

    public void ReplaceSource(AtlasMarkerKind kind, IEnumerable<AtlasMarker> replacement)
    {
        lock (sync)
        {
            foreach (var key in markers
                         .Where(pair => pair.Value.Kind == kind)
                         .Select(pair => pair.Key)
                         .ToArray())
                markers.Remove(key);

            foreach (var marker in replacement)
                markers[marker.Key] = marker;

            resetBlockedTreasureKeys.RemoveWhere(key => !markers.ContainsKey(key));
        }
    }

    public void Upsert(IEnumerable<AtlasMarker> updates)
    {
        lock (sync)
        {
            foreach (var marker in updates)
                markers[marker.Key] = marker;
        }
    }
}
