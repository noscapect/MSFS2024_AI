using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Msfs2024Ai.Copilot.Gsx;

internal sealed class GsxLiveState
{
    public string SummaryText { get; set; } = "Ready";
    public string? PassengerProgressText { get; set; }
    public int? PassengerPercent { get; set; }
    public int? PassengerCurrent { get; set; }
    public int? PassengerTotal { get; set; }
    public string PassengerOperationText { get; set; } = "Passengers";
    public bool BoardingInProgress { get; set; }
    public bool BoardingComplete { get; set; }
    public bool DeboardingInProgress { get; set; }
    public string? ActionRequiredText { get; set; }
    public bool HasActionRequired => !string.IsNullOrWhiteSpace(ActionRequiredText);
    public IReadOnlyList<string> ActiveServices { get; set; } = Array.Empty<string>();
}

internal static class GsxLiveStatusFormatter
{
    private static readonly Regex PassengerRegex = new(
        @"(\d+)\s*/\s*(\d+)\s+passengers",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static GsxLiveState Format(
        IReadOnlyList<string> tooltipLines,
        GsxMenuSnapshot? menu,
        bool enableGsxIntegration,
        bool isGsxInstalled,
        bool couatlStarted)
    {
        var state = new GsxLiveState();

        if (!enableGsxIntegration)
        {
            state.SummaryText = "Disabled - flights continue without GSX coordination.";
            return state;
        }

        if (!isGsxInstalled)
        {
            state.SummaryText = "Not installed - optional integration inactive.";
            return state;
        }

        if (!couatlStarted)
        {
            state.SummaryText = "Installed - waiting for the Couatl engine.";
            return state;
        }

        var cleanLines = (tooltipLines ?? Array.Empty<string>())
            .Select(line => line.Replace("[GSX]", "").Trim())
            .Where(line => line.Length > 0)
            .ToList();

        state.ActiveServices = cleanLines;

        var boardingLines = cleanLines
            .Where(MentionsBoarding)
            .ToList();
        var deboardingLines = cleanLines
            .Where(MentionsDeboarding)
            .ToList();
        var isDeboarding = deboardingLines.Count > 0;
        var isBoarding = !isDeboarding && boardingLines.Count > 0;
        state.PassengerOperationText = isDeboarding
            ? "Deboarding"
            : isBoarding
                ? "Boarding"
                : "Passengers";

        // Parse passenger progress if present
        foreach (var line in cleanLines)
        {
            var match = PassengerRegex.Match(line);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var current) && int.TryParse(match.Groups[2].Value, out var total) && total > 0)
            {
                var percent = Math.Min(100, Math.Max(0, (int)Math.Round((double)current / total * 100.0)));
                state.PassengerCurrent = current;
                state.PassengerTotal = total;
                state.PassengerPercent = percent;
                var operation = isDeboarding
                    ? " deboarded"
                    : isBoarding
                        ? " boarded"
                        : string.Empty;
                state.PassengerProgressText =
                    $"{current} / {total} passengers{operation} ({percent}%)";
                break;
            }
        }

        state.BoardingComplete =
            isBoarding
            &&
            state.PassengerCurrent.HasValue
            && state.PassengerTotal.HasValue
            && state.PassengerCurrent.Value >= state.PassengerTotal.Value;
        if (!state.BoardingComplete)
        {
            state.BoardingComplete = boardingLines.Any(
                line => line.IndexOf("boarding complete", StringComparison.OrdinalIgnoreCase) >= 0
                        || line.IndexOf("boarding completed", StringComparison.OrdinalIgnoreCase) >= 0
                        || line.IndexOf("boarding finished", StringComparison.OrdinalIgnoreCase) >= 0
                        || line.IndexOf("all passengers boarded", StringComparison.OrdinalIgnoreCase) >= 0);
        }
        state.BoardingInProgress = boardingLines.Count > 0
                                   && !state.BoardingComplete;
        state.DeboardingInProgress = isDeboarding
                                     && !deboardingLines.Any(line =>
                                         line.IndexOf("deboarding complete", StringComparison.OrdinalIgnoreCase) >= 0
                                         || line.IndexOf("deboarding completed", StringComparison.OrdinalIgnoreCase) >= 0
                                         || line.IndexOf("all passengers deboarded", StringComparison.OrdinalIgnoreCase) >= 0);

        // Parse actions required
        var actions = new List<string>();

        foreach (var line in cleanLines)
        {
            if (line.StartsWith("Waiting for your action:", StringComparison.OrdinalIgnoreCase))
            {
                var prompt = line.Substring("Waiting for your action:".Length).Trim();
                if (!string.IsNullOrWhiteSpace(prompt))
                {
                    actions.Add(prompt);
                }
            }
            else if (line.Equals("Release parking brakes", StringComparison.OrdinalIgnoreCase))
            {
                actions.Add("Release parking brakes");
            }
            else if (line.Equals("Set parking brakes", StringComparison.OrdinalIgnoreCase))
            {
                actions.Add("Set parking brakes");
            }
        }

        if (menu != null
            && !menu.IsEmpty
            && !GsxPromptPolicy.IsRootServicesMenu(menu)
            && !string.IsNullOrWhiteSpace(menu.Title))
        {
            if (!actions.Any(a => a.IndexOf(menu.Title, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                actions.Add($"Select: {menu.Title}");
            }
        }

        if (actions.Count > 0)
        {
            state.ActionRequiredText = string.Join(" | ", actions.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        // Summary string
        if (state.PassengerProgressText != null && isDeboarding)
        {
            state.SummaryText = $"Deboarding in progress ({state.PassengerProgressText})";
        }
        else if (state.PassengerProgressText != null && isBoarding)
        {
            state.SummaryText = $"Boarding in progress ({state.PassengerProgressText})";
        }
        else if (cleanLines.Count > 0)
        {
            state.SummaryText = string.Join(" • ", cleanLines.Take(2));
        }
        else
        {
            state.SummaryText = "Ready - Couatl connected; monitoring active.";
        }

        return state;
    }

    private static bool MentionsBoarding(string line) =>
        !MentionsDeboarding(line)
        && (line.IndexOf("boarding", StringComparison.OrdinalIgnoreCase) >= 0
            || line.IndexOf("passengers boarded", StringComparison.OrdinalIgnoreCase) >= 0);

    private static bool MentionsDeboarding(string line) =>
        line.IndexOf("deboarding", StringComparison.OrdinalIgnoreCase) >= 0
        || line.IndexOf("passengers deboarded", StringComparison.OrdinalIgnoreCase) >= 0;
}
