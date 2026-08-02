using Microsoft.VisualStudio.TestTools.UnitTesting;
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

        Assert.IsTrue(
            steps.FindIndex(step => step.Id == "fo-engine-two-starter")
            < steps.FindIndex(step => step.Id == "fo-engine-one-starter"));
        StringAssert.Contains(
            steps.Single(step => step.Id == "fo-engine-two-fuel").Label,
            "20 percent N2");
        StringAssert.Contains(
            steps.Single(step => step.Id == "fo-engine-one-fuel").Label,
            "20 percent N2");
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
        StringAssert.Contains(
            A310ProcedureLibrary.TakeoffAndClimb.Steps
                .Single(step => step.Id == "packs-on").ManualInstruction!,
            "10 seconds");
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
    }

    [TestMethod]
    public void ParkingPreservesApuFuelPumpExceptionAndSecureDelay()
    {
        var flow = A310ProcedureLibrary.ParkingAndShutdown;

        StringAssert.Contains(
            flow.Steps.Single(step => step.Id == "fuel-pumps-parking").ManualInstruction!,
            "left inner tank Pump 2");
        StringAssert.Contains(
            flow.Steps.Single(step => step.Id == "irs-off").ManualInstruction!,
            "10 seconds");
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
            .Where(item => item.Id != "aircraft-state")
            .All(item => item.Support != CapabilitySupport.Supported));
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
                "emergency-exit", "egpws-test", "atc-radar-rudder"
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
