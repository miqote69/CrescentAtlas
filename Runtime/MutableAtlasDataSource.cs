using CrescentAtlas.Contracts;

namespace CrescentAtlas.Runtime;

public sealed class MutableAtlasDataSource : IAtlasDataSource
{
    private readonly object sync = new();
    private readonly Dictionary<string, AtlasMarker> markers = new(StringComparer.Ordinal);

    public uint TerritoryId { get; private set; }

    public string TerritoryName { get; private set; } = string.Empty;

    public Vector3? PlayerPosition { get; private set; }

    public IReadOnlyList<AtlasMarker> GetMarkers()
    {
        lock (sync)
            return markers.Values.ToArray();
    }

    public void SetContext(uint territoryId, string territoryName, Vector3? playerPosition)
    {
        lock (sync)
        {
            if (TerritoryId != territoryId)
                markers.Clear();

            TerritoryId = territoryId;
            TerritoryName = territoryName;
            PlayerPosition = playerPosition;
        }
    }

    public void SetPlayerPosition(Vector3? playerPosition)
    {
        lock (sync)
            PlayerPosition = playerPosition;
    }

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
