namespace CrescentAtlas.Notifications;

public static class FateSoundNotificationPolicy
{
    public const string AudioFileName = "CrescentAtlas.FateSpawn.wav";

    public static bool ShouldPlay(
        bool isOccultCrescentActive,
        bool isEnabled,
        bool isInitialSnapshot,
        bool isMagicPotFate)
        => isOccultCrescentActive
           && isEnabled
           && !isInitialSnapshot
           && !isMagicPotFate;
}

public readonly record struct FateSoundMigrationResult(
    int Version,
    bool Enabled,
    bool Changed);

public static class FateSoundConfigurationMigration
{
    public const int Version = 6;

    public static FateSoundMigrationResult Apply(int currentVersion, bool currentEnabled)
        => currentVersion >= Version
            ? new FateSoundMigrationResult(currentVersion, currentEnabled, false)
            : new FateSoundMigrationResult(Version, false, true);
}
