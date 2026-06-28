using System.IO.Compression;
using System.Reflection;
using System.Text;
using Msfs2024Ai.Copilot.Settings;
using Msfs2024Ai.Copilot.Telemetry;

namespace Msfs2024Ai.Copilot.Diagnostics;

internal static class DiagnosticLog
{
    private static readonly object Sync = new();
    private static readonly string RootDirectory =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MSFS2024_AI");
    private static readonly string DirectoryPath =
        Path.Combine(RootDirectory, "diagnostics");
    private static readonly string ExportDirectory =
        Path.Combine(RootDirectory, "exports");
    private const long MaximumDiagnosticBytes = 1024 * 1024;

    public static string FilePath => Path.Combine(DirectoryPath, "diagnostics.log");

    public static void RecordFailure(
        string summary,
        AircraftState? state,
        string? procedureName = null,
        string? stepId = null,
        string? stepLabel = null,
        IEnumerable<string>? details = null)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(DirectoryPath);
                TrimIfNeeded();
                File.AppendAllText(
                    FilePath,
                    BuildEntry(summary, state, procedureName, stepId, stepLabel, details),
                    new UTF8Encoding(false));
            }
        }
        catch
        {
            // Diagnostics must never interrupt simulator interaction.
        }
    }

    public static string GetLastEntry()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return "No diagnostic entry has been recorded yet.";
            }

            var content = File.ReadAllText(FilePath);
            var marker = $"{Environment.NewLine}---{Environment.NewLine}";
            var index = content.LastIndexOf(marker, StringComparison.Ordinal);
            return index >= 0
                ? content.Substring(index + marker.Length).Trim()
                : content.Trim();
        }
        catch (Exception ex)
        {
            return $"Could not read diagnostics: {ex.Message}";
        }
    }

    public static string ExportLatest(FlightTelemetryStore telemetryStore)
    {
        Directory.CreateDirectory(ExportDirectory);
        var zipPath = Path.Combine(
            ExportDirectory,
            $"MSFS2024_AI_diagnostics_{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        AddIfExists(archive, AppLog.FilePath, "logs/copilot.log");
        AddIfExists(archive, FilePath, "diagnostics/diagnostics.log");
        AddIfExists(archive, SettingsStore.SettingsPath, "settings/settings.xml");

        foreach (var recording in telemetryStore.Recordings)
        {
            AddIfExists(
                archive,
                recording,
                $"flights/{Path.GetFileName(recording)}");
        }

        var manifestBuilder = new StringBuilder()
            .AppendLine("MSFS 2024 AI First Officer diagnostic package")
            .AppendLine($"Created local: {DateTime.Now:O}")
            .AppendLine($"Created UTC: {DateTime.UtcNow:O}")
            .AppendLine($"Diagnostics file: {FilePath}")
            .AppendLine($"App log file: {AppLog.FilePath}")
            .AppendLine($"Settings file: {SettingsStore.SettingsPath}")
            .AppendLine("Included flight recordings:");
        foreach (var recording in telemetryStore.Recordings)
        {
            manifestBuilder.AppendLine($"- {Path.GetFileName(recording)}");
        }
        var manifest = manifestBuilder.ToString();
        var bytes = Encoding.UTF8.GetBytes(manifest);
        var entry = archive.CreateEntry("MANIFEST.txt", CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);

        return zipPath;
    }

    private static string BuildEntry(
        string summary,
        AircraftState? state,
        string? procedureName,
        string? stepId,
        string? stepLabel,
        IEnumerable<string>? details)
    {
        var builder = new StringBuilder()
            .AppendLine("---")
            .AppendLine($"Time local: {DateTime.Now:O}")
            .AppendLine($"Time UTC:   {DateTime.UtcNow:O}")
            .AppendLine($"Summary:    {summary}");
        if (!string.IsNullOrWhiteSpace(procedureName))
        {
            builder.AppendLine($"Procedure:  {procedureName}");
        }
        if (!string.IsNullOrWhiteSpace(stepId) || !string.IsNullOrWhiteSpace(stepLabel))
        {
            builder.AppendLine($"Step:       {stepId ?? "unknown"} — {stepLabel ?? "unknown"}");
        }
        if (details != null)
        {
            foreach (var detail in details.Where(item => !string.IsNullOrWhiteSpace(item)))
            {
                builder.AppendLine($"Detail:     {detail}");
            }
        }

        if (state != null)
        {
            builder.AppendLine("Aircraft state:");
            foreach (var property in typeof(AircraftState)
                         .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                         .Where(property => property.CanRead)
                         .OrderBy(property => property.Name))
            {
                builder.AppendLine($"  {property.Name}: {property.GetValue(state) ?? "null"}");
            }
        }

        return builder.ToString();
    }

    private static void AddIfExists(ZipArchive archive, string path, string entryName)
    {
        if (File.Exists(path))
        {
            archive.CreateEntryFromFile(path, entryName, CompressionLevel.Optimal);
        }
    }

    private static void TrimIfNeeded()
    {
        var file = new FileInfo(FilePath);
        if (!file.Exists || file.Length <= MaximumDiagnosticBytes)
        {
            return;
        }

        var content = File.ReadAllText(FilePath);
        var keepFrom = Math.Max(0, content.Length - (int)(MaximumDiagnosticBytes / 2));
        File.WriteAllText(
            FilePath,
            content.Substring(keepFrom),
            new UTF8Encoding(false));
    }
}
