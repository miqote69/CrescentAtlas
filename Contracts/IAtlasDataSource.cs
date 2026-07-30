namespace CrescentAtlas.Contracts;

public interface IAtlasDataSource
{
    uint TerritoryId { get; }

    string TerritoryName { get; }

    Vector3? PlayerPosition { get; }

    float? PlayerRotation { get; }

    IReadOnlyList<AtlasMarker> GetMarkers();
}
