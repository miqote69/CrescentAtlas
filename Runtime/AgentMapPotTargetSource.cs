using CrescentAtlas.Contracts;
using CrescentAtlas.Data;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace CrescentAtlas.Runtime;

/// <summary>
/// Borrows the marker list already populated by the game's map agent.
/// It never invokes marker-population functions, mutates native state, stores
/// native pointers, or disposes game-owned memory.
/// </summary>
public sealed class AgentMapPotTargetSource(
    IDataManager dataManager,
    IPluginLog log)
{
    private const int MaximumMarkerCount = 512;
    private const int MaximumMarkerCapacity = 2048;
    private readonly HashSet<string> recordedKeys = new(StringComparer.Ordinal);
    private string previousDiagnosticSignature = string.Empty;

    public unsafe IReadOnlyList<AtlasMarker> Scan(
        string sessionId,
        uint territoryId,
        string territoryName,
        uint currentMapId,
        bool isMagicalElixirActive,
        DateTimeOffset observedAtUtc,
        out IReadOnlyList<ObservationRecord> observations)
    {
        var result = new List<AtlasMarker>();
        var newObservations = new List<ObservationRecord>();
        observations = newObservations;

        if (!isMagicalElixirActive)
        {
            previousDiagnosticSignature = string.Empty;
            return result;
        }

        try
        {
            var agentMap = AgentMap.Instance();
            if (agentMap == null)
                return result;
            if (agentMap->CurrentTerritoryId != 0
                && agentMap->CurrentTerritoryId != territoryId)
            {
                return result;
            }

            // Copy only the three pointer values that form the vector view.
            // The underlying allocation remains owned by the game.
            var nativeMarkers = agentMap->EventMarkers;
            if (!TryValidateBorrowedVector(
                    nativeMarkers.First,
                    nativeMarkers.Last,
                    nativeMarkers.End,
                    out var markerCount))
            {
                LogDiagnostic("invalid-vector");
                return result;
            }

            var levelSheet = dataManager.GetExcelSheet<Level>();
            var diagnosticRows = new List<string>(Math.Min(markerCount, 12));
            for (var index = 0; index < markerCount; index++)
            {
                // Copy the native value immediately. Never retain pointers into
                // AgentMap and never dereference its borrowed tooltip pointer.
                var native = nativeMarkers.First[index];
                if (diagnosticRows.Count < 12)
                {
                    diagnosticRows.Add(FormattableString.Invariant(
                        $"{native.LevelId}/{native.ObjectiveId}/{native.DataId}/{native.TerritoryTypeId}/{native.MapId}/{native.IconId}"));
                }

                if (native.TerritoryTypeId != 0
                    && native.TerritoryTypeId != territoryId)
                {
                    continue;
                }

                if (native.MapId != 0
                    && currentMapId != 0
                    && native.MapId != currentMapId)
                {
                    continue;
                }

                var levelObjectId = 0u;
                if (native.LevelId != 0
                    && levelSheet.TryGetRow(native.LevelId, out var level))
                {
                    levelObjectId = level.Object.RowId;
                }

                var dataId = MagicalElixirMapMarkerClassifier.ResolveTargetDataId(
                    native.ObjectiveId,
                    levelObjectId);
                if (dataId == 0 || !IsPlausiblePosition(native.Position))
                    continue;

                var label = MagicalElixirMapMarkerClassifier.ResolveLabel(dataId);
                var key = FormattableString.Invariant(
                    $"agent-map-pot-target:{territoryId}:{dataId}:{native.LevelId}:{native.Position.X:F2}:{native.Position.Y:F2}:{native.Position.Z:F2}");
                result.Add(new AtlasMarker(
                    key,
                    AtlasMarkerKind.PotTarget,
                    label,
                    native.Position,
                    observedAtUtc,
                    IsActive: true,
                    territoryId,
                    dataId,
                    native.DataId,
                    TreasureType: MagicalElixirMapMarkerClassifier.ResolveTreasureType(dataId),
                    IconId: native.IconId));

                if (!recordedKeys.Add(key))
                    continue;

                newObservations.Add(new ObservationRecord
                {
                    SessionId = sessionId,
                    ObservedAtUtc = observedAtUtc,
                    Source = ObservationSource.AgentMap,
                    Kind = "pot-target",
                    Key = key,
                    TerritoryId = territoryId,
                    TerritoryName = territoryName,
                    DataId = dataId,
                    EventId = native.DataId,
                    Name = label,
                    X = native.Position.X,
                    Y = native.Position.Y,
                    Z = native.Position.Z,
                    IsActive = true,
                    Properties = new Dictionary<string, string>
                    {
                        ["targetType"] = "magic-pot-destination",
                        ["markerSource"] = "agent-map-borrowed",
                        ["levelId"] = native.LevelId.ToString(),
                        ["objectiveId"] = native.ObjectiveId.ToString(),
                        ["mapId"] = native.MapId.ToString(),
                        ["iconId"] = native.IconId.ToString(),
                        ["markerType"] = native.MarkerType.ToString(),
                    },
                });
            }

            LogDiagnostic(FormattableString.Invariant(
                $"count={markerCount};targets={result.Count};rows={string.Join(",", diagnosticRows)}"));
        }
        catch (Exception ex)
        {
            log.Debug(ex, "Failed to read borrowed AgentMap event markers.");
            LogDiagnostic($"managed-exception:{ex.GetType().Name}");
        }

        return result
            .GroupBy(marker => marker.Key, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private unsafe static bool TryValidateBorrowedVector(
        MapMarkerData* first,
        MapMarkerData* last,
        MapMarkerData* end,
        out int count)
    {
        count = 0;
        if (first == null || last == null || end == null)
            return first == null && last == null && end == null;

        var firstAddress = (nuint)first;
        var lastAddress = (nuint)last;
        var endAddress = (nuint)end;
        if (lastAddress < firstAddress || endAddress < lastAddress)
            return false;

        var elementSize = (nuint)sizeof(MapMarkerData);
        var usedBytes = lastAddress - firstAddress;
        var capacityBytes = endAddress - firstAddress;
        if (usedBytes % elementSize != 0 || capacityBytes % elementSize != 0)
            return false;

        var longCount = usedBytes / elementSize;
        var longCapacity = capacityBytes / elementSize;
        if (longCount > MaximumMarkerCount
            || longCapacity > MaximumMarkerCapacity
            || longCount > longCapacity)
        {
            return false;
        }

        count = (int)longCount;
        return true;
    }

    private static bool IsPlausiblePosition(Vector3 position)
        => float.IsFinite(position.X)
           && float.IsFinite(position.Y)
           && float.IsFinite(position.Z)
           && MathF.Abs(position.X) <= 10_000
           && MathF.Abs(position.Y) <= 10_000
           && MathF.Abs(position.Z) <= 10_000;

    private void LogDiagnostic(string signature)
    {
        if (string.Equals(signature, previousDiagnosticSignature, StringComparison.Ordinal))
            return;

        previousDiagnosticSignature = signature;
        BootstrapDiagnostics.Write($"AgentMap Elixir marker scan: {signature}");
    }
}
