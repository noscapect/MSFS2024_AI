using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.SayIntentions;

namespace Copilot.Tests;

[TestClass]
public sealed class SayIntentionsAtcResponseClassifierTests
{
    [DataTestMethod]
    [DataRow("captain-ifr-clearance", "KLM123, cleared to EHAM as filed", true)]
    [DataRow("captain-ifr-clearance", "KLM123, standby for clearance", false)]
    [DataRow("captain-pushback-clearance", "Pushback and start approved", true)]
    [DataRow("captain-pushback-clearance", "Unable pushback, stand by", false)]
    [DataRow("fo-taxi-clearance", "Taxi to runway 25 via Alpha", true)]
    [DataRow("fo-taxi-clearance", "Hold position", false)]
    [DataRow("fo-takeoff-clearance", "Cleared for takeoff runway 25", true)]
    [DataRow("fo-takeoff-clearance", "Line up and wait runway 25", false)]
    public void CompletionRequiresStepSpecificAtcAuthorization(
        string stepId,
        string reply,
        bool expected)
    {
        Assert.AreEqual(
            expected,
            SayIntentionsAtcResponseClassifier.IsCompletionReply(stepId, reply));
    }

    [DataTestMethod]
    [DataRow("captain-ifr-clearance", "Request IFR clearance to EHAM")]
    [DataRow("captain-pushback-clearance", "Request pushback and engine start")]
    [DataRow("fo-taxi-clearance", "Ready for taxi, request taxi clearance")]
    [DataRow("fo-takeoff-clearance", "Holding short, ready for departure")]
    public void MatchingOutgoingRequestRecognizesEachSupportedFlow(
        string stepId,
        string outgoing)
    {
        Assert.IsTrue(
            SayIntentionsAtcResponseClassifier.IsMatchingOutgoingRequest(
                new SayIntentionsCommunication { OutgoingMessage = outgoing },
                stepId));
    }

    [TestMethod]
    public void RecentCommunicationRequiresAUsableTimestampWithinWindow()
    {
        var now = new DateTimeOffset(2026, 7, 18, 10, 0, 0, TimeSpan.Zero);
        Assert.IsTrue(SayIntentionsAtcResponseClassifier.IsRecent(
            new SayIntentionsCommunication { TimestampUtc = "2026-07-18T09:58:00Z" },
            now,
            TimeSpan.FromMinutes(5)));
        Assert.IsFalse(SayIntentionsAtcResponseClassifier.IsRecent(
            new SayIntentionsCommunication { TimestampUtc = "2026-07-18T09:30:00Z" },
            now,
            TimeSpan.FromMinutes(5)));
    }
}
