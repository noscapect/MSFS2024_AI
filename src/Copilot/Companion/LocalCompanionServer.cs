using Msfs2024Ai.Copilot.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Msfs2024Ai.Copilot.Companion;

internal sealed class LocalCompanionServer : IDisposable
{
    public const int DefaultPort = 49384;
    private const int MaximumFrameBytes = 256 * 1024;

    private readonly RelayCompanionOptions _options;
    private readonly CompanionBridge _bridge;
    private readonly CompanionCipher _cipher;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly object _clientSync = new();
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly Task _acceptTask;
    private bool _disposed;

    public LocalCompanionServer(
        RelayCompanionOptions options,
        CompanionBridge bridge,
        int port = DefaultPort)
    {
        _options = options;
        _bridge = bridge;
        _cipher = new CompanionCipher(
            options.SessionId,
            options.PairingSecret,
            "desktop");
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start(1);
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _bridge.MessagePublished += SendPublishedMessage;
        _acceptTask = Task.Run(AcceptLoopAsync);
        AppLog.Write($"Encrypted Android companion LAN listener started on TCP {Port}.");
    }

    public int Port { get; }

    private async Task AcceptLoopAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            TcpClient accepted;
            try
            {
                accepted = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException) when (_cancellation.IsCancellationRequested)
            {
                return;
            }

            accepted.NoDelay = true;
            ReplaceClient(accepted);
            _ = Task.Run(() => ReceiveLoopAsync(accepted));
        }
    }

    private void ReplaceClient(TcpClient client)
    {
        lock (_clientSync)
        {
            _stream?.Dispose();
            _client?.Close();
            _client = client;
            _stream = client.GetStream();
        }
        AppLog.Write("Android companion connected over the encrypted LAN transport.");
    }

    private async Task ReceiveLoopAsync(TcpClient client)
    {
        try
        {
            var stream = client.GetStream();
            while (!_cancellation.IsCancellationRequested && client.Connected)
            {
                var lengthBytes = await ReadExactlyAsync(
                    stream,
                    4,
                    _cancellation.Token).ConfigureAwait(false);
                if (lengthBytes == null)
                {
                    return;
                }
                var length = IPAddress.NetworkToHostOrder(
                    BitConverter.ToInt32(lengthBytes, 0));
                if (length <= 0 || length > MaximumFrameBytes)
                {
                    AppLog.Write("Rejected an invalid Android companion LAN frame length.");
                    return;
                }
                var payload = await ReadExactlyAsync(
                    stream,
                    length,
                    _cancellation.Token).ConfigureAwait(false);
                if (payload == null)
                {
                    return;
                }
                var envelope = System.Text.Encoding.UTF8.GetString(payload);
                if (!_cipher.TryOpen(envelope, out var command, out var decryptError))
                {
                    AppLog.Write(
                        "Rejected encrypted Android companion LAN message: "
                        + decryptError);
                    continue;
                }
                if (!CompanionCommandGate.TryForward(
                        _bridge,
                        command,
                        _options.ControlsAllowed,
                        out var commandError))
                {
                    AppLog.Write(
                        "Rejected Android companion LAN command: "
                        + commandError);
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException
            or SocketException
            or ObjectDisposedException
            or OperationCanceledException)
        {
            if (!_cancellation.IsCancellationRequested)
            {
                AppLog.Write("Android companion LAN connection closed: " + exception.Message);
            }
        }
        finally
        {
            lock (_clientSync)
            {
                if (ReferenceEquals(_client, client))
                {
                    _stream?.Dispose();
                    _stream = null;
                    _client?.Close();
                    _client = null;
                }
            }
        }
    }

    private void SendPublishedMessage(string plaintext)
    {
        if (_disposed)
        {
            return;
        }
        var envelope = _cipher.Seal(plaintext);
        _ = Task.Run(() => SendFrameAsync(envelope));
    }

    private async Task SendFrameAsync(string envelope)
    {
        await _sendGate.WaitAsync().ConfigureAwait(false);
        try
        {
            NetworkStream? stream;
            lock (_clientSync)
            {
                stream = _stream;
            }
            if (stream == null)
            {
                return;
            }
            var payload = System.Text.Encoding.UTF8.GetBytes(envelope);
            var length = BitConverter.GetBytes(
                IPAddress.HostToNetworkOrder(payload.Length));
            await stream.WriteAsync(length, 0, length.Length).ConfigureAwait(false);
            await stream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException
            or SocketException
            or ObjectDisposedException)
        {
            AppLog.Write("Could not send Android companion LAN state: " + exception.Message);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private static async Task<byte[]?> ReadExactlyAsync(
        Stream stream,
        int count,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = await stream.ReadAsync(
                buffer,
                offset,
                count - offset,
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }
            offset += read;
        }
        return buffer;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _bridge.MessagePublished -= SendPublishedMessage;
        _cancellation.Cancel();
        _listener.Stop();
        lock (_clientSync)
        {
            _stream?.Dispose();
            _client?.Close();
            _stream = null;
            _client = null;
        }
        try
        {
            _acceptTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }
        _sendGate.Dispose();
        _cancellation.Dispose();
    }
}
