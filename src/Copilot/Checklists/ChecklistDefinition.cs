namespace Msfs2024Ai.Copilot.Checklists;

internal sealed class ChecklistDefinition
{
    public ChecklistDefinition(
        string procedureId,
        string name,
        IReadOnlyList<ChecklistItem> items)
    {
        ProcedureId = procedureId;
        Name = name;
        Items = items;
    }

    public string ProcedureId { get; }
    public string Name { get; }
    public IReadOnlyList<ChecklistItem> Items { get; }
}
