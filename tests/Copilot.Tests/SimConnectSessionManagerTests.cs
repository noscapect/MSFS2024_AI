using Microsoft.FlightSimulator.SimConnect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Simulation;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Copilot.Tests;

[TestClass]
public sealed class SimConnectSessionManagerTests
{
    [TestMethod]
    public void ConnectCreatesOnlyOneActiveConnection()
    {
        var factory = new FakeConnectionFactory();
        using var manager = CreateManager(factory);

        manager.Connect();
        manager.Connect();

        Assert.AreEqual(1, factory.CreateCount);
        Assert.IsTrue(manager.HasActiveConnection);
    }

    [TestMethod]
    public void OpenMarksConnectedWithoutCreatingAnotherConnection()
    {
        var factory = new FakeConnectionFactory();
        using var manager = CreateManager(factory);
        var callbackCount = 0;
        manager.Connected += _ => callbackCount++;

        manager.Connect();
        factory.LastConnection!.RaiseOpened();
        manager.Connect();

        Assert.IsTrue(manager.IsConnected);
        Assert.AreEqual(1, callbackCount);
        Assert.AreEqual(1, factory.CreateCount);
    }

    [TestMethod]
    public void FailedConnectionDisposesPartialStateAndSchedulesReconnect()
    {
        var factory = new FakeConnectionFactory();
        var timers = new FakeTimerFactory();
        using var manager = CreateManager(
            factory,
            timers,
            _ => throw new COMException("configuration failed"));
        var failures = 0;
        manager.ConnectionFailed += _ => failures++;

        manager.Connect();

        Assert.AreEqual(1, failures);
        Assert.IsTrue(factory.LastConnection!.Disposed);
        Assert.IsFalse(manager.HasActiveConnection);
        Assert.IsTrue(manager.IsReconnectScheduled);
        Assert.AreEqual(1, timers.CreateCount);
        Assert.AreEqual(
            SimConnectSessionManager.ReconnectIntervalMilliseconds,
            timers.LastTimer!.IntervalMilliseconds);
    }

    [TestMethod]
    public void DisconnectClearsAndDisposesActiveConnection()
    {
        var factory = new FakeConnectionFactory();
        using var manager = CreateManager(factory);
        manager.Connect();
        var connection = factory.LastConnection!;
        connection.RaiseOpened();

        connection.RaiseQuit();

        Assert.IsFalse(manager.HasActiveConnection);
        Assert.IsFalse(manager.IsConnected);
        Assert.IsTrue(connection.Disposed);
    }

    [TestMethod]
    public void RepeatedDisconnectSchedulesExactlyOneReconnect()
    {
        var factory = new FakeConnectionFactory();
        var timers = new FakeTimerFactory();
        using var manager = CreateManager(factory, timers);
        var callbacks = 0;
        manager.Disconnected += () => callbacks++;
        manager.Connect();
        var connection = factory.LastConnection!;

        connection.RaiseQuit();
        connection.RaiseQuit();

        Assert.AreEqual(1, callbacks);
        Assert.AreEqual(1, timers.CreateCount);
        Assert.IsTrue(manager.IsReconnectScheduled);
    }

    [TestMethod]
    public void ReconnectTimerCreatesOneReplacementConnection()
    {
        var factory = new FakeConnectionFactory();
        var timers = new FakeTimerFactory();
        using var manager = CreateManager(factory, timers);
        manager.Connect();
        factory.LastConnection!.RaiseQuit();

        timers.LastTimer!.Fire();

        Assert.AreEqual(2, factory.CreateCount);
        Assert.IsTrue(manager.HasActiveConnection);
        Assert.IsFalse(manager.IsReconnectScheduled);
        Assert.IsTrue(timers.Timers[0].Disposed);
    }

    [TestMethod]
    public void ReceivePendingMessageForwardsOnlyWhileSessionIsActive()
    {
        var factory = new FakeConnectionFactory();
        var manager = CreateManager(factory);
        manager.Connect();
        var connection = factory.LastConnection!;

        manager.ReceivePendingMessage();
        manager.Dispose();
        manager.ReceivePendingMessage();

        Assert.AreEqual(1, connection.ReceiveCount);
    }

    [TestMethod]
    public void ReconnectDoesNotRunAfterDispose()
    {
        var factory = new FakeConnectionFactory();
        var timers = new FakeTimerFactory();
        var manager = CreateManager(factory, timers);
        manager.Connect();
        factory.LastConnection!.RaiseQuit();
        var timer = timers.LastTimer!;

        manager.Dispose();
        timer.FireEvenIfDisposed();

        Assert.AreEqual(1, factory.CreateCount);
        Assert.IsTrue(timer.Disposed);
        Assert.IsFalse(manager.IsReconnectScheduled);
    }

