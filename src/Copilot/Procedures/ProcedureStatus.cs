namespace Msfs2024Ai.Copilot.Procedures;

internal enum ProcedureStatus
{
    Idle,
    Running,
    WaitingForManualAction,
    WaitingForVerification,
    Completed,
    Failed,
    Paused
}

internal enum ProcedureStepKind
{
    Observe,
    AutomaticAction,
    ManualAction
}
