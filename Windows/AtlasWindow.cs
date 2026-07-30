using CrescentAtlas.Contracts;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Lumina.Excel.Sheets;

namespace CrescentAtlas.Windows;

/// <summary>
/// A passive, game-map-independent view of the observations collected for the
/// current territory. The window deliberately has no dependency on a map row
/// or map texture so it remains useful while North Horn data is being learned.
/// </summary>
public sealed class AtlasWindow : Window, IDisposable
{
    private const float CanvasMinimumHeight = 280.0f;
    private const float CanvasPadding = 24.0f;
    private const float BoundsPaddingWorld = 12.0f;
    private const float MarkerRadius = 5.0f;
    private const float NearbyTreasureDistance = 120.0f;

    private static readonly Vector4 BackgroundColor = new(0.035f, 0.045f, 0.055f, 0.96f);
    private static readonly Vector4 GridColor = new(0.28f, 0.34f, 0.39f, 0.23f);
    private static readonly Vector4 BorderColor = new(0.55f, 0.64f, 0.69f, 0.62f);

    private static readonly LegendEntry[] Legend =
    [
        new(AtlasMarkerKind.Player, "Player", new Vector4(0.96f, 0.96f, 1.00f, 1.0f)),
        new(AtlasMarkerKind.TreasureCandidate, "Treasure candidate", new Vector4(0.55f, 0.73f, 0.85f, 1.0f)),
        new(AtlasMarkerKind.ActiveTreasure, "Active treasure", new Vector4(0.26f, 0.92f, 1.00f, 1.0f)),
        new(AtlasMarkerKind.Carrot, "Carrot", new Vector4(1.00f, 0.55f, 0.18f, 1.0f)),
        new(AtlasMarkerKind.Fate, "FATE", new Vector4(0.78f, 0.42f, 1.00f, 1.0f)),
        new(AtlasMarkerKind.CriticalEncounter, "Critical encounter", new Vector4(1.00f, 0.24f, 0.31f, 1.0f)),
        new(AtlasMarkerKind.PotFate, "Magic pot", new Vector4(1.00f, 0.83f, 0.25f, 1.0f)),
        new(AtlasMarkerKind.PotChest, "Pot chest", new Vector4(0.45f, 1.00f, 0.48f, 1.0f)),
    ];

    private readonly IAtlasDataSource dataSource;
    private readonly Configuration configuration;
    private readonly IDataManager dataManager;
    private readonly IClientState clientState;
    private readonly ITextureProvider textureProvider;

    public string MapDiagnostic { get; private set; } = "Map not checked.";

