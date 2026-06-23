namespace Msfs2024Ai.Copilot.Settings;

public sealed class ProcedureSession
{
    public string? ActiveProcedureId { get; set; }
    public int ActiveStepIndex { get; set; }
    public List<string> CompletedProcedureIds { get; set; } = new();
    public DateTime SavedUtc { get; set; }
}
