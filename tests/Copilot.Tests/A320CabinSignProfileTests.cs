using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.AircraftAdapters.IniBuildsA320;
using Msfs2024Ai.Copilot.Procedures;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class A320CabinSignProfileTests
{
    [TestMethod]
    public void AutoPolicyRequiresPhysicalAutoPosition()
    {
        Assert.IsTrue(A320CabinSignProfile.IsAuto(1));
        Assert.IsFalse(A320CabinSignProfile.IsAuto(0));
        Assert.IsFalse(A320CabinSignProfile.IsAuto(2));
        Assert.IsFalse(A320CabinSignProfile.IsAuto(null));
    }

    [TestMethod]
    public void AllA320FlightPhaseSeatbeltStepsRetainAuto()
    {
        var seatbeltSteps = A320ProcedureLibrary.GateToGate
            .SelectMany(flow => flow.Steps)
            .Where(step => step.Id.IndexOf("seatbelt", StringComparison.OrdinalIgnoreCase) >= 0)
            .ToArray();

        Assert.IsTrue(seatbeltSteps.Length >= 4);
        Assert.IsTrue(seatbeltSteps.All(step =>
            step.Command == A320CabinSignProfile.SeatbeltsAutoCommand));
    }

    [TestMethod]
    public void ApproachCommandsLandingLightsImmediatelyAfterTenThousandGate()
    {
        var ids = A320ProcedureLibrary.ApproachAndLanding.Steps
            .Select(step => step.Id)
            .ToArray();
        var altitudeGate = Array.IndexOf(ids, "below-ten-thousand");
        var landingLights = Array.IndexOf(ids, "fo-landing-lights-on");
        var seatbelts = Array.IndexOf(ids, "fo-seatbelts-auto-approach");

        Assert.AreEqual(altitudeGate + 1, landingLights);
        Assert.IsTrue(landingLights < seatbelts);
    }
}
