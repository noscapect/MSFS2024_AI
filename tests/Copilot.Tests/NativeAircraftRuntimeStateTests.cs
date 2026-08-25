using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Telemetry;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class NativeAircraftRuntimeStateTests
{
    [TestMethod]
    public void NativeReadbackUpdatesAndDuplicateSuppressesDiagnostic()
    {
        var runtime = new NativeAircraftRuntimeState();

        var first = runtime.TryApplyMobiFlightReadback(Request.NativeCrewOxygen, 1);
        var duplicate = runtime.TryApplyMobiFlightReadback(Request.NativeCrewOxygen, 1);

        Assert.IsTrue(first.Handled);
        Assert.IsNotNull(first.Diagnostic);
        Assert.AreEqual(1f, runtime.NativeAirbus.CrewOxygen);
        Assert.IsNull(duplicate.Diagnostic);
    }

    [TestMethod]
    public void DuplicateInputEventReadbackSuppressesDiagnostic()
    {
        var runtime = new NativeAircraftRuntimeState();

        var first = runtime.TryApplyInputEvent(Request.A330LandingLightInputEvent, 1);
        var duplicate = runtime.TryApplyInputEvent(Request.A330LandingLightInputEvent, 1);

        Assert.IsNotNull(first.Diagnostic);
        Assert.IsNull(duplicate.Diagnostic);
    }

    [TestMethod]
    public void NativeReadinessRequiresEveryOriginalReadback()
    {
        var runtime = new NativeAircraftRuntimeState();

        for (var value = (int)Request.NativeBattery1;
             value < (int)Request.NativeRightLandingLightSelector;
             value++)
        {
            runtime.TryApplyMobiFlightReadback((Request)value, 0);
        }

        Assert.IsFalse(runtime.AirbusNativeStateReady);

        runtime.TryApplyMobiFlightReadback(Request.NativeRightLandingLightSelector, 0);

        Assert.IsTrue(runtime.AirbusNativeStateReady);
    }

    [TestMethod]
    public void ConnectionResetClearsReadbacksAndCommandFallbacks()
    {
        var runtime = new NativeAircraftRuntimeState();
        var now = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
        runtime.TryApplyMobiFlightReadback(Request.NativeBattery1, 1);
        runtime.TryApplyInputEvent(Request.A330FuelPump1InputEvent, 1);
        runtime.RecordFbwSpoilersCommand(true, now);

        runtime.ResetConnectionState();

        Assert.IsNull(runtime.NativeAirbus.Battery1On);
        Assert.IsNull(runtime.A330.FuelPumpInputStates[0]);
        Assert.IsFalse(runtime.ResolveFbwSpoilersArmed(0, now.AddSeconds(1)));
    }

    [TestMethod]
    public void AircraftResetPreventsCrossAircraftContamination()
    {
        var runtime = new NativeAircraftRuntimeState();
        runtime.TryApplyMobiFlightReadback(Request.A310FuelPump4, 1);
        runtime.TryApplyInputEvent(Request.A330Adirs2InputEvent, 2);
        runtime.TryApplyMobiFlightReadback(Request.FbwBattery1Auto, 1);

        runtime.ResetAircraftState();

        Assert.IsNull(runtime.A310.FuelPumpStates[3]);
        Assert.IsNull(runtime.A330.AdirsInputStates[1]);
        Assert.IsNull(runtime.Fbw.Battery1Auto);
    }

    [TestMethod]
    public void CommandFallbackExpiresAtExistingTenSecondWindow()
    {
        var runtime = new NativeAircraftRuntimeState();
        var now = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
        runtime.RecordFbwAutobrakeCommand(3, now);

        Assert.AreEqual(3d, runtime.ResolveFbwAutobrake(now.AddSeconds(9)));
        Assert.IsNull(runtime.ResolveFbwAutobrake(now.AddSeconds(10)));
    }

    [TestMethod]
    public void TypedAdirsReadbackPrecedesConflictingUntypedAndCommandedValues()
    {
        var runtime = new NativeAircraftRuntimeState();
        var now = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
        runtime.TryApplyMobiFlightReadback(Request.FbwAdirs1Selector, 0);
        runtime.TryApplyMobiFlightReadback(Request.FbwAdirs1SelectorTyped, 2);
        runtime.RecordFbwAdirsCommand(1, 1, now);

        Assert.AreEqual(2d, runtime.ResolveFbwAdirsSelector(1, now.AddSeconds(1)));
    }

    [TestMethod]
    public void LiveReadbackReconcilesAndOverridesCommandFallback()
    {
        var runtime = new NativeAircraftRuntimeState();
        var now = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
        runtime.RecordFbwAutobrakeCommand(3, now);
        runtime.TryApplyMobiFlightReadback(Request.FbwAutobrakeLevel, 2);

        Assert.AreEqual(2d, runtime.ResolveFbwAutobrake(now.AddSeconds(1)));
    }

    [TestMethod]
    public void A310MapsFlowObservationAndFuelPumpState()
    {
        var runtime = new NativeAircraftRuntimeState();

        runtime.TryApplyMobiFlightReadback(Request.A310NavLogoLight, 1);
        runtime.TryApplyMobiFlightReadback(Request.A310ApuFireTest, 1);
        runtime.TryApplyMobiFlightReadback(Request.A310FuelPump7, 1);

        Assert.AreEqual(1f, runtime.A310.InitialLightStates[0]);
        Assert.IsTrue(runtime.A310.ApuFireTestObserved);
        Assert.AreEqual(1f, runtime.A310.FuelPumpStates[6]);
    }

    [TestMethod]
    public void A330MapsFuelLightTcasAndAdirsInputEvents()
    {
        var runtime = new NativeAircraftRuntimeState();

        runtime.TryApplyInputEvent(Request.A330FuelPump3InputEvent, 1);
        runtime.TryApplyInputEvent(Request.A330LandingLightInputEvent, 1);
        runtime.TryApplyInputEvent(Request.A330TcasTrafficInputEvent, 2);
        runtime.TryApplyInputEvent(Request.A330Adirs3InputEvent, 1);

        Assert.AreEqual(1d, runtime.A330.FuelPumpInputStates[2]);
        Assert.AreEqual(1d, runtime.A330.LandingLightInputState);
        Assert.AreEqual(2d, runtime.A330.TcasTrafficInputState);
        Assert.AreEqual(1d, runtime.A330.AdirsInputStates[2]);
    }

    [TestMethod]
    public void FbwMapsBatterySpoilersAutobrakeTcasAndAdirs()
    {
        var runtime = new NativeAircraftRuntimeState();

        runtime.TryApplyMobiFlightReadback(Request.FbwBattery1AutoTyped, 1);
        runtime.TryApplyMobiFlightReadback(Request.FbwSpoilersArmed, 1);
        runtime.TryApplyMobiFlightReadback(Request.FbwAutobrakeLevel, 2);
        runtime.TryApplyMobiFlightReadback(Request.FbwTcasMode, 3);
        runtime.TryApplyMobiFlightReadback(Request.FbwAdirs2SelectorTyped, 1);

        Assert.IsTrue(runtime.Fbw.Battery1AutoTyped);
        Assert.IsTrue(runtime.ResolveFbwBattery(1, genericMasterBattery: 0));
        Assert.IsTrue(runtime.Fbw.SpoilersArmed);
        Assert.AreEqual(2f, runtime.Fbw.AutobrakeLevel);
        Assert.AreEqual(3f, runtime.Fbw.TcasMode);
        Assert.AreEqual(1f, runtime.Fbw.Adirs2SelectorTyped);
    }
}
