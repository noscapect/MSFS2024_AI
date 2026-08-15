using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Domain;
using Msfs2024Ai.Copilot.Procedures;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class Pmdg777FlowTwoTests
{
    [TestMethod]
    public void FlowTwoFollowsBoeingPreflightOrder()
    {
        var flow = Pmdg777ProcedureLibrary.FlightComputerAndPreFlight;

        CollectionAssert.AreEqual(
            new[]
            {
                "electrical-power",
                "captain-fd-qnh",
                "captain-displays",
                "captain-parking-brake",
                "captain-uft-setup",
                "captain-cdu-ident-pos-init",
                "captain-cdu-route",
                "captain-cdu-performance",
                "captain-cdu-takeoff-reference",
                "captain-ifr-clearance",
                "fo-overhead-electrical-hydraulic",
                "fo-overhead-engine-fuel-fire",
                "fo-fire-overheat-test",
                "fo-overhead-lights",
                "fo-overhead-air",
                "fo-flight-director-on",
                "fo-oxygen-test",
                "fo-instruments",
                "transponder-standby",
                "console-starting-configuration",
                "fo-radios-audio",
                "irs-aligned",
                "preflight-checklist"
            },
            flow.Steps.Select(step => step.Id).ToArray());

        Assert.AreEqual(9, flow.AutomaticStepCount);
        Assert.AreEqual(9, flow.ManualStepCount);
        Assert.IsFalse(flow.Steps.Any(step =>
            step.Kind == ProcedureStepKind.ManualAction
            && step.AssignedRole == CrewRole.FirstOfficer),
            "Flow 2 must never use manual confirmation as a placeholder for virtual-FO work.");
        Assert.IsFalse(flow.Steps.Any(step => step.Id == "mcp-initial-altitude"),
            "The initial MCP altitude belongs to the Before Start procedure in the PMDG checklist.");
    }

    [TestMethod]
    public void CaptainOwnsFlightDeckSetupAndFlightComputerProgramming()
    {
        var flow = Pmdg777ProcedureLibrary.FlightComputerAndPreFlight;
        var captainSteps = new[]
        {
            "captain-fd-qnh",
            "captain-displays",
            "captain-parking-brake",
            "captain-uft-setup",
            "captain-cdu-ident-pos-init",
            "captain-cdu-route",
            "captain-cdu-performance",
            "captain-cdu-takeoff-reference",
            "captain-ifr-clearance"
        };

        Assert.IsTrue(captainSteps.All(id =>
            flow.Steps.Single(step => step.Id == id).AssignedRole == CrewRole.Captain));
        Assert.AreEqual(CrewRole.FirstOfficer,
            flow.Steps.Single(step => step.Id == "preflight-checklist").AssignedRole);
    }

    [TestMethod]
    public void FirstOfficerAutomaticActionsUseDedicatedReadbacks()
    {
        var flow = Pmdg777ProcedureLibrary.FlightComputerAndPreFlight;
        var commands = flow.Steps
            .Where(step => step.Kind == ProcedureStepKind.AutomaticAction)
            .ToDictionary(step => step.Id, step => step.Command);

        Assert.AreEqual("pmdg777 electrical hydraulic preflight", commands["fo-overhead-electrical-hydraulic"]);
        Assert.AreEqual("pmdg777 engine fuel fire preflight", commands["fo-overhead-engine-fuel-fire"]);
        Assert.AreEqual("pmdg777 fire overheat test", commands["fo-fire-overheat-test"]);
        Assert.AreEqual("pmdg777 exterior lights preflight", commands["fo-overhead-lights"]);
        Assert.AreEqual("pmdg777 air panel preflight", commands["fo-overhead-air"]);
        Assert.AreEqual("pmdg777 fo oxygen test", commands["fo-oxygen-test"]);
        Assert.AreEqual("pmdg777 instruments preflight", commands["fo-instruments"]);
        Assert.AreEqual("pmdg777 fo flight director on", commands["fo-flight-director-on"]);
        Assert.AreEqual("pmdg777 transponder standby", commands["transponder-standby"]);
    }

    [TestMethod]
    public void EmergencyLightGuardCannotPassWhileOpen()
    {
        var guardStep = Pmdg777ProcedureLibrary.FlightComputerAndPreFlight.Steps
            .Single(step => step.Id == "fo-overhead-electrical-hydraulic");
        var state = CompleteFirstOfficerState();
        state.Pmdg777EmergencyLightsGuardClosed = false;

        Assert.IsFalse(guardStep.IsComplete(state));
        state.Pmdg777EmergencyLightsGuardClosed = true;
        Assert.IsTrue(guardStep.IsComplete(state));
    }

    [TestMethod]
    public void RadioAndAudioStepNeverSendsControls()
    {
        var step = Pmdg777ProcedureLibrary.FlightComputerAndPreFlight.Steps
            .Single(item => item.Id == "fo-radios-audio");

        Assert.AreEqual(ProcedureStepKind.Observe, step.Kind);
        Assert.IsNull(step.Command);
        Assert.IsTrue(step.IsComplete(new AircraftState
        {
            SayIntentionsAtcActive = true,
            Pmdg777TransponderAltitudeSourceNormal = true
        }));
    }

    [TestMethod]
    public void ExactPmdgStateCompletesAllObservableAndAutomaticGates()
    {
        var state = CompleteFirstOfficerState();
        state.Pmdg777SdkDataReady = true;
        state.Pmdg777BatteryOn = true;
        state.Pmdg777PrimaryExternalPowerOn = true;
        state.ParkingBrakeSet = true;
        state.Pmdg777FmcCruiseAltitude = 35000;
        state.Pmdg777FmcDistanceToDestination = 2200;
        state.Pmdg777FmcFlightNumber = "PMDG777";
        state.Pmdg777FmcPerformanceInputComplete = true;
        state.Pmdg777FmcTakeoffFlaps = 15;
        state.Pmdg777FmcV1 = 140;
        state.Pmdg777FmcVr = 145;
        state.Pmdg777FmcV2 = 150;
        state.AtcClearedIfr = true;

        Assert.IsTrue(Pmdg777ProcedureLibrary.FlightComputerAndPreFlight.Steps
            .Where(step => step.Kind != ProcedureStepKind.ManualAction)
            .All(step => step.IsComplete(state)));
    }

    [TestMethod]
    public void SignsUsePreflightOffThenBeforeStartAutoSchedule()
    {
        var preflightSigns = Pmdg777ProcedureLibrary.FlightComputerAndPreFlight.Steps
            .Single(step => step.Id == "fo-overhead-lights");
        var beforeStartSigns = Pmdg777ProcedureLibrary.BeforeStartAndPushback.Steps
            .Single(step => step.Id == "fo-seatbelts-auto");
        var state = CompleteFirstOfficerState();

        Assert.IsTrue(preflightSigns.IsComplete(state));
        state.Pmdg777NoSmokingAuto = false;
        Assert.IsFalse(preflightSigns.IsComplete(state));

        Assert.AreEqual(ProcedureStepKind.AutomaticAction, beforeStartSigns.Kind);
        Assert.AreEqual(CrewRole.FirstOfficer, beforeStartSigns.AssignedRole);
        Assert.AreEqual("pmdg777 seatbelts auto", beforeStartSigns.Command);
        Assert.IsFalse(beforeStartSigns.IsComplete(state));
        state.Pmdg777SeatBeltsAuto = true;
        Assert.IsTrue(beforeStartSigns.IsComplete(state));
    }

    [TestMethod]
    public void FlowThreeHasNoManualFirstOfficerPlaceholders()
    {
        var flow = Pmdg777ProcedureLibrary.BeforeStartAndPushback;

        Assert.IsFalse(flow.Steps.Any(step =>
            step.Kind == ProcedureStepKind.ManualAction
            && step.AssignedRole == CrewRole.FirstOfficer));
    }

    [TestMethod]
    public void FlowThreeDoorsUseAircraftTelemetryInsteadOfConfirmation()
    {
        var step = Pmdg777ProcedureLibrary.BeforeStartAndPushback.Steps
            .Single(item => item.Id == "doors-cargo");
        var open = new AircraftState
        {
            Exits = new[] { new AircraftExitState(1, 0, 100, 1, 1, 1) }
        };
        var closed = new AircraftState
        {
            Exits = new[] { new AircraftExitState(1, 0, 0, 1, 1, 1) }
        };

        Assert.AreEqual(ProcedureStepKind.Observe, step.Kind);
        Assert.AreEqual(CrewRole.FirstOfficer, step.AssignedRole);
        Assert.IsFalse(step.IsComplete(open));
        Assert.IsTrue(step.IsComplete(closed));
    }

    [TestMethod]
    public void FlowThreeFirstOfficerSystemsUseCommandsAndIndependentReadbacks()
    {
        var steps = Pmdg777ProcedureLibrary.BeforeStartAndPushback.Steps
            .ToDictionary(step => step.Id);

        Assert.AreEqual("pmdg777 apu start", steps["apu-start"].Command);
        Assert.AreEqual("pmdg777 apu power air", steps["apu-power-air"].Command);
        Assert.AreEqual("pmdg777 external power off", steps["ground-services-disconnect"].Command);
        Assert.AreEqual("pmdg777 hydraulics before start", steps["fo-hydraulics-before-start"].Command);
        Assert.AreEqual("pmdg777 fuel pumps before start", steps["fo-fuel-pumps-before-start"].Command);
        Assert.AreEqual("pmdg777 transponder xpndr", steps["fo-transponder-xpndr"].Command);
        Assert.AreEqual(ProcedureStepKind.Observe, steps["before-start-checklist"].Kind);

        var state = new AircraftState();
        Assert.IsFalse(steps["apu-start"].IsComplete(state));
        state.Pmdg777ApuRunning = true;
        Assert.IsTrue(steps["apu-start"].IsComplete(state));
        Assert.IsFalse(steps["apu-power-air"].IsComplete(state));
        state.Pmdg777ApuGeneratorPowerEstablished = true;
        state.Pmdg777ApuBleedAirAvailable = true;
        Assert.IsTrue(steps["apu-power-air"].IsComplete(state));
        Assert.IsTrue(steps["ground-services-disconnect"].IsComplete(state));
        state.Exits = new[] { new AircraftExitState(1, 0, 0, 1, 1, 1) };
        state.Pmdg777SeatBeltsAuto = true;
        state.Pmdg777HydraulicsBeforeStart = true;
        state.Pmdg777FuelPumpsBeforeStart = true;
        state.Pmdg777BeaconOn = true;
        Assert.IsFalse(steps["before-start-checklist"].IsComplete(state));
        state.Pmdg777TransponderXpndr = true;
        Assert.IsTrue(steps["before-start-checklist"].IsComplete(state));
    }

    [TestMethod]
    public void FlowThreeGroupedActionsIncludeHumanPacedVerificationTime()
    {
        var steps = Pmdg777ProcedureLibrary.BeforeStartAndPushback.Steps
            .ToDictionary(step => step.Id);

        Assert.IsTrue(steps["apu-power-air"].MinimumDuration >= TimeSpan.FromSeconds(6));
        Assert.IsTrue(steps["ground-services-disconnect"].MinimumDuration >= TimeSpan.FromSeconds(5));
        Assert.IsTrue(steps["fo-hydraulics-before-start"].MinimumDuration >= TimeSpan.FromSeconds(10));
        Assert.IsTrue(steps["fo-fuel-pumps-before-start"].MinimumDuration >= TimeSpan.FromSeconds(10));
    }

    [TestMethod]
    public void FlowThreeUsesCanonicalSayIntentionsPushbackRequestStep()
    {
        var step = Pmdg777ProcedureLibrary.BeforeStartAndPushback.Steps
            .Single(item => item.Id == "captain-pushback-clearance");

        Assert.AreEqual(ProcedureStepKind.ManualAction, step.Kind);
        Assert.AreEqual(CrewRole.Captain, step.AssignedRole);
        Assert.IsTrue(SayIntentions.SayIntentionsCopilotActionMap.TryGetActionName(
            step.Id,
            out var action));
        Assert.AreEqual("preflight_request_push_and_start", action);
    }

    [TestMethod]
    public void FlowThreeCannotFinishUntilPushbackActuallyMoves()
    {
        var step = Pmdg777ProcedureLibrary.BeforeStartAndPushback.Steps
            .Single(item => item.Id == "pushback-underway");
        var state = new AircraftState { OnGround = true };

        Assert.AreEqual(ProcedureStepKind.Observe, step.Kind);
        Assert.IsFalse(step.IsComplete(state));
        state.ParkingBrakeSet = false;
        state.GroundSpeedKnots = 0.2;
        Assert.IsTrue(step.IsComplete(state));
    }

    [TestMethod]
    public void WheelChockLimitationUsesReadbackInsteadOfConfirmation()
    {
        var step = Pmdg777ProcedureLibrary.BeforeStartAndPushback.Steps
            .Single(item => item.Id == "captain-remove-wheel-chocks");
        var state = new AircraftState { Pmdg777WheelChocksSet = true };

        Assert.AreEqual(ProcedureStepKind.ManualAction, step.Kind);
        Assert.AreEqual(CrewRole.Captain, step.AssignedRole);
        Assert.IsFalse(step.IsComplete(state));
        state.Pmdg777WheelChocksSet = false;
        Assert.IsTrue(step.IsComplete(state));
    }

    private static AircraftState CompleteFirstOfficerState() =>
        new()
        {
            Pmdg777IfePassengerSeatsOn = true,
            Pmdg777CabinUtilityOn = true,
            Pmdg777EmergencyLightsArmed = true,
            Pmdg777EmergencyLightsGuardClosed = true,
            Pmdg777NavigationLightOn = true,
            Pmdg777ThrustAsymmetryCompensationAuto = true,
            Pmdg777PrimaryFlightComputersAuto = true,
            Pmdg777PrimaryFlightComputersGuardClosed = true,
            Pmdg777ApuGeneratorSwitchOn = true,
            Pmdg777EngineGeneratorOneSwitchOn = true,
            Pmdg777EngineGeneratorTwoSwitchOn = true,
            Pmdg777BackupGeneratorOneSwitchOn = true,
            Pmdg777BackupGeneratorTwoSwitchOn = true,
            Pmdg777BusTiesAuto = true,
            Pmdg777WipersOff = true,
            Pmdg777PassengerOxygenNormal = true,
            Pmdg777PassengerOxygenGuardClosed = true,
            Pmdg777LeftSideWindowHeatOn = true,
            Pmdg777LeftForwardWindowHeatOn = true,
            Pmdg777RightForwardWindowHeatOn = true,
            Pmdg777RightSideWindowHeatOn = true,
            Pmdg777LeftEnginePrimaryHydraulicPumpOn = true,
            Pmdg777RightEnginePrimaryHydraulicPumpOn = true,
            Pmdg777HydraulicPanelSafe = true,
            Pmdg777FirePanelNormal = true,
            Pmdg777EngineControlPanelNormal = true,
            Pmdg777FuelPanelPreflight = true,
            Pmdg777FuelToRemainSelectorIn = true,
            Pmdg777AntiIceAuto = true,
            Pmdg777ExteriorLightsPreflight = true,
            Pmdg777NoSmokingAuto = true,
            Pmdg777SeatBeltsOff = true,
            Pmdg777AirPanelPreflight = true,
            Pmdg777TemperatureControlsPreflight = true,
            Pmdg777FireOverheatTestComplete = true,
            Pmdg777FirstOfficerOxygenTestComplete = true,
            Pmdg777AutobrakeRto = true,
            Pmdg777FirstOfficerNdMap = true,
            Pmdg777TransponderAltitudeSourceNormal = true,
            Pmdg777FirstOfficerFlightDirectorOn = true,
            Pmdg777FirstOfficerSourcesNormal = true,
            Pmdg777FirstOfficerDisplaysReady = true,
            Pmdg777SpeedbrakeDown = true,
            Pmdg777FlapsUp = true,
            Pmdg777FuelControlsCutoff = true,
            Pmdg777TransponderStandby = true,
            Pmdg777IrsAligned = true,
        };
}
