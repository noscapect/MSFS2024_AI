using System.Text.RegularExpressions;

namespace Msfs2024Ai.Copilot.Gsx;

internal static class GsxPushbackDirectionCoordinator
{
    private static readonly Regex FacingPattern = new(
        @"\b(?:face|facing)\s+(north[\s-]?east|south[\s-]?east|south[\s-]?west|north[\s-]?west|north|east|south|west)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool TryParseTargetHeading(string? message, out double headingDegrees)
    {
        headingDegrees = 0;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var match = FacingPattern.Match(message!);
        if (!match.Success)
        {
            return false;
        }

        var direction = Regex.Replace(
                match.Groups[1].Value,
                @"[\s-]",
                string.Empty)
            .ToLowerInvariant();
        headingDegrees = direction switch
        {
            "north" => 0,
            "northeast" => 45,
            "east" => 90,
            "southeast" => 135,
            "south" => 180,
            "southwest" => 225,
            "west" => 270,
            "northwest" => 315,
            _ => double.NaN
        };
        return !double.IsNaN(headingDegrees);
    }

    public static int? FindChoice(
        GsxMenuSnapshot menu,
        double currentHeadingDegrees,
        double targetHeadingDegrees)
    {
        if (!GsxPromptPolicy.IsPushbackDirectionMenu(menu)
            || double.IsNaN(currentHeadingDegrees)
            || double.IsInfinity(currentHeadingDegrees))
        {
            return null;
        }

        var turn = NormalizeSigned(targetHeadingDegrees - currentHeadingDegrees);
        var magnitude = Math.Abs(turn);
        if (magnitude < 10)
        {
            for (var index = 0; index < menu.Choices.Count; index++)
            {
                var choice = menu.Choices[index];
                if (choice.IndexOf(
                        "straight pushback",
                        StringComparison.OrdinalIgnoreCase) >= 0
                    && choice.IndexOf(
                        "straight pull",
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return index;
                }
            }
            return null;
        }

        if (magnitude > 170)
        {
            return null;
        }

        var requiredNoseDirection = turn > 0 ? "nose right" : "nose left";
        for (var index = 0; index < menu.Choices.Count; index++)
        {
            if (menu.Choices[index].IndexOf(
                    requiredNoseDirection,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return index;
            }
        }

        return null;
    }

    private static double NormalizeSigned(double degrees)
    {
        var normalized = (degrees + 180) % 360;
        if (normalized < 0)
        {
            normalized += 360;
        }
        return normalized - 180;
    }
}
