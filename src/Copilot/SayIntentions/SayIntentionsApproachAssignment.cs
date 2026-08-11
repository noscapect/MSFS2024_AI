using System.Text.RegularExpressions;

namespace Msfs2024Ai.Copilot.SayIntentions;

internal readonly struct SayIntentionsApproachAssignment
{
    private static readonly Regex ApproachPattern = new(
        @"\b(?:(?<type>ILS|RNAV|RNP|VOR|NDB|visual)\s*(?:approach\s+)?(?:runway|rwy)?|approach\s+(?:runway|rwy)?|(?:runway|rwy)\s*)(?<runway>\d{1,2}[LRC]?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public SayIntentionsApproachAssignment(string runway, bool isIls)
    {
        Runway = runway;
        IsIls = isIls;
    }

    public string Runway { get; }
    public bool IsIls { get; }

    public static bool TryParse(
        string? message,
        out SayIntentionsApproachAssignment assignment)
    {
        assignment = default;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }
        var text = message!;
        if (text.IndexOf("approach", StringComparison.OrdinalIgnoreCase) < 0
            && text.IndexOf("ILS", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        var match = ApproachPattern.Match(text);
        if (!match.Success)
        {
            return false;
        }

        var runwayText = match.Groups["runway"].Value.ToUpperInvariant();
        var lastCharacter = runwayText[runwayText.Length - 1];
        var suffix = char.IsLetter(lastCharacter)
            ? lastCharacter.ToString()
            : "";
        var numberText = suffix.Length == 0
            ? runwayText
            : runwayText.Substring(0, runwayText.Length - 1);
        if (!int.TryParse(numberText, out var number)
            || number is < 1 or > 36)
        {
            return false;
        }

        assignment = new SayIntentionsApproachAssignment(
            $"{number:00}{suffix}",
            string.Equals(
                match.Groups["type"].Value,
                "ILS",
                StringComparison.OrdinalIgnoreCase)
            || text.IndexOf("ILS", StringComparison.OrdinalIgnoreCase) >= 0);
        return true;
    }
}
