namespace Msfs2024Ai.Copilot.Gsx;

internal sealed class GsxFileReader
{
    private readonly GsxInstallation _installation;

    public GsxFileReader(GsxInstallation installation)
    {
        _installation = installation;
    }

    public GsxMenuSnapshot ReadMenu() =>
        GsxMenuSnapshot.Parse(ReadLines(_installation.MenuPath));

    public IReadOnlyList<string> ReadTooltip() =>
        ReadLines(_installation.TooltipPath)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();

    private static IReadOnlyList<string> ReadLines(string path)
    {
        try
        {
            return File.Exists(path)
                ? File.ReadAllLines(path)
                : Array.Empty<string>();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }
}
