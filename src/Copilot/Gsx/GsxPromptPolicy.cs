namespace Msfs2024Ai.Copilot.Gsx;

internal static class GsxPromptPolicy
{
    private static readonly TimeSpan HiddenRemoteChoiceLifetime =
        TimeSpan.FromSeconds(25);

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

    public static bool IsPushbackDirectionMenu(GsxMenuSnapshot menu)
    {
        if (menu.IsEmpty)
        {
            return false;
        }

        var title = Normalize(menu.Title);
        if (!title.Contains("pushback direction"))
        {
            return false;
        }

        return menu.Choices
            .Select(Normalize)
            .Any(choice =>
                choice.Contains("nose left")
                || choice.Contains("nose right")
                || choice.Contains("tail left")
                || choice.Contains("tail right")
                || choice.Contains("straight pushback"));
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

    public static bool CanAnswerGoodEngineStart(
        bool promptPending,
        IReadOnlyList<string> currentNotifications,
        GsxMenuSnapshot menu) =>
        promptPending
        && RequiresGoodEngineStartMenu(currentNotifications)
        && FindGoodEngineStartConfirmation(menu).HasValue;

    public static bool CanSubmitRemoteChoice(
        bool menuOpen,
        GsxMenuSnapshot menu,
        int choiceIndex) =>
        menuOpen
        && !menu.IsEmpty
        && !IsRootServicesMenu(menu)
        && choiceIndex >= 0
        && choiceIndex < menu.Choices.Count;

    public static bool CanSubmitRecentHiddenChoice(
        bool menuOpen,
        bool menuHidden,
        DateTime menuReceivedUtc,
        DateTime utcNow) =>
        menuOpen
        && menuHidden
        && menuReceivedUtc != DateTime.MinValue
        && utcNow >= menuReceivedUtc
        && utcNow - menuReceivedUtc <= HiddenRemoteChoiceLifetime;

    public static int? FindMatchingChoice(
        GsxMenuSnapshot menu,
        string expectedTitle,
        string expectedChoice)
    {
        if (menu.IsEmpty
            || !string.Equals(
                Normalize(menu.Title),
                Normalize(expectedTitle),
                StringComparison.Ordinal))
        {
            return null;
        }

        var normalizedChoice = Normalize(expectedChoice);
        for (var index = 0; index < menu.Choices.Count; index++)
        {
            if (string.Equals(
                    Normalize(menu.Choices[index]),
                    normalizedChoice,
                    StringComparison.Ordinal))
            {
                return index;
            }
        }

        return null;
    }

    private static string Normalize(string value)
    {
        var words = new string(value
                .ToLowerInvariant()
                .Select(character =>
                    char.IsLetterOrDigit(character)
                    || char.IsWhiteSpace(character)
                        ? character
                        : ' ')
                .ToArray())
            .Split(
                new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words).Replace("push back", "pushback");
    }
}
