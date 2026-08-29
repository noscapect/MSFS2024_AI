using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot;
using Msfs2024Ai.Copilot.AircraftAdapters.Asobo737Max;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class Asobo737MaxRuntimeStateTests
{
    [TestMethod]
    public void EnumerationStoresDynamicHashesWithoutChangingKnownMappings()
    {
        var runtime = new Asobo737MaxRuntimeState();
        runtime.MarkInputEventsEnumerated();
        Consume(runtime.RecordEnumeratedInputEvent("ELECTRICAL_BATTERY", 11));
        Consume(runtime.RecordEnumeratedInputEvent("COMMON_ELECTRICAL_BATTERY_COVER", 12));
        Consume(runtime.RecordEnumeratedInputEvent("AFT_OVHD_L_IRS", 13));
        Consume(runtime.RecordEnumeratedInputEvent("FUEL_PUMP_CTR_L", 14));
        Consume(runtime.RecordEnumeratedInputEvent("UNKNOWN", 99));

        Assert.IsTrue(runtime.InputEventsEnumerated);
        Assert.AreEqual(11UL, runtime.BatteryInputEventHash);
        Assert.AreEqual(12UL, runtime.BatteryCoverInputEventHash);
        Assert.AreEqual(13UL, runtime.LeftIrsInputEventHash);
        Assert.AreEqual(14UL, runtime.FuelPumpInputEventHashes[2]);
        Assert.IsNull(runtime.RightIrsInputEventHash);
    }

    [TestMethod]
    public void ReadbacksPreserveBatteryCoverAndIndependentIrsValues()
    {
        var runtime = new Asobo737MaxRuntimeState();
        runtime.ApplyInputEvent(Request.Asobo737MaxBatteryInputEvent, 1.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxBatteryCoverInputEvent, 0.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxLeftIrsInputEvent, 2.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxRightIrsInputEvent, 1.0);

        Assert.IsTrue(runtime.BatteryInputEventOn);
        Assert.IsTrue(runtime.BatteryCoverInputEventOn);
        Assert.AreEqual(2.0, runtime.LeftIrsInputState);
        Assert.AreEqual(1.0, runtime.RightIrsInputState);
    }

    [TestMethod]
    public void CommandedIrsFallbackLastsForExistingFifteenSecondWindow()
    {
        var runtime = new Asobo737MaxRuntimeState();
        var now = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);
        runtime.RecordLeftIrsCommand(2.0, now);

        Assert.AreEqual(2.0, runtime.ResolveIrsState(true, now.AddSeconds(14.999)));
        Assert.IsNull(runtime.ResolveIrsState(true, now.AddSeconds(15)));

        runtime.ApplyInputEvent(Request.Asobo737MaxLeftIrsInputEvent, 1.0);
        Assert.AreEqual(1.0, runtime.ResolveIrsState(true, now.AddSeconds(1)));
    }

    [TestMethod]
    public void SystemArraysKeepOriginalIndexOrdering()
    {
        var runtime = new Asobo737MaxRuntimeState();
        runtime.ApplyInputEvent(Request.Asobo737MaxFuelPump1InputEvent, 1.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxFuelPump3InputEvent, 2.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxFuelPump6InputEvent, 3.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxEngineBleed1InputEvent, 4.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxEngineGenerator2InputEvent, 5.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxElectricHydraulicPump1InputEvent, 6.0);

        Assert.AreEqual(1.0, runtime.FuelPumpInputStates[0]);
        Assert.AreEqual(2.0, runtime.FuelPumpInputStates[2]);
        Assert.AreEqual(3.0, runtime.FuelPumpInputStates[5]);
        Assert.AreEqual(4.0, runtime.EngineBleedInputStates[0]);
        Assert.AreEqual(5.0, runtime.EngineGeneratorInputStates[1]);
        Assert.AreEqual(6.0, runtime.ElectricHydraulicPumpInputStates[0]);
    }

    [TestMethod]
    public void ExteriorAndTransponderReadbacksRemainIndependent()
    {
        var runtime = new Asobo737MaxRuntimeState();
        runtime.ApplyInputEvent(Request.Asobo737MaxTaxiLightInputEvent, 1.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxRunwayTurnoffRightInputEvent, 2.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxLandingLightLeftInputEvent, 3.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxPositionLightInputEvent, 4.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxLogoLightInputEvent, 5.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxAntiCollisionInputEvent, 6.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxFlapsInputEvent, 7.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxAutobrakeInputEvent, 8.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxAutothrottleInputEvent, 9.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxTransponderModeInputEvent, 10.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxTransponderOperatingModeInputEvent, 11.0);

        Assert.AreEqual(1.0, runtime.TaxiLightInputState);
        Assert.AreEqual(2.0, runtime.RunwayTurnoffInputStates[1]);
        Assert.AreEqual(3.0, runtime.LandingLightInputStates[0]);
        Assert.AreEqual(4.0, runtime.PositionLightInputState);
        Assert.AreEqual(5.0, runtime.LogoLightInputState);
        Assert.AreEqual(6.0, runtime.AntiCollisionInputState);
        Assert.AreEqual(7.0, runtime.FlapsInputState);
        Assert.AreEqual(8.0, runtime.AutobrakeInputState);
        Assert.AreEqual(9.0, runtime.AutothrottleInputState);
        Assert.AreEqual(10.0, runtime.TransponderModeInputState);
        Assert.AreEqual(11.0, runtime.TransponderOperatingModeInputState);
    }

    [TestMethod]
    public void ConnectionResetClearsAllConnectionScopedRuntimeState()
    {
        var runtime = new Asobo737MaxRuntimeState();
        var now = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);
        runtime.MarkInputEventsEnumerated();
        Consume(runtime.RecordEnumeratedInputEvent("ELECTRICAL_BATTERY", 11));
        Consume(runtime.RecordEnumeratedInputEvent("COMMON_ELECTRICAL_BATTERY_COVER", 12));
        Consume(runtime.RecordEnumeratedInputEvent("AFT_OVHD_L_IRS", 13));
        Consume(runtime.RecordEnumeratedInputEvent("AFT_OVHD_R_IRS", 14));
        Consume(runtime.RecordEnumeratedInputEvent("LIGHTING_POSITION_LIGHT", 15));
        Consume(runtime.RecordEnumeratedInputEvent("LIGHTING_LOGO_LIGHT", 16));
        Consume(runtime.RecordEnumeratedInputEvent("PASSENGER_EXIT_LIGHTS", 17));
        Consume(runtime.RecordEnumeratedInputEvent("COMMON_PASSENGER_EXIT_LIGHTS_COVER", 18));
        Consume(runtime.RecordEnumeratedInputEvent("PASSENGER_FASTEN_BELTS", 19));
        Consume(runtime.RecordEnumeratedInputEvent("PASSENGER_NO_SMOKING", 20));
        Consume(runtime.RecordEnumeratedInputEvent("ENGINE_APU", 21));
        Consume(runtime.RecordEnumeratedInputEvent("PNEUMATICS_APU_BLEED", 22));
        Consume(runtime.RecordEnumeratedInputEvent("ELECTRICAL_APU_GENERATOR_1", 23));
        Consume(runtime.RecordEnumeratedInputEvent("ELECTRICAL_APU_GENERATOR_2", 24));
        Consume(runtime.RecordEnumeratedInputEvent("FUEL_PUMP_AFT_1", 25));
        Consume(runtime.RecordEnumeratedInputEvent("FUEL_PUMP_FWD_1", 26));
        Consume(runtime.RecordEnumeratedInputEvent("FUEL_PUMP_CTR_L", 27));
        Consume(runtime.RecordEnumeratedInputEvent("FUEL_PUMP_CTR_R", 28));
        Consume(runtime.RecordEnumeratedInputEvent("FUEL_PUMP_FWD_2", 29));
        Consume(runtime.RecordEnumeratedInputEvent("FUEL_PUMP_AFT_2", 30));
        runtime.ApplyInputEvent(Request.Asobo737MaxBatteryInputEvent, 1.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxBatteryCoverInputEvent, 0.0);
        PopulateAllReadbacks(runtime);
        runtime.RecordLeftIrsCommand(2.0, now);
        runtime.RecordRightIrsCommand(1.0, now);
        runtime.ResetConnectionState();

        Assert.IsFalse(runtime.InputEventsEnumerated);
        Assert.IsNull(runtime.BatteryInputEventHash);
        Assert.IsNull(runtime.BatteryCoverInputEventHash);
        Assert.IsNull(runtime.LeftIrsInputEventHash);
        Assert.IsNull(runtime.RightIrsInputEventHash);
        Assert.IsNull(runtime.PositionLightInputEventHash);
        Assert.IsNull(runtime.LogoLightInputEventHash);
        Assert.IsNull(runtime.EmergencyExitInputEventHash);
        Assert.IsNull(runtime.EmergencyExitCoverInputEventHash);
        Assert.IsNull(runtime.SeatbeltsInputEventHash);
        Assert.IsNull(runtime.NoSmokingInputEventHash);
        Assert.IsNull(runtime.ApuInputEventHash);
        Assert.IsNull(runtime.ApuBleedInputEventHash);
        AssertAllNull(runtime.ApuGeneratorInputEventHashes);
        AssertAllNull(runtime.FuelPumpInputEventHashes);
        Assert.IsNull(runtime.BatteryInputEventOn);
        Assert.IsNull(runtime.BatteryCoverInputEventOn);
        Assert.IsNull(runtime.LeftIrsInputState);
        Assert.IsNull(runtime.RightIrsInputState);
        Assert.IsNull(runtime.PositionLightInputState);
        Assert.IsNull(runtime.LogoLightInputState);
        Assert.IsNull(runtime.EmergencyExitInputState);
        Assert.IsNull(runtime.EmergencyExitCoverInputState);
        Assert.IsNull(runtime.SeatbeltsInputState);
        Assert.IsNull(runtime.NoSmokingInputState);
        Assert.IsNull(runtime.ApuInputState);
        Assert.IsNull(runtime.ApuBleedInputState);
        Assert.IsNull(runtime.IsolationValveInputState);
        Assert.IsNull(runtime.LeftPackInputState);
        Assert.IsNull(runtime.RightPackInputState);
        Assert.IsNull(runtime.TaxiLightInputState);
        Assert.IsNull(runtime.AntiCollisionInputState);
        Assert.IsNull(runtime.FlapsInputState);
        Assert.IsNull(runtime.AutobrakeInputState);
        Assert.IsNull(runtime.AutothrottleInputState);
        Assert.IsNull(runtime.TransponderModeInputState);
        Assert.IsNull(runtime.TransponderOperatingModeInputState);
        AssertAllNull(runtime.ApuGeneratorInputStates);
        AssertAllNull(runtime.FuelPumpInputStates);
        AssertAllNull(runtime.EngineBleedInputStates);
        AssertAllNull(runtime.EngineGeneratorInputStates);
        AssertAllNull(runtime.ElectricHydraulicPumpInputStates);
        AssertAllNull(runtime.RunwayTurnoffInputStates);
        AssertAllNull(runtime.LandingLightInputStates);
        Assert.IsNull(runtime.ResolveIrsState(true, now.AddSeconds(5)));
        Assert.IsNull(runtime.ResolveIrsState(false, now.AddSeconds(5)));
    }

    [TestMethod]
    public void ConnectionResetClearsCommandedIrsFallbackAndAcceptsFreshReadbacks()
    {
        var runtime = new Asobo737MaxRuntimeState();
        var now = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);
        runtime.RecordLeftIrsCommand(2.0, now);

        Assert.AreEqual(2.0, runtime.ResolveIrsState(true, now.AddSeconds(5)));

        runtime.ResetConnectionState();

        Assert.IsNull(runtime.ResolveIrsState(true, now.AddSeconds(5)));
        runtime.ApplyInputEvent(Request.Asobo737MaxLeftIrsInputEvent, 1.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxFuelPump1InputEvent, 2.0);
        Assert.AreEqual(1.0, runtime.ResolveIrsState(true, now.AddSeconds(5)));
        Assert.AreEqual(2.0, runtime.FuelPumpInputStates[0]);
    }

    [TestMethod]
    public void AircraftResetIsAtLeastAsThoroughAsConnectionReset()
    {
        var runtime = new Asobo737MaxRuntimeState();
        runtime.ApplyInputEvent(Request.Asobo737MaxBatteryInputEvent, 1.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxFuelPump1InputEvent, 2.0);

        runtime.ResetAircraftState();
        Assert.IsNull(runtime.BatteryInputEventOn);
        Assert.IsNull(runtime.FuelPumpInputStates[0]);
    }

    [TestMethod]
    public void DynamicEnumerationCanStartFreshAfterConnectionReset()
    {
        var runtime = new Asobo737MaxRuntimeState();
        runtime.MarkInputEventsEnumerated();
        Consume(runtime.RecordEnumeratedInputEvent("AFT_OVHD_L_IRS", 13));
        runtime.ResetConnectionState();

        Assert.IsFalse(runtime.InputEventsEnumerated);
        Assert.IsNull(runtime.LeftIrsInputEventHash);

        runtime.MarkInputEventsEnumerated();
        Consume(runtime.RecordEnumeratedInputEvent("AFT_OVHD_L_IRS", 99));
        Assert.IsTrue(runtime.InputEventsEnumerated);
        Assert.AreEqual(99UL, runtime.LeftIrsInputEventHash);
    }

    private static void PopulateAllReadbacks(Asobo737MaxRuntimeState runtime)
    {
        var requests = new[]
        {
            Request.Asobo737MaxLeftIrsInputEvent, Request.Asobo737MaxRightIrsInputEvent,
            Request.Asobo737MaxPositionLightInputEvent, Request.Asobo737MaxLogoLightInputEvent,
            Request.Asobo737MaxEmergencyExitInputEvent, Request.Asobo737MaxEmergencyExitCoverInputEvent,
            Request.Asobo737MaxSeatbeltsInputEvent, Request.Asobo737MaxNoSmokingInputEvent,
            Request.Asobo737MaxApuInputEvent, Request.Asobo737MaxApuBleedInputEvent,
            Request.Asobo737MaxApuGenerator1InputEvent, Request.Asobo737MaxApuGenerator2InputEvent,
            Request.Asobo737MaxIsolationValveInputEvent, Request.Asobo737MaxLeftPackInputEvent,
            Request.Asobo737MaxRightPackInputEvent, Request.Asobo737MaxEngineBleed1InputEvent,
            Request.Asobo737MaxEngineBleed2InputEvent, Request.Asobo737MaxEngineGenerator1InputEvent,
            Request.Asobo737MaxEngineGenerator2InputEvent, Request.Asobo737MaxElectricHydraulicPump1InputEvent,
            Request.Asobo737MaxElectricHydraulicPump2InputEvent, Request.Asobo737MaxTaxiLightInputEvent,
            Request.Asobo737MaxRunwayTurnoffLeftInputEvent, Request.Asobo737MaxRunwayTurnoffRightInputEvent,
            Request.Asobo737MaxLandingLightLeftInputEvent, Request.Asobo737MaxLandingLightRightInputEvent,
            Request.Asobo737MaxAntiCollisionInputEvent, Request.Asobo737MaxFlapsInputEvent,
            Request.Asobo737MaxAutobrakeInputEvent, Request.Asobo737MaxAutothrottleInputEvent,
            Request.Asobo737MaxTransponderModeInputEvent, Request.Asobo737MaxTransponderOperatingModeInputEvent,
            Request.Asobo737MaxFuelPump1InputEvent, Request.Asobo737MaxFuelPump2InputEvent,
            Request.Asobo737MaxFuelPump3InputEvent, Request.Asobo737MaxFuelPump4InputEvent,
            Request.Asobo737MaxFuelPump5InputEvent, Request.Asobo737MaxFuelPump6InputEvent
        };

        for (var index = 0; index < requests.Length; index++)
        {
            runtime.ApplyInputEvent(requests[index], index + 1);
        }
    }

    private static void AssertAllNull<T>(IEnumerable<T?> values) where T : struct
    {
        foreach (var value in values)
        {
            Assert.IsNull(value);
        }
    }

    private static void Consume(IEnumerable<string> diagnostics)
    {
        foreach (var _ in diagnostics)
        {
        }
    }
}
