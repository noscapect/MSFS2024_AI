using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.AircraftAdapters.FbwA320;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class FbwA320CrewOxygenAdapterTests
{
    [TestMethod]
    public void BlocksWhenAircraftIsNotFbwA320()
    {
        var plan = FbwA320CrewOxygenAdapter.CreatePlan(
            new AircraftState { Title = "A320neo V2" },
            desiredOn: true);

        Assert.AreEqual(FbwA320CrewOxygenCommandPlanKind.Blocked, plan.Kind);
    }

    [TestMethod]
    public void SendsZeroForCrewOxygenOn()
    {
        var plan = FbwA320CrewOxygenAdapter.CreatePlan(
            new AircraftState
            {
                Title = "FlyByWire A32NX",
                CrewOxygenOn = false
            },
            desiredOn: true);

        Assert.AreEqual(FbwA320CrewOxygenCommandPlanKind.Command, plan.Kind);
        Assert.AreEqual(0, plan.RawState);
        Assert.AreEqual(FbwA320CrewOxygenAdapter.InputEventHash, plan.InputEventHash);
        CollectionAssert.Contains(
            plan.MobiFlightCommands.ToList(),
            "MF.SimVars.Set.0 (>L:PUSH_OVHD_OXYGEN_CREW)");
    }

    [TestMethod]
    public void RawOneMeansOffSoOnCommandIsNotSkippedEvenWhenStateIsStale()
    {
        var plan = FbwA320CrewOxygenAdapter.CreatePlan(
            new AircraftState
            {
                Title = "FlyByWire A32NX",
                CrewOxygenOn = true
            },
            desiredOn: true,
            typedRawOffState: true);

        Assert.AreEqual(FbwA320CrewOxygenCommandPlanKind.Command, plan.Kind);
        Assert.AreEqual(0, plan.RawState);
    }

    [TestMethod]
    public void RawZeroMeansOnSoOnCommandMayBeSkipped()
    {
        var plan = FbwA320CrewOxygenAdapter.CreatePlan(
            new AircraftState
            {
                Title = "FlyByWire A32NX",
                CrewOxygenOn = false
            },
            desiredOn: true,
            typedRawOffState: false);

        Assert.AreEqual(FbwA320CrewOxygenCommandPlanKind.AlreadySet, plan.Kind);
    }

    [TestMethod]
    public void SendsOneForCrewOxygenOff()
    {
        var plan = FbwA320CrewOxygenAdapter.CreatePlan(
            new AircraftState
            {
                Title = "FlyByWire A32NX",
                CrewOxygenOn = true
            },
            desiredOn: false);

        Assert.AreEqual(FbwA320CrewOxygenCommandPlanKind.Command, plan.Kind);
        Assert.AreEqual(1, plan.RawState);
    }
}
