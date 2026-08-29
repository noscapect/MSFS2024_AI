using Microsoft.FlightSimulator.SimConnect;
using System.Collections.Generic;

namespace Msfs2024Ai.Copilot.Efb;

internal sealed class EfbCompanionTransport
{
    private readonly Dictionary<EfbCommBusEvent, List<string>> _commBusChunks = new();
    private DateTime _lastStateRequestResponseUtc = DateTime.MinValue;
    private DateTime _lastStatePublishedUtc = DateTime.MinValue;

    public bool TryAcceptCommandChunk(EfbCommBusEvent eventId, uint entryNumber, uint totalEntries, string? data, out string payload)
    {
        payload = string.Empty;
        if (eventId != EfbCommBusEvent.Command) return false;
        if (!_commBusChunks.TryGetValue(eventId, out var chunks))
        {
            chunks = new List<string>();
            _commBusChunks[eventId] = chunks;
        }
        chunks.Add(data ?? string.Empty);
        if (entryNumber + 1 < totalEntries) return false;

        payload = string.Concat(chunks);
        chunks.Clear();
        return true;
    }

    public bool CanAcknowledgeStateRequest(DateTime utcNow)
    {
        if (utcNow - _lastStateRequestResponseUtc < TimeSpan.FromSeconds(1)) return false;
        _lastStateRequestResponseUtc = utcNow;
        return true;
    }

    public bool ShouldPublishState(DateTime utcNow, bool force)
    {
        if (!force && utcNow - _lastStatePublishedUtc < TimeSpan.FromMilliseconds(750)) return false;
        _lastStatePublishedUtc = utcNow;
        return true;
    }

    public Dictionary<string, object?> CreateCommandResultEnvelope(string requestId, bool accepted, string message, DateTime utcNow) => new()
    {
        ["protocolVersion"] = EfbCompanionProtocol.Version,
        ["kind"] = "commandResult",
        ["requestId"] = requestId,
        ["accepted"] = accepted,
        ["message"] = message,
        ["sentUtc"] = utcNow.ToString("O")
    };

    public void SendEnvelope(SimConnect connection, object envelope, Action<string> log)
    {
        try
        {
            connection.CallCommBusEvent(EfbCompanionProtocol.StateEventName, SIMCONNECT_COMM_BUS_BROADCAST_TO.JS, EfbCompanionProtocol.Serialize(envelope));
        }
        catch (Exception exception)
        {
            log("Could not publish MSFS EFB companion state: " + exception.Message);
        }
    }

    public void ResetConnectionState()
    {
        _commBusChunks.Clear();
        _lastStateRequestResponseUtc = DateTime.MinValue;
        _lastStatePublishedUtc = DateTime.MinValue;
    }
}
