using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.SimBrief;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class SimBriefPayloadSummaryTests
{
    [TestMethod]
    public void KilogramsConvertsPoundsAndPreservesMetricPlans()
    {
        Assert.AreEqual(1000d, SimBriefPayloadSummary.Kilograms(2204.62262185, "lbs")!.Value, 0.001);
        Assert.AreEqual(1000d, SimBriefPayloadSummary.Kilograms(1000, "kgs")!.Value, 0.001);
        Assert.IsNull(SimBriefPayloadSummary.Kilograms(null, "kgs"));
    }

    [TestMethod]
    public void PassengerAndBaggageMassUseActualImportedCounts()
    {
        var plan = new ImportedFlightPlan
        {
            PassengerCount = 140,
            PassengerWeight = 84,
            BaggageCount = 120,
            BaggageWeight = 20
        };

        Assert.AreEqual(11760d, SimBriefPayloadSummary.PassengerMass(plan));
        Assert.AreEqual(2400d, SimBriefPayloadSummary.BaggageMass(plan));
    }

    [TestMethod]
    public void FormattedWeightIncludesKilogramsTonnesAndPounds()
    {
        var formatted = SimBriefPayloadSummary.FormatWeight(1000, "kgs");

        StringAssert.Contains(formatted, "kg");
        StringAssert.Contains(formatted, "t");
        StringAssert.Contains(formatted, "lb");
    }
}
