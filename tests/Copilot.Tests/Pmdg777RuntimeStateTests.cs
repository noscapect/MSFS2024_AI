using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot;
using Msfs2024Ai.Copilot.AircraftAdapters.Pmdg777;
using Msfs2024Ai.Copilot.Telemetry;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class Pmdg777RuntimeStateTests
{
    [TestMethod]
    public void AircraftDataMarksRuntimeReadyAndExposesRepresentativeReadbacks()
    {
        var runtime = new Pmdg777RuntimeState();
        var data = CreateData();
        data[28] = 1;
        data[37] = 1;
        data[50] = 1;
        data[52] = 1;
        data[113] = 1;
        data[140] = 0;
        data[484] = 1;

        Assert.IsFalse(runtime.DataReady);
        var update = runtime.ApplyAircraftData(data);

        Assert.IsTrue(update.Accepted);
        Assert.IsTrue(update.BecameDataReady);
        Assert.IsTrue(runtime.DataReady);
        Assert.IsNotNull(runtime.State);
        Assert.IsTrue(runtime.State.BatteryOn);
        Assert.IsTrue(runtime.State.PrimaryExternalPowerOn);
        Assert.IsTrue(runtime.State.PrimaryExternalPowerAvailable);
        Assert.IsTrue(runtime.State.AdiruOn);
        Assert.IsTrue(runtime.State.NavigationLightOn);
        Assert.IsTrue(runtime.State.EngineOneStartSelectorStart);
        Assert.IsTrue(runtime.State.EngineOneStartValveOpen);
    }

    [TestMethod]
    public void InvalidAircraftDataDoesNotMarkRuntimeReady()
    {
        var runtime = new Pmdg777RuntimeState();

        var update = runtime.ApplyAircraftData(new byte[Pmdg777ControlProfile.DataSize]);

        Assert.IsFalse(update.Accepted);
        Assert.IsFalse(runtime.DataReady);
        Assert.IsNull(runtime.State);
    }

    [TestMethod]
    public void ControlDataMarksRuntimeReadyAndStoresReadback()
    {
        var runtime = new Pmdg777RuntimeState();
        var control = new Pmdg777Control { Event = 42, Parameter = 7 };

        Assert.IsFalse(runtime.ControlReady);
        Assert.IsTrue(runtime.ApplyControlData(control));
        Assert.IsTrue(runtime.ControlReady);
        Assert.AreEqual(42U, runtime.ControlState.Event);
        Assert.AreEqual(7U, runtime.ControlState.Parameter);
        Assert.IsFalse(runtime.ApplyControlData(control));
    }

    [TestMethod]
    public void FireAndOxygenObservationsAreStickyUntilCommandedStateClear()
    {
        var runtime = new Pmdg777RuntimeState();

        runtime.RecordFireAndOxygenObservations(1.0, 1.0);
        runtime.RecordFireAndOxygenObservations(0.0, 0.0);

        Assert.IsTrue(runtime.FireOverheatTestObserved);
        Assert.IsTrue(runtime.FirstOfficerOxygenTestObserved);
        runtime.ClearObservedTests();
        Assert.IsFalse(runtime.FireOverheatTestObserved);
        Assert.IsFalse(runtime.FirstOfficerOxygenTestObserved);
    }

    [TestMethod]
    public void AdiruObservationTracksOffDurationAndClearsWhenItReturnsOn()
    {
        var runtime = new Pmdg777RuntimeState();
        var now = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);

        runtime.ApplyAircraftData(CreateData());
        runtime.ObserveAdiruState(now);
        runtime.ObserveAdiruState(now.AddSeconds(30));
        Assert.AreEqual(now, runtime.AdiruOffSinceUtc!.Value);

        var on = CreateData();
        on[28] = 1;
        runtime.ApplyAircraftData(on);
        runtime.ObserveAdiruState(now.AddSeconds(31));
        Assert.IsNull(runtime.AdiruOffSinceUtc);
    }

    [TestMethod]
    public void RawDataDiagnosticsAreSuppressedUntilTheReadbackChanges()
    {
        var runtime = new Pmdg777RuntimeState();
        var data = CreateData();

        Assert.AreEqual(
            "PMDG 777X raw-data change monitor initialized for Flow 1/2 validation.",
            runtime.ObserveRawDataChanges(data));
        Assert.IsTrue(runtime.HasRawSnapshot);
        Assert.IsNull(runtime.ObserveRawDataChanges(data));

        data[37] = 1;
        Assert.AreEqual("PMDG 777X raw-data changes: 37:0>1.", runtime.ObserveRawDataChanges(data));
    }

    [TestMethod]
    public void FlowReadbackDiagnosticIsSuppressedUntilTheStateChanges()
    {
        var runtime = new Pmdg777RuntimeState();
        var data = CreateData();
        runtime.ApplyAircraftData(data);

        Assert.IsNotNull(runtime.ObserveFlowOneDiagnostic(runtime.State!, false));
        Assert.IsNull(runtime.ObserveFlowOneDiagnostic(runtime.State!, false));

        data[37] = 1;
        runtime.ApplyAircraftData(data);
        var diagnostic = runtime.ObserveFlowOneDiagnostic(runtime.State!, false);
        Assert.IsNotNull(diagnostic);
        StringAssert.Contains(diagnostic!, "BAT=ON");
    }

    [TestMethod]
    public void ConnectionResetClearsAllRuntimeReadbackAndObservationState()
    {
        var runtime = new Pmdg777RuntimeState();
        var now = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
        runtime.ApplyAircraftData(CreateData());
        runtime.ApplyControlData(new Pmdg777Control { Event = 1, Parameter = 2 });
        runtime.RecordFireAndOxygenObservations(1, 1);
        runtime.ObserveAdiruState(now);
        runtime.ObserveRawDataChanges(CreateData());

        runtime.ResetConnectionState();

        Assert.IsFalse(runtime.DataReady);
        Assert.IsFalse(runtime.ControlReady);
        Assert.IsNull(runtime.State);
        Assert.AreEqual(0U, runtime.ControlState.Event);
        Assert.IsFalse(runtime.HasRawSnapshot);
        Assert.IsFalse(runtime.FireOverheatTestObserved);
        Assert.IsFalse(runtime.FirstOfficerOxygenTestObserved);
        Assert.IsNull(runtime.AdiruOffSinceUtc);
    }

    private static byte[] CreateData()
    {
        var data = new byte[Pmdg777ControlProfile.DataSize];
        data[542] = 6;
        return data;
    }
}
