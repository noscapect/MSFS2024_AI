using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot;
using Msfs2024Ai.Copilot.Procedures;

namespace Copilot.Tests;

[TestClass]
public sealed class SayIntentionsAtcFlowTests
{
    [TestMethod]
    public void TaxiClearanceStep_IsOnlyBlockingForActiveSayIntentionsFlight()
    {
        var procedures = new[]
        {
            A320ProcedureLibrary.GateToGate[4],
            A321ProcedureLibrary.GateToGate[4],
            A330ProcedureLibrary.GateToGate[4],
            FbwA320ProcedureLibrary.GateToGate[4],
            B737ProcedureLibrary.GateToGate[4]
        };

        foreach (var procedure in procedures)
        {
            var step = procedure.Steps.Single(item => item.Id == "fo-taxi-clearance");
            Assert.IsTrue(step.IsComplete(new AircraftState
            {
                SayIntentionsAtcActive = false
            }));
            Assert.IsFalse(step.IsComplete(new AircraftState
            {
                SayIntentionsAtcActive = true
            }));
        }
    }
}
