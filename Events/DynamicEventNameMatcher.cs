using System.Text;

namespace CrescentAtlas.Events;

public static class DynamicEventNameMatcher
{
    public static bool IsMatch(string displayedName, string dynamicEventName)
    {
        var displayed = Normalize(displayedName);
        var dynamicEvent = Normalize(dynamicEventName);
        if (displayed.Length == 0 || dynamicEvent.Length == 0)
            return false;
        if (StringComparer.Ordinal.Equals(displayed, dynamicEvent))
            return true;

        // The standard CE panel can expose only the enemy name while the
        // DynamicEvent container exposes "title「enemy」". Match the quoted
        // enemy portion before using a conservative containment fallback.
        var quoted = Normalize(ExtractQuotedName(dynamicEventName));
        if (quoted.Length >= 4
            && (StringComparer.Ordinal.Equals(displayed, quoted)
                || displayed.Contains(quoted, StringComparison.Ordinal)
                || quoted.Contains(displayed, StringComparison.Ordinal)))
        {
            return true;
        }

        var shorterLength = Math.Min(displayed.Length, dynamicEvent.Length);
        return shorterLength >= 6
               && (displayed.Contains(dynamicEvent, StringComparison.Ordinal)
                   || dynamicEvent.Contains(displayed, StringComparison.Ordinal));
    }

    private static string ExtractQuotedName(string value)
    {
        var start = value.LastIndexOf('「');
        var end = value.LastIndexOf('」');
        return start >= 0 && end > start
            ? value[(start + 1)..end]
            : string.Empty;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToUpperInvariant(character));
        }

        return builder.ToString();
    }
}
