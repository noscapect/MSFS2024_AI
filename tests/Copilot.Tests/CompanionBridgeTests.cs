using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Companion;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class CompanionBridgeTests
{
    [TestMethod]
    public void ConvertsCompanionCommandToExistingAuthoritativeProtocol()
    {
        var bridge = new CompanionBridge();
        string? forwarded = null;
        bridge.CommandReceived += payload => forwarded = payload;

        var accepted = bridge.TryReceiveCommand(
            "{\"protocolVersion\":1,\"requestId\":\"android-1\"," +
            "\"action\":\"gsx_menu_choice\",\"choiceIndex\":2}",
            out var error);

        Assert.IsTrue(accepted, error);
        Assert.IsNotNull(forwarded);
        StringAssert.Contains(forwarded, "\"protocolVersion\":2");
        StringAssert.Contains(forwarded, "\"choiceIndex\":2");
    }

    [TestMethod]
    public void DoesNotForwardRejectedCommand()
    {
        var bridge = new CompanionBridge();
        var forwarded = false;
        bridge.CommandReceived += _ => forwarded = true;

        Assert.IsFalse(bridge.TryReceiveCommand(
            "{\"protocolVersion\":1,\"requestId\":\"android-2\"," +
            "\"action\":\"calculator_code\"}",
            out _));
        Assert.IsFalse(forwarded);
    }
}
