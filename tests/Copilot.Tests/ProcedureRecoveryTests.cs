using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Domain;
using Msfs2024Ai.Copilot.Procedures;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class ProcedureRecoveryTests
{
    [TestMethod]
    public void TakeoffFlowStartedAirborneSkipsHistoricalRunwayMilestones()
    {
        var commands = new List<string>();
        var runner = new ProcedureRunner(
            commands.Add,
            () => AutomationPolicy.AutomaticWhenSupported);
        var state = new AircraftState
        {
            Title = "A320neo V2",
            OnGround = false,
            AltitudeAboveGroundFeet = 800,
            VerticalSpeedFeetPerMinute = 900,
            GroundSpoilersArmed = false,
            GearHandleDown = false,
            NoseLightSelectorPosition = 0
        };

        runner.Start(A320ProcedureLibrary.TakeoffAndClimb, state);

        Assert.IsTrue(runner.CompletedStepCount >= 8);
        Assert.AreEqual("nose-light off", commands.Last());
    }

    [TestMethod]
    public void RestoredApproachFlowCommandsExactFirstFlapDetent()
    {
        var commands = new List<string>();
        var runner = new ProcedureRunner(
            commands.Add,
            () => AutomationPolicy.AutomaticWhenSupported);
        var definition = A320ProcedureLibrary.ApproachAndLanding;
        var flapOneIndex = definition.Steps
            .Select((step, index) => new { step.Id, index })
            .Single(item => item.Id == "fo-flaps-one")
            .index;
        var state = new AircraftState
        {
            Title = "A320neo V2",
            OnGround = false,
            AltitudeAboveGroundFeet = 4500,
            IndicatedAirspeedKnots = 220,
            FlapsHandleIndex = 0,
            LeftFlapPositionPercent = 0,
            RightFlapPositionPercent = 0,
            FlapReadbackSane = true
        };

        runner.Restore(definition, flapOneIndex, state);

        CollectionAssert.AreEqual(
            new[] { "flaps config-1" },
            commands);
    }

    [TestMethod]
    public void WxrPwsCommandIsIssuedEvenWhenReadbackAlreadyReportsOne()
    {
        var commands = new List<string>();
        var runner = new ProcedureRunner(
            commands.Add,
            () => AutomationPolicy.AutomaticWhenSupported);
        var definition = A320ProcedureLibrary.AfterStartAndTaxi;
        var wxrIndex = definition.Steps
            .Select((step, index) => new { step.Id, index })
            .Single(item => item.Id == "fo-wxr-pws")
            .index;
        var state = new AircraftState
        {
            Title = "A320neo V2",
            WeatherRadarPwsSelectorPosition = 1
        };

        runner.Restore(definition, wxrIndex, state);

        CollectionAssert.AreEqual(new[] { "wxr-pws 1" }, commands);
    }

    [TestMethod]
    public void ExactFlapHandleDetentCompletesWhileSurfacesAreStillMoving()
    {
        var state = new AircraftState
        {
            FlapsHandleIndex = 1,
            FlapReadbackSane = false,
            LeftFlapPositionPercent = 0,
            RightFlapPositionPercent = 0
        };

        Assert.IsTrue(state.FlapsAtDetent(1));
    }
}
