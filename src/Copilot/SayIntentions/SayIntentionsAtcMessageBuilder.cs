namespace Msfs2024Ai.Copilot.SayIntentions;

internal static class SayIntentionsAtcMessageBuilder
{
    public static string Build(
        string stepId,
        string callsign,
        string fallbackCallsign,
        string originIcao,
        string destinationIcao,
        string assignedGate)
    {
        var effectiveCallsign = !string.IsNullOrWhiteSpace(callsign)
            ? callsign.Trim()
            : !string.IsNullOrWhiteSpace(fallbackCallsign)
                ? fallbackCallsign.Trim()
                : "Aircraft";
        if (stepId == "captain-ifr-clearance")
        {
            var origin = string.IsNullOrWhiteSpace(originIcao)
                ? "the departure airport"
                : originIcao.Trim().ToUpperInvariant();
            var destination = string.IsNullOrWhiteSpace(destinationIcao)
                ? "our destination"
                : destinationIcao.Trim().ToUpperInvariant();
            return $"{effectiveCallsign}, at {origin}, request IFR clearance to {destination}, ready to copy";
        }

        if (stepId == "captain-pushback-clearance")
        {
            var gate = string.IsNullOrWhiteSpace(assignedGate)
                ? "the gate"
                : assignedGate.Trim();
            return $"{effectiveCallsign}, at {gate}, request pushback and engine start";
        }

        throw new ArgumentOutOfRangeException(nameof(stepId));
    }
}
