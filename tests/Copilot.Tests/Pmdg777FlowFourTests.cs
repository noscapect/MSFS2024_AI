using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Domain;
using Msfs2024Ai.Copilot.Procedures;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class Pmdg777FlowFourTests
{
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
    public void CruiseCompletesFromTelemetryWithoutConfirmation()
    {
        var flow = Pmdg777ProcedureLibrary.Cruise;

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
