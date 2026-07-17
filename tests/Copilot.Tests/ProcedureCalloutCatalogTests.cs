using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot;
using Msfs2024Ai.Copilot.Voice;

namespace Copilot.Tests;

[TestClass]
public sealed class ProcedureCalloutCatalogTests
{
    [TestMethod]
    public void Minimal_KeepsCriticalCallsButOmitsRoutineConfiguration()
    {
        Assert.AreEqual(
            "V one",
            ProcedureCalloutCatalog.ForStep("fo-v1", null, CalloutDetail.Minimal));
        Assert.IsNull(
            ProcedureCalloutCatalog.ForStep("fo-flaps-one", null, CalloutDetail.Minimal));
    }

    [TestMethod]
    public void Standard_UsesAircraftAppropriateVerifiedFlapCalls()
    {
        var boeing = new AircraftState
        {
            Title = "PMDG 737-800",
            BoeingTakeoffFlaps = 5,
            BoeingLandingFlaps = 30
        };

        Assert.AreEqual(
            "Flaps five",
            ProcedureCalloutCatalog.ForStep(
                "fo-flaps-takeoff",
                boeing,
                CalloutDetail.Standard));
        Assert.AreEqual(
            "Flaps thirty",
            ProcedureCalloutCatalog.ForStep(
                "fo-flaps-landing",
                boeing,
                CalloutDetail.Standard));
        Assert.AreEqual(
            "Flaps full",
            ProcedureCalloutCatalog.ForStep(
                "fo-flaps-full",
                new AircraftState(),
                CalloutDetail.Standard));
    }

    [TestMethod]
    public void Expanded_AddsStatusAndChecklistCompletionCalls()
    {
        Assert.AreEqual(
            "APU available",
            ProcedureCalloutCatalog.ForStep(
                "apu-available",
                null,
                CalloutDetail.Expanded));
        Assert.AreEqual(
            "Takeoff configuration normal. Before takeoff checklist complete",
            ProcedureCalloutCatalog.ForCompletedProcedure(
                "before-takeoff",
                CalloutDetail.Expanded));
        Assert.IsNull(
            ProcedureCalloutCatalog.ForCompletedProcedure(
                "after-start-taxi",
                CalloutDetail.Standard));
    }

    [TestMethod]
    public void RoutineSwitches_RemainSilentAtEveryDetailLevel()
    {
        foreach (var detail in new[]
                 {
                     CalloutDetail.Minimal,
                     CalloutDetail.Standard,
                     CalloutDetail.Expanded
                 })
        {
            Assert.IsNull(
                ProcedureCalloutCatalog.ForStep("fo-wxr-pws", null, detail));
            Assert.IsNull(
                ProcedureCalloutCatalog.ForStep("fo-nav-logo", null, detail));
            Assert.IsNull(
                ProcedureCalloutCatalog.ForStep("fo-fuel-pumps", null, detail));
        }
    }
}
