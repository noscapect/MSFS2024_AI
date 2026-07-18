namespace Msfs2024Ai.Copilot.SayIntentions;

internal static class SayIntentionsAtcResponseClassifier
{
    public static bool IsMatchingOutgoingRequest(
        SayIntentionsCommunication communication,
        string stepId)
    {
        var text = Normalize(communication.OutgoingMessage);
        if (text.Length == 0)
        {
            return false;
        }

        return stepId switch
        {
            "captain-ifr-clearance" => text.Contains("clearance"),
            "captain-pushback-clearance" => text.Contains("pushback")
                                                   || text.Contains("push and start"),
            "fo-taxi-clearance" => text.Contains("taxi")
                                    && !text.Contains("takeoff"),
            "fo-takeoff-clearance" => text.Contains("ready for departure")
                                       || text.Contains("ready for takeoff")
                                       || text.Contains("holding short"),
            _ => false
        };
    }

    public static bool IsCompletionReply(string stepId, string reply)
    {
        var text = Normalize(reply);
        if (text.Length == 0
            || text.Contains("stand by")
            || text.Contains("standby")
            || text.Contains("unable"))
        {
            return false;
        }

        return stepId switch
        {
            "captain-ifr-clearance" => text.Contains("cleared")
                                       || text.Contains("clearance delivery")
                                       || text.Contains("readback correct"),
            "captain-pushback-clearance" =>
                (text.Contains("pushback") || text.Contains("push and start"))
                && (text.Contains("approved")
                    || text.Contains("cleared")
                    || text.Contains("at your discretion")),
            "fo-taxi-clearance" => text.Contains("taxi to")
                                   || text.Contains("taxi via")
                                   || text.Contains("taxi runway")
                                   || text.Contains("cleared to taxi"),
            "fo-takeoff-clearance" => text.Contains("cleared for takeoff"),
            _ => false
        };
    }

    public static bool IsRecent(
        SayIntentionsCommunication communication,
        DateTimeOffset now,
        TimeSpan maximumAge)
    {
        if (!DateTimeOffset.TryParse(
                communication.TimestampUtc,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out var timestamp))
        {
            return false;
        }

        var age = now - timestamp.ToUniversalTime();
        return age >= TimeSpan.Zero && age <= maximumAge;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var characters = value!.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray();
        return string.Join(
            " ",
            new string(characters).Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries));
    }
}
