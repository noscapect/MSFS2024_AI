using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Gsx;

namespace Copilot.Tests;

[TestClass]
public sealed class GsxIntegrationControllerTests
{
    private static readonly DateTime Now =
        new(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void ExternalRemoteControlOwnerIsObservedWithoutClaimingIt()
    {
        using var runtime = new TestRuntime();

        runtime.Controller.ObserveTelemetry(true, true, Now);

        Assert.IsTrue(runtime.Controller.Snapshot.RemoteControlActive);
        Assert.IsFalse(runtime.Controller.Snapshot.OwnsRemoteControl);
        Assert.AreEqual(0, runtime.Effects.RemoteControlWrites.Count);
    }

    [TestMethod]
    public void LocalClaimAndReleaseTrackOwnershipAndWriteOnlyTransitions()
    {
        using var runtime = new TestRuntime();
        runtime.Controller.ObserveTelemetry(true, false, Now);

        Assert.IsTrue(runtime.Controller.ClaimRemoteControl(Now));
        Assert.IsTrue(runtime.Controller.ClaimRemoteControl(Now.AddSeconds(1)));
        Assert.IsTrue(runtime.Controller.Snapshot.OwnsRemoteControl);
        CollectionAssert.AreEqual(
            new[] { true },
            runtime.Effects.RemoteControlWrites);

        Assert.IsTrue(runtime.Controller.ReleaseRemoteControl(canTransmit: true));
        Assert.IsFalse(runtime.Controller.Snapshot.OwnsRemoteControl);
        Assert.IsFalse(runtime.Controller.Snapshot.RemoteControlActive);
        CollectionAssert.AreEqual(
            new[] { true, false },
            runtime.Effects.RemoteControlWrites);
    }

    [TestMethod]
    public void MenuOpenUpdateAndHideReplaceThenClearLiveQuestion()
    {
        using var runtime = new TestRuntime();
        var first = Menu("Choose pushback direction", "Nose left", "Nose right");
        var updated = Menu("Choose pushback direction", "Straight", "Nose right");

        runtime.Controller.OnMenuOpened(first, Now);
        Assert.IsTrue(runtime.Controller.Snapshot.MenuOpen);
        Assert.AreEqual("Nose left", runtime.Controller.Snapshot.CurrentMenu.Choices[0]);

        runtime.Controller.OnMenuOpened(updated, Now.AddSeconds(1));
        Assert.AreEqual("Straight", runtime.Controller.Snapshot.CurrentMenu.Choices[0]);

        Assert.AreEqual(
            GsxMenuHiddenResult.UnansweredMenuClosed,
            runtime.Controller.OnMenuHidden());
        Assert.IsFalse(runtime.Controller.Snapshot.MenuOpen);
        Assert.IsTrue(runtime.Controller.Snapshot.CurrentMenu.IsEmpty);
    }

    [TestMethod]
    public void MenuCancelOrTimeoutClearsTheLiveQuestion()
    {
        using var runtime = new TestRuntime();
        runtime.Controller.OnMenuOpened(
            Menu("Deicing required?", "No", "Yes"),
            Now);

        runtime.Controller.OnMenuCancelledOrTimedOut();

        Assert.IsFalse(runtime.Controller.Snapshot.MenuOpen);
        Assert.IsTrue(runtime.Controller.Snapshot.CurrentMenu.IsEmpty);
    }

    [TestMethod]
    public void RefreshedChoiceRequiresSameQuestionAndLabel()
    {
        var same = Menu("Select tug", "Small", "Large");
        var changedQuestion = Menu("Select deicing", "Small", "Large");
        var removedLabel = Menu("Select tug", "Small");

        using (var runtime = new TestRuntime())
        {
            runtime.Controller.CacheChoiceForRefresh(
                "Select tug",
                "Large",
                "same",
                Now);

            Assert.IsTrue(runtime.Controller.OnMenuOpened(same, Now.AddSeconds(1)));
            CollectionAssert.AreEqual(new[] { 1 }, runtime.Effects.MenuChoices);
            Assert.IsTrue(runtime.Controller.Snapshot.AwaitingChoiceAcknowledgement);
        }

        using (var runtime = new TestRuntime())
        {
            runtime.Controller.CacheChoiceForRefresh(
                "Select tug",
                "Large",
                "changed",
                Now);

            Assert.IsFalse(runtime.Controller.OnMenuOpened(
                changedQuestion,
                Now.AddSeconds(1)));
            Assert.AreEqual(0, runtime.Effects.MenuChoices.Count);
            Assert.IsFalse(runtime.Effects.CommandResults[0].Success);
        }

        using (var runtime = new TestRuntime())
        {
            runtime.Controller.CacheChoiceForRefresh(
                "Select tug",
                "Large",
                "removed",
                Now);

            Assert.IsFalse(runtime.Controller.OnMenuOpened(
                removedLabel,
                Now.AddSeconds(1)));
            Assert.AreEqual(0, runtime.Effects.MenuChoices.Count);
            Assert.IsFalse(runtime.Effects.CommandResults[0].Success);
        }
    }

    [TestMethod]
    public void SubmittedChoiceWaitsForHideAcknowledgementThenSucceeds()
    {
        using var runtime = new TestRuntime();
        runtime.Controller.OnMenuOpened(
            Menu("Attach tug now?", "No", "Yes"),
            Now);

        Assert.IsTrue(runtime.Controller.RequestMenuChoice(
            1,
            "Yes",
            "request-1",
            Now));
        Assert.IsTrue(runtime.Controller.Snapshot.AwaitingChoiceAcknowledgement);

        Assert.AreEqual(
            GsxMenuHiddenResult.ChoiceAcknowledged,
            runtime.Controller.OnMenuHidden());
        Assert.IsFalse(runtime.Controller.Snapshot.AwaitingChoiceAcknowledgement);
        Assert.AreEqual(1, runtime.Effects.CommandResults.Count);
        Assert.IsTrue(runtime.Effects.CommandResults[0].Success);
        Assert.AreEqual("request-1", runtime.Effects.CommandResults[0].RequestId);
    }

    [TestMethod]
    public void ChoiceAcknowledgementTimeoutFailsAndClearsRequest()
    {
        using var runtime = new TestRuntime();
        runtime.Controller.OnMenuOpened(Menu("Question", "Answer"), Now);
        runtime.Controller.RequestMenuChoice(0, "Answer", "request-2", Now);

        runtime.Controller.Update(Now.AddSeconds(6));

        Assert.IsFalse(runtime.Controller.Snapshot.AwaitingChoiceAcknowledgement);
        Assert.AreEqual(1, runtime.Effects.CommandResults.Count);
        Assert.IsFalse(runtime.Effects.CommandResults[0].Success);
    }

    [TestMethod]
    public void ChoiceCancellationFailsAndClearsRequest()
    {
        using var runtime = new TestRuntime();
        runtime.Controller.OnMenuOpened(Menu("Question", "Answer"), Now);
        runtime.Controller.RequestMenuChoice(0, "Answer", "request-3", Now);

        runtime.Controller.OnMenuCancelledOrTimedOut();

        Assert.IsFalse(runtime.Controller.Snapshot.AwaitingChoiceAcknowledgement);
        Assert.AreEqual(1, runtime.Effects.CommandResults.Count);
        Assert.IsFalse(runtime.Effects.CommandResults[0].Success);
    }

    [TestMethod]
    public void GoodEngineStartWaitsUntilEnginesAreStable()
    {
        using var runtime = new TestRuntime();
        runtime.Controller.ObserveTelemetry(true, false, Now);
        runtime.Controller.OnStatusEvent(
            30,
            new[]
            {
                "[GSX] Waiting your confirmation for good engine start (Confirm from the GSX Menu)"
            },
            Now,
            enginesStabilized: false);
        runtime.Controller.OnMenuOpened(
            Menu("Engine start", "Confirm good engine start"),
            Now.AddSeconds(1));

        Assert.IsTrue(runtime.Controller.TryAutoConfirmGoodEngineStart(
            enginesStabilized: false,
            Now.AddSeconds(1)));
        Assert.AreEqual(0, runtime.Effects.MenuChoices.Count);

        Assert.IsTrue(runtime.Controller.TryAutoConfirmGoodEngineStart(
            enginesStabilized: true,
            Now.AddSeconds(2)));
        CollectionAssert.AreEqual(new[] { 0 }, runtime.Effects.MenuChoices);
    }

    [TestMethod]
    public void GoodEngineStartDoesNotAnswerClosedOrStaleMenu()
    {
        using var runtime = new TestRuntime();
        runtime.Controller.ObserveTelemetry(true, false, Now);
        runtime.Controller.OnStatusEvent(
            30,
            new[]
            {
                "[GSX] Waiting your confirmation for good engine start (Confirm from the GSX Menu)"
            },
            Now,
            enginesStabilized: true);
        runtime.Controller.OnMenuOpened(
            Menu("Engine start", "Confirm good engine start"),
            Now.AddSeconds(1));
        runtime.Controller.OnMenuCancelledOrTimedOut();

        Assert.IsFalse(runtime.Controller.TryAutoConfirmGoodEngineStart(
            enginesStabilized: true,
            Now.AddSeconds(2)));
        Assert.AreEqual(0, runtime.Effects.MenuChoices.Count);
    }

    [TestMethod]
    public void DisconnectClearsOwnershipMenuAndPendingInteractions()
    {
        using var runtime = new TestRuntime();
        runtime.Controller.ObserveTelemetry(true, false, Now);
        runtime.Controller.ClaimRemoteControl(Now);
        runtime.Controller.OnMenuOpened(Menu("Question", "Answer"), Now);
        runtime.Controller.RequestMenuChoice(0, "Answer", "request-4", Now);
        runtime.Controller.BeginAction(
            GsxDepartureAction.PrepareForDeparture,
            Now);

        runtime.Controller.OnSimConnectDisconnected();

        var snapshot = runtime.Controller.Snapshot;
        Assert.IsFalse(snapshot.CouatlStarted);
        Assert.IsFalse(snapshot.RemoteControlActive);
        Assert.IsFalse(snapshot.OwnsRemoteControl);
        Assert.IsFalse(snapshot.MenuOpen);
        Assert.IsTrue(snapshot.CurrentMenu.IsEmpty);
        Assert.IsFalse(snapshot.PendingChoice);
        Assert.IsFalse(snapshot.AwaitingChoiceAcknowledgement);
        Assert.IsNull(snapshot.PendingAction);
    }

    [TestMethod]
    public void PositiveLifetimeWithTemporaryEmptyFileRetainsStatus()
    {
        using var runtime = new TestRuntime();
        var status = new[] { "[GSX] Release parking brakes" };
        runtime.Controller.OnStatusEvent(
            10,
            status,
            Now,
            enginesStabilized: false);

        runtime.Controller.OnStatusEvent(
            10,
            Array.Empty<string>(),
            Now.AddSeconds(1),
            enginesStabilized: false);

        CollectionAssert.AreEqual(
            status,
            runtime.Controller.CurrentNotifications(Now.AddSeconds(2)).ToArray());
    }

    private static GsxMenuSnapshot Menu(
        string title,
        params string[] choices) =>
        new(title, choices);

    private sealed class TestRuntime : IDisposable
    {
        private readonly string _leasePath = Path.Combine(
            Path.GetTempPath(),
            $"gsx-controller-{Guid.NewGuid():N}.lease");

        public TestRuntime()
        {
            Effects = new FakeEffects();
            Controller = new GsxIntegrationController(
                Effects,
                new GsxOwnershipLease(_leasePath));
        }

        public FakeEffects Effects { get; }
        public GsxIntegrationController Controller { get; }

        public void Dispose()
        {
            if (File.Exists(_leasePath))
            {
                File.Delete(_leasePath);
            }
        }
    }

    private sealed class FakeEffects : IGsxRuntimeEffects
    {
        public List<bool> RemoteControlWrites { get; } = new();
        public List<TimeSpan> MenuOpenRequests { get; } = new();
        public List<int> MenuChoices { get; } = new();
        public List<string> Logs { get; } = new();
        public List<string> DashboardLogs { get; } = new();
        public List<CommandResult> CommandResults { get; } = new();

        public void SetRemoteControl(bool enabled) =>
            RemoteControlWrites.Add(enabled);

        public void RequestMenuOpen(TimeSpan delay) =>
            MenuOpenRequests.Add(delay);

        public void SendMenuChoice(int choice) => MenuChoices.Add(choice);
        public void Log(string message) => Logs.Add(message);
        public void DashboardLog(string message) => DashboardLogs.Add(message);

        public void SendCommandResult(
            string requestId,
            bool success,
            string message) =>
            CommandResults.Add(new CommandResult(requestId, success, message));
    }

    private sealed class CommandResult
    {
        public CommandResult(string requestId, bool success, string message)
        {
            RequestId = requestId;
            Success = success;
            Message = message;
        }

        public string RequestId { get; }
        public bool Success { get; }
        public string Message { get; }
    }
}
