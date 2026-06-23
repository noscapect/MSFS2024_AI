namespace Msfs2024Ai.Copilot.Settings;

public sealed class ProcedureSession
{
    public string? ActiveProcedureId { get; set; }
    public int ActiveStepIndex { get; set; }
    public List<string> CompletedProcedureIds { get; set; } = new();
    public DateTime SavedUtc { get; set; }

    public void ResetProgress(DateTime savedUtc)
    {
        ActiveProcedureId = null;
        ActiveStepIndex = 0;
        CompletedProcedureIds.Clear();
        SavedUtc = savedUtc;
    }
}
