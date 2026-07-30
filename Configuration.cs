using Dalamud.Configuration;

namespace CrescentAtlas;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 3;

    public bool CollectionEnabled { get; set; } = true;

    public bool MapVisible { get; set; } = true;

    public bool MapClickThrough { get; set; }

    public bool ShowBronzeTreasure { get; set; } = true;

    public bool ShowSilverTreasure { get; set; } = true;

    public bool ShowGoldTreasure { get; set; } = true;

    public bool ShowFates { get; set; } = true;

    public bool ShowCriticalEncounters { get; set; } = true;

    public bool DetailedEventDisplay { get; set; }

    public bool ShowForkedTower { get; set; } = true;

    public bool ShowPotPrediction { get; set; } = true;

    public bool FateNotificationsEnabled { get; set; } = true;

    public bool CriticalEncounterNotificationsEnabled { get; set; } = true;

    public bool CarrotNotificationsEnabled { get; set; } = true;

    public bool TreasureNotificationsEnabled { get; set; } = true;

    public bool PotNotificationsEnabled { get; set; } = true;

    public HashSet<uint> ConfirmedCarrotDataIds { get; set; } = [];

    public HashSet<uint> ConfirmedCarrotEventIds { get; set; } = [];

    public HashSet<uint> ConfirmedPotFateIds { get; set; } = [];
}
