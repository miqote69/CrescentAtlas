using CrescentAtlas.Data;

namespace CrescentAtlas.Runtime;

public static class MagicalElixirMapMarkerClassifier
{
    private static readonly string[] TreasureNames =
    [
        "黄金の財宝箱",
        "白銀の財宝箱",
        "青銅の財宝箱",
        "gold treasure",
        "silver treasure",
        "bronze treasure",
        "gold coffer",
        "silver coffer",
        "bronze coffer",
    ];

    public static uint ResolveTargetDataId(
        uint objectiveId,
        uint levelObjectId,
        string tooltip)
    {
        if (ConfirmedPotTargetObservations.EventObjectDataIds.Contains(objectiveId))
            return objectiveId;
        if (ConfirmedPotTargetObservations.EventObjectDataIds.Contains(levelObjectId))
            return levelObjectId;

        if (string.IsNullOrWhiteSpace(tooltip)
            || !TreasureNames.Any(name =>
                tooltip.Contains(name, StringComparison.OrdinalIgnoreCase)))
        {
            return 0;
        }

        if (tooltip.Contains("黄金", StringComparison.Ordinal)
            || tooltip.Contains("gold", StringComparison.OrdinalIgnoreCase))
        {
            return 2014741;
        }

        if (tooltip.Contains("白銀", StringComparison.Ordinal)
            || tooltip.Contains("silver", StringComparison.OrdinalIgnoreCase))
        {
            return 2014742;
        }

        if (tooltip.Contains("青銅", StringComparison.Ordinal)
            || tooltip.Contains("bronze", StringComparison.OrdinalIgnoreCase))
        {
            return 2014743;
        }

        return 0;
    }

    public static string ResolveLabel(uint dataId, string tooltip)
    {
        if (!string.IsNullOrWhiteSpace(tooltip))
            return tooltip.Trim();

        return dataId switch
        {
            2014741 => "Gold treasure target",
            2014742 => "Silver treasure target",
            2014743 => "Bronze treasure target",
            _ => "Magical Elixir target",
        };
    }

    public static string ResolveTreasureType(uint dataId)
        => dataId switch
        {
            2014741 => "gold",
            2014742 => "silver",
            2014743 => "bronze",
            _ => string.Empty,
        };
}
