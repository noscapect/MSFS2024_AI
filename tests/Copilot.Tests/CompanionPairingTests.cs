using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Companion;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class CompanionPairingTests
{
    [TestMethod]
    public void CreatesStrongUniquePairingMaterial()
    {
        var first = CompanionPairing.Create("wss://relay.example.test/");
        var second = CompanionPairing.Create("wss://relay.example.test/");

        Assert.AreEqual("wss://relay.example.test", first.RelayEndpoint);
        Assert.AreEqual(32, first.SessionId.Length);
        Assert.IsTrue(Base64Url.TryDecode(first.PairingSecret, out var secret));
        Assert.AreEqual(32, secret.Length);
        Assert.AreNotEqual(first.SessionId, second.SessionId);
        Assert.AreNotEqual(first.PairingSecret, second.PairingSecret);
        Assert.IsFalse(first.ControlsAllowed);
    }

    [TestMethod]
    public void PairingUriCanConfigureRelayWithoutExposingDerivedCredential()
    {
        var pairing = CompanionPairing.Create("wss://relay.example.test");

        Assert.IsTrue(RelayCompanionOptions.TryFromPairing(
            pairing,
            out var options,
            out var error), error);
        var uri = pairing.ToPairingUri();

        StringAssert.StartsWith(uri, "vfo://pair?");
        StringAssert.Contains(uri, "controls=0");
        StringAssert.Contains(uri, Uri.EscapeDataString(pairing.PairingSecret));
        Assert.IsFalse(uri.Contains(options!.RelayCredential));
    }
}
