namespace CrescentAtlas.Contracts;

public interface IAtlasDataSource
{
    bool IsInOccultCrescent { get; }

    uint TerritoryId { get; }

    uint MapId { get; }

    OccultCrescentMapLayer MapLayer { get; }

    string TerritoryName { get; }

    Vector3? PlayerPosition { get; }

    float? PlayerRotation { get; }

    AtlasPotPrediction? PotPrediction { get; }

    IReadOnlyList<AtlasMarker> GetMarkers();

    void ResetTreasureChecks();
}
