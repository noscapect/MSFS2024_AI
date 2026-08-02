using System.Text.RegularExpressions;

namespace Msfs2024Ai.Copilot.Gsx;

internal sealed class GsxArrivalGateSelection
{
    public GsxArrivalGateSelection(int choiceIndex, bool completesSelection)
    {
        ChoiceIndex = choiceIndex;
        CompletesSelection = completesSelection;
    }

    public int ChoiceIndex { get; }
    public bool CompletesSelection { get; }
}

internal static class GsxArrivalGateCoordinator
{
    private static readonly Regex AssignedStandPattern = new(
        @"\b(?:gate|stand)\s+([a-z]{1,3}[\s-]*\d{1,4}[a-z]?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool IsBridgeAvailable(
        bool gsxEnabledAndConnected,
        bool gsxRemoteControlOwned,
        bool sayIntentionsFlightAvailable,
        bool aircraftOnGround,
        bool arrivalFlowActive) =>
        gsxEnabledAndConnected
        && gsxRemoteControlOwned
        && sayIntentionsFlightAvailable
        && aircraftOnGround
        && arrivalFlowActive;

    public static string? ExtractAssignedStand(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var match = AssignedStandPattern.Match(message!);
        return match.Success ? NormalizeStand(match.Groups[1].Value) : null;
    }

    public static string? NormalizeStand(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = new string(value!
            .Trim()
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
        foreach (var prefix in new[] { "PARKING", "STAND", "GATE" })
        {
            if (normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                normalized = normalized.Substring(prefix.Length);
                break;
            }
        }

        return normalized.Length > 0
            && normalized.Any(char.IsDigit)
            ? normalized
            : null;
    }

    public static GsxArrivalGateSelection? FindSelection(
        GsxMenuSnapshot menu,
        string? assignedStand)
    {
        var stand = NormalizeStand(assignedStand);
        if (menu.IsEmpty || stand == null)
        {
            return null;
        }

        for (var index = 0; index < menu.Choices.Count; index++)
        {
            if (ContainsExactStand(menu.Choices[index], stand))
            {
                return new GsxArrivalGateSelection(index, true);
            }
        }

        if (menu.Title.StartsWith(
                "All Gate ",
                StringComparison.OrdinalIgnoreCase))
        {
            for (var index = 0; index < menu.Choices.Count; index++)
            {
                if (menu.Choices[index].IndexOf(
                        "Next Page",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return new GsxArrivalGateSelection(index, false);
                }
            }
        }

        var terminal = new string(stand.TakeWhile(char.IsLetter).ToArray());
        if (terminal.Length == 0 || !LooksLikePositionSelection(menu))
        {
            return null;
        }

        for (var index = 0; index < menu.Choices.Count; index++)
        {
            var choice = menu.Choices[index];
            if (Regex.IsMatch(
                    choice,
                    $@"\bgate\s+{Regex.Escape(terminal)}\b",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return new GsxArrivalGateSelection(index, false);
            }
        }

        return null;
    }

    private static bool ContainsExactStand(string choice, string stand)
    {
        var pattern = string.Join(
            @"[\s-]*",
            stand.Select(character => Regex.Escape(character.ToString())));
        return Regex.IsMatch(
            choice,
            $@"(?<![a-z0-9])(?:gate\s+|stand\s+)?{pattern}(?![a-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool LooksLikePositionSelection(GsxMenuSnapshot menu) =>
        menu.Title.IndexOf("select position", StringComparison.OrdinalIgnoreCase) >= 0
        || menu.Choices.Any(choice =>
            choice.IndexOf("suitable parking", StringComparison.OrdinalIgnoreCase) >= 0);
}
