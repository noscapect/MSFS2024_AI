using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Efb;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class EfbCompanionProtocolTests
{
    [TestMethod]
    public void ParsesAllowedStartFlowCommand()
    {
        const string json =
            "{\"protocolVersion\":2,\"requestId\":\"req-1\","
            + "\"action\":\"start_flow\",\"flowId\":\"power-up-initial-setup\"}";

        var parsed = EfbCompanionProtocol.TryParseCommand(
            json,
            out var command,
            out var error);

        Assert.IsTrue(parsed, error);
        Assert.AreEqual("req-1", command.RequestId);
        Assert.AreEqual("start_flow", command.Action);
        Assert.AreEqual("power-up-initial-setup", command.FlowId);
    }

    [TestMethod]
    public void ParsesAllowedStartNextFlowCommandWithoutFlowId()
    {
        const string json =
            "{\"protocolVersion\":2,\"requestId\":\"next-1\","
            + "\"action\":\"start_next_flow\"}";

        var parsed = EfbCompanionProtocol.TryParseCommand(
            json,
            out var command,
            out var error);

        Assert.IsTrue(parsed, error);
        Assert.AreEqual("start_next_flow", command.Action);
        Assert.IsNull(command.FlowId);
    }

    [TestMethod]
    public void ParsesGsxMenuChoice()
    {
        const string json =
            "{\"protocolVersion\":2,\"requestId\":\"gsx-1\","
            + "\"action\":\"gsx_menu_choice\",\"choiceIndex\":1}";

        var parsed = EfbCompanionProtocol.TryParseCommand(
            json,
            out var command,
            out var error);

        Assert.IsTrue(parsed, error);
        Assert.AreEqual("gsx_menu_choice", command.Action);
        Assert.AreEqual(1, command.ChoiceIndex);
    }

    [TestMethod]
    public void ParsesGsxOpenMenuCommand()
    {
        const string json =
            "{\"protocolVersion\":2,\"requestId\":\"gsx-open-1\","
            + "\"action\":\"gsx_open_menu\"}";

        var parsed = EfbCompanionProtocol.TryParseCommand(
            json,
            out var command,
            out var error);

        Assert.IsTrue(parsed, error);
        Assert.AreEqual("gsx_open_menu", command.Action);
    }

    [TestMethod]
    public void RejectsGsxMenuChoiceWithoutIndex()
    {
        const string json =
            "{\"protocolVersion\":2,\"requestId\":\"gsx-2\","
            + "\"action\":\"gsx_menu_choice\"}";

        var parsed = EfbCompanionProtocol.TryParseCommand(
            json,
            out _,
            out var error);

        Assert.IsFalse(parsed);
        StringAssert.Contains(error, "choice index");
    }

    [TestMethod]
    public void RejectsArbitraryCockpitCommand()
    {
        const string json =
            "{\"protocolVersion\":2,\"requestId\":\"req-2\","
            + "\"action\":\"external-power on\"}";

        var parsed = EfbCompanionProtocol.TryParseCommand(
            json,
            out _,
            out var error);

        Assert.IsFalse(parsed);
        StringAssert.Contains(error, "not allowed");
    }

    [TestMethod]
    public void RejectsUnknownProtocolVersion()
    {
        const string json =
            "{\"protocolVersion\":1,\"requestId\":\"req-3\","
            + "\"action\":\"confirm\"}";

        var parsed = EfbCompanionProtocol.TryParseCommand(
            json,
            out _,
            out var error);

        Assert.IsFalse(parsed);
        StringAssert.Contains(error, "expected 2");
    }
}
