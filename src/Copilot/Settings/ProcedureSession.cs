using Msfs2024Ai.Copilot.SimBrief;

namespace Msfs2024Ai.Copilot.Settings;

public sealed class ProcedureSession
{
    public string? ActiveProcedureId { get; set; }
    public int ActiveStepIndex { get; set; }
    public List<string> CompletedProcedureIds { get; set; } = new();
    public ImportedFlightPlan? ActiveFlightPlan { get; set; }
    public DateTime SavedUtc { get; set; }

    public void ResetProgress(DateTime savedUtc)
    {
        ActiveProcedureId = null;
        ActiveStepIndex = 0;
        CompletedProcedureIds.Clear();
        ActiveFlightPlan = null;
        SavedUtc = savedUtc;
    }
}
