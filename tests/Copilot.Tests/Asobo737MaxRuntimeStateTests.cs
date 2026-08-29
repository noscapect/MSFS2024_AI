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
    public void ConnectionAndAircraftResetMatchExistingClearedState()
    {
        var runtime = new Asobo737MaxRuntimeState();
        runtime.MarkInputEventsEnumerated();
        Consume(runtime.RecordEnumeratedInputEvent("ELECTRICAL_BATTERY", 11));
        Consume(runtime.RecordEnumeratedInputEvent("AFT_OVHD_L_IRS", 12));
        runtime.ApplyInputEvent(Request.Asobo737MaxBatteryInputEvent, 1.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxLeftIrsInputEvent, 2.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxAutothrottleInputEvent, 1.0);
        runtime.ApplyInputEvent(Request.Asobo737MaxTransponderModeInputEvent, 3.0);
        runtime.ResetConnectionState();

        Assert.IsFalse(runtime.InputEventsEnumerated);
        Assert.IsNull(runtime.BatteryInputEventHash);
        Assert.IsNull(runtime.LeftIrsInputEventHash);
        Assert.IsNull(runtime.BatteryInputEventOn);
        Assert.IsNull(runtime.LeftIrsInputState);
        Assert.IsNull(runtime.AutothrottleInputState);
        Assert.IsNull(runtime.TransponderModeInputState);

        runtime.ResetAircraftState();
        Assert.IsNull(runtime.BatteryInputEventOn);
    }

    private static void Consume(IEnumerable<string> diagnostics)
    {
        foreach (var _ in diagnostics)
        {
        }
    }
}
