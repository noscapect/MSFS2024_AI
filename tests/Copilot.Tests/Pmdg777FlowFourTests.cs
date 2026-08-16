using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.AircraftAdapters.Pmdg777;
using Msfs2024Ai.Copilot.Domain;
using Msfs2024Ai.Copilot.Procedures;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class Pmdg777FlowFourTests
{
    [TestMethod]
    public void FlowThreeRequestsDepartureBeforeWaitingForClosedDoors()
    {
        var ids = Pmdg777ProcedureLibrary.BeforeStartAndPushback.Steps
            .Select(step => step.Id)
            .ToList();

        Assert.IsTrue(
            ids.IndexOf("captain-pushback-clearance") < ids.IndexOf("doors-cargo"));
    }

    [TestMethod]
    public void FlowsFourThroughCruiseHaveNoManualFirstOfficerPlaceholders()
    {
        var flows = new[]
        {
            Pmdg777ProcedureLibrary.EngineStartSequence,
            Pmdg777ProcedureLibrary.BeforeTaxiAndTaxi,
            Pmdg777ProcedureLibrary.BeforeTakeoff,
            Pmdg777ProcedureLibrary.TakeoffAndClimb,
            Pmdg777ProcedureLibrary.Cruise
        };

        Assert.IsFalse(flows.SelectMany(flow => flow.Steps).Any(step =>
            step.Kind == ProcedureStepKind.ManualAction
            && step.AssignedRole == CrewRole.FirstOfficer));
    }

    [TestMethod]
    public void ArrivalFlowsHaveNoManualFirstOfficerPlaceholders()
    {
        var flows = new[]
        {
            Pmdg777ProcedureLibrary.ApproachAndLanding,
            Pmdg777ProcedureLibrary.AfterLandingAndTaxi,
            Pmdg777ProcedureLibrary.ParkingAndShutdown
        };

        Assert.IsFalse(flows.SelectMany(flow => flow.Steps).Any(step =>
            step.Kind == ProcedureStepKind.ManualAction
            && step.AssignedRole == CrewRole.FirstOfficer));
        Assert.AreEqual(
            1,
            flows.SelectMany(flow => flow.Steps)
                .Count(step => step.Kind == ProcedureStepKind.ManualAction));
    }

    [TestMethod]
    public void ArrivalFirstOfficerActionsUseCommandsAndReadbacks()
    {
        var steps = new[]
        {
            Pmdg777ProcedureLibrary.ApproachAndLanding,
            Pmdg777ProcedureLibrary.AfterLandingAndTaxi,
            Pmdg777ProcedureLibrary.ParkingAndShutdown
        }.SelectMany(flow => flow.Steps).ToDictionary(step => step.Id);

        Assert.AreEqual("pmdg777 approach lights", steps["fo-approach-lights"].Command);
        Assert.AreEqual("pmdg777 gear down", steps["fo-gear-down"].Command);
        Assert.AreEqual("pmdg777 speedbrake arm", steps["fo-speedbrake-arm"].Command);
        Assert.AreEqual("pmdg777 landing flaps", steps["fo-landing-flaps"].Command);
        Assert.AreEqual("pmdg777 after landing lights", steps["fo-after-landing-lights"].Command);
        Assert.AreEqual("pmdg777 apu start", steps["fo-apu-start"].Command);
        Assert.AreEqual("pmdg777 shutdown pumps", steps["fo-shutdown-pumps"].Command);
        Assert.IsTrue(steps.Values
            .Where(step => step.Kind == ProcedureStepKind.AutomaticAction)
            .All(step => !string.IsNullOrWhiteSpace(step.Command)));
    }

    [TestMethod]
    public void ApproachIntermediateFlapStepsAcceptLaterDetentsAndNeverNeedRetraction()
    {
        var steps = Pmdg777ProcedureLibrary.ApproachAndLanding.Steps
            .ToDictionary(step => step.Id);
        var state = new AircraftState { Pmdg777FlapsLever = 6 };

        Assert.IsTrue(steps["fo-flaps-one"].IsComplete(state));
        Assert.IsTrue(steps["fo-flaps-five"].IsComplete(state));
        Assert.IsTrue(steps["fo-flaps-fifteen"].IsComplete(state));
        Assert.IsTrue(steps["fo-flaps-twenty"].IsComplete(state));
        Assert.IsTrue(Pmdg777ControlProfile.ApproachFlapCommandWouldRetract(6, 2));
        Assert.IsFalse(Pmdg777ControlProfile.ApproachFlapCommandWouldRetract(1, 2));
    }

    [TestMethod]
    public void ApproachUses777SpecificFlapSchedulingSpeeds()
    {
        var steps = Pmdg777ProcedureLibrary.ApproachAndLanding.Steps
            .ToDictionary(step => step.Id);
        var state = new AircraftState
        {
            IndicatedAirspeedKnots = 235,
            Pmdg777FmcLandingFlaps = 30
        };

        Assert.IsTrue(steps["flaps-one-speed"].IsComplete(state));
        Assert.IsTrue(steps["flaps-five-speed"].IsComplete(state));
        Assert.IsFalse(steps["flaps-fifteen-speed"].IsComplete(state));

        state.IndicatedAirspeedKnots = 175;
        Assert.IsTrue(steps["landing-flaps-speed"].IsComplete(state));
        state.IndicatedAirspeedKnots = 176;
        Assert.IsFalse(steps["landing-flaps-speed"].IsComplete(state));
    }

    [TestMethod]
    public void DepartureFirstOfficerActionsUseCommandsAndReadbacks()
    {
        var steps = new[]
        {
            Pmdg777ProcedureLibrary.BeforeTaxiAndTaxi,
            Pmdg777ProcedureLibrary.BeforeTakeoff,
            Pmdg777ProcedureLibrary.TakeoffAndClimb
        }.SelectMany(flow => flow.Steps).ToDictionary(step => step.Id);

        Assert.AreEqual("pmdg777 after start air apu", steps["fo-after-start-air-apu"].Command);
        Assert.AreEqual("pmdg777 takeoff flaps", steps["flaps"].Command);
        Assert.AreEqual("sayintentions taxi clearance", steps["fo-taxi-clearance"].Command);
        Assert.AreEqual("pmdg777 transponder tara", steps["fo-transponder-tara"].Command);
        Assert.AreEqual("pmdg777 lnav arm", steps["fo-lnav-arm"].Command);
        Assert.AreEqual("pmdg777 vnav arm", steps["fo-vnav-arm"].Command);
        Assert.AreEqual("sayintentions takeoff clearance", steps["fo-takeoff-clearance"].Command);
        Assert.AreEqual("pmdg777 takeoff lights", steps["fo-takeoff-lights"].Command);
        Assert.AreEqual("pmdg777 gear up", steps["gear-up"].Command);
        Assert.AreEqual("pmdg777 flaps up", steps["flap-retraction"].Command);
        Assert.AreEqual("pmdg777 climb lights", steps["fo-climb-lights"].Command);

        Assert.IsTrue(steps.Values
            .Where(step => step.Kind == ProcedureStepKind.AutomaticAction)
            .All(step => step.Command != null));
    }

    [TestMethod]
    public void FlowSixRequiresNavigationModesAndActualPmdgChecklistCompletion()
    {
        var steps = Pmdg777ProcedureLibrary.BeforeTakeoff.Steps
            .ToDictionary(step => step.Id);
        var state = new AircraftState
        {
            Pmdg777TakeoffFlapsSet = true,
            Pmdg777TransponderTaRa = true,
            Pmdg777TakeoffLightsSet = true
        };

        Assert.IsFalse(steps["fo-lnav-arm"].IsComplete(state));
        Assert.IsFalse(steps["fo-vnav-arm"].IsComplete(state));
        Assert.IsFalse(steps["before-takeoff-checklist"].IsComplete(state));

        state.Pmdg777LnavArmed = true;
        state.Pmdg777VnavArmed = true;
        Assert.IsFalse(steps["before-takeoff-checklist"].IsComplete(state));

        state.Pmdg777BeforeTakeoffChecklistComplete = true;
        Assert.IsTrue(steps["before-takeoff-checklist"].IsComplete(state));
    }

    [TestMethod]
    public void FlowSixRecoversImmediatelyWhenAircraftIsAlreadyAirborne()
    {
        var commands = new List<string>();
        var runner = new ProcedureRunner(
            commands.Add,
            () => AutomationPolicy.AutomaticWhenSupported);

        runner.Start(
            Pmdg777ProcedureLibrary.BeforeTakeoff,
            new AircraftState { OnGround = false });

        Assert.AreEqual(ProcedureStatus.Completed, runner.Status);
        Assert.AreEqual(0, commands.Count);
    }

    [TestMethod]
    public void FlowFiveTaxiLightsCannotSkipBeforeFoCommand()
    {
        var step = Pmdg777ProcedureLibrary.BeforeTaxiAndTaxi.Steps
            .Single(item => item.Id == "taxi-lights");
        var state = new AircraftState { Pmdg777TaxiLightsSet = true };

        Assert.IsFalse(step.IsComplete(state));
        state.Pmdg777TaxiLightsCommandedThisFlow = true;
        Assert.IsTrue(step.IsComplete(state));
    }

    [TestMethod]
    public void FlowFiveRequiresNormalApuBleedAutoAfterEngineStart()
    {
        var step = Pmdg777ProcedureLibrary.BeforeTaxiAndTaxi.Steps
            .Single(item => item.Id == "fo-after-start-air-apu");
        var state = new AircraftState
        {
            Pmdg777EngineBleedsAuto = true,
            Pmdg777PacksAuto = true,
            Pmdg777ApuSelectorOff = true,
            Pmdg777ApuBleedOff = true
        };

        Assert.IsFalse(step.IsComplete(state));
        state.Pmdg777ApuBleedOff = false;
        state.Pmdg777ApuBleedAuto = true;
        Assert.IsTrue(step.IsComplete(state));
    }

    [TestMethod]
    public void CruiseCompletesFromTelemetryWithoutConfirmation()
    {
        var flow = Pmdg777ProcedureLibrary.Cruise;

        Assert.AreEqual("8. 777 Cruise", flow.Name);
        Assert.IsFalse(flow.Steps.Any(step => step.Kind == ProcedureStepKind.ManualAction));
        CollectionAssert.AreEqual(
            new[] { "cruise-established", "systems-monitor" },
            flow.Steps.Select(step => step.Id).ToArray());
    }

    [TestMethod]
    public void FlowFourHasNoManualFirstOfficerPlaceholders()
    {
        var flow = Pmdg777ProcedureLibrary.EngineStartSequence;

        Assert.IsFalse(flow.Steps.Any(step =>
            step.Kind == ProcedureStepKind.ManualAction
            && step.AssignedRole == CrewRole.FirstOfficer));
        Assert.IsFalse(flow.Steps.Any(step => step.Id == "start-abnormal-review"));
    }

    [TestMethod]
    public void FlowFourUsesPmdgCommandsAndIndependentReadbacks()
    {
        var steps = Pmdg777ProcedureLibrary.EngineStartSequence.Steps
            .ToDictionary(step => step.Id);

        Assert.AreEqual("pmdg777 secondary engine display", steps["secondary-engine-display"].Command);
        Assert.AreEqual("pmdg777 engine two fuel control run", steps["engine-two-fuel-control"].Command);
        Assert.AreEqual("pmdg777 engine one fuel control run", steps["engine-one-fuel-control"].Command);

        var state = new AircraftState();
        Assert.IsFalse(steps["secondary-engine-display"].IsComplete(state));
        state.Pmdg777SecondaryEngineDisplaySelected = true;
        Assert.IsTrue(steps["secondary-engine-display"].IsComplete(state));
        Assert.IsFalse(steps["engine-two-fuel-control"].IsComplete(state));
        state.Pmdg777EngineTwoFuelControlRun = true;
        Assert.IsTrue(steps["engine-two-fuel-control"].IsComplete(state));
    }

    [TestMethod]
    public void CaptainStartSelectorsAdvanceFromTelemetry()
    {
        var steps = Pmdg777ProcedureLibrary.EngineStartSequence.Steps
            .ToDictionary(step => step.Id);
        var state = new AircraftState();

        Assert.AreEqual(CrewRole.Captain, steps["engine-two-selector"].AssignedRole);
        Assert.IsFalse(steps["engine-two-selector"].IsComplete(state));
        state.Pmdg777EngineTwoStartSelectorStart = true;
        Assert.IsTrue(steps["engine-two-selector"].IsComplete(state));
    }

    [TestMethod]
    public void FlowFourRequiresActualPushbackMovement()
    {
        var step = Pmdg777ProcedureLibrary.EngineStartSequence.Steps.First();
        var state = new AircraftState { OnGround = true };

        Assert.AreEqual("pushback-underway", step.Id);
        Assert.IsFalse(step.IsComplete(state));
        state.GroundSpeedKnots = 0.2;
        Assert.IsTrue(step.IsComplete(state));
    }
}
