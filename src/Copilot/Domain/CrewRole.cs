namespace Msfs2024Ai.Copilot.Domain;

public enum CrewRole
{
    Captain,
    FirstOfficer,
    Either
}

public enum AutomationPolicy
{
    AutomaticWhenSupported,
    AlwaysAskBeforeAction,
    MonitorOnly
}
