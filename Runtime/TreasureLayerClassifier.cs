namespace CrescentAtlas.Runtime;

public static class TreasureLayerClassifier
{
    // Collected North Horn surface objects reach Y=-48.86, while confirmed
    // subterranean treasure objects begin at Y=-92.03. Keep a conservative
    // gap so low surface coffers are retained without leaking underground
    // layout candidates onto the surface map.
    public const float MinimumSurfaceElevation = -70.0f;

    public static bool IsSurfaceCandidate(Vector3 position)
        => float.IsFinite(position.X)
           && float.IsFinite(position.Y)
           && float.IsFinite(position.Z)
           && position.Y > MinimumSurfaceElevation;
}
