using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Companion;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class CompanionCipherTests
{
    private const string SessionId = "abcdefghijklmnopqrstuvwx";
    private static readonly byte[] Secret =
        Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();

    [TestMethod]
    public void DesktopAndTabletExchangeAuthenticatedMessage()
    {
        var desktop = new CompanionCipher(SessionId, Secret, "desktop");
        var tablet = new CompanionCipher(SessionId, Secret, "tablet");

        var envelope = desktop.Seal("{\"kind\":\"state\"}");

        Assert.IsTrue(tablet.TryOpen(envelope, out var plaintext, out var error), error);
        Assert.AreEqual("{\"kind\":\"state\"}", plaintext);
    }

    [TestMethod]
    public void RejectsReplay()
    {
        var desktop = new CompanionCipher(SessionId, Secret, "desktop");
        var tablet = new CompanionCipher(SessionId, Secret, "tablet");
        var envelope = desktop.Seal("state");

        Assert.IsTrue(tablet.TryOpen(envelope, out _, out _));
        Assert.IsFalse(tablet.TryOpen(envelope, out _, out var error));
        StringAssert.Contains(error, "already received");
    }

    [TestMethod]
    public void RejectsTamperedCiphertext()
    {
        var desktop = new CompanionCipher(SessionId, Secret, "desktop");
        var tablet = new CompanionCipher(SessionId, Secret, "tablet");
        var envelope = desktop.Seal("confirm");
        var ciphertextMarker = "\"ciphertext\":\"";
        var valueIndex = envelope.IndexOf(ciphertextMarker, StringComparison.Ordinal)
                         + ciphertextMarker.Length;
        var replacement = envelope[valueIndex] == 'A' ? 'B' : 'A';
        var tampered = envelope.Substring(0, valueIndex)
                       + replacement
                       + envelope.Substring(valueIndex + 1);

        Assert.IsFalse(tablet.TryOpen(tampered, out _, out var error));
        StringAssert.Contains(error, "authenticated");
    }

    [TestMethod]
    public void RelayCredentialIsDomainSeparatedFromEncryptionKey()
    {
        var credential = CompanionCipher.DeriveRelayCredential(SessionId, Secret);

        Assert.AreEqual(43, credential.Length);
        Assert.AreNotEqual(Base64Url.Encode(Secret), credential);
    }

    [TestMethod]
    public void MatchesCrossPlatformChaCha20Poly1305Vector()
    {
        var desktop = new CompanionCipher(SessionId, Secret, "desktop");
        var envelope = desktop.Seal(
            "{\"kind\":\"state\"}",
            "00112233445566778899aabbccddeeff",
            DateTimeOffset.FromUnixTimeMilliseconds(1785854400000),
            Enumerable.Range(0, 12).Select(value => (byte)value).ToArray());
        var values = new JavaScriptSerializer().DeserializeObject(envelope)
            as IDictionary<string, object>;

        Assert.IsNotNull(values);
        Assert.AreEqual("AAECAwQFBgcICQoL", values["nonce"] as string);
        Assert.AreEqual(
            "j8Ufwl6bvEwinuM7UR_NlbeLMcVDqhZ58_FF3qb78Uc",
            values["ciphertext"] as string);
        Assert.AreEqual(
            "Qc5fX5NDx5wmI_CPTqiVSVZq4P91VM9bsHdLKZWAGK4",
            CompanionCipher.DeriveRelayCredential(SessionId, Secret));
    }
}
