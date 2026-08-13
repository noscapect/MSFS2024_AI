using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Checklists;
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
    public void CompleteGateToGateCatalogIsVisibleAndReadOnly()
    {
        var flows = Pmdg777ProcedureLibrary.GateToGate;

        CollectionAssert.AreEqual(ExpectedFlowIds, flows.Select(flow => flow.Id).ToArray());
        Assert.AreEqual(12, Pmdg777ChecklistLibrary.GateToGate.Count);
        Assert.IsTrue(flows.All(flow => flow.Steps.Count > 0));
        Assert.IsTrue(flows.All(flow => flow.AutomaticStepCount == 0));
        Assert.IsTrue(flows.SelectMany(flow => flow.Steps)
            .All(step => string.IsNullOrWhiteSpace(step.Command)));
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
    public void FlowOneIsReadOnlyAndHasItsOwnChecklist()
    {
        var flow = Pmdg777ProcedureLibrary.PowerUpAndPreliminaryPreflight;
        var checklist = Pmdg777ChecklistLibrary.FindForProcedure(flow.Id);

        Assert.AreEqual("power-up-initial-setup", flow.Id);
        Assert.AreEqual(0, flow.AutomaticStepCount);
        Assert.IsFalse(flow.Steps.Any(step => !string.IsNullOrWhiteSpace(step.Command)));
        Assert.IsNotNull(checklist);
        Assert.AreEqual(flow.Id, checklist!.ProcedureId);
    }

    [TestMethod]
    public void FlowOneReadbacksFollowThePmdgTutorialStartingState()
    {
        var state = ReadyState();
        var incomplete = Pmdg777ProcedureLibrary.PowerUpAndPreliminaryPreflight.Steps
            .Where(step => step.Kind == ProcedureStepKind.Observe)
            .Where(step => !step.IsComplete(state))
            .Select(step => step.Id)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), incomplete);
    }

    [TestMethod]
    public void FlowOneDoesNotAcceptMissingSdkData()
    {
        var step = Pmdg777ProcedureLibrary.PowerUpAndPreliminaryPreflight.Steps
            .Single(item => item.Id == "sdk-data-ready");

        Assert.IsFalse(step.IsComplete(new AircraftState { Title = "777-300ER" }));
    }

    private static AircraftState ReadyState() =>
        new()
        {
            Title = "777-300ER",
            Pmdg777SdkDataReady = true,
            Pmdg777BatteryOn = true,
            Pmdg777HydraulicPanelSafe = true,
            Pmdg777WipersOff = true,
            Pmdg777GearLeverDown = true,
            Pmdg777AlternateFlapsOff = true,
            Pmdg777ExternalPowerAvailable = true,
            Pmdg777ExternalPowerOn = true,
            ParkingBrakeSet = true,
            Pmdg777NavigationLightOn = true,
            Pmdg777GroundAirConfigurationSet = true,
            Pmdg777AdiruOn = true,
            Pmdg777EmergencyLightsArmed = true
        };
}
