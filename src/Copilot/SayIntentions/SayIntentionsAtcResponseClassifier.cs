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
        TimeSpan maximumAge)
    {
        var eligible = communications
            .Where(item => item.Id > minimumExclusiveId)
            .Where(item => IsRecent(item.TimestampUtc, nowUtc, maximumAge))
            .OrderBy(item => item.Id)
            .ToList();

        if (stepId != "captain-ifr-clearance")
        {
            return eligible
                .Where(item => IsClearanceResponse(
                    stepId,
                    item.OutgoingMessage,
                    item.IncomingMessage))
                .OrderByDescending(item => item.Id)
                .FirstOrDefault();
        }

        var explicitAcceptance = eligible
            .Where(item => IsClearanceResponse(
                stepId,
                item.OutgoingMessage,
                item.IncomingMessage))
            .OrderByDescending(item => item.Id)
            .FirstOrDefault();
        if (explicitAcceptance != null)
        {
            return explicitAcceptance;
        }

        var issuedClearance = eligible
            .LastOrDefault(item => IsStructuredIfrClearance(item.OutgoingMessage));
        return issuedClearance == null
            ? null
            : eligible
                .Where(item => item.Id > issuedClearance.Id)
                .LastOrDefault(item => IsStructuredIfrClearance(item.IncomingMessage));
    }

    public static bool IsClearanceResponse(
        string stepId,
        string? atcMessage,
        string? aircraftMessage = null)
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
                ContainsAny(message, "readback correct", "read back correct")
                || IsStructuredIfrClearance(message)
                && IsStructuredIfrClearance(aircraftMessage),
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

    private static bool IsStructuredIfrClearance(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var message = source!.Trim().ToLowerInvariant();
        return message.Contains("cleared to")
               && ContainsAny(message, "departure", "sid")
               && ContainsAny(message, "runway", "squawk", "initial climb");
    }
}
