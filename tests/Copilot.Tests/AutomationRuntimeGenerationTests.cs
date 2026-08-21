using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Automation;

namespace Copilot.Tests;

[TestClass]
public sealed class AutomationRuntimeGenerationTests
{
    [TestMethod]
    public void CurrentGenerationCommandIsAccepted()
    {
        var runtime = new AutomationRuntimeGeneration(10);
        var command = runtime.CreateCommand("battery-1 on");

        Assert.AreEqual(10L, command.Generation);
        Assert.IsTrue(runtime.IsCurrent(command.Generation));
    }

    [TestMethod]
    public void OldGenerationCommandIsRejected()
    {
        var runtime = new AutomationRuntimeGeneration(11);
        var command = new QueuedCockpitCommand("battery-1 on", 10, DateTime.UtcNow);

        Assert.IsFalse(runtime.IsCurrent(command.Generation));
    }

    [TestMethod]
    public void AdvancingGenerationInvalidatesCapturedWork()
    {
        var runtime = new AutomationRuntimeGeneration(10);
        var action = runtime.CaptureDelayedAction();

        runtime.Advance();

        Assert.IsFalse(action.IsCurrent);
        action.Dispose();
    }

    [TestMethod]
    public void StaleDelayedActionDoesNotExecute()
    {
        var runtime = new AutomationRuntimeGeneration(10);
        var action = runtime.CaptureDelayedAction();
        var executed = false;
        runtime.Advance();

        var accepted = action.TryExecute(true, () => executed = true);

        Assert.IsFalse(accepted);
        Assert.IsFalse(executed);
    }

    [TestMethod]
    public void CurrentDelayedActionExecutes()
    {
        var runtime = new AutomationRuntimeGeneration(10);
        var action = runtime.CaptureDelayedAction();
        var executed = false;

        var accepted = action.TryExecute(true, () => executed = true);

        Assert.IsTrue(accepted);
        Assert.IsTrue(executed);
    }

    [TestMethod]
    public void PendingDelayedActionBlocksCompletionUntilResolved()
    {
        var runtime = new AutomationRuntimeGeneration(10);
        var action = runtime.CaptureDelayedAction();

        Assert.IsTrue(runtime.HasPendingActions);

        action.Dispose();

        Assert.IsFalse(runtime.HasPendingActions);
    }

    [TestMethod]
    public void AircraftOrSessionTransitionInvalidatesPreviousAutomation()
    {
        var runtime = new AutomationRuntimeGeneration(20);
        var command = runtime.CreateCommand("procedure confirm");
        var action = runtime.CaptureDelayedAction();

        var newGeneration = runtime.Advance();

        Assert.AreEqual(21L, newGeneration);
        Assert.IsFalse(runtime.IsCurrent(command.Generation));
        Assert.IsFalse(action.IsCurrent);
        action.Dispose();
    }
}
