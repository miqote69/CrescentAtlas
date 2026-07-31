using CrescentAtlas.Contracts;
using CrescentAtlas.Runtime;
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
                     && AtlasMarkerSelector.IsTreasureVisible(
                         marker,
                         configuration.ShowBronzeTreasure,
                         configuration.ShowSilverTreasure)
                     && Vector3.DistanceSquared(player, marker.Position) <= maximumDistanceSquared))
        {
            if (!gameGui.WorldToScreen(treasure.Position, out var treasureScreen))
                continue;

            if (configuration.ShowTreasureGuideLines)
            {
                drawList.AddLine(playerScreen, treasureScreen, shadowColor, 6.0f);
                drawList.AddLine(playerScreen, treasureScreen, lineColor, 3.0f);
            }

            drawList.AddCircle(treasureScreen, 11.0f, shadowColor, 0, 5.0f);
            drawList.AddCircle(treasureScreen, 10.0f, lineColor, 0, 2.5f);

            var distance = Vector3.Distance(player, treasure.Position);
            drawList.AddText(
                treasureScreen + new Vector2(13.0f, -8.0f),
                lineColor,
                $"{treasure.Label}  {distance:F0}y");
        }

        if (configuration.ShowPotTarget)
            DrawMagicalElixirTargets(drawList, markers, player, playerScreen, shadowColor);
        if (configuration.ShowCarrots)
            DrawNearestLiveCarrot(drawList, markers, player, playerScreen, shadowColor);
        DrawNearestKnownSpot(drawList, markers, player, playerScreen, shadowColor);
    }

    private void DrawMagicalElixirTargets(
        ImDrawListPtr drawList,
        IReadOnlyList<AtlasMarker> markers,
        Vector3 player,
        Vector2 playerScreen,
        uint shadowColor)
    {
        var maximumDistanceSquared = MaximumDistance * MaximumDistance;
        foreach (var target in markers.Where(marker =>
                     marker.Kind == AtlasMarkerKind.PotTarget
                     && marker.IsActive
                     && !marker.EventState.Equals("direction-search-area", StringComparison.Ordinal)
                     && Vector3.DistanceSquared(player, marker.Position) <= maximumDistanceSquared))
        {
            var isDirectionCandidate = target.EventState.Equals(
                "direction-candidate",
                StringComparison.Ordinal);
            var targetColor = ImGui.GetColorU32(isDirectionCandidate
                ? new Vector4(1.00f, 0.72f, 0.12f, 0.98f)
                : new Vector4(0.45f, 1.00f, 0.48f, 0.98f));
            if (!gameGui.WorldToScreen(target.Position, out var targetScreen))
                continue;

            if (configuration.ShowTreasureGuideLines)
            {
                drawList.AddLine(playerScreen, targetScreen, shadowColor, 7.0f);
                drawList.AddLine(playerScreen, targetScreen, targetColor, 3.5f);
            }

            drawList.AddCircle(targetScreen, 14.0f, shadowColor, 0, 5.0f);
            drawList.AddCircle(targetScreen, 13.0f, targetColor, 0, 3.0f);
            if (isDirectionCandidate)
            {
                drawList.AddLine(
                    targetScreen + new Vector2(-7.0f, 0.0f),
                    targetScreen + new Vector2(7.0f, 0.0f),
                    targetColor,
                    2.0f);
                drawList.AddLine(
                    targetScreen + new Vector2(0.0f, -7.0f),
                    targetScreen + new Vector2(0.0f, 7.0f),
                    targetColor,
                    2.0f);
                drawList.AddCircleFilled(targetScreen, 2.5f, targetColor, 12);
            }

            var distance = Vector3.Distance(player, target.Position);
            drawList.AddText(
                targetScreen + new Vector2(16.0f, -9.0f),
                targetColor,
                configuration.Language == UiLanguage.Japanese
                    ? $"マジカルエリクサー目標  {distance:F0}y"
                    : $"Magical Elixir target  {distance:F0}y");
        }
    }

    private void DrawNearestLiveCarrot(
        ImDrawListPtr drawList,
        IReadOnlyList<AtlasMarker> markers,
        Vector3 player,
        Vector2 playerScreen,
        uint shadowColor)
    {
        var carrot = AtlasMarkerSelector.FindNearestActiveCarrot(
            markers,
            player,
            MaximumDistance);
        if (carrot is null
            || !gameGui.WorldToScreen(carrot.Position, out var carrotScreen))
        {
            return;
        }

        var carrotColor = ImGui.GetColorU32(new Vector4(1.00f, 0.55f, 0.18f, 0.98f));
        drawList.AddLine(playerScreen, carrotScreen, shadowColor, 7.0f);
        drawList.AddLine(playerScreen, carrotScreen, carrotColor, 3.5f);
        drawList.AddCircle(carrotScreen, 14.0f, shadowColor, 0, 5.0f);
        drawList.AddCircle(carrotScreen, 13.0f, carrotColor, 0, 3.0f);

        var distance = Vector3.Distance(player, carrot.Position);
        drawList.AddText(
            carrotScreen + new Vector2(16.0f, -9.0f),
            carrotColor,
            configuration.Language == UiLanguage.Japanese
                ? $"にんじん  {distance:F0}y"
                : $"Carrot  {distance:F0}y");
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
                && !marker.IsChecked
                && AtlasMarkerSelector.IsTreasureVisible(
                    marker,
                    configuration.ShowBronzeTreasure,
                    configuration.ShowSilverTreasure))
            .MinBy(marker => HorizontalDistanceSquared(player, marker.Position));
        if (nearest is null
            || !gameGui.WorldToScreen(nearest.Position, out var spotScreen))
        {
            return;
        }

        var spotColor = ImGui.GetColorU32(new Vector4(1.00f, 0.67f, 0.18f, 0.94f));
        if (configuration.ShowTreasureGuideLines)
        {
            drawList.AddLine(playerScreen, spotScreen, shadowColor, 6.0f);
            drawList.AddLine(playerScreen, spotScreen, spotColor, 3.0f);
        }

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
