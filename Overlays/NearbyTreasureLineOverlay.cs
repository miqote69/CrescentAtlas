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
    IAtlasDataSource dataSource,
    Configuration configuration)
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
        var lineColor = ImGui.GetColorU32(new Vector4(0.20f, 1.00f, 0.38f, 0.96f));
        var shadowColor = ImGui.GetColorU32(new Vector4(0.01f, 0.03f, 0.04f, 0.78f));
        var markers = dataSource.GetMarkers();

        foreach (var treasure in markers.Where(marker =>
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

        DrawNearestKnownSpot(drawList, markers, player, playerScreen, shadowColor);
    }

    private void DrawNearestKnownSpot(
        ImDrawListPtr drawList,
        IReadOnlyList<AtlasMarker> markers,
        Vector3 player,
        Vector2 playerScreen,
        uint shadowColor)
    {
        var nearest = markers
            .Where(marker =>
                marker.Kind == AtlasMarkerKind.TreasureCandidate
                && !marker.IsChecked)
            .MinBy(marker => HorizontalDistanceSquared(player, marker.Position));
        if (nearest is null
            || !gameGui.WorldToScreen(nearest.Position, out var spotScreen))
        {
            return;
        }

        var spotColor = ImGui.GetColorU32(new Vector4(1.00f, 0.67f, 0.18f, 0.94f));
        drawList.AddLine(playerScreen, spotScreen, shadowColor, 6.0f);
        drawList.AddLine(playerScreen, spotScreen, spotColor, 3.0f);
        drawList.AddCircle(spotScreen, 13.0f, shadowColor, 0, 5.0f);
        drawList.AddCircle(spotScreen, 12.0f, spotColor, 0, 2.5f);

        var distance = MathF.Sqrt(HorizontalDistanceSquared(player, nearest.Position));
        drawList.AddText(
            spotScreen + new Vector2(15.0f, 9.0f),
            spotColor,
            configuration.Language == UiLanguage.Japanese
                ? $"最寄りの宝箱ポイント  {distance:F0}y"
                : $"Nearest treasure spot  {distance:F0}y");
    }

    private static float HorizontalDistanceSquared(Vector3 left, Vector3 right)
        => ((left.X - right.X) * (left.X - right.X))
           + ((left.Z - right.Z) * (left.Z - right.Z));
}
