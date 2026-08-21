using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Automation;

namespace Copilot.Tests;

[TestClass]
public sealed class CockpitAutomationSchedulerTests
{
    [TestMethod]
    public void EnqueueStampsCurrentGenerationAndDrainsCommand()
    {
        using var scheduler = CreateScheduler(initialGeneration: 12);
        var executed = new List<string>();

        scheduler.Enqueue("battery-1 on");
        scheduler.Drain(executed.Add);

        Assert.AreEqual(12L, scheduler.CurrentGeneration);
        CollectionAssert.AreEqual(new[] { "battery-1 on" }, executed);
    }

    [TestMethod]
    public void StaleCommandIsRejectedAfterGenerationAdvance()
    {
        using var scheduler = CreateScheduler();
        var executed = new List<string>();
        scheduler.Enqueue("battery-1 on");

        scheduler.AdvanceGeneration();
        scheduler.Drain(executed.Add);

        Assert.AreEqual(0, executed.Count);
    }

    [TestMethod]
    public void InvalidationClearsQueuedCockpitWork()
    {
        using var scheduler = CreateScheduler();
        scheduler.Enqueue("first");
        scheduler.Enqueue("second");

        scheduler.InvalidateLiveWork();
        var executed = new List<string>();
        scheduler.Drain(executed.Add);

        Assert.AreEqual(0, executed.Count);
    }

    [TestMethod]
    public void RuntimeUnavailableDiscardsCommandWithoutExecuting()
    {
        var logs = new List<string>();
        using var scheduler = new CockpitAutomationScheduler(
            runtimeAvailable: () => false,
            currentVariant: () => null,
            log: logs.Add,
            delayedActionCompleted: () => { });
        var executed = new List<string>();
        scheduler.Enqueue("battery-1 on");

        scheduler.Drain(executed.Add);

        Assert.AreEqual(0, executed.Count);
        Assert.AreEqual(1, logs.Count);
        StringAssert.Contains(logs[0], "automation is unavailable");
    }

    [TestMethod]
    public void CurrentGenerationDelayedActionExecutes()
    {
        using var scheduler = CreateScheduler();
        using var timer = new System.Windows.Forms.Timer();
        var registration = scheduler.Track(timer);
        var executed = false;

        var accepted = registration.TryExecute(true, () => executed = true);
        scheduler.Complete(timer);

        Assert.IsTrue(accepted);
        Assert.IsTrue(executed);
        Assert.IsFalse(scheduler.HasPendingActions);
    }

    [TestMethod]
    public void StaleGenerationDelayedActionDoesNotExecute()
    {
        using var scheduler = CreateScheduler();
        using var timer = new System.Windows.Forms.Timer();
        var registration = scheduler.Track(timer);
        var executed = false;

        scheduler.InvalidateLiveWork();
        var accepted = registration.TryExecute(true, () => executed = true);

        Assert.IsFalse(accepted);
        Assert.IsFalse(executed);
        Assert.IsFalse(scheduler.HasPendingActions);
    }

    [TestMethod]
    public void CompletingDelayedActionDecrementsPendingCountExactlyOnce()
    {
        using var scheduler = CreateScheduler();
        using var timer = new System.Windows.Forms.Timer();
        scheduler.Track(timer);

        scheduler.Complete(timer);
        scheduler.Complete(timer);

        Assert.IsFalse(scheduler.HasPendingActions);
    }

    [TestMethod]
    public void MultipleDelayedActionsKeepPendingStateUntilAllComplete()
    {
        using var scheduler = CreateScheduler();
        using var firstTimer = new System.Windows.Forms.Timer();
        using var secondTimer = new System.Windows.Forms.Timer();
        scheduler.Track(firstTimer);
        scheduler.Track(secondTimer);

        scheduler.Complete(firstTimer);
        Assert.IsTrue(scheduler.HasPendingActions);

        scheduler.Complete(secondTimer);
        Assert.IsFalse(scheduler.HasPendingActions);
    }

    [TestMethod]
    public void GenerationAdvanceAndCancellationLeaveNoPendingActions()
    {
        using var scheduler = CreateScheduler();
        using var firstTimer = new System.Windows.Forms.Timer();
        using var secondTimer = new System.Windows.Forms.Timer();
        scheduler.Track(firstTimer);
        scheduler.Track(secondTimer);

        scheduler.InvalidateLiveWork();

        Assert.AreEqual(1L, scheduler.CurrentGeneration);
        Assert.IsFalse(scheduler.HasPendingActions);
    }

    private static CockpitAutomationScheduler CreateScheduler(
        long initialGeneration = 0) =>
        new(
            runtimeAvailable: () => true,
            currentVariant: () => null,
            log: _ => { },
            delayedActionCompleted: () => { },
            initialGeneration);
}
