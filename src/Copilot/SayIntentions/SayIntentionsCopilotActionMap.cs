namespace Msfs2024Ai.Copilot.SayIntentions;

internal static class SayIntentionsCopilotActionMap
{
    public static bool TryGetActionName(string stepId, out string actionName)
    {
        actionName = stepId switch
        {
            "captain-ifr-clearance" => "preflight_request_clearance_ifr",
            "captain-pushback-clearance" => "preflight_request_push_and_start",
            _ => ""
        };

        return actionName.Length > 0;
    }
}
