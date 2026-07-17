namespace Msfs2024Ai.Copilot.SayIntentions;

internal static class SayIntentionsCommunicationMatcher
{
    public static bool IsGenuineReply(
        SayIntentionsCommunication communication,
        string transmittedMessage)
    {
        var incoming = communication.IncomingMessage?.Trim() ?? "";
        if (incoming.Length == 0)
        {
            return false;
        }

        return !string.Equals(
                   incoming,
                   transmittedMessage?.Trim(),
                   StringComparison.OrdinalIgnoreCase)
               && !string.Equals(
                   incoming,
                   communication.OutgoingMessage?.Trim(),
                   StringComparison.OrdinalIgnoreCase);
    }
}
