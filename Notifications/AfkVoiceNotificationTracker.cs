using System.Text;

namespace CrescentAtlas.Notifications;

public enum AfkVoiceStage
{
    FiveMinutes,
    SevenMinutes,
    NineMinutes,
}

public enum AfkVoiceLanguage
{
    Japanese,
    English,
}

public sealed class AfkVoiceNotificationTracker
{
    private readonly HashSet<AfkVoiceStage> acceptedStages = [];

    public bool TryAccept(
        string? message,
        bool isOccultCrescentActive,
        bool notificationsEnabled,
        out AfkVoiceStage stage)
    {
        stage = default;
        if (!isOccultCrescentActive
            || !notificationsEnabled
            || !TryClassify(message, out stage))
        {
            return false;
        }

        // A new five-minute message after a later warning starts a fresh AFK cycle.
        // No later warning is synthesized: only actual game log messages advance it.
        if (stage == AfkVoiceStage.FiveMinutes
            && (acceptedStages.Contains(AfkVoiceStage.SevenMinutes)
                || acceptedStages.Contains(AfkVoiceStage.NineMinutes)))
        {
            acceptedStages.Clear();
        }

        return acceptedStages.Add(stage);
    }

    public void Reset()
        => acceptedStages.Clear();

    public static bool TryClassify(string? message, out AfkVoiceStage stage)
    {
        stage = default;
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var normalized = Normalize(message);
        if (normalized.Contains(
                "操作がない状態になってから5分が経過しました",
                StringComparison.Ordinal)
            || normalized.Contains(
                "youhavebeeninactiveforfiveminutes",
                StringComparison.Ordinal))
        {
            stage = AfkVoiceStage.FiveMinutes;
            return true;
        }

        if (normalized.Contains(
                "操作がない状態になってから7分が経過しました",
                StringComparison.Ordinal)
            || normalized.Contains(
                "youhavebeeninactiveforsevenminutes",
                StringComparison.Ordinal))
        {
            stage = AfkVoiceStage.SevenMinutes;
            return true;
        }

        if (normalized.Contains(
                "操作がない状態になってから9分が経過しました",
                StringComparison.Ordinal)
            || normalized.Contains(
                "youhavebeeninactivefornineminutes",
                StringComparison.Ordinal))
        {
            stage = AfkVoiceStage.NineMinutes;
            return true;
        }

        return false;
    }

    public static string GetFileName(AfkVoiceLanguage language, AfkVoiceStage stage)
    {
        var languageSuffix = language == AfkVoiceLanguage.Japanese ? "ja" : "en";
        var stageName = stage switch
        {
            AfkVoiceStage.FiveMinutes => "FiveMinute",
            AfkVoiceStage.SevenMinutes => "SevenMinute",
            AfkVoiceStage.NineMinutes => "NineMinute",
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null),
        };
        return $"CrescentAtlas.Afk{stageName}.{languageSuffix}.wav";
    }

    private static string Normalize(string message)
    {
        var builder = new StringBuilder(message.Length);
        foreach (var character in message)
        {
            if (character is >= '０' and <= '９')
            {
                builder.Append((char)('0' + character - '０'));
                continue;
            }

            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