    public AtlasWindow(
        IAtlasDataSource dataSource,
        Configuration configuration,
        IDataManager dataManager,
        IClientState clientState,
        ITextureProvider textureProvider)
        : base("Crescent Atlas###CrescentAtlasMap")
    {
        this.dataSource = dataSource;
        this.configuration = configuration;
        this.dataManager = dataManager;
        this.clientState = clientState;
        this.textureProvider = textureProvider;
        IsOpen = configuration.MapVisible;

        Flags |= ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoBringToFrontOnFocus
            | ImGuiWindowFlags.NoNav
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(440.0f, 420.0f),
            MaximumSize = new Vector2(1600.0f, 1200.0f),
        };
    }

    public void Dispose()
    {
    }

    public override void PreDraw()
    {
        IsOpen = configuration.MapVisible;

        if (configuration.MapClickThrough)
            Flags |= ImGuiWindowFlags.NoInputs;
        else
            Flags &= ~ImGuiWindowFlags.NoInputs;
    }

    public override void Draw()
    {
        var opacity = Math.Clamp(configuration.MapOpacity, 0.15f, 1.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, opacity);
        try
        {
            DrawContents();
        }
        finally
        {
            ImGui.PopStyleVar();
        }
    }

    private void DrawContents()
    {
        var markers = dataSource.GetMarkers() ?? Array.Empty<AtlasMarker>();
        var visibleMarkers = markers
            .Where(marker => marker.TerritoryId == 0 || marker.TerritoryId == dataSource.TerritoryId)
            .ToArray();

        var territoryName = string.IsNullOrWhiteSpace(dataSource.TerritoryName)
            ? "Unknown territory"
            : dataSource.TerritoryName;
        ImGui.TextUnformatted($"{territoryName}  (Territory {dataSource.TerritoryId})");
        ImGui.SameLine();
        ImGui.TextDisabled(configuration.MapClickThrough ? "Display only / click-through" : "Layout mode");

        var legendHeight = DrawLegend();
        var available = ImGui.GetContentRegionAvail();
        var canvasSize = new Vector2(
            Math.Max(1.0f, available.X),
            Math.Max(CanvasMinimumHeight, available.Y - legendHeight));

        var canvasMinimum = ImGui.GetCursorScreenPos();
        ImGui.Dummy(canvasSize);
        DrawField(canvasMinimum, canvasSize, visibleMarkers, dataSource.PlayerPosition);
    }

    private static float DrawLegend()
    {
        ImGui.Spacing();
        var startY = ImGui.GetCursorPosY();
        var availableWidth = Math.Max(100.0f, ImGui.GetContentRegionAvail().X);
        var usedWidth = 0.0f;

        foreach (var entry in Legend)
        {
            var itemWidth = 18.0f + ImGui.CalcTextSize(entry.Label).X + ImGui.GetStyle().ItemSpacing.X;
            if (usedWidth > 0.0f && usedWidth + itemWidth > availableWidth)
            {
                usedWidth = 0.0f;
            }
            else if (usedWidth > 0.0f)
            {
                ImGui.SameLine();
            }

            ImGui.TextColored(entry.Color, MarkerGlyph(entry.Kind));
            ImGui.SameLine(0.0f, 3.0f);
            ImGui.TextDisabled(entry.Label);
            usedWidth += itemWidth;
        }

        ImGui.Spacing();
        return ImGui.GetCursorPosY() - startY;
    }

    private void DrawField(
        Vector2 canvasMinimum,
        Vector2 canvasSize,
        IReadOnlyList<AtlasMarker> markers,
        Vector3? playerPosition)
    {
        var canvasMaximum = canvasMinimum + canvasSize;
        var drawList = ImGui.GetWindowDrawList();

        drawList.PushClipRect(canvasMinimum, canvasMaximum, true);
        drawList.AddRectFilled(canvasMinimum, canvasMaximum, ImGui.GetColorU32(BackgroundColor), 5.0f);

        Func<Vector3, Vector2> project;
        if (TryGetGameMap(out var map, out var mapTexture))
        {
            var side = Math.Min(canvasSize.X, canvasSize.Y);
            var mapSize = new Vector2(side, side);
            var mapMinimum = canvasMinimum + ((canvasSize - mapSize) * 0.5f);
            var mapMaximum = mapMinimum + mapSize;
            drawList.AddImage(
                mapTexture.Handle,
                mapMinimum,
                mapMaximum,
                Vector2.Zero,
                Vector2.One,
                ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, 0.92f)));
            drawList.AddRect(mapMinimum, mapMaximum, ImGui.GetColorU32(BorderColor), 4.0f);
            drawList.AddText(
                mapMinimum + new Vector2((side * 0.5f) - 5.0f, 6.0f),
                ImGui.GetColorU32(new Vector4(1.0f, 0.94f, 0.72f, 1.0f)),
                "N");
            project = position => ProjectToGameMap(position, map, mapMinimum, mapSize);
        }
        else
        {
            DrawGrid(drawList, canvasMinimum, canvasMaximum);
            drawList.AddText(
                canvasMinimum + new Vector2(12.0f, canvasSize.Y - ImGui.GetTextLineHeight() - 8.0f),
                ImGui.GetColorU32(new Vector4(1.0f, 0.52f, 0.28f, 0.92f)),
                $"Game map unavailable: {MapDiagnostic}");
            var bounds = FieldBounds.Create(markers, playerPosition);
            var fallback = new FieldProjection(bounds, canvasMinimum, canvasSize, CanvasPadding);
            project = fallback.Project;
        }

        DrawTreasureCandidateRoute(drawList, project, markers, playerPosition);
        DrawNearbyTreasureLines(drawList, project, markers, playerPosition);

        foreach (var marker in markers.Where(marker => marker.Kind != AtlasMarkerKind.Player))
            DrawMarker(drawList, project(marker.Position), marker);

        if (playerPosition is { } position)
            DrawPlayer(drawList, project(position));
        else
            DrawMissingPlayerNotice(drawList, canvasMinimum);

        drawList.AddRect(canvasMinimum, canvasMaximum, ImGui.GetColorU32(BorderColor), 5.0f, ImDrawFlags.None, 1.0f);
        drawList.PopClipRect();
    }

    private bool TryGetGameMap(out Map map, out IDalamudTextureWrap texture)
    {
        map = default;
        texture = null!;

        if (clientState.MapId == 0
            || !dataManager.GetExcelSheet<Map>().TryGetRow(clientState.MapId, out map))
        {
            MapDiagnostic = $"Map row {clientState.MapId} is unavailable.";
            return false;
        }

        var mapId = map.Id.ToString().Trim().TrimEnd('\0');
        if (string.IsNullOrWhiteSpace(mapId))
        {
            MapDiagnostic = $"Map row {clientState.MapId} has an empty Id.";
            return false;
        }

        var textureName = mapId.Replace('/', '_');
        var paths = new[]
        {
            $"ui/map/{mapId}/{textureName}_m.tex",
            $"ui/map/{mapId}/{textureName}_s.tex",
        };

        foreach (var path in paths)
        {
            if (!dataManager.FileExists(path))
                continue;

            var candidate = textureProvider.GetFromGame(path).GetWrapOrEmpty();
            if (candidate.Width <= 1 || candidate.Height <= 1)
            {
                MapDiagnostic = $"Map {clientState.MapId} '{mapId}': loading {path}";
                continue;
            }

            MapDiagnostic = $"Map {clientState.MapId} '{mapId}': {path}";
            texture = candidate;
            return true;
        }

        MapDiagnostic = $"Map {clientState.MapId} '{mapId}': no _m/_s texture found.";
        return false;
    }

    private static Vector2 ProjectToGameMap(
        Vector3 world,
        Map map,
        Vector2 mapMinimum,
        Vector2 mapSize)
    {
        var coordinate = MapUtil.WorldToMap(new Vector2(world.X, world.Z), map);
        var normalized = new Vector2(
            Math.Clamp((coordinate.X - 1.0f) / 41.0f, 0.0f, 1.0f),
            Math.Clamp((coordinate.Y - 1.0f) / 41.0f, 0.0f, 1.0f));
        return mapMinimum + (normalized * mapSize);
    }

    private static void DrawTreasureCandidateRoute(
        ImDrawListPtr drawList,
        Func<Vector3, Vector2> project,
        IReadOnlyList<AtlasMarker> markers,
        Vector3? playerPosition)
    {
        if (playerPosition is not { } player)
            return;

        var remaining = markers
            .Where(marker => marker.Kind == AtlasMarkerKind.TreasureCandidate)
            .Select(marker => marker.Position)
            .ToList();
        if (remaining.Count == 0)
            return;

        var current = player;
        var currentScreen = project(current);
        var routeColor = ImGui.GetColorU32(new Vector4(0.36f, 0.80f, 0.92f, 0.45f));

        while (remaining.Count > 0)
        {
            var closestIndex = 0;
            var closestDistance = Vector3.DistanceSquared(current, remaining[0]);
            for (var index = 1; index < remaining.Count; index++)
            {
                var distance = Vector3.DistanceSquared(current, remaining[index]);
                if (distance >= closestDistance)
                    continue;

                closestIndex = index;
                closestDistance = distance;
            }

            var next = remaining[closestIndex];
            var nextScreen = project(next);
            drawList.AddLine(currentScreen, nextScreen, routeColor, 1.5f);
            current = next;
            currentScreen = nextScreen;
            remaining.RemoveAt(closestIndex);
        }
    }

    private static void DrawNearbyTreasureLines(
        ImDrawListPtr drawList,
        Func<Vector3, Vector2> project,
        IReadOnlyList<AtlasMarker> markers,
        Vector3? playerPosition)
    {
        if (playerPosition is not { } player)
            return;

        var playerScreen = project(player);
        var maximumDistanceSquared = NearbyTreasureDistance * NearbyTreasureDistance;
        var lineColor = ImGui.GetColorU32(new Vector4(0.18f, 0.95f, 1.00f, 0.92f));

        foreach (var marker in markers.Where(marker =>
                     marker.Kind == AtlasMarkerKind.ActiveTreasure
                     && Vector3.DistanceSquared(player, marker.Position) <= maximumDistanceSquared))
        {
            var treasureScreen = project(marker.Position);
            drawList.AddLine(playerScreen, treasureScreen, lineColor, 3.0f);
            drawList.AddCircle(treasureScreen, MarkerRadius + 5.0f, lineColor, 0, 2.0f);

            var distance = Vector3.Distance(player, marker.Position);
            var midpoint = (playerScreen + treasureScreen) * 0.5f;
            drawList.AddText(midpoint, lineColor, $"{distance:F0}y");
        }
    }

    private static void DrawGrid(ImDrawListPtr drawList, Vector2 minimum, Vector2 maximum)
    {
        const int divisions = 8;
        var color = ImGui.GetColorU32(GridColor);

        for (var index = 1; index < divisions; index++)
        {
            var fraction = index / (float)divisions;
            var x = minimum.X + ((maximum.X - minimum.X) * fraction);
            var y = minimum.Y + ((maximum.Y - minimum.Y) * fraction);
            drawList.AddLine(new Vector2(x, minimum.Y), new Vector2(x, maximum.Y), color);
            drawList.AddLine(new Vector2(minimum.X, y), new Vector2(maximum.X, y), color);
        }
    }

    private static void DrawMarker(ImDrawListPtr drawList, Vector2 point, AtlasMarker marker)
    {
        var color = MarkerColor(marker.Kind);
        if (!marker.IsActive)
            color.W *= 0.45f;

        var packedColor = ImGui.GetColorU32(color);
        var radius = marker.Kind == AtlasMarkerKind.TreasureCandidate ? MarkerRadius - 1.0f : MarkerRadius;

        if (marker.Kind == AtlasMarkerKind.TreasureCandidate)
        {
            drawList.AddCircle(point, radius, packedColor, 0, 1.5f);
        }
        else if (marker.Kind is AtlasMarkerKind.ActiveTreasure or AtlasMarkerKind.PotChest)
        {
            DrawDiamond(drawList, point, radius + 1.0f, packedColor);
        }
        else
        {
            drawList.AddCircleFilled(point, radius, packedColor);
        }

        if (!string.IsNullOrWhiteSpace(marker.Label))
        {
            var labelPosition = point + new Vector2(radius + 4.0f, -ImGui.GetTextLineHeight() * 0.5f);
            drawList.AddText(labelPosition, packedColor, marker.Label);
        }
    }

    private static void DrawPlayer(ImDrawListPtr drawList, Vector2 point)
    {
        var color = ImGui.GetColorU32(MarkerColor(AtlasMarkerKind.Player));
        var shadow = ImGui.GetColorU32(new Vector4(0.02f, 0.03f, 0.04f, 0.85f));
        var top = point + new Vector2(0.0f, -8.0f);
        var left = point + new Vector2(-6.0f, 7.0f);
        var right = point + new Vector2(6.0f, 7.0f);

        drawList.AddCircleFilled(point, 10.0f, shadow);
        drawList.AddTriangleFilled(top, right, left, color);
        drawList.AddText(point + new Vector2(10.0f, -ImGui.GetTextLineHeight() * 0.5f), color, "Player");
    }

    private static void DrawDiamond(ImDrawListPtr drawList, Vector2 point, float radius, uint color)
    {
        drawList.AddQuadFilled(
            point + new Vector2(0.0f, -radius),
            point + new Vector2(radius, 0.0f),
            point + new Vector2(0.0f, radius),
            point + new Vector2(-radius, 0.0f),
            color);
    }

    private static void DrawMissingPlayerNotice(ImDrawListPtr drawList, Vector2 canvasMinimum)
    {
        drawList.AddText(
            canvasMinimum + new Vector2(12.0f, 10.0f),
            ImGui.GetColorU32(new Vector4(0.72f, 0.76f, 0.80f, 0.8f)),
            "Waiting for player position");
    }

    private static Vector4 MarkerColor(AtlasMarkerKind kind)
        => Legend.First(entry => entry.Kind == kind).Color;

    private static string MarkerGlyph(AtlasMarkerKind kind)
        => kind switch
        {
            AtlasMarkerKind.Player => "\u25b2",
            AtlasMarkerKind.TreasureCandidate => "\u25c7",
            AtlasMarkerKind.ActiveTreasure => "\u25c6",
            AtlasMarkerKind.Carrot => "\u25cf",
            AtlasMarkerKind.Fate => "\u25cf",
            AtlasMarkerKind.CriticalEncounter => "\u25cf",
            AtlasMarkerKind.PotFate => "\u2605",
            AtlasMarkerKind.PotChest => "\u25c6",
            _ => "\u2022",
        };

    private readonly record struct LegendEntry(AtlasMarkerKind Kind, string Label, Vector4 Color);

    private readonly record struct FieldBounds(float MinimumX, float MaximumX, float MinimumZ, float MaximumZ)
    {
        public static FieldBounds Create(IReadOnlyList<AtlasMarker> markers, Vector3? playerPosition)
        {
            var positions = markers.Select(marker => marker.Position).ToList();
            if (playerPosition is { } player)
                positions.Add(player);

            if (positions.Count == 0)
                return new FieldBounds(-50.0f, 50.0f, -50.0f, 50.0f);

            var minimumX = positions.Min(position => position.X);
            var maximumX = positions.Max(position => position.X);
            var minimumZ = positions.Min(position => position.Z);
            var maximumZ = positions.Max(position => position.Z);

            if (maximumX - minimumX < 1.0f)
            {
                minimumX -= 0.5f;
                maximumX += 0.5f;
            }

            if (maximumZ - minimumZ < 1.0f)
            {
                minimumZ -= 0.5f;
                maximumZ += 0.5f;
            }

            return new FieldBounds(
                minimumX - BoundsPaddingWorld,
                maximumX + BoundsPaddingWorld,
                minimumZ - BoundsPaddingWorld,
                maximumZ + BoundsPaddingWorld);
        }
    }

    private readonly record struct FieldProjection(
        FieldBounds Bounds,
        Vector2 CanvasMinimum,
        Vector2 CanvasSize,
        float Padding)
    {
        public Vector2 Project(Vector3 world)
        {
            var drawableWidth = Math.Max(1.0f, CanvasSize.X - (Padding * 2.0f));
            var drawableHeight = Math.Max(1.0f, CanvasSize.Y - (Padding * 2.0f));
            var normalizedX = (world.X - Bounds.MinimumX) / (Bounds.MaximumX - Bounds.MinimumX);
            var normalizedZ = (world.Z - Bounds.MinimumZ) / (Bounds.MaximumZ - Bounds.MinimumZ);

            return CanvasMinimum + new Vector2(
                Padding + (normalizedX * drawableWidth),
                Padding + ((1.0f - normalizedZ) * drawableHeight));
        }
    }
}
