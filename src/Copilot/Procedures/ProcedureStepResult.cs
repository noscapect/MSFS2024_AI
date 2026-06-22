namespace Msfs2024Ai.Copilot.Procedures;

internal sealed class ProcedureStepResult
{
    public ProcedureStepResult(string id, string label, bool complete, string? actionHint = null)
    {
        Id = id;
        Label = label;
        Complete = complete;
        ActionHint = actionHint;
    }

    public string Id { get; }
    public string Label { get; }
    public bool Complete { get; }
    public string? ActionHint { get; }
}
