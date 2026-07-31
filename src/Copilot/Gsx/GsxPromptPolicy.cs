namespace Msfs2024Ai.Copilot.Gsx;

internal static class GsxPromptPolicy
{
    public static bool IsRootServicesMenu(GsxMenuSnapshot menu)
    {
        if (menu.IsEmpty)
        {
            return false;
        }

        var title = Normalize(menu.Title);
        if (title.StartsWith("activate services", StringComparison.Ordinal))
        {
            return true;
        }

        var rootChoiceCount = menu.Choices
            .Select(Normalize)
            .Count(choice =>
                choice.Contains("request boarding")
                || choice.Contains("request deboarding")
                || choice.Contains("request catering")
                || choice.Contains("request refueling")
                || choice.Contains("prepare for pushback")
                || choice.Contains("customize this parking position"));

        return rootChoiceCount >= 3;
    }

    public static bool RequiresGoodEngineStartMenu(IReadOnlyList<string> statusLines)
    {
        var status = string.Join(" ", statusLines).ToLowerInvariant();
        return status.Contains("good engine start")
               && (status.Contains("waiting") || status.Contains("confirm"));
    }

    public static int? FindGoodEngineStartConfirmation(GsxMenuSnapshot menu)
    {
        for (var index = 0; index < menu.Choices.Count; index++)
        {
            var value = Normalize(menu.Choices[index]);
            if (value.Contains("good engine start")
                || value.Contains("engine start is good")
                || value.Contains("confirm engine start"))
            {
                return index;
            }
        }

        return null;
    }

    private static string Normalize(string value) =>
        new string(value
                .ToLowerInvariant()
                .Where(character => char.IsLetterOrDigit(character)
                                    || char.IsWhiteSpace(character))
                .ToArray())
            .Replace("push back", "pushback");
}
