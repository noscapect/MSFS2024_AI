using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.SayIntentions;

namespace Copilot.Tests;

[TestClass]
public sealed class SayIntentionsRuntimeStateTests
{
    [TestMethod]
    public void NewRuntime_HasNoSessionOrCapturedState()
    {
        var runtime = new SayIntentionsRuntimeState();

        Assert.IsNull(runtime.Flight);
        Assert.IsNull(runtime.CommunicationSessionKey);
        Assert.AreEqual(0L, runtime.LastCommunicationId);
        Assert.IsFalse(runtime.CopilotModeApplied);
        Assert.AreEqual("", runtime.ApproachRunway);
        Assert.IsFalse(runtime.ApproachIsIls);
        Assert.IsNull(runtime.PushbackTargetHeadingDegrees);
        Assert.AreEqual(DateTime.MinValue, runtime.PushbackTargetCapturedUtc);
    }

    [TestMethod]
    public void FlightAndCopilotMode_AreScopedToTheRecordedSession()
    {
        var runtime = new SayIntentionsRuntimeState();
        var flight = Flight("flight-a");

        runtime.SetFlight(flight);
        runtime.RecordCopilotModeApplied("A", true);

        Assert.AreSame(flight, runtime.Flight);
        Assert.IsTrue(runtime.IsCopilotModeCurrent("A", true));
        Assert.IsFalse(runtime.IsCopilotModeCurrent("A", false));
        Assert.IsFalse(runtime.IsCopilotModeCurrent("B", true));

        runtime.ClearCopilotModeState();

        Assert.IsFalse(runtime.CopilotModeApplied);
        Assert.IsNull(runtime.CopilotModeSessionKey);
    }

    [TestMethod]
    public void BeginCommunicationSession_PrimesHistoryAndUsesItsMaximumId()
    {
        var runtime = new SayIntentionsRuntimeState();
        var history = new[]
        {
            Communication(4, "Old request", "Old response"),
            Communication(12, "Newer request", "Newer response")
        };

        runtime.BeginCommunicationSession("A", history);

        Assert.AreEqual("A", runtime.CommunicationSessionKey);
        Assert.AreEqual(12L, runtime.LastCommunicationId);
        Assert.IsFalse(runtime.ObserveCommunication(history[0]).HasChanges);
        Assert.IsFalse(runtime.ObserveCommunication(history[1]).HasChanges);
    }

    [TestMethod]
    public void CommunicationObservation_NeverMovesLastIdBackward()
    {
        var runtime = new SayIntentionsRuntimeState();
        runtime.BeginCommunicationSession("A", Array.Empty<SayIntentionsCommunication>());

        runtime.ObserveCommunication(Communication(9, "Request", "Response"));
        runtime.ObserveCommunication(Communication(3, "Older", "Older response"));

        Assert.AreEqual(9L, runtime.LastCommunicationId);
    }

    [TestMethod]
    public void ReplacingCommunicationSession_ResetsApproachAndReprimesTracker()
    {
        var runtime = new SayIntentionsRuntimeState();
        runtime.BeginCommunicationSession("A", new[] { Communication(18, "A", "A response") });
        Assert.IsTrue(runtime.RecordApproachAssignment(new SayIntentionsApproachAssignment("18R", true)));

        var replacement = Communication(4, "B", "B response");
        runtime.BeginCommunicationSession("B", new[] { replacement });

        Assert.AreEqual("B", runtime.CommunicationSessionKey);
        Assert.AreEqual(4L, runtime.LastCommunicationId);
        Assert.AreEqual("", runtime.ApproachRunway);
        Assert.IsFalse(runtime.ApproachIsIls);
        Assert.IsFalse(runtime.ObserveCommunication(replacement).HasChanges);
    }

    [TestMethod]
    public void ApproachAndPushback_KeepExactRecordedValuesAndSuppressDuplicates()
    {
        var runtime = new SayIntentionsRuntimeState();
        var timestamp = new DateTime(2026, 8, 29, 12, 34, 56, DateTimeKind.Utc);

        Assert.IsTrue(runtime.RecordApproachAssignment(new SayIntentionsApproachAssignment("18R", true)));
        Assert.IsFalse(runtime.RecordApproachAssignment(new SayIntentionsApproachAssignment("18R", true)));
        Assert.IsTrue(runtime.RecordApproachAssignment(new SayIntentionsApproachAssignment("27", false)));
        runtime.RecordPushbackTargetHeading(182.5, timestamp);

        Assert.AreEqual("27", runtime.ApproachRunway);
        Assert.IsFalse(runtime.ApproachIsIls);
        Assert.AreEqual(182.5, runtime.PushbackTargetHeadingDegrees);
        Assert.AreEqual(timestamp, runtime.PushbackTargetCapturedUtc);
    }

    [TestMethod]
    public void DiscoveryReset_ClearsOnlyExistingDiscoverySessionStateAndIsReusable()
    {
        var runtime = new SayIntentionsRuntimeState();
        runtime.SetFlight(Flight("flight-a"));
        runtime.RecordCopilotModeApplied("A", true);
        runtime.BeginCommunicationSession("A", new[] { Communication(7, "Request", "Response") });
        runtime.RecordApproachAssignment(new SayIntentionsApproachAssignment("18R", true));
        runtime.RecordPushbackTargetHeading(180, DateTime.UtcNow);

        runtime.ResetDiscoverySession();

        Assert.IsNull(runtime.Flight);
        Assert.IsFalse(runtime.CopilotModeApplied);
        Assert.IsNull(runtime.CopilotModeSessionKey);
        Assert.IsNull(runtime.CommunicationSessionKey);
        Assert.AreEqual(0L, runtime.LastCommunicationId);
        Assert.AreEqual("18R", runtime.ApproachRunway);
        Assert.IsTrue(runtime.ApproachIsIls);
        Assert.AreEqual(180d, runtime.PushbackTargetHeadingDegrees);

        runtime.BeginCommunicationSession("B", Array.Empty<SayIntentionsCommunication>());

        Assert.AreEqual("B", runtime.CommunicationSessionKey);
        Assert.AreEqual(0L, runtime.LastCommunicationId);
        Assert.AreEqual("", runtime.ApproachRunway);
    }

    private static SayIntentionsFlightContext Flight(string flightId)
    {
        var json = "{\"flight_details\":{\"api_key\":\"test\",\"flight_id\":\""
                   + flightId
                   + "\",\"current_flight\":{}}}";
        Assert.IsTrue(SayIntentionsFlightContext.TryParse(json, out var flight));
        return flight!;
    }

    private static SayIntentionsCommunication Communication(
        long id,
        string incoming,
        string outgoing) =>
        new()
        {
            Id = id,
            Channel = "COM1",
            IncomingMessage = incoming,
            OutgoingMessage = outgoing
        };
}
