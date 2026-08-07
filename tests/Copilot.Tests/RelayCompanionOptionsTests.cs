using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Companion;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class RelayCompanionOptionsTests
{
    private static readonly string[] VariableNames =
    {
        "VFO_COMPANION_DEVELOPMENT",
        "VFO_COMPANION_RELAY",
        "VFO_COMPANION_SESSION",
        "VFO_COMPANION_SECRET",
        "VFO_COMPANION_ALLOW_CONTROL"
    };

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var name in VariableNames)
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [TestMethod]
    public void DevelopmentRelayIsOffByDefault()
    {
        Cleanup();
        Assert.IsFalse(RelayCompanionOptions.TryFromEnvironment(out var options, out _));
        Assert.IsNull(options);
    }

    [TestMethod]
    public void BuildsDesktopWebSocketUriWithoutCredentialInQuery()
    {
        Environment.SetEnvironmentVariable("VFO_COMPANION_DEVELOPMENT", "1");
        Environment.SetEnvironmentVariable("VFO_COMPANION_RELAY", "wss://relay.example.test");
        Environment.SetEnvironmentVariable("VFO_COMPANION_SESSION", "abcdefghijklmnopqrstuvwx");
        var secret = Base64Url.Encode(Enumerable.Range(0, 32).Select(value => (byte)value).ToArray());
        Environment.SetEnvironmentVariable("VFO_COMPANION_SECRET", secret);

        Assert.IsTrue(RelayCompanionOptions.TryFromEnvironment(
            out var options,
            out var error), error);
        Assert.AreEqual(
            "wss://relay.example.test/v1/session/abcdefghijklmnopqrstuvwx?role=desktop",
            options!.DesktopWebSocketUri.AbsoluteUri);
        Assert.IsFalse(options.DesktopWebSocketUri.AbsoluteUri.Contains(secret));
        Assert.AreNotEqual(secret, options.RelayCredential);
        Assert.IsFalse(options.ControlsAllowed);
    }
}
