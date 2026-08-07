namespace Msfs2024Ai.Copilot.Companion;

internal static class CompanionCommandGate
{
    public static bool TryForward(
        CompanionBridge bridge,
        string commandJson,
        bool controlsAllowed,
        out string error)
    {
        if (!CompanionProtocol.TryParseCommand(
                commandJson,
                out var command,
                out error))
        {
            return false;
        }
        if (!controlsAllowed && command.Action != "request_state")
        {
            bridge.Publish(
                new Dictionary<string, object?>
                {
                    ["protocolVersion"] = CompanionProtocol.Version,
                    ["kind"] = "commandResult",
                    ["requestId"] = command.RequestId,
                    ["accepted"] = false,
                    ["message"] = "Remote controls are disabled on the Windows companion.",
                    ["sentUtc"] = DateTime.UtcNow.ToString("O")
                });
            error = string.Empty;
            return true;
        }
        return bridge.TryReceiveCommand(commandJson, out error);
    }
}
