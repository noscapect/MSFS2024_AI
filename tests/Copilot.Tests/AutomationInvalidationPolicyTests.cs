using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Automation;
using Msfs2024Ai.Copilot.Domain;
using Msfs2024Ai.Copilot.Procedures;
using Msfs2024Ai.Copilot.Settings;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class AutomationInvalidationPolicyTests
{
    [TestMethod]
    public void DisconnectPreservesActiveProcedureProgressAndAllowsRetry()
    {
        var commands = new List<string>();
        var runner = CreateRunnerAtSecondStep(commands, out var session);
        var policy = AutomationInvalidationPolicy.For(
            AutomationInvalidationReason.SimConnectDisconnected);

        policy.ApplyToProcedure(runner);
        AutomationInvalidationPolicy
            .For(AutomationInvalidationReason.NewSimConnectSession)
            .ApplyToProcedure(runner);

        Assert.AreEqual("test-flow", session.ActiveProcedureId);
        Assert.AreEqual(1, session.ActiveStepIndex);
        Assert.AreEqual("test-flow", runner.Definition?.Id);
        Assert.AreEqual(1, runner.CurrentStepIndex);

        runner.Update(new AircraftState());

        CollectionAssert.AreEqual(
            new[] { "second command", "second command" },
            commands);
    }

    [TestMethod]
    public void DisconnectInvalidatesOldCockpitWork()
    {
        var runtime = new AutomationRuntimeGeneration(30);
        var command = runtime.CreateCommand("second command");
        var delayedAction = runtime.CaptureDelayedAction();

        runtime.Advance();

        Assert.IsFalse(runtime.IsCurrent(command.Generation));
        Assert.IsFalse(delayedAction.TryExecute(true, () => Assert.Fail()));
    }

    [TestMethod]
    public void ReconnectStillRejectsPreDisconnectWork()
    {
        var runtime = new AutomationRuntimeGeneration(40);
        var command = runtime.CreateCommand("second command");

        runtime.Advance();
        runtime.Advance();

        Assert.IsFalse(runtime.IsCurrent(command.Generation));
        Assert.AreEqual(42L, runtime.Current);
    }

    [TestMethod]
    public void AircraftChangeCancelsActiveProcedure()
    {
        var runner = CreateRunnerAtSecondStep(new List<string>(), out var session);
        var policy = AutomationInvalidationPolicy.For(
            AutomationInvalidationReason.AircraftChanged);

        policy.ApplyToProcedure(runner);

        Assert.AreEqual(ProcedureStatus.Idle, runner.Status);
        Assert.IsNull(runner.Definition);
        Assert.IsNull(session.ActiveProcedureId);
        Assert.AreEqual(0, session.ActiveStepIndex);
    }

    [TestMethod]
    public void AutomaticFlowIntentSurvivesDisconnectButNotAircraftChange()
    {
        var pendingBeforeTakeoff = true;
        var pendingTakeoff = true;
        var taxiArmed = true;
        void ResetIntent()
        {
            pendingBeforeTakeoff = false;
            pendingTakeoff = false;
            taxiArmed = false;
        }

        AutomationInvalidationPolicy
            .For(AutomationInvalidationReason.SimConnectDisconnected)
            .ApplyToLogicalFlowIntent(ResetIntent);
        AutomationInvalidationPolicy
            .For(AutomationInvalidationReason.NewSimConnectSession)
            .ApplyToLogicalFlowIntent(ResetIntent);

        Assert.IsTrue(pendingBeforeTakeoff);
        Assert.IsTrue(pendingTakeoff);
        Assert.IsTrue(taxiArmed);

        AutomationInvalidationPolicy
            .For(AutomationInvalidationReason.AircraftChanged)
            .ApplyToLogicalFlowIntent(ResetIntent);

        Assert.IsFalse(pendingBeforeTakeoff);
        Assert.IsFalse(pendingTakeoff);
        Assert.IsFalse(taxiArmed);
    }

    private static ProcedureRunner CreateRunnerAtSecondStep(
        List<string> commands,
        out ProcedureSession session)
    {
        var runner = new ProcedureRunner(
            commands.Add,
            () => AutomationPolicy.AutomaticWhenSupported);
        session = new ProcedureSession();
        var capturedSession = session;
        runner.Changed += () => SaveRunnerProgress(runner, capturedSession);
        var definition = new ProcedureDefinition(
            "test-flow",
            "Test flow",
            new[]
            {
                new ProcedureStep(
                    "first",
                    "First",
                    ProcedureStepKind.Observe,
                    _ => true),
                new ProcedureStep(
                    "second",
                    "Second",
                    ProcedureStepKind.AutomaticAction,
                    _ => false,
                    command: "second command")
            });

        runner.Start(definition, new AircraftState());
        return runner;
    }

    private static void SaveRunnerProgress(
        ProcedureRunner runner,
        ProcedureSession session)
    {
        var active = runner.Definition != null
                     && runner.Status is ProcedureStatus.Running
                         or ProcedureStatus.WaitingForManualAction
                         or ProcedureStatus.WaitingForVerification
                         or ProcedureStatus.Paused;
        session.ActiveProcedureId = active ? runner.Definition!.Id : null;
        session.ActiveStepIndex = active ? runner.CurrentStepIndex : 0;
    }
}
