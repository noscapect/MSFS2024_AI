using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Companion;
using System.Web.Script.Serialization;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class CompanionProtocolTests
{
    [TestMethod]
    public void ParsesAllowListedCommand()
    {
        const string json =
            "{\"protocolVersion\":1,\"requestId\":\"tablet-1\"," +
            "\"action\":\"start_flow\",\"flowId\":\"apu-start-pushback\"}";

        var parsed = CompanionProtocol.TryParseCommand(
            json,
            out var command,
            out var error);

        Assert.IsTrue(parsed, error);
        Assert.AreEqual("tablet-1", command.RequestId);
        Assert.AreEqual("start_flow", command.Action);
        Assert.AreEqual("apu-start-pushback", command.FlowId);
    }

    [TestMethod]
    public void RejectsArbitraryCockpitCommand()
    {
        const string json =
            "{\"protocolVersion\":1,\"requestId\":\"tablet-2\"," +
            "\"action\":\"set_simvar\"}";

        Assert.IsFalse(CompanionProtocol.TryParseCommand(
            json,
            out _,
            out var error));
        StringAssert.Contains(error, "not allowed");
    }

    [TestMethod]
    public void RejectsOversizedPayloadBeforeParsing()
    {
        var json = new string('x', CompanionProtocol.MaximumPayloadCharacters + 1);

        Assert.IsFalse(CompanionProtocol.TryParseCommand(
            json,
            out _,
            out var error));
        StringAssert.Contains(error, "payload limit");
    }

    [TestMethod]
    public void SharedStateFixtureUsesCurrentProtocolVersion()
    {
        var fixturePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "CompanionFixtures",
            "state.json");
        var values = new JavaScriptSerializer().DeserializeObject(
            File.ReadAllText(fixturePath)) as IDictionary<string, object>;

        Assert.IsNotNull(values);
        Assert.AreEqual(CompanionProtocol.Version, Convert.ToInt32(values["protocolVersion"]));
        Assert.AreEqual("state", values["kind"] as string);
    }
}
