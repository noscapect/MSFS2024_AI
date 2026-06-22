using Msfs2024Ai.Copilot.Domain;

namespace Msfs2024Ai.Copilot.Settings;

public sealed class CopilotSettings
{
    public CrewRole PilotFlying { get; set; } = CrewRole.Captain;
    public AutomationPolicy AutomationPolicy { get; set; } = AutomationPolicy.AutomaticWhenSupported;
    public bool RequireManualStepConfirmation { get; set; } = true;
    public bool EnableStandardCallouts { get; set; } = true;
    public bool AutoAdvanceObservedSteps { get; set; } = true;
    public int TransitionAltitudeFeet { get; set; } = 5000;
    public int TakeoffV1SpeedKnots { get; set; } = 140;
    public int TakeoffRotateSpeedKnots { get; set; } = 143;
}
