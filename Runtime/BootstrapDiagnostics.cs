using System.IO;
using System.Reflection;
using System.Text;
using Dalamud.Plugin;

namespace CrescentAtlas.Runtime;

/// <summary>
/// Minimal local diagnostics that starts before the collector and UI are initialized.
/// Every write is fail-safe so diagnostics can never prevent the plugin from loading.
/// </summary>
internal static class BootstrapDiagnostics
{
    private const long MaximumPrimaryLogBytes = 4 * 1024 * 1024;
    private static readonly object SyncRoot = new();
    private static string? logPath;

    public static string LogPath => logPath ?? "(diagnostics not initialized)";

    public static void Initialize(IDalamudPluginInterface pluginInterface)
    {
        try
        {
            var directory = Path.Combine(pluginInterface.ConfigDirectory.FullName, "diagnostics");
            Directory.CreateDirectory(directory);
            var primaryPath = Path.Combine(directory, "bootstrap.log");
            logPath = File.Exists(primaryPath)
                      && new FileInfo(primaryPath).Length >= MaximumPrimaryLogBytes
                ? Path.Combine(directory, $"bootstrap-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}.log")
                : primaryPath;

            var assembly = Assembly.GetExecutingAssembly().GetName();
            Write($"bootstrap initialized; assembly={assembly.Name} {assembly.Version}; runtime={Environment.Version}");
        }
        catch
        {
            logPath = null;
        }
    }

    public static void Write(string message)
    {
        var path = logPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var line = $"{DateTimeOffset.Now:O} [T{Environment.CurrentManagedThreadId}] {message}{Environment.NewLine}";
            lock (SyncRoot)
                File.AppendAllText(path, line, new UTF8Encoding(false));
        }
        catch
        {
            // Diagnostics must never become a second load failure.
        }
    }

    public static void WriteException(string stage, Exception exception)
        => Write($"{stage} FAILED{Environment.NewLine}{exception}");
}
