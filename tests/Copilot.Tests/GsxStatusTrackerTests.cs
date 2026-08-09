using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Gsx;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class GsxStatusTrackerTests
{
    private static readonly DateTime Now =
        new(2026, 8, 9, 15, 5, 42, DateTimeKind.Utc);

    [TestMethod]
    public void TransientNotificationExpiresAtGsxSuppliedLifetime()
    {
        var tracker = new GsxStatusTracker();
        tracker.Update(
            new[] { "[GSX] Release parking brakes" },
            TimeSpan.FromSeconds(5),
            Now);

        Assert.AreEqual(1, tracker.Snapshot(Now.AddSeconds(4)).Count);
        Assert.AreEqual(0, tracker.Snapshot(Now.AddSeconds(5)).Count);
    }

    [TestMethod]
    public void PassengerProgressSurvivesUnrelatedBaggageNotification()
    {
        var tracker = new GsxStatusTracker();
        tracker.Update(
            new[] { "[GSX] 10/220 passengers boarded" },
            TimeSpan.FromSeconds(5),
            Now);
        tracker.Update(
            new[] { "[GSX] Baggage loading progress 50%" },
            TimeSpan.FromSeconds(5),
            Now.AddSeconds(2));

        var snapshot = tracker.Snapshot(Now.AddSeconds(3));

        CollectionAssert.Contains(
            snapshot.ToArray(),
            "[GSX] 10/220 passengers boarded");
        CollectionAssert.Contains(
            snapshot.ToArray(),
            "[GSX] Baggage loading progress 50%");
    }

    [TestMethod]
    public void DepartureStatusClearsCompletedBoardingProgress()
    {
        var tracker = new GsxStatusTracker();
        tracker.Update(
            new[] { "[GSX] 220/220 passengers boarded" },
            TimeSpan.FromSeconds(5),
            Now);
        tracker.Update(
            new[] { "[GSX] Departure clearance requested" },
            TimeSpan.FromSeconds(5),
            Now.AddSeconds(2));

        var snapshot = tracker.Snapshot(Now.AddSeconds(3));

        Assert.AreEqual(1, snapshot.Count);
        Assert.AreEqual("[GSX] Departure clearance requested", snapshot[0]);
    }
}
