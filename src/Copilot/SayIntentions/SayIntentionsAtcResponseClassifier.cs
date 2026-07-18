namespace Msfs2024Ai.Copilot.SayIntentions;

internal static class SayIntentionsAtcResponseClassifier
{
    public static bool IsRecent(
        string? timestampUtc,
        DateTimeOffset nowUtc,
        TimeSpan maximumAge)
    {
        if (string.IsNullOrWhiteSpace(timestampUtc)
            || !DateTimeOffset.TryParse(
                timestampUtc,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal
                | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var timestamp))
        {
            return false;
        }

        var age = nowUtc - timestamp;
        return age >= TimeSpan.Zero && age <= maximumAge;
    }

    public static SayIntentionsCommunication? FindRecentClearance(
        string stepId,
        IEnumerable<SayIntentionsCommunication> communications,
        long minimumExclusiveId,
        DateTimeOffset nowUtc,
        TimeSpan maximumAge) =>
        communications
            .Where(item => item.Id > minimumExclusiveId)
            .Where(item => IsRecent(item.TimestampUtc, nowUtc, maximumAge))
            .Where(item => IsClearanceResponse(stepId, item.OutgoingMessage))
            .OrderByDescending(item => item.Id)
            .FirstOrDefault();

    public static bool IsClearanceResponse(string stepId, string? atcMessage)
    {
        if (string.IsNullOrWhiteSpace(atcMessage))
        {
            return false;
        }

        var message = atcMessage!.Trim().ToLowerInvariant();
        if (ContainsAny(message, "unable", "denied", "stand by", "standby"))
        {
            return false;
        }

        return stepId switch
        {
            // Waiting for the accepted readback verifies that the clearance was
            // received and acknowledged, rather than merely requested.
            "captain-ifr-clearance" =>
                ContainsAny(message, "readback correct", "read back correct"),
            "captain-pushback-clearance" =>
                ContainsAny(
                    message,
                    "pushback",
                    "push and start",
                    "push & start")
                && ContainsAny(message, "approved", "cleared", "at your discretion"),
            "fo-taxi-clearance" =>
                message.Contains("taxi")
                && ContainsAny(message, "runway", "via", "hold short", "cleared"),
            "fo-takeoff-clearance" =>
                ContainsAny(message, "cleared for takeoff", "cleared for take-off"),
            _ => false
        };
    }

    public static string VerificationMessage(string stepId) => stepId switch
    {
        "captain-ifr-clearance" =>
            "SayIntentions ATC verified: IFR clearance received and readback accepted.",
        "captain-pushback-clearance" =>
            "SayIntentions ATC verified: pushback/start clearance received.",
        "fo-taxi-clearance" =>
            "SayIntentions ATC verified: taxi clearance received.",
        "fo-takeoff-clearance" =>
            "SayIntentions ATC verified: takeoff clearance received.",
        _ => "SayIntentions ATC response verified."
    };

    private static bool ContainsAny(string source, params string[] candidates) =>
        candidates.Any(source.Contains);
}
