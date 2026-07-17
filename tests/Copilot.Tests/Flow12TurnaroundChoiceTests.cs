using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Procedures;

namespace Copilot.Tests;

[TestClass]
public sealed class Flow12TurnaroundChoiceTests
{
    [TestMethod]
    public void EverySupportedAircraft_OffersFollowUpFlightCancellationBeforeFinalSecure()
    {
        var procedures = new[]
        {
            A320ProcedureLibrary.GateToGate[11],
            A321ProcedureLibrary.GateToGate[11],
            A330ProcedureLibrary.GateToGate[11],
            FbwA320ProcedureLibrary.GateToGate[11],
            B737ProcedureLibrary.GateToGate[11]
        };

        foreach (var procedure in procedures)
        {
            var decision = procedure.Steps.Single(step =>
                step.Id is "secure-decision" or "captain-secure");
            StringAssert.Contains(decision.Label, "follow-up flight");
            StringAssert.Contains(decision.ManualInstruction!, "Confirm now");
            StringAssert.Contains(decision.ManualInstruction!, "Cancel");
            StringAssert.Contains(decision.ManualInstruction!, "follow-up flight");
        }
    }
}
