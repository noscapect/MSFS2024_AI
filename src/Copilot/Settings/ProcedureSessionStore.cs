using System.Xml.Serialization;

namespace Msfs2024Ai.Copilot.Settings;

internal static class ProcedureSessionStore
{
    private static readonly string SessionDirectory =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MSFS2024_AI");

    public static string SessionPath =>
        Path.Combine(SessionDirectory, "session.xml");

    public static ProcedureSession Load()
    {
        try
        {
            if (!File.Exists(SessionPath))
            {
                return new ProcedureSession();
            }

            using var stream = File.OpenRead(SessionPath);
            var serializer = new XmlSerializer(typeof(ProcedureSession));
            return serializer.Deserialize(stream) as ProcedureSession
                   ?? new ProcedureSession();
        }
        catch
        {
            return new ProcedureSession();
        }
    }

    public static void Save(ProcedureSession session)
    {
        Directory.CreateDirectory(SessionDirectory);
        using var stream = File.Create(SessionPath);
        var serializer = new XmlSerializer(typeof(ProcedureSession));
        serializer.Serialize(stream, session);
    }
}
