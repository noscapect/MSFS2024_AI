using Microsoft.FlightSimulator.SimConnect;
using System.Runtime.InteropServices;

namespace Msfs2024Ai.Copilot.Simulation;

internal sealed class SimConnectSessionManager : IDisposable
{
    internal const int ReconnectIntervalMilliseconds = 5000;

    private readonly Func<ISimConnectSessionConnection> _connectionFactory;
    private readonly Func<ISimConnectReconnectTimer> _reconnectTimerFactory;
    private readonly Action<Microsoft.FlightSimulator.SimConnect.SimConnect> _configureConnection;
    private ISimConnectSessionConnection? _sessionConnection;
    private ISimConnectReconnectTimer? _reconnectTimer;
    private bool _connecting;
    private bool _connected;
    private bool _disposed;

    public SimConnectSessionManager(
        Func<ISimConnectSessionConnection> connectionFactory,
        Action<Microsoft.FlightSimulator.SimConnect.SimConnect> configureConnection,
        Func<ISimConnectReconnectTimer>? reconnectTimerFactory = null)
    {
        _connectionFactory = connectionFactory;
        _configureConnection = configureConnection;
        _reconnectTimerFactory = reconnectTimerFactory
                                 ?? (() => new WinFormsSimConnectReconnectTimer());
    }

    public event Action<SIMCONNECT_RECV_OPEN>? Connected;
    public event Action? Disconnected;
    public event Action<SIMCONNECT_RECV_EXCEPTION>? SimConnectException;
    public event Action<COMException>? ConnectionFailed;

    public Microsoft.FlightSimulator.SimConnect.SimConnect? Connection =>
        _sessionConnection?.Connection;

    public bool IsConnected => _connected;

    internal bool IsReconnectScheduled => _reconnectTimer != null;
    internal bool HasActiveConnection => _sessionConnection != null;

    public void Connect()
    {
        if (_disposed || _connecting || _sessionConnection != null)
        {
            return;
        }

        _connecting = true;
        ISimConnectSessionConnection? connection = null;
        try
        {
            connection = _connectionFactory();
            connection.Opened += HandleOpened;
            connection.Quit += HandleQuit;
            connection.ExceptionReceived += HandleExceptionReceived;
            var nativeConnection = connection.Connection;
            if (nativeConnection != null)
            {
                _configureConnection(nativeConnection);
            }
            _sessionConnection = connection;
            connection = null;
        }
        catch (COMException exception)
        {
            DisposeSafely(connection);
            try
            {
                if (!_disposed)
                {
                    ConnectionFailed?.Invoke(exception);
                }
            }
            finally
            {
                ScheduleReconnect();
            }
        }
        finally
        {
            _connecting = false;
        }
    }

    public void ReceivePendingMessage()
    {
        if (!_disposed)
        {
            _sessionConnection?.ReceiveMessage();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelReconnect();
        DisposeConnection();
    }

    private void HandleOpened(
        ISimConnectSessionConnection connection,
        SIMCONNECT_RECV_OPEN data)
    {
        if (_disposed || !ReferenceEquals(connection, _sessionConnection))
        {
            return;
        }

        _connected = true;
        CancelReconnect();
        Connected?.Invoke(data);
    }

    private void HandleQuit(
        ISimConnectSessionConnection connection,
        SIMCONNECT_RECV data)
    {
        if (_disposed || !ReferenceEquals(connection, _sessionConnection))
        {
            return;
        }

        DisposeConnection();
        try
        {
            if (!_disposed)
            {
                Disconnected?.Invoke();
            }
        }
        finally
        {
            ScheduleReconnect();
        }
    }

    private void HandleExceptionReceived(
        ISimConnectSessionConnection connection,
        SIMCONNECT_RECV_EXCEPTION data)
    {
        if (!_disposed && ReferenceEquals(connection, _sessionConnection))
        {
            SimConnectException?.Invoke(data);
        }
    }

    private void ScheduleReconnect()
    {
        if (_disposed || _reconnectTimer != null)
        {
            return;
        }

        var timer = _reconnectTimerFactory();
        _reconnectTimer = timer;
        timer.Start(ReconnectIntervalMilliseconds, () => OnReconnectTimerTick(timer));
    }

    private void OnReconnectTimerTick(ISimConnectReconnectTimer timer)
    {
        if (_disposed || !ReferenceEquals(timer, _reconnectTimer))
        {
            return;
        }

        timer.Stop();
        timer.Dispose();
        _reconnectTimer = null;
        Connect();
    }

    private void CancelReconnect()
    {
        var timer = _reconnectTimer;
        _reconnectTimer = null;
        if (timer == null)
        {
            return;
        }

        timer.Stop();
        timer.Dispose();
    }

    private void DisposeConnection()
    {
        var connection = _sessionConnection;
        _sessionConnection = null;
        _connected = false;
        if (connection == null)
        {
            return;
        }

        connection.Opened -= HandleOpened;
        connection.Quit -= HandleQuit;
        connection.ExceptionReceived -= HandleExceptionReceived;
        DisposeSafely(connection);
    }

    private static void DisposeSafely(ISimConnectSessionConnection? connection)
    {
        try
        {
            connection?.Dispose();
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException)
        {
            // A session already closed by MSFS is still considered disposed.
        }
    }
}

internal interface ISimConnectSessionConnection : IDisposable
{
    event Action<ISimConnectSessionConnection, SIMCONNECT_RECV_OPEN>? Opened;
    event Action<ISimConnectSessionConnection, SIMCONNECT_RECV>? Quit;
    event Action<ISimConnectSessionConnection, SIMCONNECT_RECV_EXCEPTION>? ExceptionReceived;

