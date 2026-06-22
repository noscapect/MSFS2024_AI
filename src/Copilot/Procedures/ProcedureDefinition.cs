namespace Msfs2024Ai.Copilot.Procedures;

internal sealed class ProcedureDefinition
{
    public ProcedureDefinition(string id, string name, IReadOnlyList<ProcedureStep> steps)
    {
        Id = id;
        Name = name;
        Steps = steps;
    }

    public string Id { get; }
    public string Name { get; }
    public IReadOnlyList<ProcedureStep> Steps { get; }
    public int AutomaticStepCount =>
        Steps.Count(step => step.Kind == ProcedureStepKind.AutomaticAction);
    public int ManualStepCount =>
        Steps.Count(step => step.Kind == ProcedureStepKind.ManualAction);
    public int ObservableStepCount =>
        Steps.Count(step => step.Kind == ProcedureStepKind.Observe);
    public bool IsFullyAutomated => ManualStepCount == 0;
    public string AutomationSummary =>
        $"{AutomaticStepCount} automatic, {ObservableStepCount} monitored, {ManualStepCount} manual";
}
