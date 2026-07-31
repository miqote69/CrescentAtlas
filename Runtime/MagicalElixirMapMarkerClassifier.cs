using CrescentAtlas.Data;

namespace CrescentAtlas.Runtime;

public static class MagicalElixirMapMarkerClassifier
{
    public static uint ResolveTargetDataId(uint objectiveId, uint levelObjectId)
    {
        if (ConfirmedPotTargetObservations.EventObjectDataIds.Contains(objectiveId))
            return objectiveId;
        if (ConfirmedPotTargetObservations.EventObjectDataIds.Contains(levelObjectId))
            return levelObjectId;
        return 0;
    }

    public static string ResolveLabel(uint dataId)
        => dataId switch
        {
            2014741 => "Gold Magical Elixir target",
            2014742 => "Silver Magical Elixir target",
            2014743 => "Bronze Magical Elixir target",
            _ => "Magical Elixir target",
        };

    public static string ResolveTreasureType(uint dataId)
        => dataId switch
        {
            2014741 => "gold",
            2014742 => "silver",
            2014743 => "bronze",
            _ => string.Empty,
        };
}
