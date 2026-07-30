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
            "{\"protocolVersion\":1,\"requestId\":\"req-1\","
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
    public void RejectsArbitraryCockpitCommand()
    {
        const string json =
            "{\"protocolVersion\":1,\"requestId\":\"req-2\","
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
            "{\"protocolVersion\":2,\"requestId\":\"req-3\","
            + "\"action\":\"confirm\"}";

        var parsed = EfbCompanionProtocol.TryParseCommand(
            json,
            out _,
            out var error);

        Assert.IsFalse(parsed);
        StringAssert.Contains(error, "expected 1");
    }
}
