using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Checklists;
using Msfs2024Ai.Copilot.Domain;
using Msfs2024Ai.Copilot.Procedures;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class ProcedureCatalogTests
{
    [DataTestMethod]
    [DataRow("A320neo V2")]
    [DataRow("A321")]
    [DataRow("A330-300 (GE)")]
    [DataRow("FlyByWire A32NX")]
    [DataRow("PMDG 737-800")]
    public void RunwayTurnoffLightsAreFirstOfficerAutomaticAndVerified(string title)
    {
        var state = new AircraftState { Title = title };
        var step = ProcedureCatalog.ForAircraft(state)
            .SelectMany(flow => flow.Steps)
            .First(item => item.Id == "fo-runway-turnoff-on");

        Assert.AreEqual(ProcedureStepKind.AutomaticAction, step.Kind);
        Assert.AreEqual(CrewRole.FirstOfficer, step.AssignedRole);
        Assert.IsFalse(step.IsComplete(new AircraftState
        {
            Title = title,
            RunwayTurnoffLightsOn = false
        }));
        Assert.IsTrue(step.IsComplete(new AircraftState
        {
            Title = title,
            RunwayTurnoffLightsOn = true
        }));
    }

    [TestMethod]
    public void Pmdg737UsesBoeingProcedureCatalog()
    {
        var state = new AircraftState { Title = "PMDG 737-800" };

        var flows = ProcedureCatalog.ForAircraft(state);

        Assert.AreEqual("1. 737 Power Up & Initial Setup", flows[0].Name);
        Assert.AreEqual(B737ProcedureLibrary.GateToGate.Count, flows.Count);
    }

    [TestMethod]
    public void Pmdg737PowerUpIncludesAllFourVerifiedFireTests()
    {
        var flow = B737ProcedureLibrary.PowerUpAndInitialSetup;
        var fireSteps = flow.Steps.Where(step => step.Id.Contains("fire") || step.Id.Contains("extinguisher")).ToList();

        CollectionAssert.AreEqual(
            new[] { "fo-fire-fault-inop-test", "fo-fire-overheat-test", "fo-extinguisher-test-1", "fo-extinguisher-test-2" },
            fireSteps.Select(step => step.Id).ToArray());
        Assert.IsTrue(fireSteps.All(step => step.Command?.StartsWith("pmdg fire-test", StringComparison.Ordinal) == true));
        Assert.IsTrue(fireSteps[0].IsComplete(new AircraftState { PmdgFireFaultInopTestCompleted = true }));
        Assert.IsTrue(fireSteps[1].IsComplete(new AircraftState { PmdgFireOverheatTestCompleted = true }));
        Assert.IsTrue(fireSteps[2].IsComplete(new AircraftState { PmdgExtinguisherTest1Completed = true }));
        Assert.IsTrue(fireSteps[3].IsComplete(new AircraftState { PmdgExtinguisherTest2Completed = true }));
    }

    [TestMethod]
    public void Pmdg737FlapsFiveRequiresPositionAndScheduledSpeed()
    {
        var flow = B737ProcedureLibrary.ApproachAndLanding;
        var flapsOneGate = flow.Steps.Single(step => step.Id == "flaps-one-gate");
        var flapsFiveGate = flow.Steps.Single(step => step.Id == "flaps-five-gate");
        var flapsFiveSpeed = flow.Steps.Single(step => step.Id == "flaps-five-speed");
        var state = new AircraftState
        {
            Title = "PMDG 737-800",
            IndicatedAltitudeFeet = 9500,
            AltitudeAboveGroundFeet = 9500,
            ApproachDistanceToTouchdownNm = 25,
            ApproachFlaps1DistanceNm = 15,
            ApproachFlaps1AltitudeFeet = 10000,
            ApproachFlaps2DistanceNm = 12,
            ApproachFlaps2AltitudeAglFeet = 4000,
            ApproachFlaps2SpeedKnots = 190,
            IndicatedAirspeedKnots = 185
        };

        Assert.IsFalse(flapsOneGate.IsComplete(state));
        Assert.IsFalse(flapsFiveGate.IsComplete(state));
        state.ApproachDistanceToTouchdownNm = 11.5;
        Assert.IsTrue(flapsFiveGate.IsComplete(state));
        Assert.IsTrue(flapsFiveSpeed.IsComplete(state));
        state.IndicatedAirspeedKnots = 195;
        Assert.IsFalse(flapsFiveSpeed.IsComplete(state));
    }

    [TestMethod]
    public void Pmdg737AfterLandingTurnsTaxiLightOn()
    {
        var step = B737ProcedureLibrary.AfterLandingAndTaxi.Steps
            .Single(item => item.Id == "fo-taxi-light-on");

        Assert.AreEqual("pmdg taxi-light on", step.Command);
        Assert.IsFalse(step.IsComplete(new AircraftState { Title = "PMDG 737-800", NoseLightSelectorPosition = 2 }));
        Assert.IsTrue(step.IsComplete(new AircraftState { Title = "PMDG 737-800", NoseLightSelectorPosition = 1 }));
    }

    [TestMethod]
    public void Pmdg737PackageTitleUsesBoeingProcedureCatalog()
    {
        var state = new AircraftState { Title = "737-800 PAX BW TC" };

        var flows = ProcedureCatalog.ForAircraft(state);

        Assert.IsTrue(state.IsSupportedBoeing737);
        Assert.AreEqual("1. 737 Power Up & Initial Setup", flows[0].Name);
    }

    [TestMethod]
    public void AirbusStillUsesA320ProcedureCatalog()
    {
        var state = new AircraftState { Title = "A320neo V2" };

        var flows = ProcedureCatalog.ForAircraft(state);

        Assert.AreEqual("1. Power Up & Initial Setup", flows[0].Name);
        Assert.AreEqual(A320ProcedureLibrary.GateToGate.Count, flows.Count);
    }

    [DataTestMethod]
    [DataRow("A320neo V2")]
    [DataRow("A321")]
    [DataRow("A330-300 (GE)")]
    [DataRow("FlyByWire A32NX")]
    public void AirbusEngineModeIgnStartIsMonitoredWithoutConfirmation(string title)
    {
        var state = new AircraftState { Title = title };
        var step = ProcedureCatalog.ForAircraft(state)[3].Steps
            .Single(item => item.Id == "captain-engine-mode-start");

        Assert.AreEqual(ProcedureStepKind.Observe, step.Kind);
        Assert.IsNull(step.ManualInstruction);
        Assert.IsFalse(step.IsComplete(state));
        state.EngineModeSelectorPosition = 2;
        Assert.IsTrue(step.IsComplete(state));
    }

    [DataTestMethod]
    [DataRow("A320neo V2")]
    [DataRow("A321")]
    [DataRow("A330-300 (GE)")]
    [DataRow("FlyByWire A32NX")]
    public void AirbusTakeoffLightsAreSetAfterTakeoffClearance(string title)
    {
        var beforeTakeoff = ProcedureCatalog.ForAircraft(new AircraftState { Title = title })
            .Single(flow => flow.Id == "before-takeoff");
        var takeoffClearanceIndex = beforeTakeoff.Steps
            .Select((step, index) => new { step.Id, index })
            .Single(item => item.Id == "fo-takeoff-clearance")
            .index;
        var lightIndexes = beforeTakeoff.Steps
            .Select((step, index) => new { step.Id, index })
            .Where(item => item.Id is "fo-nose-light-takeoff"
                           or "fo-landing-lights-on"
                           or "fo-runway-turnoff-on")
            .Select(item => item.index)
            .ToArray();

        Assert.AreEqual(3, lightIndexes.Length);
        Assert.IsTrue(lightIndexes.All(index => index > takeoffClearanceIndex));
    }

    [DataTestMethod]
    [DataRow("A320neo V2")]
    [DataRow("A321")]
    [DataRow("A330-300 (GE)")]
    [DataRow("FlyByWire A32NX")]
    public void AirbusRunwayTurnoffLightsAreSwitchedOffAboveTenThousandFeet(string title)
    {
        var takeoffClimb = ProcedureCatalog.ForAircraft(new AircraftState { Title = title })
            .Single(flow => flow.Id == "takeoff-climb");
        var aboveTenThousandIndex = takeoffClimb.Steps
            .Select((step, index) => new { step.Id, index })
            .Single(item => item.Id == "above-ten-thousand")
            .index;
        var runwayTurnoffOff = takeoffClimb.Steps
            .Select((step, index) => new { Step = step, index })
            .Single(item => item.Step.Id == "fo-runway-turnoff-off");

        Assert.IsTrue(runwayTurnoffOff.index > aboveTenThousandIndex);
        Assert.AreEqual(ProcedureStepKind.AutomaticAction, runwayTurnoffOff.Step.Kind);
        Assert.AreEqual(CrewRole.FirstOfficer, runwayTurnoffOff.Step.AssignedRole);
        Assert.AreEqual(
            title.Contains("A330", StringComparison.OrdinalIgnoreCase)
                ? "a330 runway-turnoff off"
                : "runway-turnoff off",
            runwayTurnoffOff.Step.Command);
        Assert.IsFalse(runwayTurnoffOff.Step.IsComplete(new AircraftState
        {
            Title = title,
            RunwayTurnoffLightsOn = true
        }));
        Assert.IsTrue(runwayTurnoffOff.Step.IsComplete(new AircraftState
        {
            Title = title,
            RunwayTurnoffLightsOn = false
        }));
    }

    [TestMethod]
    public void IniBuildsEngineModeReadbackPrefersNativeIgnitionKnob()
    {
        var resolver = typeof(CopilotService).GetMethod(
            "ResolveEngineModeSelectorPosition",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(resolver);

        var resolved = (double?)resolver!.Invoke(null, new object[] { null!, 2f, 0d, 0d });

        Assert.AreEqual(2d, resolved);
    }

    [TestMethod]
    public void IniBuildsEngineModeReadbackPrefersDirectLVar()
    {
        var resolver = typeof(CopilotService).GetMethod(
            "ResolveEngineModeSelectorPosition",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(resolver);

        var resolved = (double?)resolver!.Invoke(null, new object[] { 2d, 0f, 0d, 0d });

        Assert.AreEqual(2d, resolved);
    }

    [TestMethod]
    public void IniBuildsA330UsesA330ProcedureCatalog()
    {
        var state = new AircraftState { Title = "A330-300 (GE)" };

        var flows = ProcedureCatalog.ForAircraft(state);
        var checklist = ProcedureCatalog.FindChecklist(state, flows[0].Id);

        Assert.IsTrue(state.IsIniBuildsA330);
        Assert.IsTrue(state.IsIniBuildsAirbusFamily);
        Assert.IsTrue(state.IsSupportedA320);
        Assert.AreEqual("iniBuilds A330", state.AircraftFamilyLabel);
        Assert.AreEqual("1. Power Up & Initial Setup", flows[0].Name);
        Assert.AreEqual(A330ProcedureLibrary.GateToGate.Count, flows.Count);
        Assert.AreEqual(A330ChecklistLibrary.FindForProcedure(flows[0].Id)!.Name, checklist!.Name);
    }

    [TestMethod]
    public void IniBuildsA330UsesSingleOnOffLandingLightContract()
    {
        var beforeTakeoff = A330ProcedureLibrary.BeforeTakeoff.Steps
            .Single(step => step.Id == "fo-landing-lights-on");
        var aboveTenThousand = A330ProcedureLibrary.TakeoffAndClimb.Steps
            .Single(step => step.Id == "fo-landing-lights-off");
        var afterLanding = A330ProcedureLibrary.AfterLandingAndTaxi.Steps
            .Single(step => step.Id == "fo-landing-lights-retract");
        var on = new AircraftState
        {
            Title = "A330-300 (GE)",
            LeftLandingLightSelectorPosition = 0,
            RightLandingLightSelectorPosition = 0
        };
        var off = new AircraftState
        {
            Title = "A330-300 (GE)",
            LeftLandingLightSelectorPosition = 1,
            RightLandingLightSelectorPosition = 1
        };

        Assert.AreEqual("a330 landing-lights on", beforeTakeoff.Command);
        Assert.IsTrue(beforeTakeoff.IsComplete(on));
        Assert.IsFalse(beforeTakeoff.IsComplete(off));
        Assert.AreEqual("a330 landing-lights off", aboveTenThousand.Command);
        Assert.AreEqual("Landing light OFF", aboveTenThousand.Label);
        Assert.IsTrue(aboveTenThousand.IsComplete(off));
        Assert.IsFalse(aboveTenThousand.IsComplete(on));
        Assert.AreEqual("a330 landing-lights off", afterLanding.Command);
        Assert.IsFalse(afterLanding.Label.Contains("RETRACT", StringComparison.Ordinal));
    }

    [TestMethod]
    public void IniBuildsA330ApproachPrefersDistanceOverAltitudeFallback()
    {
        var flow = A330ProcedureLibrary.ApproachAndLanding;
        var flapsOneGate = flow.Steps.Single(step => step.Id == "approach-config-start");
        var flapsTwoGate = flow.Steps.Single(step => step.Id == "flaps-two-point");
        var landingConfigGate = flow.Steps.Single(step => step.Id == "landing-config-point");
        var state = new AircraftState
        {
            Title = "A330-300 (GE)",
            IndicatedAltitudeFeet = 9000,
            AltitudeAboveGroundFeet = 1700,
            ApproachDistanceToTouchdownNm = 20,
            ApproachFlaps1DistanceNm = 16,
            ApproachFlaps1AltitudeFeet = 10000,
            ApproachFlaps2DistanceNm = 11,
            ApproachFlaps2AltitudeAglFeet = 3500,
            ApproachLandingConfigDistanceNm = 5,
            ApproachLandingConfigAltitudeAglFeet = 1800
        };

        Assert.IsFalse(flapsOneGate.IsComplete(state));
        Assert.IsFalse(flapsTwoGate.IsComplete(state));
        Assert.IsFalse(landingConfigGate.IsComplete(state));

        state.ApproachDistanceToTouchdownNm = null;

        Assert.IsTrue(flapsOneGate.IsComplete(state));
        Assert.IsTrue(flapsTwoGate.IsComplete(state));
        Assert.IsTrue(landingConfigGate.IsComplete(state));
    }

    [TestMethod]
    public void IniBuildsA330ApproachRecoveryAcceptsLaterFlapConfiguration()
    {
        var flow = A330ProcedureLibrary.ApproachAndLanding;
        var configOne = flow.Steps.Single(step => step.Id == "fo-flaps-one");
        var configTwo = flow.Steps.Single(step => step.Id == "fo-flaps-two");
        var configThree = flow.Steps.Single(step => step.Id == "fo-flaps-three");
        var configFull = flow.Steps.Single(step => step.Id == "fo-flaps-full");
        var state = new AircraftState
        {
            Title = "A330-300 (GE)",
            FlapsHandleIndex = 3
        };

        Assert.IsTrue(configOne.IsComplete(state));
        Assert.IsTrue(configTwo.IsComplete(state));
        Assert.IsTrue(configThree.IsComplete(state));
        Assert.IsFalse(configFull.IsComplete(state));
    }

    [TestMethod]
    public void IniBuildsA321UsesA321ProcedureCatalog()
    {
        var state = new AircraftState { Title = "A321" };

        var flows = ProcedureCatalog.ForAircraft(state);
        var checklist = ProcedureCatalog.FindChecklist(state, flows[0].Id);

        Assert.IsTrue(state.IsIniBuildsA321Lr);
        Assert.AreEqual("1. Power Up & Initial Setup", flows[0].Name);
        Assert.AreEqual(A321ProcedureLibrary.GateToGate.Count, flows.Count);
        Assert.AreEqual(A321ChecklistLibrary.FindForProcedure(flows[0].Id)!.Name, checklist!.Name);
    }

    [TestMethod]
    public void IniBuildsA321KeepsSignSelectorsAutoAfterPreflight()
    {
        var autoState = new AircraftState
        {
            Title = "A321",
            SeatbeltSelectorPosition = 1,
            NoSmokingSelectorPosition = 1
        };

        foreach (var flowIndex in new[] { 7, 9 })
        {
            var signSteps = A321ProcedureLibrary.GateToGate[flowIndex].Steps
                .Where(step => step.Id.Contains("seatbelt")
                               || step.Id.Contains("no-smoking"))
                .ToList();

            Assert.IsTrue(signSteps.Count >= 2);
            Assert.IsTrue(signSteps.All(step => step.IsComplete(autoState)));
            Assert.IsTrue(signSteps.All(step => step.Command != null
                                                && step.Command.EndsWith(" auto")));
        }

        var shutdown = A321ProcedureLibrary.GateToGate[11];
        var secureDecisionIndex = shutdown.Steps
            .Select((step, index) => new { step.Id, index })
            .Single(item => item.Id == "secure-decision")
            .index;
        var noSmokingAutoIndex = shutdown.Steps
            .Select((step, index) => new { step.Id, index })
            .Single(item => item.Id == "fo-no-smoking-auto")
            .index;
        var noSmokingOff = shutdown.Steps
            .Select((step, index) => new { Step = step, index })
            .Single(item => item.Step.Id == "secure-no-smoking-off");

        Assert.IsTrue(noSmokingAutoIndex < secureDecisionIndex);
        Assert.IsTrue(noSmokingOff.index > secureDecisionIndex);
        Assert.AreEqual("no-smoking off", noSmokingOff.Step.Command);
        Assert.IsTrue(noSmokingOff.Step.IsComplete(
            new AircraftState { Title = "A321", NoSmokingSelectorPosition = 2 }));
    }

    [TestMethod]
    public void FlyByWireA320UsesFbwProcedureCatalog()
    {
        var state = new AircraftState { Title = "FlyByWire A32NX" };

        var flows = ProcedureCatalog.ForAircraft(state);
        var checklist = ProcedureCatalog.FindChecklist(state, flows[0].Id);

        Assert.IsTrue(state.IsFlyByWireA320Neo);
        Assert.AreEqual("FlyByWire A32NX", state.AircraftFamilyLabel);
        Assert.AreEqual("1. Power Up & Initial Setup", flows[0].Name);
        Assert.AreEqual(FbwA320ProcedureLibrary.GateToGate.Count, flows.Count);
        Assert.AreEqual(FbwA320ChecklistLibrary.FindForProcedure(flows[0].Id)!.Name, checklist!.Name);
    }

    [TestMethod]
    public void FlyByWireA320Flow2UsesSeatbeltOnNotAuto()
    {
        var step = FbwA320ProcedureLibrary.GateToGate[1].Steps
            .Single(item => item.Id == "fo-seatbelts-on");

        Assert.AreEqual("Seatbelt signs ON", step.Label);
        Assert.AreEqual("seatbelts on", step.Command);
        Assert.IsTrue(step.IsComplete(new AircraftState { SeatbeltSignsOn = true }));
        Assert.IsFalse(step.IsComplete(new AircraftState { SeatbeltSignsOn = false }));
    }

    [TestMethod]
    public void FlyByWireA320ApproachAndShutdownUseSeatbeltSignState()
    {
        var approachStep = FbwA320ProcedureLibrary.GateToGate[9].Steps
            .Single(item => item.Id == "fo-seatbelts-on");
        var shutdownStep = FbwA320ProcedureLibrary.GateToGate[11].Steps
            .Single(item => item.Id == "fo-seatbelts-off");

        Assert.AreEqual("seatbelts on", approachStep.Command);
        Assert.IsTrue(approachStep.IsComplete(new AircraftState { SeatbeltSignsOn = true }));
        Assert.IsFalse(approachStep.IsComplete(new AircraftState { SeatbeltSignsOn = false }));

        Assert.AreEqual("seatbelts off", shutdownStep.Command);
        Assert.IsTrue(shutdownStep.IsComplete(new AircraftState { SeatbeltSignsOn = false }));
        Assert.IsFalse(shutdownStep.IsComplete(new AircraftState { SeatbeltSignsOn = true }));
    }

    [TestMethod]
    public void FlyByWireA380XIsParkedAndNotExposedAsSupported()
    {
        var state = new AircraftState { Title = "FlyByWire A380X" };

        var flows = ProcedureCatalog.ForAircraft(state);

        Assert.IsTrue(state.HasFlyByWireA380XSignature);
        Assert.IsFalse(state.IsFlyByWireA380X);
        Assert.IsFalse(state.IsFlyByWireAirbus);
        Assert.IsFalse(state.IsSupportedAircraft);
        Assert.AreEqual(
            0,
            flows.Count,
            "Parked/unsupported aircraft must not inherit the A320 procedure library.");
    }

    [TestMethod]
    public void Pmdg737ChecklistDoesNotAcceptOffDetentsAsConfigured()
    {
        var beforeTakeoff = B737ChecklistLibrary.FindForProcedure("before-takeoff")!;
        var landingLights = beforeTakeoff.Items.Single(item => item.Challenge == "Landing lights");

        Assert.IsFalse(landingLights.Verify(new AircraftState
        {
            LeftLandingLightSelectorPosition = 0,
            RightLandingLightSelectorPosition = 0
        }));
        Assert.IsTrue(landingLights.Verify(new AircraftState
        {
            LeftLandingLightSelectorPosition = 2,
            RightLandingLightSelectorPosition = 2
        }));

        var takeoffClimb = B737ChecklistLibrary.FindForProcedure("takeoff-climb")!;
        var gear = takeoffClimb.Items.Single(item => item.Challenge == "Landing gear");

        Assert.IsFalse(gear.Verify(new AircraftState { GearHandlePosition = 1 }));
        Assert.IsTrue(gear.Verify(new AircraftState { GearHandlePosition = 0 }));

        var approachLanding = B737ChecklistLibrary.FindForProcedure("approach-landing")!;
        var flaps = approachLanding.Items.Single(item => item.Challenge == "Flaps");

        Assert.IsFalse(flaps.Verify(new AircraftState
        {
            Title = "PMDG 737-800",
            BoeingLandingFlaps = 30,
            FlapsHandleIndex = 2
        }));
        Assert.IsTrue(flaps.Verify(new AircraftState
        {
            Title = "PMDG 737-800",
            BoeingLandingFlaps = 30,
            FlapsHandleIndex = 7
        }));
    }

    [TestMethod]
    public void Pmdg737UsesFmcLandingDataForStableApproachGate()
    {
        var state = new AircraftState
        {
            Title = "PMDG 737-800",
            BoeingLandingFlaps = 30,
            BoeingLandingVrefKnots = 137,
            GearHandlePosition = 2,
            GearHandleDown = true,
            GroundSpoilersArmed = true,
            FlapsHandleIndex = 7,
            IndicatedAirspeedKnots = 151
        };

        Assert.AreEqual(142, state.EffectiveBoeingApproachTargetSpeedKnots);
        Assert.IsTrue(state.BoeingApproachStable);

        state.IndicatedAirspeedKnots = 160;

        Assert.IsFalse(state.BoeingApproachStable);
    }

    [TestMethod]
    public void Pmdg737ApuGeneratorStepRequiresActualBusPower()
    {
        var apuStart = B737ProcedureLibrary.ApuStartAndPushback;
        var apuGenerators = apuStart.Steps.Single(step => step.Id == "fo-apu-generators");

        Assert.IsFalse(apuGenerators.IsComplete(new AircraftState
        {
            Title = "PMDG 737-800",
            ApuGeneratorSwitchOn = true,
            ApuGeneratorPowerEstablished = false
        }));

        Assert.IsTrue(apuGenerators.IsComplete(new AircraftState
        {
            Title = "PMDG 737-800",
            ApuGeneratorSwitchOn = true,
            ApuGeneratorPowerEstablished = true
        }));
    }

    [TestMethod]
    public void A330AfterLandingRequiresRealApuElectricalReadiness()
    {
        var electricalPower = A330ProcedureLibrary.AfterLandingAndTaxi.Steps
            .Single(step => step.Id == "apu-electrical-power");

        Assert.IsFalse(electricalPower.IsComplete(new AircraftState
        {
            Title = "A330-300 (GE)",
            ApuAvailable = false,
            ApuGeneratorSwitchOn = true
        }));
        Assert.IsFalse(electricalPower.IsComplete(new AircraftState
        {
            Title = "A330-300 (GE)",
            ApuAvailable = true,
            ApuGeneratorSwitchOn = false,
            ApuGeneratorActive = false
        }));
        Assert.IsTrue(electricalPower.IsComplete(new AircraftState
        {
            Title = "A330-300 (GE)",
            ApuAvailable = true,
            ApuGeneratorSwitchOn = true
        }));
    }

    [TestMethod]
    public void A330ParkingChecksPowerBeforeEngineShutdown()
    {
        var flow = A330ProcedureLibrary.ParkingAndShutdown;
        var parked = flow.Steps.Single(step => step.Id == "captain-park");
        var power = flow.Steps.Single(step => step.Id == "shutdown-power");

        Assert.IsTrue(parked.IsComplete(new AircraftState
        {
            Title = "A330-300 (GE)",
            OnGround = true,
            GroundSpeedKnots = 0,
            ParkingBrakeSet = true,
            Engine1Running = true,
            Engine2Running = true
        }));
        Assert.IsFalse(power.IsComplete(new AircraftState
        {
            Title = "A330-300 (GE)",
            ApuAvailable = true
        }));
        Assert.IsTrue(power.IsComplete(new AircraftState
        {
            Title = "A330-300 (GE)",
            ApuAvailable = true,
            ApuGeneratorActive = true
        }));
    }
}
