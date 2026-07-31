namespace CrescentAtlas.Runtime;

public static class MagicalElixirStatusMatcher
{
    public static bool IsMatch(string? statusName)
        => !string.IsNullOrWhiteSpace(statusName)
           && (statusName.Contains("マジカルエリクサー", StringComparison.Ordinal)
               || statusName.Contains("magical elixir", StringComparison.OrdinalIgnoreCase));
}
