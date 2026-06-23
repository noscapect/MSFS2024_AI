using Msfs2024Ai.Copilot.Domain;

namespace Msfs2024Ai.Copilot.Procedures;

internal sealed class ProcedureStep
{
    public ProcedureStep(
        string id,
        string label,
        ProcedureStepKind kind,
        Func<AircraftState, bool> isComplete,
        CrewRole assignedRole = CrewRole.FirstOfficer,
        string? command = null,
        string? manualInstruction = null,
        Func<AircraftState, bool>? isCompleteWhenRecovering = null)
    {
        Id = id;
        Label = label;
        Kind = kind;
        IsComplete = isComplete;
        AssignedRole = assignedRole;
        Command = command;
        ManualInstruction = manualInstruction;
        IsCompleteWhenRecovering = isCompleteWhenRecovering;
    }

    public string Id { get; }
    public string Label { get; }
    public ProcedureStepKind Kind { get; }
    public Func<AircraftState, bool> IsComplete { get; }
    public CrewRole AssignedRole { get; }
    public string? Command { get; }
    public string? ManualInstruction { get; }
    public Func<AircraftState, bool>? IsCompleteWhenRecovering { get; }
}
