using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Checklists;
using Msfs2024Ai.Copilot.Domain;
using Msfs2024Ai.Copilot.Procedures;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class Pmdg777FlowOneTests
{
    private static readonly string[] ExpectedFlowIds =
    {
        "power-up-initial-setup",
        "flight-computer-preflight",
        "apu-start-pushback",
        "engine-start-sequence",
        "after-start-taxi",
        "before-takeoff",
        "takeoff-climb",
        "cruise",
        "descent-preparation",
        "approach-landing",
        "after-landing-taxi",
        "parking-shutdown"
    };

    [TestMethod]
    public void CompleteGateToGateCatalogIsVisibleWithFlowOnePowerUpActionsEnabled()
    {
        var flows = Pmdg777ProcedureLibrary.GateToGate;

        CollectionAssert.AreEqual(ExpectedFlowIds, flows.Select(flow => flow.Id).ToArray());
        Assert.AreEqual(12, Pmdg777ChecklistLibrary.GateToGate.Count);
        Assert.IsTrue(flows.All(flow => flow.Steps.Count > 0));
        var flowOne = Pmdg777ProcedureLibrary.PowerUpAndPreliminaryPreflight;
        Assert.AreEqual(4, flowOne.AutomaticStepCount);
        CollectionAssert.AreEqual(
            new[]
            {
                "pmdg777 battery on",
                "pmdg777 primary external power on",
                "pmdg777 secondary external power on",
                "pmdg777 adiru on"
            },
            flowOne.Steps
                .Where(step => step.Command != null)
                .Select(step => step.Command)
                .ToArray());
        Assert.IsTrue(flows.All(flow =>
            Pmdg777ChecklistLibrary.FindForProcedure(flow.Id) != null));
    }

    [TestMethod]
    public void CompatibilityAliasesResolveToDedicated777Flows()
    {
        Assert.AreEqual("power-up-initial-setup", Pmdg777ProcedureLibrary.Find("cockpit-preparation")!.Id);
        Assert.AreEqual("apu-start-pushback", Pmdg777ProcedureLibrary.Find("before-start")!.Id);
        Assert.AreEqual("engine-start-sequence", Pmdg777ProcedureLibrary.Find("engine-start")!.Id);
        Assert.AreEqual("after-start-taxi", Pmdg777ProcedureLibrary.Find("taxi")!.Id);
        Assert.AreEqual("takeoff-climb", Pmdg777ProcedureLibrary.Find("climb-to-cruise")!.Id);
    }

    [TestMethod]
    public void FlightDeckRolesFollowTheCaptainPfFirstOfficerPmContract()
    {
        var engineStart = Pmdg777ProcedureLibrary.EngineStartSequence;
        Assert.AreEqual(
            CrewRole.Captain,
            engineStart.Steps.Single(step => step.Id == "engine-two-selector").AssignedRole);
        Assert.AreEqual(
            CrewRole.FirstOfficer,
            engineStart.Steps.Single(step => step.Id == "engine-two-fuel-control").AssignedRole);

        var beforeTaxi = Pmdg777ProcedureLibrary.BeforeTaxiAndTaxi;
        Assert.AreEqual(
            CrewRole.FirstOfficer,
            beforeTaxi.Steps.Single(step => step.Id == "flaps").AssignedRole);
        Assert.AreEqual(
            CrewRole.FirstOfficer,
            beforeTaxi.Steps.Single(step => step.Id == "taxi-lights").AssignedRole);

        var departure = Pmdg777ProcedureLibrary.TakeoffAndClimb;
        Assert.AreEqual(
            CrewRole.FirstOfficer,
            departure.Steps.Single(step => step.Id == "gear-up").AssignedRole);
        Assert.AreEqual(
            CrewRole.FirstOfficer,
            departure.Steps.Single(step => step.Id == "flap-retraction").AssignedRole);

        var approach = Pmdg777ProcedureLibrary.ApproachAndLanding;
        Assert.AreEqual(
            CrewRole.FirstOfficer,
            approach.Steps.Single(step => step.Id == "gear-down").AssignedRole);
        Assert.AreEqual(
            CrewRole.FirstOfficer,
            approach.Steps.Single(step => step.Id == "flaps-schedule").AssignedRole);
    }

    [TestMethod]
    public void FlowOneContainsTheOrderedReadbackBackedPowerUpSequence()
    {
        var flow = Pmdg777ProcedureLibrary.PowerUpAndPreliminaryPreflight;
        var checklist = Pmdg777ChecklistLibrary.FindForProcedure(flow.Id);

        Assert.AreEqual("power-up-initial-setup", flow.Id);
        Assert.AreEqual(4, flow.AutomaticStepCount);
        CollectionAssert.AreEqual(
            new[]
            {
                "battery-on",
                "primary-external-power-on",
                "secondary-external-power-on",
                "bus-ties-auto",
                "hydraulic-starting-state",
                "wipers-off",
                "gear-down",
                "alternate-flaps-off",
                "adiru-on"
            },
            flow.Steps.Select(step => step.Id).ToArray());
        var battery = flow.Steps[0];
        Assert.AreEqual("battery-on", battery.Id);
        Assert.AreEqual(ProcedureStepKind.AutomaticAction, battery.Kind);
        Assert.AreEqual(CrewRole.FirstOfficer, battery.AssignedRole);
        Assert.AreEqual("pmdg777 battery on", battery.Command);
        Assert.IsTrue(flow.Steps.All(step => step.AssignedRole == CrewRole.FirstOfficer));
        CollectionAssert.AreEqual(
            new[] { 3d, 5d, 5d, 3d, 4d, 3d, 3d, 3d, 5d },
            flow.Steps.Select(step => step.MinimumDuration.TotalSeconds).ToArray());
        Assert.IsNotNull(checklist);
        Assert.AreEqual(flow.Id, checklist!.ProcedureId);
    }

    [TestMethod]
    public void FlowOneReadbacksFollowThePmdgTutorialStartingState()
    {
        var state = ReadyState();
        Assert.IsTrue(Pmdg777ProcedureLibrary.PowerUpAndPreliminaryPreflight.Steps
            .All(step => step.IsComplete(state)));
    }

    [TestMethod]
    public void FlowOneDoesNotAcceptMissingSdkData()
    {
        var step = Pmdg777ProcedureLibrary.PowerUpAndPreliminaryPreflight.Steps[0];

        Assert.IsFalse(step.IsComplete(new AircraftState { Title = "777-300ER" }));
    }

    private static AircraftState ReadyState() =>
        new()
        {
            Title = "777-300ER",
            Pmdg777SdkDataReady = true,
            Pmdg777BatteryOn = true,
            Pmdg777BusTiesAuto = true,
            Pmdg777HydraulicPanelSafe = true,
            Pmdg777WipersOff = true,
            Pmdg777GearLeverDown = true,
            Pmdg777AlternateFlapsOff = true,
            Pmdg777ExternalPowerAvailable = true,
            Pmdg777ExternalPowerOn = true,
            Pmdg777PrimaryExternalPowerAvailable = true,
            Pmdg777SecondaryExternalPowerAvailable = true,
            Pmdg777PrimaryExternalPowerOn = true,
            Pmdg777SecondaryExternalPowerOn = true,
            ParkingBrakeSet = true,
            Pmdg777NavigationLightOn = true,
            Pmdg777GroundAirConfigurationSet = true,
            Pmdg777AdiruOn = true,
            Pmdg777EmergencyLightsArmed = true
        };
}
