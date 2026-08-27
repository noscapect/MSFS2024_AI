using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Telemetry;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class PmdgNg3RuntimeStateTests
{
    [TestMethod]
    public void DataApplicationParsesElectricalAndIrsReadbacksAndMarksReady()
    {
        var runtime = new PmdgNg3RuntimeState();
        var data = CreateData();
        data[11] = 2;
        data[12] = 1;
        data[133] = 1;
        data[142] = 1;
        data[143] = 1;
        data[145] = 1;
        data[189] = 1;

        Assert.IsFalse(runtime.IsReady);
        var update = runtime.ApplyData(data);

        Assert.IsTrue(update.BecameReady);
        Assert.IsTrue(runtime.IsReady);
        Assert.IsNotNull(runtime.State);
        Assert.AreEqual(2, runtime.State.IrsLeftMode);
        Assert.AreEqual(1, runtime.State.IrsRightMode);
        Assert.AreEqual(1, runtime.State.BatterySelector);
        Assert.IsTrue(runtime.State.GroundPowerAvailable);
        Assert.IsTrue(runtime.State.GroundPowerOn);
        Assert.IsTrue(runtime.State.EngineGen1On);
        Assert.IsTrue(runtime.State.AcTransferBus1Powered);
    }

    [TestMethod]
    public void ConnectionResetClearsLiveStateApuTimingAndCommandFallbacks()
    {
        var runtime = new PmdgNg3RuntimeState();
        var now = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
        var data = CreateData();
        data[155] = 1;
        WriteSingle(data, 200, 1);
        runtime.ApplyData(data);
        runtime.ObserveAircraftFrame(true, now);
        runtime.RecordLeftIrsCommand(2, now);

        runtime.ResetConnectionState();

        Assert.IsFalse(runtime.IsReady);
        Assert.IsNull(runtime.State);
        Assert.IsNull(runtime.ApuAvailableSinceUtc);
        Assert.IsFalse(runtime.FireFaultInopTestCompleted);

        var afterReset = CreateData();
        afterReset[11] = 0;
        runtime.ApplyData(afterReset);
        Assert.AreEqual(0d, runtime.ResolveLeftIrsMode(now.AddSeconds(1)));
    }

    [TestMethod]
    public void ApuOffBusObservationRemainsStickyUntilPowerIsEstablished()
    {
        var runtime = new PmdgNg3RuntimeState();
        var now = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
        var offBus = CreateData();
        offBus[155] = 1;
        WriteSingle(offBus, 200, 1);
        runtime.ApplyData(offBus);
        Assert.IsTrue(runtime.ObserveAircraftFrame(true, now).Available);

        var powered = CreateData();
        powered[147] = 1;
        powered[148] = 1;
        powered[189] = 1;
        powered[190] = 1;
        WriteSingle(powered, 200, 1);
        runtime.ApplyData(powered);

        var observed = runtime.ObserveAircraftFrame(true, now.AddSeconds(1));

        Assert.IsTrue(observed.PowerEstablished);
        Assert.IsTrue(observed.Available);
    }

    [TestMethod]
    public void ApuBleedWarmupCompletesAtExistingSixtySecondBoundary()
    {
        var runtime = new PmdgNg3RuntimeState();
        var now = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
        var data = CreateData();
        data[155] = 1;
        WriteSingle(data, 200, 1);
        runtime.ApplyData(data);

        Assert.IsFalse(runtime.ObserveAircraftFrame(true, now).BleedWarmupComplete);
        Assert.IsFalse(runtime.ObserveAircraftFrame(true, now.AddSeconds(59)).BleedWarmupComplete);
        Assert.IsTrue(runtime.ObserveAircraftFrame(true, now.AddSeconds(60)).BleedWarmupComplete);
    }

    [TestMethod]
    public void CommandedIrsReadbackTakesPrecedenceUntilExistingTimeoutExpires()
    {
        var runtime = new PmdgNg3RuntimeState();
        var now = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
        var data = CreateData();
        data[11] = 0;
        runtime.ApplyData(data);
        runtime.RecordLeftIrsCommand(2, now);

        Assert.AreEqual(2d, runtime.ResolveLeftIrsMode(now.AddMinutes(1)));
        Assert.AreEqual(0d, runtime.ResolveLeftIrsMode(now.AddMinutes(2)));
    }

    [TestMethod]
    public void CommandedLandingLightFallbackAppliesBeforeConflictingSdkReadback()
    {
        var runtime = new PmdgNg3RuntimeState();
        var now = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
        var data = CreateData();
        data[372] = 0;
        runtime.ApplyData(data);
        runtime.RecordLandingLightCommand(2, now);

        Assert.AreEqual(2d, runtime.ResolveLandingLightSelector(true, now.AddSeconds(1)));
    }

    [TestMethod]
    public void FireTestObservationIsStickyUntilReset()
    {
        var runtime = new PmdgNg3RuntimeState();
        var active = CreateData();
        active[579] = 0;
        active[587] = 1;
        active[588] = 1;
        runtime.ApplyData(active);

        var released = CreateData();
        released[579] = 1;
        runtime.ApplyData(released);

        Assert.IsTrue(runtime.FireFaultInopTestCompleted);
        runtime.ClearCommandedState();
        Assert.IsFalse(runtime.FireFaultInopTestCompleted);
    }

    private static byte[] CreateData() => new byte[SimConnectContractConstants.PmdgNg3DataSize];

    private static void WriteSingle(byte[] data, int offset, float value) =>
        BitConverter.GetBytes(value).CopyTo(data, offset);
}
