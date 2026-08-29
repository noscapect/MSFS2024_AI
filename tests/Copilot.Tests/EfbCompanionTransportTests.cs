using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Efb;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class EfbCompanionTransportTests
{
    [TestMethod]
    public void SingleCommandChunkReturnsPayloadImmediately()
    {
        var transport = new EfbCompanionTransport();
        Assert.IsTrue(transport.TryAcceptCommandChunk(EfbCommBusEvent.Command, 0, 1, "payload", out var payload));
        Assert.AreEqual("payload", payload);
    }

    [TestMethod]
    public void MultipleCommandChunksReturnOrderedPayloadOnlyWhenComplete()
    {
        var transport = new EfbCompanionTransport();
        Assert.IsFalse(transport.TryAcceptCommandChunk(EfbCommBusEvent.Command, 0, 3, "one", out _));
        Assert.IsFalse(transport.TryAcceptCommandChunk(EfbCommBusEvent.Command, 1, 3, "two", out _));
        Assert.IsTrue(transport.TryAcceptCommandChunk(EfbCommBusEvent.Command, 2, 3, "three", out var payload));
        Assert.AreEqual("onetwothree", payload);
    }

    [TestMethod]
    public void CompletedCommandDoesNotContributeChunksToNextCommand()
    {
        var transport = new EfbCompanionTransport();
        transport.TryAcceptCommandChunk(EfbCommBusEvent.Command, 0, 1, "first", out var first);
        transport.TryAcceptCommandChunk(EfbCommBusEvent.Command, 0, 1, "second", out var second);
        Assert.AreEqual("first", first);
        Assert.AreEqual("second", second);
    }

    [TestMethod]
    public void UnsupportedCommBusEventDoesNotProducePayload()
    {
        var transport = new EfbCompanionTransport();
        Assert.IsFalse(transport.TryAcceptCommandChunk((EfbCommBusEvent)99, 0, 1, "payload", out var payload));
        Assert.AreEqual(string.Empty, payload);
    }

    [TestMethod]
    public void ConnectionResetDiscardsPartialCommandPayload()
    {
        var transport = new EfbCompanionTransport();
        transport.TryAcceptCommandChunk(EfbCommBusEvent.Command, 0, 2, "old", out _);
        transport.ResetConnectionState();
        Assert.IsTrue(transport.TryAcceptCommandChunk(EfbCommBusEvent.Command, 1, 2, "new", out var payload));
        Assert.AreEqual("new", payload);
    }

    [TestMethod]
    public void StatePublicationThrottlePreservesCurrentBoundariesAndForceBehavior()
    {
        var transport = new EfbCompanionTransport();
        var now = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);
        Assert.IsTrue(transport.ShouldPublishState(now, false));
        Assert.IsFalse(transport.ShouldPublishState(now.AddMilliseconds(749), false));
        Assert.IsTrue(transport.ShouldPublishState(now.AddMilliseconds(750), false));
        Assert.IsTrue(transport.ShouldPublishState(now.AddMilliseconds(751), true));
    }

    [TestMethod]
    public void StateRequestAcknowledgementThrottlePreservesCurrentBoundaries()
    {
        var transport = new EfbCompanionTransport();
        var now = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);
        Assert.IsTrue(transport.CanAcknowledgeStateRequest(now));
        Assert.IsFalse(transport.CanAcknowledgeStateRequest(now.AddMilliseconds(999)));
        Assert.IsTrue(transport.CanAcknowledgeStateRequest(now.AddSeconds(1)));
    }

    [TestMethod]
    public void ConnectionResetClearsPublicationAndAcknowledgementThrottles()
    {
        var transport = new EfbCompanionTransport();
        var now = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);
        transport.ShouldPublishState(now, false);
        transport.CanAcknowledgeStateRequest(now);
        transport.ResetConnectionState();
        Assert.IsTrue(transport.ShouldPublishState(now.AddMilliseconds(1), false));
        Assert.IsTrue(transport.CanAcknowledgeStateRequest(now.AddMilliseconds(1)));
    }

    [TestMethod]
    public void CommandResultEnvelopePreservesProtocolFields()
    {
        var transport = new EfbCompanionTransport();
        var now = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);
        var envelope = transport.CreateCommandResultEnvelope("request-1", true, "Accepted.", now);
        Assert.AreEqual(EfbCompanionProtocol.Version, envelope["protocolVersion"]);
        Assert.AreEqual("commandResult", envelope["kind"]);
        Assert.AreEqual("request-1", envelope["requestId"]);
        Assert.AreEqual(true, envelope["accepted"]);
        Assert.AreEqual("Accepted.", envelope["message"]);
        Assert.AreEqual(now.ToString("O"), envelope["sentUtc"]);
    }
}
