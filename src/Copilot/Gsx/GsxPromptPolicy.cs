namespace Msfs2024Ai.Copilot.Gsx;

internal static class GsxPromptPolicy
{
    public static bool RequiresGoodEngineStartMenu(IReadOnlyList<string> statusLines)
    {
        var status = string.Join(" ", statusLines).ToLowerInvariant();
        return status.Contains("good engine start")
               && (status.Contains("waiting") || status.Contains("confirm"));
    }
}
