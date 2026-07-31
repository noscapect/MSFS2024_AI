using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace Msfs2024Ai.Copilot.Efb;

internal sealed class EfbCompanionCommand
{
    public string RequestId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? FlowId { get; set; }
    public int? ChoiceIndex { get; set; }
}

internal static class EfbCompanionProtocol
{
    public const int Version = 2;
    public const string CommandEventName = "VFO_EFB_COMMAND_V2";
    public const string StateEventName = "VFO_EFB_STATE_V2";

    private static readonly HashSet<string> AllowedActions =
        new(StringComparer.Ordinal)
        {
            "request_state",
            "start_flow",
            "start_next_flow",
            "gsx_menu_choice",
            "confirm",
            "pause",
            "resume",
            "cancel"
        };

    public static bool TryParseCommand(
        string json,
        out EfbCompanionCommand command,
        out string error)
    {
        command = new EfbCompanionCommand();
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The EFB command was empty.";
            return false;
        }

        IDictionary<string, object>? values;
        try
        {
            values = new JavaScriptSerializer().DeserializeObject(json)
                as IDictionary<string, object>;
        }
        catch
        {
            error = "The EFB command was not valid JSON.";
            return false;
        }

        if (values == null
            || !TryReadInt(values, "protocolVersion", out var version)
            || version != Version)
        {
            error = $"Unsupported EFB protocol version; expected {Version}.";
            return false;
        }

        var action = ReadString(values, "action")?.Trim().ToLowerInvariant();
        if (action == null || !AllowedActions.Contains(action))
        {
            error = "The requested EFB action is not allowed.";
            return false;
        }

        var requestId = ReadString(values, "requestId")?.Trim();
        if (string.IsNullOrWhiteSpace(requestId)
            || requestId == null
            || requestId.Length > 80)
        {
            error = "The EFB command requires a valid request ID.";
            return false;
        }

        var flowId = ReadString(values, "flowId")?.Trim();
        if (action == "start_flow" && string.IsNullOrWhiteSpace(flowId))
        {
            error = "Starting a flow requires a flow ID.";
            return false;
        }

        int? choiceIndex = null;
        if (action == "gsx_menu_choice")
        {
            if (!TryReadInt(values, "choiceIndex", out var parsedChoiceIndex)
                || parsedChoiceIndex < 0)
            {
                error = "A GSX menu selection requires a valid choice index.";
                return false;
            }
            choiceIndex = parsedChoiceIndex;
        }

        command = new EfbCompanionCommand
        {
            RequestId = requestId,
            Action = action,
            FlowId = flowId,
            ChoiceIndex = choiceIndex
        };
        return true;
    }

    public static string Serialize(object value) =>
        new JavaScriptSerializer().Serialize(value);

    private static string? ReadString(
        IDictionary<string, object> values,
        string key) =>
        values.TryGetValue(key, out var value) ? value as string : null;

    private static bool TryReadInt(
        IDictionary<string, object> values,
        string key,
        out int result)
    {
        result = 0;
        if (!values.TryGetValue(key, out var value) || value == null)
        {
            return false;
        }

        try
        {
            result = Convert.ToInt32(value);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
