using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Companion;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class LocalCompanionServerTests
{
    [TestMethod]
    public async Task ExchangesEncryptedLengthPrefixedMessages()
    {
        var pairing = CompanionPairing.Create("wss://relay.example.test");
        pairing.ControlsAllowed = true;
        Assert.IsTrue(
            RelayCompanionOptions.TryFromPairing(pairing, out var options, out var error),
            error);

        var bridge = new CompanionBridge();
        var commandReceived = new TaskCompletionSource<string>();
        bridge.CommandReceived += value => commandReceived.TrySetResult(value);
        using var server = new LocalCompanionServer(options!, bridge, 0);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port);
        var stream = client.GetStream();
        var tabletCipher = new CompanionCipher(
            pairing.SessionId,
            options!.PairingSecret,
            "tablet");

        var command = tabletCipher.Seal(
            "{\"protocolVersion\":1,\"requestId\":\"lan-1\"," +
            "\"action\":\"request_state\"}");
        await WriteFrameAsync(stream, command);

        var completed = await Task.WhenAny(
            commandReceived.Task,
            Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.AreSame(commandReceived.Task, completed, "LAN command was not received.");
        StringAssert.Contains(await commandReceived.Task, "\"requestId\":\"lan-1\"");

        bridge.Publish(new Dictionary<string, object?>
        {
            ["protocolVersion"] = CompanionProtocol.Version,
            ["kind"] = "state",
            ["sentUtc"] = DateTime.UtcNow.ToString("O")
        });
        var responseEnvelope = await ReadFrameAsync(stream);
        Assert.IsTrue(
            tabletCipher.TryOpen(responseEnvelope, out var response, out error),
            error);
        StringAssert.Contains(response, "\"kind\":\"state\"");
    }

    private static async Task WriteFrameAsync(NetworkStream stream, string value)
    {
        var payload = Encoding.UTF8.GetBytes(value);
        var length = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(payload.Length));
        await stream.WriteAsync(length, 0, length.Length);
        await stream.WriteAsync(payload, 0, payload.Length);
        await stream.FlushAsync();
    }

    private static async Task<string> ReadFrameAsync(NetworkStream stream)
    {
        var lengthBytes = await ReadExactlyAsync(stream, 4);
        var length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBytes, 0));
        Assert.IsTrue(length > 0 && length <= 256 * 1024);
        return Encoding.UTF8.GetString(await ReadExactlyAsync(stream, length));
    }

    private static async Task<byte[]> ReadExactlyAsync(Stream stream, int count)
    {
        var bytes = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = await stream.ReadAsync(bytes, offset, count - offset);
            Assert.AreNotEqual(0, read, "LAN connection closed before the frame completed.");
            offset += read;
        }
        return bytes;
    }
}
