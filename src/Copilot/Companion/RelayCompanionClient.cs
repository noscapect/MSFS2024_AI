using Msfs2024Ai.Copilot.Diagnostics;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace Msfs2024Ai.Copilot.Companion;

internal sealed class RelayCompanionClient : IDisposable
{
    private readonly RelayCompanionOptions _options;
    private readonly CompanionBridge _bridge;
    private readonly CompanionCipher _cipher;
    private readonly ConcurrentQueue<string> _outgoing = new();
    private readonly SemaphoreSlim _messageAvailable = new(0);
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _runTask;
    private bool _disposed;

    public RelayCompanionClient(
        RelayCompanionOptions options,
        CompanionBridge bridge)
    {
        _options = options;
        _bridge = bridge;
        _cipher = new CompanionCipher(
            options.SessionId,
            options.PairingSecret,
            "desktop");
        _bridge.MessagePublished += QueueMessage;
        _runTask = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        var retryDelay = TimeSpan.FromSeconds(1);
        while (!_cancellation.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();
                socket.Options.SetRequestHeader(
                    "Authorization",
                    "Bearer " + _options.RelayCredential);
                await socket.ConnectAsync(
                    _options.DesktopWebSocketUri,
                    _cancellation.Token).ConfigureAwait(false);
                AppLog.Write("Android companion development relay connected.");
                retryDelay = TimeSpan.FromSeconds(1);

                using var connectionCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        _cancellation.Token);
                var receiveTask = ReceiveAsync(
                    socket,
                    connectionCancellation.Token);
                var sendTask = SendAsync(
                    socket,
                    connectionCancellation.Token);
                await Task.WhenAny(receiveTask, sendTask).ConfigureAwait(false);
                connectionCancellation.Cancel();
                await IgnoreCancellationAsync(receiveTask).ConfigureAwait(false);
                await IgnoreCancellationAsync(sendTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                AppLog.Write(
                    "Android companion development relay unavailable: "
                    + exception.Message);
            }

            try
            {
                await Task.Delay(retryDelay, _cancellation.Token).ConfigureAwait(false);
                retryDelay = TimeSpan.FromSeconds(
                    Math.Min(30, retryDelay.TotalSeconds * 2));
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ReceiveAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var payload = new MemoryStream();
        while (socket.State == WebSocketState.Open
               && !cancellationToken.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return;
            }
            if (result.MessageType != WebSocketMessageType.Text)
            {
                await socket.CloseOutputAsync(
                    WebSocketCloseStatus.InvalidMessageType,
                    "Text messages are required.",
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            payload.Write(buffer, 0, result.Count);
            if (payload.Length > CompanionProtocol.MaximumPayloadCharacters * 4L)
            {
                await socket.CloseOutputAsync(
                    WebSocketCloseStatus.MessageTooBig,
                    "Companion message exceeded the payload limit.",
                    cancellationToken).ConfigureAwait(false);
                return;
            }
            if (!result.EndOfMessage)
            {
                continue;
            }

            var encryptedCommand = Encoding.UTF8.GetString(payload.ToArray());
            payload.SetLength(0);
            if (!_cipher.TryOpen(
                    encryptedCommand,
                    out var command,
                    out var decryptionError))
            {
                AppLog.Write(
                    "Rejected encrypted Android companion message: "
                    + decryptionError);
                continue;
            }
            if (!CompanionCommandGate.TryForward(
                    _bridge,
                    command,
                    _options.ControlsAllowed,
                    out var error))
            {
                AppLog.Write("Rejected Android companion command: " + error);
            }
        }
    }

    private async Task SendAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        while (socket.State == WebSocketState.Open
               && !cancellationToken.IsCancellationRequested)
        {
            await _messageAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (!_outgoing.TryDequeue(out var message))
            {
                continue;
            }
            var bytes = Encoding.UTF8.GetBytes(message);
            await socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private void QueueMessage(string message)
    {
        if (_disposed)
        {
            return;
        }
        while (_outgoing.Count >= 16 && _outgoing.TryDequeue(out _))
        {
        }
        _outgoing.Enqueue(_cipher.Seal(message));
        _messageAvailable.Release();
    }

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _bridge.MessagePublished -= QueueMessage;
        _cancellation.Cancel();
        try
        {
            _runTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }
        _messageAvailable.Dispose();
        _cancellation.Dispose();
    }
}
