using CrescentAtlas.Contracts;
using CrescentAtlas.Data;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
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
    private const float MinimumMapZoom = 1.0f;
    private const float MaximumMapZoom = 4.0f;
    private const float MapZoomStep = 0.25f;
    private static readonly Vector4 BackgroundColor = new(0.035f, 0.045f, 0.055f, 0.96f);
    private static readonly Vector4 GridColor = new(0.28f, 0.34f, 0.39f, 0.23f);
    private static readonly Vector4 BorderColor = new(0.55f, 0.64f, 0.69f, 0.62f);
    private static readonly Vector4 CheckedTreasureColor = new(0.30f, 1.00f, 0.42f, 1.0f);
    private static readonly Vector4 BronzeTreasureColor = new(0.66f, 0.34f, 0.12f, 1.0f);
    private static readonly Vector4 CheckedBronzeTreasureColor = new(0.38f, 0.20f, 0.08f, 1.0f);
    private static readonly Vector4 BronzeTreasureRingColor = new(1.00f, 0.70f, 0.30f, 1.0f);
    private static readonly Vector4 SilverTreasureColor = new(0.88f, 0.94f, 1.00f, 1.0f);

    private static readonly LegendEntry[] Legend =
    [
        new(AtlasMarkerKind.Player, "Player", new Vector4(0.96f, 0.96f, 1.00f, 1.0f), LegendStyle.Player),
        new(AtlasMarkerKind.TreasureCandidate, "Unchecked treasure", new Vector4(0.20f, 0.92f, 1.00f, 1.0f)),
        new(AtlasMarkerKind.TreasureCandidate, "Checked treasure", CheckedTreasureColor, LegendStyle.CheckedTreasure),
        new(AtlasMarkerKind.TreasureCandidate, "Bronze treasure", BronzeTreasureColor, LegendStyle.BronzeTreasure),
        new(AtlasMarkerKind.TreasureCandidate, "Silver treasure", SilverTreasureColor, LegendStyle.SilverTreasure),
        new(AtlasMarkerKind.ActiveTreasure, "Active treasure", new Vector4(0.26f, 0.92f, 1.00f, 1.0f)),
        new(AtlasMarkerKind.Carrot, "Carrot", new Vector4(1.00f, 0.55f, 0.18f, 1.0f)),
        new(AtlasMarkerKind.Fate, "FATE", new Vector4(0.78f, 0.42f, 1.00f, 1.0f), LegendStyle.LiveGameIcon),
        new(AtlasMarkerKind.CriticalEncounter, "Critical encounter", new Vector4(1.00f, 0.24f, 0.31f, 1.0f), LegendStyle.LiveGameIcon),
        new(AtlasMarkerKind.CriticalEncounter, "Forked Tower", new Vector4(0.38f, 0.88f, 1.00f, 1.0f), LegendStyle.ForkedTower),
        new(AtlasMarkerKind.PotFate, "Magic pot", new Vector4(1.00f, 0.83f, 0.25f, 1.0f), LegendStyle.LiveGameIcon),
        new(AtlasMarkerKind.PotPrediction, "Magic pot prediction", new Vector4(1.00f, 0.72f, 0.08f, 1.0f), LegendStyle.PotPrediction),
        new(AtlasMarkerKind.PotTarget, "Magic Pot target", new Vector4(0.45f, 1.00f, 0.48f, 1.0f)),
        new(AtlasMarkerKind.Aetheryte, "Aetheryte", new Vector4(0.42f, 0.90f, 1.00f, 1.0f), LegendStyle.LiveGameIcon),
    ];

    private readonly IAtlasDataSource dataSource;
    private readonly Configuration configuration;
    private readonly IDataManager dataManager;
    private readonly IClientState clientState;
    private readonly ITextureProvider textureProvider;
    private readonly Func<IReadOnlyList<IslandVisitRecord>> visitHistoryProvider;
    private readonly System.Action saveConfiguration;
    private float mapZoom = MinimumMapZoom;
    private Vector2 mapCenter = new(0.5f, 0.5f);
    private AtlasPage currentPage = AtlasPage.Map;

    public string MapDiagnostic { get; private set; } = "Map not checked.";

    public AtlasWindow(
        IAtlasDataSource dataSource,
        Configuration configuration,
        IDataManager dataManager,
        IClientState clientState,
        ITextureProvider textureProvider,
        Func<IReadOnlyList<IslandVisitRecord>> visitHistoryProvider,
        System.Action saveConfiguration)
        : base("Crescent Atlas###CrescentAtlasMap")
    {
        this.dataSource = dataSource;
        this.configuration = configuration;
        this.dataManager = dataManager;
        this.clientState = clientState;
        this.textureProvider = textureProvider;
        this.visitHistoryProvider = visitHistoryProvider;
        this.saveConfiguration = saveConfiguration;
        IsOpen = configuration.MapVisible;

        Flags |= ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoBringToFrontOnFocus
            | ImGuiWindowFlags.NoNav
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse
            | ImGuiWindowFlags.MenuBar;

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
        => DrawContents();

    private void DrawContents()
    {
        DrawMenuBar();

        var markers = dataSource.GetMarkers() ?? Array.Empty<AtlasMarker>();
        var territoryMarkers = markers
            .Where(marker => marker.TerritoryId == 0 || marker.TerritoryId == dataSource.TerritoryId)
            .ToArray();
        if (currentPage == AtlasPage.IconGuide)
        {
            DrawIconGuide(territoryMarkers);
            return;
        }
        if (currentPage == AtlasPage.VisitHistory)
        {
            DrawVisitHistory();
            return;
        }

        var visibleMarkers = territoryMarkers
            .Where(IsMarkerVisible)
            .ToArray();

        var territoryName = string.IsNullOrWhiteSpace(dataSource.TerritoryName)
            ? "Unknown territory"
            : dataSource.TerritoryName;
        ImGui.TextUnformatted($"{territoryName}  (Territory {dataSource.TerritoryId})");
        ImGui.SameLine();
        ImGui.TextDisabled(configuration.MapClickThrough
            ? "Click-through mode"
            : "Drag map to pan / wheel to zoom / drag edge to resize");

        if (!configuration.MapClickThrough)
        {
            if (ImGui.Button("Reset treasure checks"))
                dataSource.ResetTreasureChecks();
        }

        DrawMapFilters();
        var available = ImGui.GetContentRegionAvail();
        var canvasSize = new Vector2(
            Math.Max(1.0f, available.X),
            Math.Max(CanvasMinimumHeight, available.Y));

        var canvasMinimum = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##CrescentAtlasMapCanvas", canvasSize, ImGuiButtonFlags.MouseButtonLeft);
        UpdateMapInteraction(canvasMinimum, canvasSize);
        DrawField(
            canvasMinimum,
            canvasSize,
            visibleMarkers,
            dataSource.PlayerPosition,
            dataSource.PlayerRotation);
    }

    private void DrawMenuBar()
    {
        if (!ImGui.BeginMenuBar())
            return;

        if (ImGui.MenuItem("Map###menu-map", string.Empty, currentPage == AtlasPage.Map))
            currentPage = AtlasPage.Map;
        if (ImGui.MenuItem(
                "Icon guide###menu-icon-guide",
                string.Empty,
                currentPage == AtlasPage.IconGuide))
        {
            currentPage = AtlasPage.IconGuide;
        }
        if (ImGui.MenuItem(
                "突入履歴###menu-visit-history",
                string.Empty,
                currentPage == AtlasPage.VisitHistory))
        {
            currentPage = AtlasPage.VisitHistory;
        }

        ImGui.EndMenuBar();
    }

    private void DrawIconGuide(IReadOnlyList<AtlasMarker> markers)
    {
        ImGui.TextUnformatted("Icon guide");
        ImGui.TextDisabled("Map symbols used by Crescent Atlas.");
        ImGui.Separator();
        DrawLegend(markers);
    }

    private void DrawVisitHistory()
    {
        var visits = visitHistoryProvider()
            .OrderByDescending(static visit => visit.EnteredAtUtc)
            .ToArray();

        ImGui.TextUnformatted("クレセントアイル 突入履歴");
        ImGui.TextDisabled("突入・退出時刻を新しい順に表示します。時刻はローカル時刻です。");
        ImGui.Separator();

        if (visits.Length == 0)
        {
            ImGui.TextDisabled("突入履歴はまだありません。");
            return;
        }

        if (!ImGui.BeginChild(
                "##CrescentAtlasVisitHistory",
                Vector2.Zero,
                false,
                ImGuiWindowFlags.AlwaysVerticalScrollbar))
        {
            ImGui.EndChild();
            return;
        }

        var flags = ImGuiTableFlags.Borders
                    | ImGuiTableFlags.RowBg
                    | ImGuiTableFlags.Resizable
                    | ImGuiTableFlags.SizingStretchProp;
        if (ImGui.BeginTable("##CrescentAtlasVisitHistoryTable", 5, flags))
        {
            ImGui.TableSetupColumn("突入", ImGuiTableColumnFlags.WidthFixed, 132.0f);
            ImGui.TableSetupColumn("退出", ImGuiTableColumnFlags.WidthFixed, 132.0f);
            ImGui.TableSetupColumn("滞在", ImGuiTableColumnFlags.WidthFixed, 72.0f);
            ImGui.TableSetupColumn("エリア");
            ImGui.TableSetupColumn("島識別");
            ImGui.TableHeadersRow();

            foreach (var visit in visits)
            {
                var enteredLocal = visit.EnteredAtUtc.ToLocalTime();
                var exitedLocal = visit.ExitedAtUtc?.ToLocalTime();
                var durationEnd = visit.ExitedAtUtc ?? DateTimeOffset.UtcNow;

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted(enteredLocal.ToString("yyyy/MM/dd HH:mm:ss"));
                ImGui.TableSetColumnIndex(1);
                if (exitedLocal is { } exited)
                    ImGui.TextUnformatted(exited.ToString("yyyy/MM/dd HH:mm:ss"));
                else
                    ImGui.TextColored(new Vector4(0.42f, 1.00f, 0.52f, 1.0f), "滞在中");
                ImGui.TableSetColumnIndex(2);
                ImGui.TextUnformatted(FormatVisitDuration(durationEnd - visit.EnteredAtUtc));
                ImGui.TableSetColumnIndex(3);
                ImGui.TextUnformatted(string.IsNullOrWhiteSpace(visit.TerritoryName)
                    ? $"Territory {visit.TerritoryId}"
                    : visit.TerritoryName);
                ImGui.TableSetColumnIndex(4);
                ImGui.TextUnformatted(visit.IslandKey);
                if (!string.IsNullOrWhiteSpace(visit.InstancePointer)
                    && ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(
                        $"Visit ID: {visit.VisitId}\n" +
                        $"Instance pointer: {visit.InstancePointer}\n" +
                        $"Last seen: {visit.LastSeenAtUtc.ToLocalTime():yyyy/MM/dd HH:mm:ss}");
                }
            }

            ImGui.EndTable();
        }

        ImGui.EndChild();
    }

    private static string FormatVisitDuration(TimeSpan duration)
    {
        var totalMinutes = Math.Max(0, (int)duration.TotalMinutes);
        return totalMinutes >= 60
            ? $"{totalMinutes / 60}:{totalMinutes % 60:00}"
            : $"{totalMinutes}分";
    }

    private void DrawMapFilters()
    {
        if (configuration.MapClickThrough)
            return;

        var showBronzeTreasure = configuration.ShowBronzeTreasure;
        var showSilverTreasure = configuration.ShowSilverTreasure;
        var showPotTarget = configuration.ShowPotTarget;
        var showFates = configuration.ShowFates;
        var showCriticalEncounters = configuration.ShowCriticalEncounters;
        var detailedEventDisplay = configuration.DetailedEventDisplay;
        var showForkedTower = configuration.ShowForkedTower;
        var showPotPrediction = configuration.ShowPotPrediction;
        var changed = false;
        var usedWidth = 0.0f;
        var availableWidth = Math.Max(100.0f, ImGui.GetContentRegionAvail().X);
        changed |= DrawFilterCheckbox(
            "Bronze chest",
            ref showBronzeTreasure,
            ref usedWidth,
            availableWidth);
        changed |= DrawFilterCheckbox(
            "Silver chest",
            ref showSilverTreasure,
            ref usedWidth,
            availableWidth);
        changed |= DrawFilterCheckbox(
            "Magic Pot target",
            ref showPotTarget,
            ref usedWidth,
            availableWidth);
        changed |= DrawFilterCheckbox(
            "FATE",
            ref showFates,
            ref usedWidth,
            availableWidth);
        changed |= DrawFilterCheckbox(
            "CE",
            ref showCriticalEncounters,
            ref usedWidth,
            availableWidth);
        changed |= DrawFilterCheckbox(
            "FATE/CE details",
            ref detailedEventDisplay,
            ref usedWidth,
            availableWidth);
        changed |= DrawFilterCheckbox(
            "Forked Tower",
            ref showForkedTower,
            ref usedWidth,
            availableWidth);
        changed |= DrawFilterCheckbox(
            "Pot prediction",
            ref showPotPrediction,
            ref usedWidth,
            availableWidth);

        if (changed)
        {
            configuration.ShowBronzeTreasure = showBronzeTreasure;
            configuration.ShowSilverTreasure = showSilverTreasure;
            configuration.ShowPotTarget = showPotTarget;
            configuration.ShowFates = showFates;
            configuration.ShowCriticalEncounters = showCriticalEncounters;
            configuration.DetailedEventDisplay = detailedEventDisplay;
            configuration.ShowForkedTower = showForkedTower;
            configuration.ShowPotPrediction = showPotPrediction;
            saveConfiguration();
        }
    }

    private static bool DrawFilterCheckbox(
        string label,
        ref bool value,
        ref float usedWidth,
        float availableWidth)
    {
        var style = ImGui.GetStyle();
        var itemWidth = ImGui.GetFrameHeight()
                        + style.ItemInnerSpacing.X
                        + ImGui.CalcTextSize(label).X
                        + style.ItemSpacing.X;
        if (usedWidth > 0.0f && usedWidth + itemWidth <= availableWidth)
            ImGui.SameLine();
        else if (usedWidth > 0.0f)
            usedWidth = 0.0f;

        var changed = ImGui.Checkbox(label, ref value);
        usedWidth += itemWidth;
        return changed;
    }

    private bool IsMarkerVisible(AtlasMarker marker)
    {
        if (marker.Kind == AtlasMarkerKind.Fate)
            return configuration.ShowFates;
        if (IsForkedTower(marker))
            return configuration.ShowForkedTower;
        if (marker.Kind == AtlasMarkerKind.CriticalEncounter)
            return configuration.ShowCriticalEncounters;
        if (marker.Kind == AtlasMarkerKind.PotTarget)
            return configuration.ShowPotTarget;
        if (IsSilverTreasure(marker))
            return configuration.ShowSilverTreasure;
        if (IsBronzeTreasure(marker))
            return configuration.ShowBronzeTreasure;

        return true;
    }

    private static bool IsForkedTower(AtlasMarker marker)
        => marker.Kind == AtlasMarkerKind.CriticalEncounter
           && (marker.EventId == 64
               || marker.Label.Contains("フォークタワー", StringComparison.OrdinalIgnoreCase)
               || marker.Label.Contains("Forked Tower", StringComparison.OrdinalIgnoreCase));

    private static bool IsBronzeTreasure(AtlasMarker marker)
        => marker.Kind is AtlasMarkerKind.TreasureCandidate or AtlasMarkerKind.ActiveTreasure
           && !marker.TreasureType.Equals("silver", StringComparison.OrdinalIgnoreCase)
           && !marker.TreasureType.Equals("gold", StringComparison.OrdinalIgnoreCase);

    private static bool IsSilverTreasure(AtlasMarker marker)
        => marker.Kind is AtlasMarkerKind.TreasureCandidate or AtlasMarkerKind.ActiveTreasure
           && marker.TreasureType.Equals("silver", StringComparison.OrdinalIgnoreCase);

    private void UpdateMapInteraction(Vector2 canvasMinimum, Vector2 canvasSize)
    {
        if (configuration.MapClickThrough)
            return;

        var side = Math.Min(canvasSize.X, canvasSize.Y);
        var io = ImGui.GetIO();
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            var mapSize = new Vector2(side * mapZoom);
            mapCenter = ClampMapCenter(
                mapCenter - (io.MouseDelta / mapSize),
                mapSize,
                canvasSize);
        }

        if (!ImGui.IsItemHovered())
            return;

        var wheel = io.MouseWheel;
        if (Math.Abs(wheel) < float.Epsilon)
            return;

        var oldZoom = mapZoom;
        var newZoom = Math.Clamp(
            oldZoom + (MathF.Sign(wheel) * MapZoomStep),
            MinimumMapZoom,
            MaximumMapZoom);
        if (Math.Abs(newZoom - oldZoom) < float.Epsilon)
            return;

        var canvasCenter = canvasMinimum + (canvasSize * 0.5f);
        var oldMapSize = new Vector2(side * oldZoom);
        var oldMapMinimum = canvasCenter - (mapCenter * oldMapSize);
        var mousePosition = ImGui.GetMousePos();
        var mouseOnMap = new Vector2(
            Math.Clamp((mousePosition.X - oldMapMinimum.X) / oldMapSize.X, 0.0f, 1.0f),
            Math.Clamp((mousePosition.Y - oldMapMinimum.Y) / oldMapSize.Y, 0.0f, 1.0f));

        var newMapSize = new Vector2(side * newZoom);
        var newMapMinimum = mousePosition - (mouseOnMap * newMapSize);
        mapCenter = ClampMapCenter(
            (canvasCenter - newMapMinimum) / newMapSize,
            newMapSize,
            canvasSize);
        mapZoom = newZoom;
    }

    private static Vector2 ClampMapCenter(
        Vector2 center,
        Vector2 mapSize,
        Vector2 canvasSize)
    {
        var halfVisible = new Vector2(
            Math.Min(0.5f, canvasSize.X / (2.0f * mapSize.X)),
            Math.Min(0.5f, canvasSize.Y / (2.0f * mapSize.Y)));
        return new Vector2(
            Math.Clamp(center.X, halfVisible.X, 1.0f - halfVisible.X),
            Math.Clamp(center.Y, halfVisible.Y, 1.0f - halfVisible.Y));
    }

    private float DrawLegend(IReadOnlyList<AtlasMarker> markers)
    {
        const float iconSize = 30.0f;
        ImGui.Spacing();
        var startY = ImGui.GetCursorPosY();
        var availableWidth = Math.Max(100.0f, ImGui.GetContentRegionAvail().X);
        var usedWidth = 0.0f;
        var drawList = ImGui.GetWindowDrawList();

        foreach (var entry in Legend)
        {
            var itemWidth = iconSize + 3.0f + ImGui.CalcTextSize(entry.Label).X
                            + ImGui.GetStyle().ItemSpacing.X;
            if (usedWidth > 0.0f && usedWidth + itemWidth > availableWidth)
            {
                usedWidth = 0.0f;
            }
            else if (usedWidth > 0.0f)
            {
                ImGui.SameLine();
            }

            var iconMinimum = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(iconSize, iconSize));
            DrawLegendIcon(
                drawList,
                iconMinimum + new Vector2(iconSize * 0.5f),
                entry,
                markers);
            ImGui.SameLine(0.0f, 3.0f);
            ImGui.TextDisabled(entry.Label);
            usedWidth += itemWidth;
        }

        ImGui.Spacing();
        return ImGui.GetCursorPosY() - startY;
    }

    private void DrawLegendIcon(
        ImDrawListPtr drawList,
        Vector2 point,
        LegendEntry entry,
        IReadOnlyList<AtlasMarker> markers)
    {
        if (entry.Style == LegendStyle.Player)
        {
            DrawPlayer(drawList, point, 0.0f);
            return;
        }

        if (entry.Style == LegendStyle.PotPrediction)
        {
            DrawPotPredictionCore(drawList, point);
            return;
        }

        var representative = entry.Style switch
        {
            LegendStyle.LiveGameIcon => markers.LastOrDefault(marker =>
                marker.Kind == entry.Kind
                && marker.IconId != 0
                && !IsForkedTower(marker)),
            LegendStyle.ForkedTower => markers.LastOrDefault(marker =>
                IsForkedTower(marker)
                && marker.IconId != 0),
            _ => null,
        };
        if (representative is not null)
        {
            DrawMarker(drawList, point, representative, false);
            return;
        }

        var marker = new AtlasMarker(
            $"legend:{entry.Kind}:{entry.Style}",
            entry.Kind,
            entry.Label,
            Vector3.Zero,
            DateTimeOffset.MinValue,
            true,
            0,
            IsChecked: entry.Style == LegendStyle.CheckedTreasure,
            TreasureType: entry.Style switch
            {
                LegendStyle.BronzeTreasure => "bronze",
                LegendStyle.SilverTreasure => "silver",
                _ => string.Empty,
            });
        DrawMarker(drawList, point, marker, false);
    }

    private void DrawField(
        Vector2 canvasMinimum,
        Vector2 canvasSize,
        IReadOnlyList<AtlasMarker> markers,
        Vector3? playerPosition,
        float? playerRotation)
    {
        var canvasMaximum = canvasMinimum + canvasSize;
        var drawList = ImGui.GetWindowDrawList();

        drawList.PushClipRect(canvasMinimum, canvasMaximum, true);
        drawList.AddRectFilled(canvasMinimum, canvasMaximum, ImGui.GetColorU32(BackgroundColor), 5.0f);

        Func<Vector3, Vector2> project;
        if (TryGetGameMap(out var map, out var mapTexture))
        {
            var side = Math.Min(canvasSize.X, canvasSize.Y) * mapZoom;
            var mapSize = new Vector2(side, side);
            var canvasCenter = canvasMinimum + (canvasSize * 0.5f);
            var mapMinimum = canvasCenter - (mapCenter * mapSize);
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
            var bounds = FieldBounds.Create(markers, playerPosition);
            var fallback = new FieldProjection(bounds, canvasMinimum, canvasSize, CanvasPadding);
            project = fallback.Project;
        }

        DrawNearestTreasureSpot(drawList, project, markers, playerPosition);
        DrawNearbyTreasureLines(drawList, project, markers, playerPosition);

        foreach (var marker in markers.Where(marker => marker.Kind != AtlasMarkerKind.Player))
            DrawMarker(
                drawList,
                project(marker.Position),
                marker,
                true,
                canvasMinimum,
                canvasMaximum);

        if (configuration.ShowPotPrediction
            && dataSource.PotPrediction is { } potPrediction)
            DrawPotPrediction(drawList, project(potPrediction.PredictedPosition), potPrediction);

        if (playerPosition is { } position)
            DrawPlayer(drawList, project(position), playerRotation);
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

        var compactTextureName = mapId.Replace("/", string.Empty, StringComparison.Ordinal);
        var legacyTextureName = mapId.Replace('/', '_');
        var paths = new[]
        {
            $"ui/map/{mapId}/{compactTextureName}_m.tex",
            $"ui/map/{mapId}/{compactTextureName}_s.tex",
            $"ui/map/{mapId}/{legacyTextureName}_m.tex",
            $"ui/map/{mapId}/{legacyTextureName}_s.tex",
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
        var lineColor = ImGui.GetColorU32(new Vector4(0.20f, 1.00f, 0.38f, 0.96f));

        foreach (var marker in markers.Where(marker =>
                     marker.Kind == AtlasMarkerKind.ActiveTreasure
                     && Vector3.DistanceSquared(player, marker.Position) <= maximumDistanceSquared))
        {
            var treasureScreen = project(marker.Position);
            drawList.AddLine(playerScreen, treasureScreen, lineColor, 3.0f);
            drawList.AddCircle(treasureScreen, MarkerRadius + 5.0f, lineColor, 0, 2.0f);
        }
    }

    private static void DrawNearestTreasureSpot(
        ImDrawListPtr drawList,
        Func<Vector3, Vector2> project,
        IReadOnlyList<AtlasMarker> markers,
        Vector3? playerPosition)
    {
        if (playerPosition is not { } player)
            return;

        var nearest = markers
            .Where(marker =>
                marker.Kind == AtlasMarkerKind.TreasureCandidate
                && !marker.IsChecked)
            .MinBy(marker => HorizontalDistanceSquared(player, marker.Position));
        if (nearest is null)
            return;

        var playerScreen = project(player);
        var spotScreen = project(nearest.Position);
        var spotColor = ImGui.GetColorU32(new Vector4(1.00f, 0.67f, 0.18f, 0.96f));
        drawList.AddLine(playerScreen, spotScreen, spotColor, 3.5f);
        drawList.AddCircle(spotScreen, MarkerRadius + 7.0f, spotColor, 0, 2.5f);
    }

    private static float HorizontalDistanceSquared(Vector3 left, Vector3 right)
        => ((left.X - right.X) * (left.X - right.X))
           + ((left.Z - right.Z) * (left.Z - right.Z));

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

    private void DrawMarker(
        ImDrawListPtr drawList,
        Vector2 point,
        AtlasMarker marker,
        bool drawEventStatus = true,
        Vector2? clipMinimum = null,
        Vector2? clipMaximum = null)
    {
        var isSilverTreasure = marker.Kind == AtlasMarkerKind.TreasureCandidate
                               && marker.TreasureType.Equals("silver", StringComparison.OrdinalIgnoreCase);
        var isBronzeTreasure = marker.Kind == AtlasMarkerKind.TreasureCandidate
                               && marker.TreasureType.Equals("bronze", StringComparison.OrdinalIgnoreCase);
        var color = isBronzeTreasure
            ? marker.IsChecked
                ? CheckedBronzeTreasureColor
                : BronzeTreasureColor
            : marker.Kind == AtlasMarkerKind.TreasureCandidate && marker.IsChecked
                ? CheckedTreasureColor
                : MarkerColor(marker.Kind);
        if (!marker.IsActive && marker.Kind != AtlasMarkerKind.TreasureCandidate)
            color.W *= 0.45f;

        var packedColor = ImGui.GetColorU32(color);
        var radius = marker.Kind == AtlasMarkerKind.TreasureCandidate ? MarkerRadius + 1.5f : MarkerRadius;

        if (marker.Kind == AtlasMarkerKind.Carrot)
        {
            DrawCarrotIcon(drawList, point);
            return;
        }

        if (marker.Kind is AtlasMarkerKind.Fate
                or AtlasMarkerKind.CriticalEncounter
                or AtlasMarkerKind.PotFate
                or AtlasMarkerKind.Aetheryte
            && marker.IconId != 0
            && TryDrawGameIcon(drawList, point, marker.IconId))
        {
            if (drawEventStatus)
                DrawEventStatus(drawList, point, marker, clipMinimum, clipMaximum);
            return;
        }

        if (marker.Kind == AtlasMarkerKind.TreasureCandidate)
        {
            var shadowColor = ImGui.GetColorU32(new Vector4(0.01f, 0.04f, 0.06f, 0.92f));
            var ring = isBronzeTreasure
                ? marker.IsChecked
                    ? CheckedTreasureColor
                    : BronzeTreasureRingColor
                : new Vector4(color.X, color.Y, color.Z, color.W * 0.72f);
            var ringColor = ImGui.GetColorU32(ring);
            drawList.AddCircleFilled(point, radius + 2.5f, shadowColor);
            drawList.AddCircleFilled(point, radius, packedColor);
            drawList.AddCircle(point, radius + 3.5f, ringColor, 0, isBronzeTreasure ? 2.5f : 2.0f);
            if (isBronzeTreasure)
                DrawDiamond(
                    drawList,
                    point,
                    radius - 2.0f,
                    ImGui.GetColorU32(new Vector4(0.96f, 0.62f, 0.24f, 1.0f)));
            if (isSilverTreasure)
                DrawDiamond(drawList, point, radius - 1.0f, ImGui.GetColorU32(SilverTreasureColor));
            if (marker.IsChecked)
            {
                var checkColor = ImGui.GetColorU32(isBronzeTreasure
                    ? new Vector4(0.50f, 1.00f, 0.55f, 1.0f)
                    : new Vector4(0.03f, 0.12f, 0.05f, 1.0f));
                drawList.AddLine(point + new Vector2(-3.0f, 0.0f), point + new Vector2(-0.5f, 3.0f), checkColor, 2.0f);
                drawList.AddLine(point + new Vector2(-0.5f, 3.0f), point + new Vector2(4.0f, -3.0f), checkColor, 2.0f);
            }
        }
        else if (marker.Kind is AtlasMarkerKind.ActiveTreasure or AtlasMarkerKind.PotTarget)
        {
            DrawDiamond(drawList, point, radius + 1.0f, packedColor);
        }
        else
        {
            drawList.AddCircleFilled(point, radius, packedColor);
        }

        if (drawEventStatus)
            DrawEventStatus(drawList, point, marker, clipMinimum, clipMaximum);
    }

    private static void DrawCarrotIcon(ImDrawListPtr drawList, Vector2 point)
    {
        var shadow = ImGui.GetColorU32(new Vector4(0.02f, 0.03f, 0.02f, 0.95f));
        var body = ImGui.GetColorU32(new Vector4(1.00f, 0.48f, 0.08f, 1.0f));
        var highlight = ImGui.GetColorU32(new Vector4(1.00f, 0.77f, 0.25f, 1.0f));
        var leaves = ImGui.GetColorU32(new Vector4(0.34f, 1.00f, 0.34f, 1.0f));

        var shoulderLeft = point + new Vector2(-5.5f, -2.5f);
        var shoulderRight = point + new Vector2(5.5f, -2.5f);
        var tip = point + new Vector2(0.0f, 8.5f);
        drawList.AddTriangleFilled(
            shoulderLeft + new Vector2(-1.5f, -1.0f),
            shoulderRight + new Vector2(1.5f, -1.0f),
            tip + new Vector2(0.0f, 1.5f),
            shadow);
        drawList.AddCircleFilled(point + new Vector2(0.0f, -2.0f), 6.5f, shadow);
        drawList.AddTriangleFilled(shoulderLeft, shoulderRight, tip, body);
        drawList.AddCircleFilled(point + new Vector2(0.0f, -2.0f), 5.5f, body);
        drawList.AddLine(
            point + new Vector2(-2.5f, 0.0f),
            point + new Vector2(2.0f, 1.0f),
            highlight,
            1.3f);

        var leafBase = point + new Vector2(0.0f, -6.0f);
        foreach (var leafTip in new[]
                 {
                     point + new Vector2(-5.5f, -11.0f),
                     point + new Vector2(0.0f, -13.0f),
                     point + new Vector2(5.5f, -11.0f),
                 })
        {
            drawList.AddLine(leafBase, leafTip, shadow, 4.5f);
            drawList.AddLine(leafBase, leafTip, leaves, 2.6f);
        }
    }

    private void DrawEventStatus(
        ImDrawListPtr drawList,
        Vector2 point,
        AtlasMarker marker,
        Vector2? clipMinimum,
        Vector2? clipMaximum)
    {
        if (marker.Kind is not (AtlasMarkerKind.Fate
            or AtlasMarkerKind.CriticalEncounter
            or AtlasMarkerKind.PotFate))
        {
            return;
        }

        var hasRemainingTime = marker.TimeRemainingSeconds >= 0;
        var remainingSeconds = Math.Max(0, marker.TimeRemainingSeconds);
        var minutes = remainingSeconds / 60;
        var seconds = remainingSeconds % 60;
        var progress = Math.Clamp(marker.Progress, (byte)0, (byte)100);
        var background = marker.Kind == AtlasMarkerKind.CriticalEncounter
                         && marker.EventState is "Register" or "Warmup"
            ? new Vector4(0.20f, 0.13f, 0.02f, 0.88f)
            : new Vector4(0.02f, 0.02f, 0.02f, 0.82f);

        if (!configuration.DetailedEventDisplay)
        {
            var compactTime = hasRemainingTime
                ? $"{minutes:00}:{seconds:00}"
                : "--:--";
            var compactProgress = $"{progress}%";
            var compactTimeSize = ImGui.CalcTextSize(compactTime);
            var compactProgressSize = ImGui.CalcTextSize(compactProgress);
            var compactGap = ImGui.CalcTextSize(" ").X;
            var compactSize = new Vector2(
                compactTimeSize.X + compactGap + compactProgressSize.X,
                Math.Max(compactTimeSize.Y, compactProgressSize.Y));
            var compactPadding = new Vector2(4.0f, 3.0f);
            var compactPosition = point + new Vector2(-(compactSize.X * 0.5f), 15.0f);
            if (clipMinimum is { } compactMinimum && clipMaximum is { } compactMaximum)
            {
                if (compactPosition.Y + compactSize.Y + compactPadding.Y > compactMaximum.Y - 2.0f)
                    compactPosition.Y = point.Y - 15.0f - compactSize.Y;

                compactPosition.X = Math.Clamp(
                    compactPosition.X,
                    compactMinimum.X + compactPadding.X + 2.0f,
                    Math.Max(
                        compactMinimum.X + compactPadding.X + 2.0f,
                        compactMaximum.X - compactSize.X - compactPadding.X - 2.0f));
                compactPosition.Y = Math.Clamp(
                    compactPosition.Y,
                    compactMinimum.Y + compactPadding.Y + 2.0f,
                    Math.Max(
                        compactMinimum.Y + compactPadding.Y + 2.0f,
                        compactMaximum.Y - compactSize.Y - compactPadding.Y - 2.0f));
            }

            drawList.AddRectFilled(
                compactPosition - compactPadding,
                compactPosition + compactSize + compactPadding,
                ImGui.GetColorU32(background),
                3.0f);
            drawList.AddText(
                compactPosition,
                ImGui.GetColorU32(new Vector4(1.0f, 0.88f, 0.46f, 1.0f)),
                compactTime);
            drawList.AddText(
                compactPosition + new Vector2(compactTimeSize.X + compactGap, 0.0f),
                ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, 1.0f)),
                compactProgress);
            return;
        }

        var timeLabel = marker.Kind == AtlasMarkerKind.CriticalEncounter
            ? marker.EventState switch
            {
                "Register" or "Warmup" => "開始まで",
                _ => "残り",
            }
            : "残り";
        var nameLine = CompactMarkerLabel(marker.Label, 18);
        var timeLine = hasRemainingTime
            ? $"{timeLabel} {minutes:00}:{seconds:00}"
            : $"{timeLabel} --:--";
        var progressLine = $"進捗 {progress}%";
        var nameLineSize = ImGui.CalcTextSize(nameLine);
        var timeLineSize = ImGui.CalcTextSize(timeLine);
        var progressLineSize = ImGui.CalcTextSize(progressLine);
        var lineHeight = Math.Max(nameLineSize.Y, Math.Max(timeLineSize.Y, progressLineSize.Y));
        var textSize = new Vector2(
            Math.Max(nameLineSize.X, Math.Max(timeLineSize.X, progressLineSize.X)),
            lineHeight * 3.0f);
        var padding = new Vector2(4.0f, 3.0f);
        var textPosition = point + new Vector2(-(textSize.X * 0.5f), 15.0f);
        if (clipMinimum is { } minimum && clipMaximum is { } maximum)
        {
            var lowerEdge = textPosition.Y + textSize.Y + padding.Y;
            if (lowerEdge > maximum.Y - 2.0f)
                textPosition.Y = point.Y - 15.0f - textSize.Y;

            textPosition.X = Math.Clamp(
                textPosition.X,
                minimum.X + padding.X + 2.0f,
                Math.Max(
                    minimum.X + padding.X + 2.0f,
                    maximum.X - textSize.X - padding.X - 2.0f));
            textPosition.Y = Math.Clamp(
                textPosition.Y,
                minimum.Y + padding.Y + 2.0f,
                Math.Max(
                    minimum.Y + padding.Y + 2.0f,
                    maximum.Y - textSize.Y - padding.Y - 2.0f));
        }
        drawList.AddRectFilled(
            textPosition - padding,
            textPosition + textSize + padding,
            ImGui.GetColorU32(background),
            3.0f);
        drawList.AddText(
            textPosition + new Vector2((textSize.X - nameLineSize.X) * 0.5f, 0.0f),
            ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, 1.0f)),
            nameLine);
        drawList.AddText(
            textPosition + new Vector2((textSize.X - timeLineSize.X) * 0.5f, lineHeight),
            ImGui.GetColorU32(new Vector4(1.0f, 0.88f, 0.46f, 1.0f)),
            timeLine);
        drawList.AddText(
            textPosition + new Vector2((textSize.X - progressLineSize.X) * 0.5f, lineHeight * 2.0f),
            ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, 1.0f)),
            progressLine);
    }

    private static string CompactMarkerLabel(string label, int maximumCharacters)
    {
        if (string.IsNullOrWhiteSpace(label))
            return "Unknown event";

        var trimmed = label.Trim();
        return trimmed.Length <= maximumCharacters
            ? trimmed
            : $"{trimmed[..Math.Max(1, maximumCharacters - 1)]}…";
    }

    private bool TryDrawGameIcon(ImDrawListPtr drawList, Vector2 point, uint iconId)
    {
        try
        {
            var texture = textureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
            if (texture.Width <= 1 || texture.Height <= 1)
                return false;

            const float halfSize = 13.0f;
            var minimum = point - new Vector2(halfSize);
            var maximum = point + new Vector2(halfSize);
            drawList.AddImage(texture.Handle, minimum, maximum);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void DrawPotPrediction(
        ImDrawListPtr drawList,
        Vector2 point,
        AtlasPotPrediction prediction)
    {
        var color = MarkerColor(AtlasMarkerKind.PotPrediction);
        var packedColor = ImGui.GetColorU32(color);
        var shadow = ImGui.GetColorU32(new Vector4(0.02f, 0.02f, 0.01f, 0.94f));
        DrawPotPredictionCore(drawList, point);

        var remaining = prediction.NextOccurrenceUtc - DateTimeOffset.UtcNow;
        var totalSeconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        var countdown = totalSeconds >= 3600
            ? $"{totalSeconds / 3600:00}:{(totalSeconds / 60) % 60:00}:{totalSeconds % 60:00}"
            : $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        var predictedLocal = prediction.NextOccurrenceUtc.ToLocalTime();
        var firstLine = $"予想時間 {predictedLocal:HH:mm:ss}";
        var secondLine = $"あと {countdown}";
        var firstLineSize = ImGui.CalcTextSize(firstLine);
        var secondLineSize = ImGui.CalcTextSize(secondLine);
        var lineHeight = Math.Max(firstLineSize.Y, secondLineSize.Y);
        var textSize = new Vector2(
            Math.Max(firstLineSize.X, secondLineSize.X),
            lineHeight * 2.0f);
        var textMinimum = point + new Vector2(18.0f, -textSize.Y * 0.5f);
        drawList.AddRectFilled(
            textMinimum - new Vector2(4.0f, 2.0f),
            textMinimum + textSize + new Vector2(4.0f, 2.0f),
            shadow,
            3.0f);
        drawList.AddText(textMinimum, packedColor, firstLine);
        drawList.AddText(
            textMinimum + new Vector2(0.0f, lineHeight),
            ImGui.GetColorU32(new Vector4(1.0f, 0.94f, 0.72f, 1.0f)),
            secondLine);
    }

    private static void DrawPotPredictionCore(ImDrawListPtr drawList, Vector2 point)
    {
        var packedColor = ImGui.GetColorU32(MarkerColor(AtlasMarkerKind.PotPrediction));
        var shadow = ImGui.GetColorU32(new Vector4(0.02f, 0.02f, 0.01f, 0.94f));
        drawList.AddCircleFilled(point, 12.0f, shadow);
        DrawDiamond(drawList, point, 8.0f, packedColor);
        drawList.AddCircle(point, 15.0f, packedColor, 0, 2.5f);
    }

    private static void DrawPlayer(ImDrawListPtr drawList, Vector2 point, float? rotation)
    {
        var angle = rotation ?? 0.0f;
        var forward = new Vector2(MathF.Sin(angle), MathF.Cos(angle));
        var right = new Vector2(forward.Y, -forward.X);
        var shadow = ImGui.GetColorU32(new Vector4(0.01f, 0.02f, 0.03f, 0.92f));
        var border = ImGui.GetColorU32(new Vector4(0.98f, 0.98f, 1.00f, 1.0f));
        var fill = ImGui.GetColorU32(new Vector4(1.00f, 0.76f, 0.18f, 1.0f));

        var outerTip = point + (forward * 14.0f);
        var outerBack = point - (forward * 8.0f);
        var outerLeft = outerBack - (right * 8.5f);
        var outerRight = outerBack + (right * 8.5f);
        drawList.AddCircleFilled(point - (forward * 2.0f), 9.5f, shadow);
        drawList.AddTriangleFilled(outerTip, outerRight, outerLeft, shadow);

        var borderTip = point + (forward * 12.0f);
        var borderBack = point - (forward * 6.5f);
        var borderLeft = borderBack - (right * 7.0f);
        var borderRight = borderBack + (right * 7.0f);
        drawList.AddCircleFilled(point - (forward * 1.5f), 7.5f, border);
        drawList.AddTriangleFilled(borderTip, borderRight, borderLeft, border);

        var fillTip = point + (forward * 9.5f);
        var fillBack = point - (forward * 4.5f);
        var fillLeft = fillBack - (right * 4.8f);
        var fillRight = fillBack + (right * 4.8f);
        drawList.AddCircleFilled(point - forward, 5.2f, fill);
        drawList.AddTriangleFilled(fillTip, fillRight, fillLeft, fill);
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
            AtlasMarkerKind.PotPrediction => "\u25c8",
            AtlasMarkerKind.PotTarget => "\u25c6",
            AtlasMarkerKind.Aetheryte => "\u25c6",
            _ => "\u2022",
        };

    private enum LegendStyle
    {
        Marker,
        Player,
        CheckedTreasure,
        BronzeTreasure,
        SilverTreasure,
        LiveGameIcon,
        PotPrediction,
        ForkedTower,
    }

    private enum AtlasPage
    {
        Map,
        IconGuide,
        VisitHistory,
    }

    private readonly record struct LegendEntry(
        AtlasMarkerKind Kind,
        string Label,
        Vector4 Color,
        LegendStyle Style = LegendStyle.Marker);

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
