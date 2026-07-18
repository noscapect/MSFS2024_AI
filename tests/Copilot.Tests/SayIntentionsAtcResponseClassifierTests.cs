using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.SayIntentions;

namespace Copilot.Tests;

[TestClass]
public sealed class SayIntentionsAtcResponseClassifierTests
{
    [TestMethod]
    public void RecentCommunicationUsesUtcTimestampAndBoundedAge()
    {
        var now = new DateTimeOffset(2026, 7, 18, 22, 35, 0, TimeSpan.Zero);

        Assert.IsTrue(SayIntentionsAtcResponseClassifier.IsRecent(
            "2026-07-18 22:31:38",
            now,
            TimeSpan.FromMinutes(10)));
        Assert.IsFalse(SayIntentionsAtcResponseClassifier.IsRecent(
            "2026-07-18 22:20:00",
            now,
            TimeSpan.FromMinutes(10)));
    }

    [TestMethod]
    public void FindsAutomaticTakeoffClearanceObtainedBeforeCheckpoint()
    {
        var clearance = new SayIntentionsCommunication
        {
            Id = 42,
            TimestampUtc = "2026-07-18 22:31:38",
            Station = "Brussels Tower",
            OutgoingMessage =
                "Brussels Tower, Runway two-five-right, Winds 320 at 5, cleared for takeoff.",
            IncomingMessage = "Holding short 25R, ready for departure"
        };

        var match = SayIntentionsAtcResponseClassifier.FindRecentClearance(
            "fo-takeoff-clearance",
            new[] { clearance },
            0,
            new DateTimeOffset(2026, 7, 18, 22, 32, 6, TimeSpan.Zero),
            TimeSpan.FromMinutes(10));

        Assert.AreSame(clearance, match);
        Assert.IsNull(SayIntentionsAtcResponseClassifier.FindRecentClearance(
            "fo-takeoff-clearance",
            new[] { clearance },
            42,
            new DateTimeOffset(2026, 7, 18, 22, 32, 6, TimeSpan.Zero),
            TimeSpan.FromMinutes(10)));
    }

    [TestMethod]
    public void IfrRequiresAcceptedReadback()
    {
        Assert.IsFalse(SayIntentionsAtcResponseClassifier.IsClearanceResponse(
            "captain-ifr-clearance",
            "Cleared to Schiphol via the LNO1R departure."));
        Assert.IsTrue(SayIntentionsAtcResponseClassifier.IsClearanceResponse(
            "captain-ifr-clearance",
            "Readback correct. Contact tower on 118.105."));
    }

    [TestMethod]
    public void OperationalClearancesMatchOnlyTheirOwnCheckpoint()
    {
        Assert.IsTrue(SayIntentionsAtcResponseClassifier.IsClearanceResponse(
            "captain-pushback-clearance",
            "Pushback and start approved, face west."));
        Assert.IsTrue(SayIntentionsAtcResponseClassifier.IsClearanceResponse(
            "captain-pushback-clearance",
            "Push and start approved. Face North-East."));
        Assert.IsTrue(SayIntentionsAtcResponseClassifier.IsClearanceResponse(
            "captain-pushback-clearance",
            "Push and start approved. Face South-West."));
        Assert.IsTrue(SayIntentionsAtcResponseClassifier.IsClearanceResponse(
            "fo-taxi-clearance",
            "Taxi runway 24 via Alpha, hold short."));
        Assert.IsTrue(SayIntentionsAtcResponseClassifier.IsClearanceResponse(
            "fo-takeoff-clearance",
            "Runway 24, cleared for takeoff."));
        Assert.IsFalse(SayIntentionsAtcResponseClassifier.IsClearanceResponse(
            "fo-takeoff-clearance",
            "Line up and wait runway 24."));
    }

    [TestMethod]
    public void DenialsAndStandbyDoNotCompleteCheckpoint()
    {
        Assert.IsFalse(SayIntentionsAtcResponseClassifier.IsClearanceResponse(
            "fo-taxi-clearance",
            "Unable taxi clearance, stand by."));
        Assert.IsFalse(SayIntentionsAtcResponseClassifier.IsClearanceResponse(
            "captain-pushback-clearance",
            "Push and start denied. Face North-East."));
    }
}
