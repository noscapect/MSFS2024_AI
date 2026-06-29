using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Domain;
using Msfs2024Ai.Copilot.Procedures;
using System.Threading;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class ProcedureRecoveryTests
{
    [TestMethod]
    public void PowerUpFlowAcceptsFlyByWireA32NxForDiscovery()
    {
        var commands = new List<string>();
        var runner = new ProcedureRunner(
            commands.Add,
            () => AutomationPolicy.AutomaticWhenSupported);
        var state = new AircraftState
        {
            Title = "FlyByWire A32NX",
            OnGround = true,
            GroundSpeedKnots = 0,
            Engine1Running = false,
            Engine2Running = false
        };

        runner.Start(A320ProcedureLibrary.PowerUpAndInitialSetup, state);

        Assert.AreEqual("fo-battery-one", runner.CurrentStep?.Id);
        Assert.IsTrue(state.IsFlyByWireA320Neo);
        Assert.IsTrue(state.IsSupportedA320);

        runner.Update(state);

        Assert.AreEqual("battery-1 on", commands.Single());
    }

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
            WeatherRadarPwsSelectorPosition = 0
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

    [TestMethod]
    public void ApproachFlapsOneGateUsesConfiguredSchedule()
    {
        var commands = new List<string>();
        var runner = new ProcedureRunner(
            commands.Add,
            () => AutomationPolicy.AutomaticWhenSupported);
        var definition = A320ProcedureLibrary.ApproachAndLanding;
        var gateIndex = definition.Steps
            .Select((step, index) => new { step.Id, index })
            .Single(item => item.Id == "approach-config-start")
            .index;
        var state = new AircraftState
        {
            Title = "A320neo V2",
            OnGround = false,
            IndicatedAltitudeFeet = 8500,
            IndicatedAirspeedKnots = 215,
            ApproachFlaps1AltitudeFeet = 9000,
            ApproachFlaps1SpeedKnots = 215,
            FlapsHandleIndex = 0
        };

        runner.Restore(definition, gateIndex, state);

        CollectionAssert.AreEqual(new[] { "flaps config-1" }, commands);
    }

    [TestMethod]
    public void ApproachFlapsTwoWaitsForDistanceOrFallbackGate()
    {
        var commands = new List<string>();
        var runner = new ProcedureRunner(
            commands.Add,
            () => AutomationPolicy.AutomaticWhenSupported);
        var definition = A320ProcedureLibrary.ApproachAndLanding;
        var gateIndex = definition.Steps
            .Select((step, index) => new { step.Id, index })
            .Single(item => item.Id == "flaps-two-point")
            .index;
        var state = new AircraftState
        {
            Title = "A320neo V2",
            OnGround = false,
            ApproachDistanceToTouchdownNm = 12,
            ApproachFlaps2DistanceNm = 10,
            ApproachFlaps2AltitudeAglFeet = 4000,
            ApproachFlaps2SpeedKnots = 200,
            AltitudeAboveGroundFeet = 5000,
            IndicatedAirspeedKnots = 195,
            FlapsHandleIndex = 1
        };

        runner.Restore(definition, gateIndex, state);
        Assert.AreEqual("Waiting for condition: Flaps 2 point.", runner.Message);
        CollectionAssert.AreEqual(Array.Empty<string>(), commands);

        state.ApproachDistanceToTouchdownNm = 9.8;
        runner.Update(state);

        CollectionAssert.AreEqual(new[] { "flaps config-2" }, commands);
    }

    [TestMethod]
    public void ApproachGearGateCanUseDistanceBeforeRadioAltitudeFallback()
    {
        var commands = new List<string>();
        var runner = new ProcedureRunner(
            commands.Add,
            () => AutomationPolicy.AutomaticWhenSupported);
        var definition = A320ProcedureLibrary.ApproachAndLanding;
        var gateIndex = definition.Steps
            .Select((step, index) => new { step.Id, index })
            .Single(item => item.Id == "gear-down-point")
            .index;
        var state = new AircraftState
        {
            Title = "A320neo V2",
            OnGround = false,
            ApproachDistanceToTouchdownNm = 6.5,
            ApproachGearDistanceNm = 7,
            ApproachGearAltitudeAglFeet = 2500,
            ApproachGearSpeedKnots = 210,
            AltitudeAboveGroundFeet = 3200,
            IndicatedAirspeedKnots = 205,
            GearHandleDown = false
        };

        runner.Restore(definition, gateIndex, state);

        CollectionAssert.AreEqual(new[] { "gear down" }, commands);
    }

    [TestMethod]
    public void ApproachFlowUsesTakeoffNoseLightBeforeLanding()
    {
        var commands = new List<string>();
        var runner = new ProcedureRunner(
            commands.Add,
            () => AutomationPolicy.AutomaticWhenSupported);
        var definition = A320ProcedureLibrary.ApproachAndLanding;
        var noseLightIndex = definition.Steps
            .Select((step, index) => new { step.Id, index })
            .Single(item => item.Id == "fo-nose-light-takeoff")
            .index;
        var state = new AircraftState
        {
            Title = "A320neo V2",
            OnGround = false,
            NoseLightSelectorPosition = 2
        };

        runner.Restore(definition, noseLightIndex, state);

        CollectionAssert.AreEqual(new[] { "nose-light takeoff" }, commands);
    }

    [TestMethod]
    public void AfterLandingStartsApuBeforeTaxiSpeedCleanup()
    {
        var commands = new List<string>();
        var runner = new ProcedureRunner(
            commands.Add,
            () => AutomationPolicy.AutomaticWhenSupported);
        var definition = A320ProcedureLibrary.AfterLandingAndTaxi;
        var reverseStowedIndex = definition.Steps
            .Select((step, index) => new { step.Id, index })
            .Single(item => item.Id == "captain-deceleration")
            .index;
        var state = new AircraftState
        {
            Title = "A320neo V2",
            OnGround = true,
            GroundSpeedKnots = 65,
            Engine1ReverseEngaged = false,
            Engine2ReverseEngaged = false,
            AutobrakeLevel = 1,
            ApuMasterSwitchOn = false
        };

        runner.Restore(definition, reverseStowedIndex, state);

        CollectionAssert.AreEqual(new[] { "autobrake off" }, commands);

        state.AutobrakeLevel = 0;
        runner.Update(state);
        Assert.AreEqual("fo-apu-master-on", runner.CurrentStep?.Id);
        Thread.Sleep(1100);
        runner.Update(state);

        CollectionAssert.AreEqual(
            new[] { "autobrake off", "apu-master on" },
            commands);
        Assert.AreNotEqual("captain-runway-exit", runner.CurrentStep?.Id);
    }

    [TestMethod]
    public void AfterLandingCleanupDoesNotWaitForApuAvailable()
    {
        var commands = new List<string>();
        var runner = new ProcedureRunner(
            commands.Add,
            () => AutomationPolicy.AutomaticWhenSupported);
        var definition = A320ProcedureLibrary.AfterLandingAndTaxi;
        var apuStartIndex = definition.Steps
            .Select((step, index) => new { step.Id, index })
            .Single(item => item.Id == "fo-apu-start-on")
            .index;
        var state = new AircraftState
        {
            Title = "A320neo V2",
            OnGround = true,
            GroundSpeedKnots = 25,
            ApuStartButtonOn = true,
            ApuAvailable = false,
            LeftLandingLightSelectorPosition = 1,
            RightLandingLightSelectorPosition = 1,
            StrobeSelectorPosition = 1,
            NoseLightSelectorPosition = 1,
            GroundSpoilersArmed = true,
            FlapsHandleIndex = 4,
            LeftFlapPositionPercent = 100,
            RightFlapPositionPercent = 100,
            TransponderModeSelectorPosition = 1
        };

        runner.Restore(definition, apuStartIndex, state);

        Assert.AreEqual("fo-landing-lights-retract", runner.CurrentStep?.Id);
        Thread.Sleep(1100);
        runner.Update(state);

        CollectionAssert.AreEqual(new[] { "landing-lights retract" }, commands);
    }

    [TestMethod]
    public void ParkingFlowDoesNotTreatTrafficHoldAsGateParking()
    {
        var commands = new List<string>();
        var runner = new ProcedureRunner(
            commands.Add,
            () => AutomationPolicy.AutomaticWhenSupported);
        var state = new AircraftState
        {
            Title = "A320neo V2",
            OnGround = true,
            GroundSpeedKnots = 0,
            ParkingBrakeSet = true,
            Engine1Running = true,
            Engine2Running = true,
            ApuAvailable = true
        };

        runner.Start(A320ProcedureLibrary.ParkingAndShutdown, state);

        Assert.AreEqual("captain-park", runner.CurrentStep?.Id);
        Assert.AreEqual(ProcedureStatus.WaitingForVerification, runner.Status);
        CollectionAssert.AreEqual(Array.Empty<string>(), commands);
    }

    [TestMethod]
    public void ParkingFlowTurnsApuBleedOnBeforeShutdownCleanup()
    {
        var commands = new List<string>();
        var runner = new ProcedureRunner(
            commands.Add,
            () => AutomationPolicy.AutomaticWhenSupported);
        var state = new AircraftState
        {
            Title = "A320neo V2",
            OnGround = true,
            GroundSpeedKnots = 0,
            ParkingBrakeSet = true,
            Engine1Running = false,
            Engine2Running = false,
            ApuAvailable = true,
            ApuBleedOn = false,
            NoseLightSelectorPosition = 1,
            BeaconOn = true,
            FuelPump1State = 1,
            FuelPump2State = 1,
            FuelPump3State = 1,
            FuelPump4State = 1,
            FuelPump5State = 1,
            FuelPump6State = 1,
            SeatbeltSelectorPosition = 0
        };

        runner.Start(A320ProcedureLibrary.ParkingAndShutdown, state);

        Assert.AreEqual("fo-apu-bleed-on", runner.CurrentStep?.Id);
        CollectionAssert.AreEqual(new[] { "apu-bleed on" }, commands);
    }
}
