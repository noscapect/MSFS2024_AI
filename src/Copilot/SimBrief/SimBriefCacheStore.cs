using System.Xml.Serialization;

namespace Msfs2024Ai.Copilot.SimBrief;

internal static class SimBriefCacheStore
{
    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MSFS2024_AI",
        "simbrief-last.xml");

    public static ImportedFlightPlan? Load()
    {
        try
        {
            if (!File.Exists(CachePath)) return null;
            using var stream = File.OpenRead(CachePath);
            return new XmlSerializer(typeof(ImportedFlightPlan)).Deserialize(stream)
                   as ImportedFlightPlan;
        }
        catch { return null; }
    }

    public static void Save(ImportedFlightPlan plan)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
        using var stream = File.Create(CachePath);
        new XmlSerializer(typeof(ImportedFlightPlan)).Serialize(stream, plan);
    }
}
