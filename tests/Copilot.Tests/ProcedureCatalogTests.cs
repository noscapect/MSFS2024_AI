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
