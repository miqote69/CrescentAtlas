using CrescentAtlas.Contracts;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace CrescentAtlas.Overlays;

/// <summary>
/// Passive screen-space projection of a world-space player-to-treasure line.
/// It does not create an input surface and never interacts with the game map.
/// </summary>
public sealed class NearbyTreasureLineOverlay(
    IGameGui gameGui,
    IAtlasDataSource dataSource)
{
    private const float MaximumDistance = 120.0f;

    public void Draw()
    {
        if (dataSource.PlayerPosition is not { } player
            || !gameGui.WorldToScreen(player, out var playerScreen))
        {
            return;
        }

        var maximumDistanceSquared = MaximumDistance * MaximumDistance;
        var drawList = ImGui.GetForegroundDrawList();
        var lineColor = ImGui.GetColorU32(new Vector4(0.18f, 0.95f, 1.00f, 0.92f));
        var shadowColor = ImGui.GetColorU32(new Vector4(0.01f, 0.03f, 0.04f, 0.78f));

        foreach (var treasure in dataSource.GetMarkers().Where(marker =>
                     marker.Kind == AtlasMarkerKind.ActiveTreasure
                     && marker.IsActive
                     && Vector3.DistanceSquared(player, marker.Position) <= maximumDistanceSquared))
        {
            if (!gameGui.WorldToScreen(treasure.Position, out var treasureScreen))
                continue;

            drawList.AddLine(playerScreen, treasureScreen, shadowColor, 6.0f);
            drawList.AddLine(playerScreen, treasureScreen, lineColor, 3.0f);
            drawList.AddCircle(treasureScreen, 11.0f, shadowColor, 0, 5.0f);
            drawList.AddCircle(treasureScreen, 10.0f, lineColor, 0, 2.5f);

            var distance = Vector3.Distance(player, treasure.Position);
            drawList.AddText(
                treasureScreen + new Vector2(13.0f, -8.0f),
                lineColor,
                $"{treasure.Label}  {distance:F0}y");
        }
    }
}
