namespace CrescentAtlas.Runtime;

/// <summary>
/// Projects world X/Z coordinates directly into the 2048x2048 game-map texture.
/// Player-facing map coordinates (for example X11.2 / Y10.9) are not texture
/// UVs and must not be normalized as though their 1..42 display range covered
/// the full texture. The distinction is visible on maps with SizeFactor 200,
/// including the Forked Tower floors.
/// </summary>
public static class GameMapTextureProjection
{
    private const float TextureCenterPixels = 1024.0f;
    private const float TextureSizePixels = 2048.0f;

    public static Vector2 Project(
        Vector3 world,
        int offsetX,
        int offsetY,
        uint sizeFactor)
    {
        var scale = sizeFactor / 100.0f;
        return new Vector2(
            (TextureCenterPixels + ((world.X + offsetX) * scale)) / TextureSizePixels,
            (TextureCenterPixels + ((world.Z + offsetY) * scale)) / TextureSizePixels);
    }

    public static bool IsOnMap(
        Vector3 world,
        int offsetX,
        int offsetY,
        uint sizeFactor)
    {
        var normalized = Project(world, offsetX, offsetY, sizeFactor);
        return float.IsFinite(normalized.X)
               && float.IsFinite(normalized.Y)
               && normalized.X is >= 0.0f and <= 1.0f
               && normalized.Y is >= 0.0f and <= 1.0f;
    }
}
