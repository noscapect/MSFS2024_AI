namespace Msfs2024Ai.Copilot.Diagnostics;

internal static class AppLog
{
    private static readonly object Sync = new();
    private static readonly string DirectoryPath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MSFS2024_AI",
            "logs");

    public static string FilePath => Path.Combine(DirectoryPath, "copilot.log");

    public static void Write(string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(DirectoryPath);
                File.AppendAllText(
                    FilePath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never stop simulator interaction.
        }
    }
}
