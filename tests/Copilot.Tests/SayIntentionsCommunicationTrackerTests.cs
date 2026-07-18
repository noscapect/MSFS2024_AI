using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.SayIntentions;

namespace Copilot.Tests;

[TestClass]
public sealed class SayIntentionsCommunicationTrackerTests
{
    [TestMethod]
    public void ExistingRecordBecomesNewAgainWhenAtcAddsItsResponse()
    {
        var tracker = new SayIntentionsCommunicationTracker();
        var request = new SayIntentionsCommunication
        {
            Id = 42,
            Channel = "COM1",
            IncomingMessage = "Requesting pushback and engine start."
        };

        var first = tracker.Observe(request);
        var unchanged = tracker.Observe(request);
        var response = tracker.Observe(new SayIntentionsCommunication
        {
            Id = 42,
            Channel = "COM1",
            IncomingMessage = request.IncomingMessage,
            OutgoingMessage = "Push and start approved. Face north-east."
        });

        Assert.IsTrue(first.IncomingChanged);
        Assert.IsFalse(first.OutgoingChanged);
        Assert.IsFalse(unchanged.HasChanges);
        Assert.IsFalse(response.IncomingChanged);
        Assert.IsTrue(response.OutgoingChanged);
    }

    [TestMethod]
    public void PrimedHistoryDoesNotReplayOldMessages()
    {
        var tracker = new SayIntentionsCommunicationTracker();
        var communication = new SayIntentionsCommunication
        {
            Id = 42,
            IncomingMessage = "Old request",
            OutgoingMessage = "Old response"
        };

        tracker.Prime(new[] { communication });

        Assert.IsFalse(tracker.Observe(communication).HasChanges);
    }
}
