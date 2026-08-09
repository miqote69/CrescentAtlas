using CrescentAtlas.Contracts;
using CrescentAtlas.Data;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;
using Lumina.Excel.Sheets;
using System.Runtime.CompilerServices;

namespace CrescentAtlas.Runtime;

public sealed class LayoutTreasureCandidateScanner(IDataManager dataManager, IPluginLog log)
{
    public unsafe IReadOnlyList<AtlasMarker> Scan(
        string sessionId,
        uint territoryId,
        string territoryName,
        OccultCrescentMapLayer mapLayer,
        uint mapId,
        DateTimeOffset observedAtUtc,
        out IReadOnlyList<ObservationRecord> observations)
    {
        var markers = new List<AtlasMarker>();
        var records = new List<ObservationRecord>();
        observations = records;

        try
        {
            var world = LayoutWorld.Instance();
            var layout = world == null ? null : world->ActiveLayout;
            if (layout == null)
                return markers;

            if (!layout->InstancesByType.TryGetValue(
                    InstanceType.Treasure,
                    out Pointer<StdMap<ulong, Pointer<ILayoutInstance>>> instances,
                    false))
                return markers;

            var treasureSheet = dataManager.GetExcelSheet<Treasure>();
            foreach (var instancePointer in instances.Value->Values)
            {
                var instance = instancePointer.Value;
                if (instance == null)
                    continue;

                var transform = instance->GetTransformImpl();
                if (transform == null)
                    continue;

                var treasureRowId = Unsafe.Read<uint>((byte*)instance + 0x30);
                var position = transform->Translation;
                if (!TreasureLayerClassifier.IsCandidateForMap(
                        mapLayer,
                        mapId,
                        treasureRowId,
                        position))
                    continue;

                if (!treasureSheet.TryGetRow(treasureRowId, out var treasureRow))
                    continue;

                var sgbId = treasureRow.SGB.RowId;
                var type = TreasureCofferTypeClassifier.ResolveFromSgbId(sgbId);
                if (string.IsNullOrEmpty(type))
                    continue;
                var key = FormattableString.Invariant(
                    $"layout-treasure:{territoryId}:{treasureRowId}:{position.X:F3}:{position.Y:F3}:{position.Z:F3}");

                markers.Add(new AtlasMarker(
                    key,
                    AtlasMarkerKind.TreasureCandidate,
                    $"{type} coffer candidate",
                    position,
                    observedAtUtc,
                    false,
                    territoryId,
                    treasureRowId,
                    TreasureType: type));

                records.Add(new ObservationRecord
                {
                    SessionId = sessionId,
                    ObservedAtUtc = observedAtUtc,
                    Source = ObservationSource.Layout,
                    Kind = "treasure-candidate",
                    Key = key,
                    TerritoryId = territoryId,
                    TerritoryName = territoryName,
                    DataId = treasureRowId,
                    Name = $"{type} coffer candidate",
                    X = position.X,
                    Y = position.Y,
                    Z = position.Z,
                    IsActive = false,
                    Properties = new Dictionary<string, string>
                    {
                        ["cofferType"] = type,
                        ["sgbId"] = sgbId.ToString(),
                        ["mapId"] = mapId.ToString(),
                    },
                });
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to scan active layout for treasure candidates.");
        }

        return markers
            .GroupBy(marker => marker.Key, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(marker => marker.Position.X)
            .ThenBy(marker => marker.Position.Z)
            .ToArray();
    }
}