    Microsoft.FlightSimulator.SimConnect.SimConnect? Connection { get; }

    void ReceiveMessage();
}

internal sealed class SimConnectSessionConnection : ISimConnectSessionConnection
{
    private Microsoft.FlightSimulator.SimConnect.SimConnect? _connection;

    public SimConnectSessionConnection(
        string name,
        IntPtr windowHandle,
        uint userMessageId)
    {
        Microsoft.FlightSimulator.SimConnect.SimConnect? connection = null;
        try
        {
            connection = new Microsoft.FlightSimulator.SimConnect.SimConnect(
                name,
                windowHandle,
                userMessageId,
                null,
                0);
            connection.OnRecvOpen += HandleOpened;
            connection.OnRecvQuit += HandleQuit;
            connection.OnRecvException += HandleExceptionReceived;
            _connection = connection;
        }
        catch
        {
            DisposeNativeConnection(connection);
            throw;
        }
    }

    public event Action<ISimConnectSessionConnection, SIMCONNECT_RECV_OPEN>? Opened;
    public event Action<ISimConnectSessionConnection, SIMCONNECT_RECV>? Quit;
    public event Action<ISimConnectSessionConnection, SIMCONNECT_RECV_EXCEPTION>? ExceptionReceived;

    public Microsoft.FlightSimulator.SimConnect.SimConnect Connection =>
        _connection ?? throw new ObjectDisposedException(nameof(SimConnectSessionConnection));

    public void ReceiveMessage() => _connection?.ReceiveMessage();

    public void Dispose()
    {
        var connection = _connection;
        _connection = null;
        if (connection == null)
        {
            return;
        }

        connection.OnRecvOpen -= HandleOpened;
        connection.OnRecvQuit -= HandleQuit;
        connection.OnRecvException -= HandleExceptionReceived;
        DisposeNativeConnection(connection);
    }

    private void HandleOpened(
        Microsoft.FlightSimulator.SimConnect.SimConnect sender,
        SIMCONNECT_RECV_OPEN data) =>
        Opened?.Invoke(this, data);

    private void HandleQuit(
        Microsoft.FlightSimulator.SimConnect.SimConnect sender,
        SIMCONNECT_RECV data) =>
        Quit?.Invoke(this, data);

    private void HandleExceptionReceived(
        Microsoft.FlightSimulator.SimConnect.SimConnect sender,
        SIMCONNECT_RECV_EXCEPTION data) =>
        ExceptionReceived?.Invoke(this, data);

    private static void DisposeNativeConnection(
        Microsoft.FlightSimulator.SimConnect.SimConnect? connection)
    {
        try
        {
            connection?.Dispose();
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException)
        {
            // SimConnect may already have released the native session.
        }
    }
}

internal interface ISimConnectReconnectTimer : IDisposable
{
    void Start(int intervalMilliseconds, Action tick);
    void Stop();
}

internal sealed class WinFormsSimConnectReconnectTimer : ISimConnectReconnectTimer
{
    private System.Windows.Forms.Timer? _timer;

    public void Start(int intervalMilliseconds, Action tick)
    {
        if (_timer != null)
        {
            return;
        }

        var timer = new System.Windows.Forms.Timer { Interval = intervalMilliseconds };
        timer.Tick += (_, _) => tick();
        _timer = timer;
        timer.Start();
    }

    public void Stop() => _timer?.Stop();

    public void Dispose()
    {
        var timer = _timer;
        _timer = null;
        timer?.Stop();
        timer?.Dispose();
    }
}
