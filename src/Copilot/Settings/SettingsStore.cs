using System.Xml.Serialization;

namespace Msfs2024Ai.Copilot.Settings;

internal static class SettingsStore
{
    private static readonly string SettingsDirectory =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MSFS2024_AI");

    public static string SettingsPath =>
        Path.Combine(SettingsDirectory, "settings.xml");

    public static CopilotSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new CopilotSettings();
            }

            using var stream = File.OpenRead(SettingsPath);
            var serializer = new XmlSerializer(typeof(CopilotSettings));
            return serializer.Deserialize(stream) as CopilotSettings
                   ?? new CopilotSettings();
        }
        catch
        {
            return new CopilotSettings();
        }
    }

    public static void Save(CopilotSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        using var stream = File.Create(SettingsPath);
        var serializer = new XmlSerializer(typeof(CopilotSettings));
        serializer.Serialize(stream, settings);
    }
}
