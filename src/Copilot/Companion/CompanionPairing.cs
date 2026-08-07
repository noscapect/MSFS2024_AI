using System.Security.Cryptography;
using System.Net;
using System.Net.Sockets;

namespace Msfs2024Ai.Copilot.Companion;

internal sealed class CompanionPairing
{
    public string RelayEndpoint { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string PairingSecret { get; set; } = string.Empty;
    public bool ControlsAllowed { get; set; }
    public string CreatedUtc { get; set; } = string.Empty;

    public static CompanionPairing Create(string relayEndpoint)
    {
        var sessionBytes = new byte[24];
        var secretBytes = new byte[32];
        using (var random = RandomNumberGenerator.Create())
        {
            random.GetBytes(sessionBytes);
            random.GetBytes(secretBytes);
        }
        return new CompanionPairing
        {
            RelayEndpoint = relayEndpoint.TrimEnd('/'),
            SessionId = Base64Url.Encode(sessionBytes),
            PairingSecret = Base64Url.Encode(secretBytes),
            ControlsAllowed = false,
            CreatedUtc = DateTime.UtcNow.ToString("O")
        };
    }

    public string ToPairingUri(int localPort = LocalCompanionServer.DefaultPort)
    {
        var controls = ControlsAllowed ? "1" : "0";
        var uri = "vfo://pair?relay="
               + Uri.EscapeDataString(RelayEndpoint)
               + "&session="
               + Uri.EscapeDataString(SessionId)
               + "&secret="
               + Uri.EscapeDataString(PairingSecret)
               + "&controls="
               + controls;
        try
        {
            foreach (var address in Dns.GetHostAddresses(Dns.GetHostName())
                         .Where(address =>
                             address.AddressFamily == AddressFamily.InterNetwork
                             && !IPAddress.IsLoopback(address))
                         .Distinct()
                         .Take(6))
            {
                uri += "&lan="
                       + Uri.EscapeDataString($"{address}:{localPort}");
            }
        }
        catch (SocketException)
        {
        }
        return uri;
    }
}
