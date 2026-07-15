using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Checklists;
using Msfs2024Ai.Copilot.Procedures;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class ProcedureCatalogTests
{
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

    [TestMethod]
    public void IniBuildsA330UsesA330ProcedureCatalog()
    {
        var state = new AircraftState { Title = "A330" };

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
}
