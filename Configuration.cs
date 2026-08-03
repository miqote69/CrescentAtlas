using Dalamud.Configuration;
using CrescentAtlas.Data;
using CrescentAtlas.Notifications;

namespace CrescentAtlas;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = FateSoundConfigurationMigration.Version;

    public UiLanguage Language { get; set; } = UiLanguage.Japanese;

    public bool CollectionEnabled { get; set; } = true;

    public bool MapVisible { get; set; } = true;

    public bool MapClickThrough { get; set; }

    public bool MapPinned { get; set; }

    public bool MapControlsExpanded { get; set; } = true;

    public string TreasureCheckVisitId { get; set; } = string.Empty;

    public HashSet<string> CheckedTreasureKeys { get; set; } = [];

    public bool ShowBronzeTreasure { get; set; } = true;

    public bool ShowSilverTreasure { get; set; } = true;

    public bool ShowPotTarget { get; set; } = true;

    public bool ShowCarrots { get; set; } = true;

    public bool ShowFates { get; set; } = true;

    public bool ShowCriticalEncounters { get; set; } = true;

    public bool DetailedEventDisplay { get; set; }

    public bool ShowForkedTower { get; set; } = true;

    public bool ShowPotPrediction { get; set; } = true;

    public bool ShowTreasureGuideLines { get; set; } = true;

    public bool FateNotificationsEnabled { get; set; } = true;

    public bool FateSoundEnabled { get; set; }

    public bool CriticalEncounterNotificationsEnabled { get; set; } = true;

    public bool CarrotNotificationsEnabled { get; set; } = true;

    public bool TreasureNotificationsEnabled { get; set; } = true;

    public bool PotNotificationsEnabled { get; set; } = true;

    public bool PotThreeMinuteNotificationEnabled { get; set; } = true;

    public bool PotOneMinuteNotificationEnabled { get; set; } = true;

    public bool PotSoundEnabled { get; set; } = true;

    public uint PotSoundEffect { get; set; } = 1;

    public PotThreeMinuteSoundMode PotSoundMode { get; set; } =
        PotThreeMinuteSoundMode.GameSoundEffect;

    // Retained for migration from configuration version 3.
    public PotThreeMinuteSoundMode PotThreeMinuteSoundMode { get; set; } =
        PotThreeMinuteSoundMode.GameSoundEffect;

    // Retained for migration from configuration version 3.
    public PotThreeMinuteSoundMode PotAppearanceSoundMode { get; set; } =
        PotThreeMinuteSoundMode.GameSoundEffect;

    public bool AfkVoiceNotificationsEnabled { get; set; }

    public AfkVoiceLanguage AfkVoiceLanguage { get; set; } = AfkVoiceLanguage.Japanese;

    public HashSet<uint> ConfirmedCarrotDataIds { get; set; } =
        [ConfirmedCarrotObjects.FortuneCarrotDataId];

    public HashSet<uint> ConfirmedCarrotEventIds { get; set; } = [];

    public HashSet<uint> ConfirmedPotFateIds { get; set; } = [];
}

public enum UiLanguage
{
    Japanese,
    English,
}

public enum PotThreeMinuteSoundMode
{
    GameSoundEffect,
    JapaneseVocalSynth,
    EnglishNaturalFemale,
}
