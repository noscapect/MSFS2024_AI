namespace Msfs2024Ai.Copilot.Automation;

internal enum AutomationInvalidationReason
{
    SimConnectDisconnected,
    NewSimConnectSession,
    AircraftChanged
}

internal readonly struct AutomationInvalidationPolicy
{
    private AutomationInvalidationPolicy(
        AutomationInvalidationReason reason,
        bool cancelActiveProcedure,
        bool resetLogicalFlowIntent)
    {
        Reason = reason;
        CancelActiveProcedure = cancelActiveProcedure;
        ResetLogicalFlowIntent = resetLogicalFlowIntent;
    }

    public AutomationInvalidationReason Reason { get; }
    public bool CancelActiveProcedure { get; }
    public bool ResetLogicalFlowIntent { get; }

    public void ApplyToProcedure(Procedures.ProcedureRunner procedureRunner)
    {
        if (CancelActiveProcedure)
        {
            procedureRunner.Cancel();
        }
        else
        {
            procedureRunner.InvalidatePendingAutomationAttempt();
        }
    }

    public void ApplyToLogicalFlowIntent(Action resetLogicalFlowIntent)
    {
        if (ResetLogicalFlowIntent)
        {
            resetLogicalFlowIntent();
        }
    }

    public static AutomationInvalidationPolicy For(
        AutomationInvalidationReason reason) =>
        reason switch
        {
            AutomationInvalidationReason.SimConnectDisconnected =>
                new(reason, cancelActiveProcedure: false, resetLogicalFlowIntent: false),
            AutomationInvalidationReason.NewSimConnectSession =>
                new(reason, cancelActiveProcedure: false, resetLogicalFlowIntent: false),
            AutomationInvalidationReason.AircraftChanged =>
                new(reason, cancelActiveProcedure: true, resetLogicalFlowIntent: true),
            _ => throw new ArgumentOutOfRangeException(nameof(reason))
        };
}
