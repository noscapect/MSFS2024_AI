using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using Msfs2024Ai.Copilot.AircraftAdapters;
using Msfs2024Ai.Copilot.AircraftAdapters.IniBuildsA310;
using Msfs2024Ai.Copilot.Domain;
using Msfs2024Ai.Copilot.Procedures;
using Msfs2024Ai.Copilot.Settings;
using Msfs2024Ai.Copilot.SimBrief;
using Msfs2024Ai.Copilot.Voice;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class A310ProcedureLibraryTests
{
    [TestMethod]
    public void A310RoutesToDedicatedTwelveFlowCatalogAndChecklists()
    {
        var state = new AircraftState { Title = "Airbus A310-300" };
        var flows = ProcedureCatalog.ForAircraft(state);

        Assert.IsTrue(state.IsIniBuildsA310);
        Assert.IsTrue(state.IsSupportedAircraft);
        Assert.AreEqual("iniBuilds A310-300", state.AircraftFamilyLabel);
        Assert.AreEqual(12, flows.Count);
        Assert.AreSame(A310ProcedureLibrary.PowerUpAndInitialSetup, flows[0]);
        Assert.AreSame(A310ProcedureLibrary.ParkingAndShutdown, flows[11]);
        Assert.IsTrue(flows.All(flow =>
            ProcedureCatalog.FindChecklist(state, flow.Id) != null));
    }

    [TestMethod]
    public void InitialFlowKeepsFireTestBeforeIrsAndCapturesThreeBatteries()
    {
        var steps = A310ProcedureLibrary.PowerUpAndInitialSetup.Steps.ToList();
        var batteries = steps.Single(step => step.Id == "batteries-auto");
        var hydraulics = steps.Single(step => step.Id == "hydraulic-safe");

        Assert.AreEqual(ProcedureStepKind.AutomaticAction, batteries.Kind);
        Assert.AreEqual(CrewRole.FirstOfficer, batteries.AssignedRole);
        Assert.AreEqual("a310 batteries auto", batteries.Command);
        Assert.IsFalse(batteries.IsComplete(new AircraftState
        {
            Title = "Airbus A310-300",
            Battery1On = true,
            Battery2On = true,
            Battery3On = false
        }));
        Assert.IsTrue(batteries.IsComplete(new AircraftState
        {
            Title = "Airbus A310-300",
            Battery1On = true,
            Battery2On = true,
            Battery3On = true
        }));
        Assert.AreEqual(ProcedureStepKind.Observe, hydraulics.Kind);
        Assert.IsFalse(hydraulics.IsComplete(new AircraftState()));
        Assert.IsTrue(hydraulics.IsComplete(new AircraftState
        {
            A310HydraulicPanelSafe = true
        }));
        Assert.IsTrue(
            steps.FindIndex(step => step.Id == "apu-fire-test")
            < steps.FindIndex(step => step.Id == "irs-nav"));
    }

    [TestMethod]
    public void EngineStartUsesEngineTwoFirstAndFuelAtTwentyPercentN2()
    {
        var steps = A310ProcedureLibrary.EngineStartSequence.Steps.ToList();

        CollectionAssert.AreEqual(
            new[] { "ignition-a-b", "fo-engine-two-starter", "fo-engine-one-starter" },
            steps.Where(step => step.Kind == ProcedureStepKind.AutomaticAction)
                .Select(step => step.Id)
                .ToArray());
        Assert.IsTrue(steps
            .Where(step => step.Kind == ProcedureStepKind.AutomaticAction)
            .All(step => step.Command!.StartsWith("a310 ", StringComparison.Ordinal)));

        Assert.IsTrue(
            steps.FindIndex(step => step.Id == "fo-engine-two-starter")
            < steps.FindIndex(step => step.Id == "fo-engine-one-starter"));
        StringAssert.Contains(
            steps.Single(step => step.Id == "fo-engine-two-fuel").Label,
            "20 percent N2");
        StringAssert.Contains(
            steps.Single(step => step.Id == "fo-engine-one-fuel").Label,
            "20 percent N2");

        var engineTwoRotation = steps.Single(step => step.Id == "engine-two-rotation");
        Assert.IsFalse(engineTwoRotation.IsComplete(new AircraftState
        {
            Engine2N1Percent = 25,
            Engine2N2Percent = 19.9
        }));
        Assert.IsTrue(engineTwoRotation.IsComplete(new AircraftState
        {
            Engine2N2Percent = 20
        }));

        var engineTwoFuel = steps.Single(step => step.Id == "fo-engine-two-fuel");
        Assert.AreEqual(ProcedureStepKind.ManualAction, engineTwoFuel.Kind);
        Assert.AreEqual(CrewRole.Captain, engineTwoFuel.AssignedRole);
        Assert.IsTrue(engineTwoFuel.IsComplete(new AircraftState
        {
            A310Engine2FuelLeverOn = true
        }));
    }

    [TestMethod]
    public void BeforeTakeoffCannotBeginAwayFromHoldingPoint()
    {
        var first = A310ProcedureLibrary.BeforeTakeoff.Steps[0];

        Assert.AreEqual("holding-short", first.Id);
        Assert.IsFalse(first.IsComplete(new AircraftState
        {
            Title = "Airbus A310-300",
            OnGround = true,
            BeforeTakeoffHoldEligible = false
        }));
        Assert.IsTrue(first.IsComplete(new AircraftState
        {
            Title = "Airbus A310-300",
            OnGround = true,
            BeforeTakeoffHoldEligible = true
        }));
    }

    [TestMethod]
    public void BeforeTakeoffBrakeFanReviewDoesNotBlockTheFlow()
    {
        var brakeFans = A310ProcedureLibrary.BeforeTakeoff.Steps
            .Single(step => step.Id == "brake-fans");

        Assert.AreEqual(ProcedureStepKind.Observe, brakeFans.Kind);
        Assert.IsTrue(brakeFans.IsComplete(new AircraftState()));
    }

    [TestMethod]
    public void BeforeTakeoffAircraftConfigurationUsesNativeAutomaticActions()
    {
        var steps = A310ProcedureLibrary.BeforeTakeoff.Steps;
        var expected = new Dictionary<string, string>
        {
            ["takeoff-lights"] = "a310 takeoff-lights",
            ["ignition-takeoff"] = "a310 ignition takeoff",
            ["packs-takeoff"] = "a310 packs on",
            ["tcas-tara"] = "a310 tcas tara"
        };

        foreach (var item in expected)
        {
            var step = steps.Single(candidate => candidate.Id == item.Key);
            Assert.AreEqual(ProcedureStepKind.AutomaticAction, step.Kind);
            Assert.AreEqual(item.Value, step.Command);
        }

        var ready = new AircraftState
        {
            A310TakeoffExteriorLightsSet = true,
            A310IgnitionContinuousRelight = true,
            A310PacksOn = true,
            A310TcasTaRaSet = true
        };
        Assert.IsTrue(steps.Single(step => step.Id == "takeoff-lights").IsComplete(ready));
        Assert.IsTrue(steps.Single(step => step.Id == "ignition-takeoff").IsComplete(ready));
        Assert.IsTrue(steps.Single(step => step.Id == "packs-takeoff").IsComplete(ready));
        Assert.IsTrue(steps.Single(step => step.Id == "tcas-tara").IsComplete(ready));
    }

    [TestMethod]
    public void TakeoffFlowIncludesA310GearPackAndSlatSequence()
    {
        var ids = A310ProcedureLibrary.TakeoffAndClimb.Steps
            .Select(step => step.Id)
            .ToArray();

        CollectionAssert.IsSubsetOf(
            new[]
            {
                "fo-100-knots", "v1", "rotate", "fo-gear-up", "gear-off",
                "flaps-zero", "slats-zero", "packs-on", "altimeters-standard"
            },
            ids);
        var automaticCommands = A310ProcedureLibrary.TakeoffAndClimb.Steps
            .Where(step => step.Kind == ProcedureStepKind.AutomaticAction)
            .ToDictionary(step => step.Id, step => step.Command);
        Assert.AreEqual("a310 gear up", automaticCommands["fo-gear-up"]);
        Assert.AreEqual("a310 speedbrake disarm", automaticCommands["fo-ground-spoilers-disarm"]);
        Assert.AreEqual("a310 packs on", automaticCommands["packs-on"]);
        Assert.AreEqual("a310 climb-lights", automaticCommands["climb-lights"]);
        Assert.AreEqual("a310 apu off", automaticCommands["apu-climb"]);
        Assert.AreEqual("a310 altimeters standard", automaticCommands["altimeters-standard"]);
        Assert.AreEqual("a310 landing-lights retract", automaticCommands["landing-lights-retract"]);

        var blockingManualSteps = A310ProcedureLibrary.TakeoffAndClimb.Steps
            .Where(step => step.Kind == ProcedureStepKind.ManualAction)
            .Select(step => step.Id)
            .ToArray();
        CollectionAssert.AreEqual(new[] { "slats-zero" }, blockingManualSteps);
    }

    [TestMethod]
    public void GearStepsUseTheA310NativeHandleInsteadOfStaleWheelPositions()
    {
        var gearUp = A310ProcedureLibrary.TakeoffAndClimb.Steps
            .Single(step => step.Id == "fo-gear-up");
        var gearDown = A310ProcedureLibrary.ApproachAndLanding.Steps
            .Single(step => step.Id == "fo-gear-down");

        var upWithStaleWheels = new AircraftState
        {
            Title = "Airbus A310-300",
            GearHandlePosition = 0,
            GearHandleDown = false,
            LeftGearPosition = 1,
            CenterGearPosition = 1,
            RightGearPosition = 1
        };
        var downWithStaleWheels = new AircraftState
        {
            Title = "Airbus A310-300",
            GearHandlePosition = 2,
            GearHandleDown = true,
            LeftGearPosition = 0,
            CenterGearPosition = 0,
            RightGearPosition = 0
        };

        Assert.IsFalse(upWithStaleWheels.GearUpVerified);
        Assert.IsTrue(gearUp.IsComplete(upWithStaleWheels));
        Assert.IsFalse(downWithStaleWheels.GearDownVerified);
        Assert.IsTrue(gearDown.IsComplete(downWithStaleWheels));
    }

    [TestMethod]
    public void ApproachUsesPublishedA310ConfigurationsAndSpeedLimits()
    {
        var flow = A310ProcedureLibrary.ApproachAndLanding;
        var labels = flow.Steps.ToDictionary(step => step.Id, step => step.Label);

        Assert.AreEqual("Slats 15 / Flaps 0", labels["slats-15"]);
        Assert.AreEqual("Slats 15 / Flaps 15", labels["flaps-15"]);
        Assert.AreEqual("Slats 15 / Flaps 20", labels["flaps-20"]);
        Assert.AreEqual("Slats 30 / Flaps 40", labels["flaps-40"]);

        var fast = new AircraftState { IndicatedAirspeedKnots = 181 };
        var safe = new AircraftState { IndicatedAirspeedKnots = 180 };
        Assert.IsFalse(flow.Steps.Single(step => step.Id == "flaps-40-speed").IsComplete(fast));
        Assert.IsTrue(flow.Steps.Single(step => step.Id == "flaps-40-speed").IsComplete(safe));

        var flaps15Point = flow.Steps.Single(step => step.Id == "flaps-15-point");
        Assert.IsFalse(flaps15Point.IsComplete(new AircraftState
        {
            IndicatedAirspeedKnots = 211,
            AltitudeAboveGroundFeet = 3000,
            ApproachDistanceToTouchdownNm = 12,
            ApproachFlaps2DistanceNm = 10
        }));
        Assert.IsTrue(flaps15Point.IsComplete(new AircraftState
        {
            IndicatedAirspeedKnots = 210,
            AltitudeAboveGroundFeet = 3000,
            ApproachDistanceToTouchdownNm = 12,
            ApproachFlaps2DistanceNm = 10
        }));
    }

    [TestMethod]
    public void ParkingPreservesApuFuelPumpUntilApuShutdown()
    {
        var flow = A310ProcedureLibrary.ParkingAndShutdown;

        var parkingPumps = flow.Steps.Single(step => step.Id == "fuel-pumps-parking");
        var finalApuPump = flow.Steps.Single(step => step.Id == "apu-fuel-pump-off");
        Assert.AreEqual(ProcedureStepKind.AutomaticAction, parkingPumps.Kind);
        Assert.AreEqual("a310 fuel-pumps parking", parkingPumps.Command);
        Assert.AreEqual(ProcedureStepKind.AutomaticAction, finalApuPump.Kind);
        Assert.AreEqual("a310 fuel-pumps parking", finalApuPump.Command);
        Assert.IsTrue(
            flow.Steps.ToList().IndexOf(finalApuPump)
            > flow.Steps.ToList().FindIndex(step => step.Id == "apu-off"));
    }

    [TestMethod]
    public void ApproachHasVerifiedAutomaticConfigurationActions()
    {
        var automatic = A310ProcedureLibrary.ApproachAndLanding.Steps
            .Where(step => step.Kind == ProcedureStepKind.AutomaticAction)
            .ToDictionary(step => step.Id, step => step.Command);

        Assert.AreEqual("a310 approach-lights", automatic["approach-signs-lights"]);
        Assert.AreEqual("a310 flaps 15-0", automatic["slats-15"]);
        Assert.AreEqual("a310 flaps 15-15", automatic["flaps-15"]);
        Assert.AreEqual("a310 gear down", automatic["fo-gear-down"]);
        Assert.AreEqual("a310 speedbrake arm", automatic["fo-spoilers-arm"]);
        Assert.AreEqual("a310 flaps 15-20", automatic["flaps-20"]);
        Assert.AreEqual("a310 flaps 30-40", automatic["flaps-40"]);
        Assert.IsFalse(A310ProcedureLibrary.ApproachAndLanding.Steps.Any(
            step => step.Kind == ProcedureStepKind.ManualAction));
    }

    [TestMethod]
    public void AfterLandingKeepsOnlyTaxiClearanceManual()
    {
        var flow = A310ProcedureLibrary.AfterLandingAndTaxi;
        CollectionAssert.AreEqual(
            new[] { "taxi-gate" },
            flow.Steps
                .Where(step => step.Kind == ProcedureStepKind.ManualAction)
                .Select(step => step.Id)
                .ToArray());
        CollectionAssert.IsSubsetOf(
            new[]
            {
                "after-landing-lights", "ignition-off", "apu-start",
                "spoilers-disarm", "transponder-standby", "radar-off", "flaps-retract"
            },
            flow.Steps
                .Where(step => step.Kind == ProcedureStepKind.AutomaticAction)
                .Select(step => step.Id)
                .ToArray());
    }

    [TestMethod]
    public void ShutdownManualStepsAreOnlyCrewDecisionsAndPowerTransfer()
    {
        var flow = A310ProcedureLibrary.ParkingAndShutdown;
        CollectionAssert.AreEqual(
            new[] { "fuel-levers-off", "secure-decision", "external-power-secure" },
            flow.Steps
                .Where(step => step.Kind == ProcedureStepKind.ManualAction)
                .Select(step => step.Id)
                .ToArray());

        var fuelLevers = flow.Steps.Single(step => step.Id == "fuel-levers-off");
        Assert.IsTrue(fuelLevers.IsComplete(new AircraftState
        {
            A310Engine1FuelLeverOn = false,
            A310Engine2FuelLeverOn = false
        }));
        Assert.IsFalse(fuelLevers.IsComplete(new AircraftState
        {
            A310Engine1FuelLeverOn = true,
            A310Engine2FuelLeverOn = false
        }));
    }

    [TestMethod]
    public void ValidatedA310InitialPanelActionsAreAutomatic()
    {
        var automatic = A310ProcedureLibrary.PowerUpAndInitialSetup.Steps
            .Where(step => step.Kind == ProcedureStepKind.AutomaticAction)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "batteries-auto", "wipers-radar-off", "apu-fire-test",
                "irs-nav", "oxygen-on", "annunciator-test", "initial-lights"
            },
            automatic.Select(step => step.Id).ToArray());
        StringAssert.Contains(
            A310ControlProfile.BatteryAutoCalculatorCode(1),
            $">L:{A310ControlProfile.Battery1State}");
        StringAssert.Contains(
            A310ControlProfile.BatteryAutoCalculatorCode(2),
            $">L:{A310ControlProfile.Battery2State}");
        StringAssert.Contains(
            A310ControlProfile.BatteryAutoCalculatorCode(3),
            $">L:{A310ControlProfile.Battery3State}");
        StringAssert.Contains(
            A310ControlProfile.SetCalculatorCode(A310ControlProfile.CaptainWiperState, 0),
            $">L:{A310ControlProfile.CaptainWiperState}");
        Assert.IsTrue(A310ControlProfile.Capabilities
            .Where(item => item.Id is not "aircraft-state" and not "engine-start")
            .All(item => item.Support != CapabilitySupport.Supported));
        Assert.AreEqual(
            CapabilitySupport.Supported,
            A310ControlProfile.Capabilities.Single(item => item.Id == "engine-start").Support);
    }

    [TestMethod]
    public void FlowTwoAutomatesDeterministicFirstOfficerPanelActions()
    {
        var flow = A310ProcedureLibrary.FlightComputerAndPreFlight;
        var automaticIds = flow.Steps
            .Where(step => step.Kind == ProcedureStepKind.AutomaticAction)
            .Select(step => step.Id)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "signs", "recorder-and-autoflight", "heat", "cargo-smoke-test",
                "emergency-exit", "egpws-test", "atc-radar-rudder", "fuel-pumps-on"
            },
            automaticIds);
        Assert.IsFalse(flow.Steps.Any(step =>
            step.AssignedRole == CrewRole.FirstOfficer
            && step.Kind == ProcedureStepKind.ManualAction));
        Assert.IsTrue(flow.Steps
            .Where(step => step.Kind == ProcedureStepKind.AutomaticAction)
            .All(step => step.Command!.StartsWith("a310 ", StringComparison.Ordinal)));
        var clearance = flow.Steps.Single(step => step.Id == "captain-ifr-clearance");
        Assert.AreEqual(ProcedureStepKind.ManualAction, clearance.Kind);
        Assert.AreEqual(CrewRole.Captain, clearance.AssignedRole);
    }

    [TestMethod]
    public void FuelPumpsAndNormalAfterStartIgnitionAreAutomated()
    {
        Assert.AreEqual(1, A310ControlProfile.IgnitionStartAValue);
        Assert.AreEqual(2, A310ControlProfile.IgnitionCrankValue);
        Assert.AreEqual(3, A310ControlProfile.IgnitionOffValue);
        Assert.AreEqual(12, A310ControlProfile.FuelPumpStates.Count);
        Assert.AreEqual(
            12,
            A310ControlProfile.FuelPumpStates.Distinct(StringComparer.Ordinal).Count());

        var fuelPumps = A310ProcedureLibrary.FlightComputerAndPreFlight.Steps
            .Single(step => step.Id == "fuel-pumps-on");
        Assert.AreEqual(ProcedureStepKind.AutomaticAction, fuelPumps.Kind);
        Assert.AreEqual("a310 fuel-pumps on", fuelPumps.Command);
        Assert.IsTrue(fuelPumps.IsComplete(new AircraftState { A310FuelPumpsOn = true }));

        var ignition = A310ProcedureLibrary.AfterStartAndTaxi.Steps
            .Single(step => step.Id == "ignition-normal");
        Assert.AreEqual(ProcedureStepKind.AutomaticAction, ignition.Kind);
        Assert.AreEqual("a310 ignition off", ignition.Command);
        Assert.IsTrue(ignition.IsComplete(new AircraftState { A310IgnitionOff = true }));
    }

    [TestMethod]
    public void OperationalRuntimeStatesMatchTheirRegisteredOffsetOrder()
    {
        var expected = new[]
        {
            A310ControlProfile.ApuMasterState,
            A310ControlProfile.ApuStartButtonState,
            A310ControlProfile.ApuAvailableState,
            A310ControlProfile.ApuBleedState,
            A310ControlProfile.ApuGeneratorState,
            A310ControlProfile.IgnitionSelectorState,
            A310ControlProfile.Pack1State,
            A310ControlProfile.Pack2State,
            A310ControlProfile.Engine1StarterState,
            A310ControlProfile.Engine2StarterState,
            A310ControlProfile.Engine1FuelLeverState,
            A310ControlProfile.Engine2FuelLeverState
        }
        .Concat(A310ControlProfile.FuelPumpStates)
        .Concat(new[]
        {
            A310ControlProfile.WeatherRadarModeState,
            A310ControlProfile.AutobrakeMaxState,
            A310ControlProfile.SpoilersArmedState,
            A310ControlProfile.GearHandleState,
            A310ControlProfile.CaptainAltimeterStandardState,
            A310ControlProfile.FirstOfficerAltimeterStandardState,
            A310ControlProfile.StandbyAltimeterStandardState
        })
        .ToArray();

        CollectionAssert.AreEqual(
            expected,
            A310ControlProfile.OperationalRuntimeStates.ToArray());
        Assert.AreEqual(31, expected.Length);
    }

    [TestMethod]
    public void IgnitionOffVerificationFallsBackToStandardEngineTelemetry()
    {
        var verifier = typeof(CopilotService).GetMethod(
            "IsA310IgnitionOff",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(verifier);
        Assert.AreEqual(
            true,
            verifier!.Invoke(null, new object?[] { 1f, 0d }),
            "The standard two-engine OFF indication must override a stale START A LVar.");
        Assert.AreEqual(
            true,
            verifier.Invoke(null, new object?[] { 3f, 1d }),
            "The native selector OFF indication remains valid.");
        Assert.AreEqual(
            false,
            verifier.Invoke(null, new object?[] { 1f, 1d }),
            "START A must not verify as OFF.");
    }

    [TestMethod]
    public void AutobrakeMaxVerificationAcceptsSelectorLevelThree()
    {
        var verifier = typeof(CopilotService).GetMethod(
            "IsA310AutobrakeMaxSelected",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(verifier);
        Assert.AreEqual(true, verifier!.Invoke(null, new object?[] { 3f, 0f }));
        Assert.AreEqual(true, verifier.Invoke(null, new object?[] { 0f, 1f }));
        Assert.AreEqual(false, verifier.Invoke(null, new object?[] { 2f, 0f }));
    }

    [TestMethod]
    public void ExternalPowerCanDisconnectAfterGsxBeginsTugMovement()
    {
        var validator = typeof(CopilotService).GetMethod(
            "ValidateExternalPowerProcedure",
            BindingFlags.NonPublic | BindingFlags.Static);
        var state = new AircraftState
        {
            Title = "Airbus A310-300",
            OnGround = true,
            GroundSpeedKnots = 1,
            ApuAvailable = true,
            ApuGeneratorSwitchOn = true
        };

        Assert.IsNotNull(validator);
        Assert.IsNull(validator!.Invoke(null, new object[] { state, false }));
        Assert.IsNotNull(validator.Invoke(null, new object[] { state, true }));
    }

    [TestMethod]
    public void FlowFiveAutomatesDeterministicFirstOfficerActions()
    {
        var flow = A310ProcedureLibrary.AfterStartAndTaxi;
        var automaticIds = flow.Steps
            .Where(step => step.Kind == ProcedureStepKind.AutomaticAction)
            .Select(step => step.Id)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "ignition-normal", "apu-off", "speedbrake-arm", "rudder-trim",
                "takeoff-flaps", "nose-taxi", "autobrake-max", "transponder-weather"
            },
            automaticIds);
        Assert.IsTrue(flow.Steps
            .Where(step => step.Kind == ProcedureStepKind.AutomaticAction)
            .All(step => step.Command!.StartsWith("a310 ", StringComparison.Ordinal)));
        Assert.AreEqual(
            ProcedureStepKind.Observe,
            flow.Steps.Single(step => step.Id == "anti-ice").Kind);

        var takeoffFlaps = flow.Steps.Single(step => step.Id == "takeoff-flaps");
        Assert.AreEqual(ProcedureStepKind.AutomaticAction, takeoffFlaps.Kind);
        Assert.AreEqual(CrewRole.FirstOfficer, takeoffFlaps.AssignedRole);
        Assert.AreEqual("a310 takeoff-flaps 15-0", takeoffFlaps.Command);
        Assert.AreEqual(
            ProcedureStepKind.ManualAction,
            flow.Steps.Single(step => step.Id == "pitch-trim").Kind);
    }

    [TestMethod]
    public void FlowThreeAutomatesFirstOfficerControlsAndUsesSharedPushbackContract()
    {
        var flow = A310ProcedureLibrary.ApuStartAndPushback;
        var automaticIds = flow.Steps
            .Where(step => step.Kind == ProcedureStepKind.AutomaticAction)
            .Select(step => step.Id)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "apu-start", "apu-generator-bleed", "beacon-on",
                "transponder-xpdr", "external-power-off"
            },
            automaticIds);
        Assert.IsFalse(flow.Steps.Any(step =>
            step.AssignedRole == CrewRole.FirstOfficer
            && step.Kind == ProcedureStepKind.ManualAction));
        var clearance = flow.Steps.Single(step =>
            step.Id == "captain-pushback-clearance");
        Assert.AreEqual(ProcedureStepKind.ManualAction, clearance.Kind);
        Assert.AreEqual(CrewRole.Captain, clearance.AssignedRole);
        Assert.IsTrue(flow.Steps
            .Where(step => step.Kind == ProcedureStepKind.AutomaticAction)
            .All(step => step.Command!.StartsWith("a310 ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void NativeReadbackNamesUseDedicatedA310Namespace()
    {
        var names = new[]
        {
            A310ControlProfile.Battery1State,
            A310ControlProfile.Battery2State,
            A310ControlProfile.Battery3State,
            A310ControlProfile.ApuMasterState,
            A310ControlProfile.IgnitionSelectorState,
            A310ControlProfile.FlapsLeftState,
            A310ControlProfile.FlapsRightState
        };

        Assert.IsTrue(names.All(name =>
            name.StartsWith("a310_", StringComparison.Ordinal)));
        Assert.AreEqual(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    [TestMethod]
    public void SimBriefAndApproachSettingsUseA310Contracts()
    {
        CollectionAssert.AreEqual(
            new[] { "A310" },
            SimBriefOperationalContext.ExpectedAircraftIcaos(
                AircraftVariant.IniBuildsA310).ToArray());
        Assert.AreEqual(
            15,
            SimBriefOperationalContext.TakeoffFlapSetting(
                new ImportedFlightPlan { TakeoffFlaps = "15/15" },
                AircraftVariant.IniBuildsA310));
        Assert.AreEqual(
            15,
            SimBriefOperationalContext.TakeoffFlapSetting(
                new ImportedFlightPlan { TakeoffFlaps = "15/0" },
                AircraftVariant.IniBuildsA310));
        Assert.AreEqual(
            20,
            SimBriefOperationalContext.TakeoffFlapSetting(
                new ImportedFlightPlan { TakeoffFlaps = "20/20" },
                AircraftVariant.IniBuildsA310));

        var profile = AircraftApproachProfiles.Resolve("Airbus A310-300");
        Assert.AreEqual("inibuilds-a310-300", profile.Key);
        Assert.AreEqual(245, profile.StandardSchedule.Flaps1SpeedKnots);
        Assert.AreEqual(210, profile.StandardSchedule.Flaps2SpeedKnots);
        Assert.AreEqual(180, profile.StandardSchedule.FlapsFullSpeedKnots);
    }

    [TestMethod]
    public void FlightCriticalA310StepsHaveVoiceCallouts()
    {
        var state = new AircraftState { Title = "Airbus A310-300" };
        var essentialIds = new[]
        {
            "fo-100-knots", "v1", "rotate", "positive-climb", "fo-gear-up",
            "fo-approaching-minimums", "fo-minimums", "fo-spoilers-callout",
            "fo-reverse-callout", "eighty"
        };
        var configurationIds = new[]
        {
            "slats-15", "flaps-15", "flaps-20", "flaps-40",
            "flaps-zero", "slats-zero"
        };

        Assert.IsTrue(essentialIds.All(id =>
            !string.IsNullOrWhiteSpace(ProcedureCalloutCatalog.ForStep(
                id, state, CalloutDetail.Minimal))));
        Assert.IsTrue(configurationIds.All(id =>
            !string.IsNullOrWhiteSpace(ProcedureCalloutCatalog.ForStep(
                id, state, CalloutDetail.Standard))));
    }
}
