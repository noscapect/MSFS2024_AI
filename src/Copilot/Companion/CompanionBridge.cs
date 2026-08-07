using Msfs2024Ai.Copilot.Efb;

namespace Msfs2024Ai.Copilot.Companion;

/// <summary>
/// Transport-neutral boundary between the authoritative desktop runtime and
/// paired companion transports. Network implementations never receive direct
/// access to CopilotService, SimConnect, or the command queue.
/// </summary>
internal sealed class CompanionBridge
{
    public event Action<string>? CommandReceived;
    public event Action<string>? MessagePublished;

    public bool TryReceiveCommand(string payload, out string error)
    {
        if (!CompanionProtocol.TryParseCommand(
                payload,
                out var command,
                out error))
        {
            return false;
        }

        // Preserve one authoritative command path. The existing CommBus
        // parser and all runtime guards are applied again by CopilotService.
        var efbPayload = EfbCompanionProtocol.Serialize(
            new Dictionary<string, object?>
            {
                ["protocolVersion"] = EfbCompanionProtocol.Version,
                ["requestId"] = command.RequestId,
                ["action"] = command.Action,
                ["flowId"] = command.FlowId,
                ["choiceIndex"] = command.ChoiceIndex
            });
        CommandReceived?.Invoke(efbPayload);
        return true;
    }

    public void Publish(object envelope)
    {
        MessagePublished?.Invoke(CompanionProtocol.Serialize(envelope));
    }
}