    [TestMethod]
    public void DisposeIsIdempotent()
    {
        var factory = new FakeConnectionFactory();
        var manager = CreateManager(factory);
        manager.Connect();
        var connection = factory.LastConnection!;

        manager.Dispose();
        manager.Dispose();

        Assert.AreEqual(1, connection.DisposeCount);
    }

    [TestMethod]
    public void CallbacksDoNotFireAfterDispose()
    {
        var factory = new FakeConnectionFactory();
        var manager = CreateManager(factory);
        var callbacks = 0;
        manager.Connected += _ => callbacks++;
        manager.Disconnected += () => callbacks++;
        manager.SimConnectException += _ => callbacks++;
        manager.Connect();
        var connection = factory.LastConnection!;

        manager.Dispose();
        connection.RaiseOpened();
        connection.RaiseQuit();
        connection.RaiseException();

        Assert.AreEqual(0, callbacks);
    }

    [TestMethod]
    public void RepeatedFailedAttemptsDoNotDuplicateReconnectTimer()
    {
        var factory = new FakeConnectionFactory { ThrowOnCreate = true };
        var timers = new FakeTimerFactory();
        using var manager = CreateManager(factory, timers);

        manager.Connect();
        manager.Connect();

        Assert.AreEqual(2, factory.CreateCount);
        Assert.AreEqual(1, timers.CreateCount);
        timers.LastTimer!.Fire();
        Assert.AreEqual(3, factory.CreateCount);
        Assert.AreEqual(2, timers.CreateCount);
        Assert.IsTrue(timers.Timers[0].Disposed);
    }

    private static SimConnectSessionManager CreateManager(
        FakeConnectionFactory connections,
        FakeTimerFactory? timers = null,
        Action<SimConnect>? configureConnection = null) =>
        new(
            connections.Create,
            configureConnection ?? (_ => { }),
            timers == null ? null : timers.Create);

    private sealed class FakeConnectionFactory
    {
        public int CreateCount { get; private set; }
        public bool ThrowOnCreate { get; set; }
        public FakeSessionConnection? LastConnection { get; private set; }

        public ISimConnectSessionConnection Create()
        {
            CreateCount++;
            if (ThrowOnCreate)
            {
                throw new COMException("simulator unavailable");
            }

            LastConnection = new FakeSessionConnection();
            return LastConnection;
        }
    }

    private sealed class FakeSessionConnection : ISimConnectSessionConnection
    {
        private readonly SimConnect _connection =
            (SimConnect)FormatterServices.GetUninitializedObject(typeof(SimConnect));

        public event Action<ISimConnectSessionConnection, SIMCONNECT_RECV_OPEN>? Opened;
        public event Action<ISimConnectSessionConnection, SIMCONNECT_RECV>? Quit;
        public event Action<ISimConnectSessionConnection, SIMCONNECT_RECV_EXCEPTION>? ExceptionReceived;

        public SimConnect? Connection => _connection;
        public bool Disposed { get; private set; }
        public int DisposeCount { get; private set; }
        public int ReceiveCount { get; private set; }

        public void ReceiveMessage() => ReceiveCount++;

        public void Dispose()
        {
            DisposeCount++;
            Disposed = true;
        }

        public void RaiseOpened() => Opened?.Invoke(this, new SIMCONNECT_RECV_OPEN());
        public void RaiseQuit() => Quit?.Invoke(this, new SIMCONNECT_RECV());
        public void RaiseException() =>
            ExceptionReceived?.Invoke(this, new SIMCONNECT_RECV_EXCEPTION());
    }

    private sealed class FakeTimerFactory
    {
        public List<FakeReconnectTimer> Timers { get; } = new();
        public int CreateCount => Timers.Count;
        public FakeReconnectTimer? LastTimer => Timers.LastOrDefault();

        public ISimConnectReconnectTimer Create()
        {
            var timer = new FakeReconnectTimer();
            Timers.Add(timer);
            return timer;
        }
    }

    private sealed class FakeReconnectTimer : ISimConnectReconnectTimer
    {
        private Action? _tick;

        public int IntervalMilliseconds { get; private set; }
        public bool Disposed { get; private set; }

        public void Start(int intervalMilliseconds, Action tick)
        {
            IntervalMilliseconds = intervalMilliseconds;
            _tick = tick;
        }

        public void Stop()
        {
        }

        public void Dispose() => Disposed = true;

        public void Fire()
        {
            if (!Disposed)
            {
                _tick?.Invoke();
            }
        }

        public void FireEvenIfDisposed() => _tick?.Invoke();
    }
}
