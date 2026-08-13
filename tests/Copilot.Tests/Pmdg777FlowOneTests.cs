using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Checklists;
using Msfs2024Ai.Copilot.Procedures;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class Pmdg777FlowOneTests
{
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
