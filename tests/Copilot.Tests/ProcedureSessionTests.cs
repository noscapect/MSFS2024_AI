using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Settings;
using Msfs2024Ai.Copilot.SimBrief;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class ProcedureSessionTests
{
    [TestMethod]
    public void ResetProgressClearsActiveAndCompletedFlowsOnly()
    {
        var savedUtc = new DateTime(2026, 6, 23, 15, 0, 0, DateTimeKind.Utc);
        var session = new ProcedureSession
        {
            ActiveProcedureId = "approach-landing",
            ActiveStepIndex = 12,
            CompletedProcedureIds = new List<string>
            {
                "power-up-initial-setup",
                "approach-landing"
            },
            ActiveFlightPlan = new ImportedFlightPlan { OriginIcao = "EBBR", DestinationIcao = "EHAM" }
        };

        session.ResetProgress(savedUtc);

        Assert.IsNull(session.ActiveProcedureId);
        Assert.AreEqual(0, session.ActiveStepIndex);
        Assert.AreEqual(0, session.CompletedProcedureIds.Count);
        Assert.IsNull(session.ActiveFlightPlan);
        Assert.AreEqual(savedUtc, session.SavedUtc);
    }
}
