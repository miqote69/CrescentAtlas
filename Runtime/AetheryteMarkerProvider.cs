using CrescentAtlas.Contracts;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace CrescentAtlas.Runtime;

/// <summary>
/// Reads the fixed aetheryte symbols embedded in the game's MapMarker sheet.
/// This keeps both the positions and icon aligned with the client data instead
/// of maintaining a separate hand-authored point list.
/// </summary>
public sealed class AetheryteMarkerProvider(IDataManager dataManager)
{
    private const ushort AetheryteMapIconId = 60959;

    public bool TryRead(
        uint territoryId,
        uint mapId,
        DateTimeOffset observedAtUtc,
        out IReadOnlyList<AtlasMarker> markers)
    {
        markers = Array.Empty<AtlasMarker>();
        if (mapId == 0
            || !dataManager.GetExcelSheet<Map>().TryGetRow(mapId, out var map))
        {
            return false;
        }

        var mapMarkerSheet = dataManager.GetSubrowExcelSheet<MapMarker>();
        if (map.MapMarkerRange == 0
            || !mapMarkerSheet.TryGetRow(map.MapMarkerRange, out var mapMarkers))
        {
            return true;
        }

        var mapScale = Math.Max(1u, map.SizeFactor);
        var worldOrigin = 102400.0f / mapScale;
        markers = mapMarkers
            .Where(marker => marker.Icon == AetheryteMapIconId)
            .Select(marker => new AtlasMarker(
                $"aetheryte:{territoryId}:{mapId}:{marker.RowId}:{marker.SubrowId}",
                AtlasMarkerKind.Aetheryte,
                "Aetheryte",
                new Vector3(
                    marker.X - map.OffsetX - worldOrigin,
                    0.0f,
                    marker.Y - map.OffsetY - worldOrigin),
                observedAtUtc,
                true,
                territoryId,
                DataId: marker.RowId,
                IconId: marker.Icon))
            .ToArray();
        return true;
    }
}
