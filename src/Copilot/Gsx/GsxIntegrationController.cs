namespace Msfs2024Ai.Copilot.Gsx;

internal enum GsxMenuHiddenResult
{
    None,
    ChoiceAcknowledged,
    UnansweredMenuClosed
}

internal sealed class GsxIntegrationController
{
    private static readonly TimeSpan PendingActionTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PendingChoiceTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ChoiceAcknowledgementTimeout = TimeSpan.FromSeconds(6);
    private static readonly GsxMenuSnapshot EmptyMenu =
        new(string.Empty, Array.Empty<string>());

    private readonly IGsxRuntimeEffects _effects;
    private readonly GsxOwnershipLease _ownershipLease;
    private readonly GsxStatusTracker _statusTracker = new();
    private bool _couatlStarted;
    private bool _remoteControlActive;
    private bool _ownsRemoteControl;
    private GsxDepartureAction? _pendingAction;
    private DateTime? _pendingActionDeadlineUtc;
    private bool _boardingRequestedThisFlight;
    private bool _boardingCompletedThisFlight;
    private bool _departureRequestedThisFlight;
    private bool _deboardingRequestedThisFlight;
    private bool _departureRequestAccepted;
    private DateTime? _departureRequestAcceptedUtc;
    private bool _goodEngineStartMenuRequested;
    private bool _goodEngineStartPromptPending;
    private bool _goodEngineStartWaitingLogged;
    private bool _menuHidden;
    private DateTime _menuReceivedUtc = DateTime.MinValue;
    private string? _pendingChoiceTitle;
    private string? _pendingChoiceLabel;
    private string? _pendingChoiceRequestId;
    private DateTime? _pendingChoiceDeadlineUtc;
    private string? _awaitingChoiceAckLabel;
    private string? _awaitingChoiceAckRequestId;
    private DateTime? _awaitingChoiceAckDeadlineUtc;
    private string? _pendingArrivalStand;
    private string? _selectedArrivalStand;
    private bool _menuOpen;
    private GsxMenuSnapshot _menu = EmptyMenu;

    public GsxIntegrationController(
        IGsxRuntimeEffects effects,
        GsxOwnershipLease? ownershipLease = null)
    {
        _effects = effects;
        _ownershipLease = ownershipLease ?? new GsxOwnershipLease();
    }

    public GsxRuntimeSnapshot Snapshot => new(
        _couatlStarted,
        _remoteControlActive,
        _ownsRemoteControl,
        _menuOpen,
        _menu,
        _menuReceivedUtc,
        _menuHidden,
        _pendingChoiceLabel != null,
        _awaitingChoiceAckLabel != null,
        _pendingAction,
        _boardingRequestedThisFlight,
        _boardingCompletedThisFlight,
        _departureRequestedThisFlight,
        _deboardingRequestedThisFlight,
        _departureRequestAccepted,
        _departureRequestAcceptedUtc,
        _pendingArrivalStand,
        _selectedArrivalStand);

    public IReadOnlyList<string> CurrentNotifications(DateTime utcNow) =>
        _statusTracker.CurrentNotifications(utcNow);

    public IReadOnlyList<string> StatusSnapshot(DateTime utcNow) =>
        _statusTracker.Snapshot(utcNow);

    public void ObserveTelemetry(
        bool couatlStarted,
        bool remoteControlActive,
        DateTime utcNow)
    {
        _remoteControlActive = remoteControlActive;
        if (_ownsRemoteControl && !_remoteControlActive)
        {
            _ownsRemoteControl = false;
            _ownershipLease.Clear();
        }
        else if (_remoteControlActive
                 && !_ownsRemoteControl
                 && _ownershipLease.CanRecover(utcNow))
        {
            _ownsRemoteControl = true;
            _effects.Log(
                "Recovered GSX Remote Control ownership from the previous VFO process.");
        }
        else if (!_remoteControlActive && !_ownsRemoteControl)
        {
            _ownershipLease.Clear();
        }

        SetCouatlStarted(couatlStarted);
    }

    public bool OnStatusEvent(
        uint notificationLifetimeSeconds,
        IReadOnlyList<string> tooltip,
        DateTime utcNow,
        bool enginesStabilized)
    {
        var menuInvalidated = false;
        if (notificationLifetimeSeconds == 0)
        {
            _statusTracker.Update(Array.Empty<string>(), TimeSpan.Zero, utcNow);
        }
        else if (tooltip.Count > 0)
        {
            _statusTracker.Update(
                tooltip,
                TimeSpan.FromSeconds(notificationLifetimeSeconds),
                utcNow);
            _effects.Log("GSX status: " + string.Join(" | ", tooltip));
            menuInvalidated = InvalidateHiddenMenuAfterStatusChange();
        }
        else
        {
            // A positive GSX lifetime with an empty file is an I/O race, not
            // a notification-clear event.
            _effects.Log(
                "GSX notification file was temporarily unavailable; retaining the previous live status.");
        }

        HandleStatusPrompt(utcNow, enginesStabilized);
        return menuInvalidated;
    }

    public bool OnMenuOpened(
        GsxMenuSnapshot menu,
        DateTime utcNow)
    {
        _menuHidden = false;
        _menu = menu;
        _menuOpen = !_menu.IsEmpty;
        if (_menuOpen)
        {
            _menuReceivedUtc = utcNow;
            _effects.Log(
                $"GSX menu: {_menu.Title} | "
                + string.Join(" | ", _menu.Choices));
        }

        if (TrySubmitRefreshedChoice(utcNow))
        {
            return true;
        }

        TrySelectPendingAction(utcNow);
        return false;
    }

    public GsxMenuHiddenResult OnMenuHidden()
    {
        if (_awaitingChoiceAckLabel != null)
        {
            CompleteChoiceAcknowledgement();
            return GsxMenuHiddenResult.ChoiceAcknowledged;
        }

        if (!_menuOpen)
        {
            return GsxMenuHiddenResult.None;
        }

        ClearMenu();
        _effects.Log(
            "GSX hid the unanswered menu; removed the stale remote question.");
        return GsxMenuHiddenResult.UnansweredMenuClosed;
    }

    public void OnMenuCancelledOrTimedOut()
    {
        FailPendingChoice(
            "GSX closed or timed out the question before confirming the selection.");
        FailChoiceAcknowledgement(
            "GSX closed or timed out the question before accepting the selection.");
        ClearMenu();
        _effects.Log(
            "GSX prompt timed out or was cancelled; cleared the cached response.");
    }

    public void OnToolbarPanelClosed() =>
        _effects.Log(
            "GSX toolbar panel closed; retaining any pending remote response.");

    public bool BeginAction(GsxDepartureAction action, DateTime utcNow)
    {
        if (!_couatlStarted)
        {
            _effects.DashboardLog(
                "GSX Couatl is not ready; use GSX manually or retry shortly.");
            return false;
        }
        if (_remoteControlActive && !_ownsRemoteControl)
        {
            _effects.DashboardLog(
                "GSX Remote Control is already in use by another add-on; no request was sent.");
            return false;
        }
        if (_pendingAction.HasValue)
        {
            _effects.DashboardLog("A GSX departure request is already pending.");
            return false;
        }

        _pendingAction = action;
        _pendingActionDeadlineUtc = utcNow.Add(PendingActionTimeout);
        if (!_ownsRemoteControl)
        {
            ClaimRemoteControl(utcNow);
        }

        _effects.RequestMenuOpen(TimeSpan.FromMilliseconds(500));
        _effects.DashboardLog(
            action switch
            {
                GsxDepartureAction.Boarding => "Requesting boarding through GSX.",
                GsxDepartureAction.Deboarding => "Requesting deboarding through GSX.",
                _ => "Requesting GSX preparation for pushback and departure."
            });
        return true;
    }

    public bool ClaimRemoteControl(DateTime utcNow)
    {
        if (_ownsRemoteControl)
        {
            return true;
        }
        if (_remoteControlActive)
        {
            return false;
        }

        _effects.SetRemoteControl(true);
        _ownsRemoteControl = true;
        _remoteControlActive = true;
        _ownershipLease.MarkOwned(utcNow);
        return true;
    }

    public void OpenMenu() => _effects.RequestMenuOpen(TimeSpan.Zero);

    public bool RecoverRemoteControl(DateTime utcNow)
    {
        if (!_couatlStarted
            || !_remoteControlActive
            || _ownsRemoteControl)
        {
            return false;
        }

        _ownsRemoteControl = true;
        _ownershipLease.MarkOwned(utcNow);
        _effects.DashboardLog(
            "Recovered GSX Remote Control after an interrupted VFO session.");
        return true;
    }

    public bool ReleaseRemoteControl(bool canTransmit)
    {
        if (!_ownsRemoteControl || !canTransmit)
        {
            return false;
        }

        _effects.SetRemoteControl(false);
        _ownsRemoteControl = false;
        _remoteControlActive = false;
        _ownershipLease.Clear();
        _pendingAction = null;
        _pendingActionDeadlineUtc = null;
        FailPendingChoice(
            "GSX remote control was released before the selected response could be submitted.");
        FailChoiceAcknowledgement(
            "GSX remote control was released before accepting the selected response.");
        ClearGoodEngineStartPrompt();
        return true;
    }

    public bool RequestMenuChoice(
        int choice,
        string label,
        string? requestId,
        DateTime utcNow)
    {
        if (_pendingChoiceLabel != null
            || _awaitingChoiceAckLabel != null)
        {
            if (!string.IsNullOrWhiteSpace(requestId))
            {
                _effects.SendCommandResult(
                    requestId!,
                    false,
                    "Another GSX response is already being submitted.");
            }
            return false;
        }

        if (_menuHidden)
        {
            CacheChoiceForRefresh(_menu.Title, label, requestId, utcNow);
            OpenMenu();
            _effects.DashboardLog(
                $"Refreshing the live GSX question before submitting '{label}'. Keep the parking brake set.");
            _effects.Log(
                $"GSX cached choice '{label}' is being matched against a refreshed live menu before transmission.");
            return false;
        }

        SubmitLiveChoice(choice, label, requestId, utcNow);
        return true;
    }

    public void SubmitLiveChoice(
        int choice,
        string label,
        string? requestId,
        DateTime utcNow)
    {
        SendMenuChoice(choice, label, utcNow);
        _awaitingChoiceAckLabel = label;
        _awaitingChoiceAckRequestId = requestId;
        _awaitingChoiceAckDeadlineUtc = utcNow.Add(ChoiceAcknowledgementTimeout);
        _effects.DashboardLog($"Waiting for GSX to accept '{label}'.");
    }

    public void CancelMenu(DateTime utcNow) =>
        SendMenuChoice(-1, "cancelled", utcNow);

    public void SendChoiceWithoutAcknowledgement(
        int choice,
        string? label,
        DateTime utcNow) =>
        SendMenuChoice(choice, label, utcNow);

    public bool TryAutoConfirmGoodEngineStart(
        bool enginesStabilized,
        DateTime utcNow)
    {
        if (!GsxPromptPolicy.CanAnswerGoodEngineStart(
                _goodEngineStartPromptPending,
                _statusTracker.CurrentNotifications(utcNow),
                _menu)
            || !_menuOpen
            || _menu.IsEmpty)
        {
            return false;
        }

        var choice = GsxPromptPolicy.FindGoodEngineStartConfirmation(_menu);
        if (!choice.HasValue)
        {
            return false;
        }

        _goodEngineStartPromptPending = true;
        if (!enginesStabilized)
        {
            if (!_goodEngineStartWaitingLogged)
            {
                _effects.Log(
                    "GSX good-engine-start prompt is open; waiting for both engines stabilized before responding.");
                _effects.DashboardLog(
                    "GSX is waiting for good engine start; First Officer will answer when both engines are stable.");
                _goodEngineStartWaitingLogged = true;
            }
            return true;
        }

        var label = _menu.Choices[choice.Value];
        var refreshRequired = _menuHidden;
        RequestMenuChoice(choice.Value, label, null, utcNow);
        if (refreshRequired)
        {
            _effects.Log(
                $"Refreshing the hidden GSX good-engine-start question before submitting: {label}.");
            return true;
        }

        _effects.DashboardLog(
            "First Officer confirmed good engine start to GSX.");
        _effects.Log(
            $"Auto-confirmed GSX good-engine-start response: {label}.");
        return true;
    }

    public void Update(DateTime utcNow)
    {
        if (_pendingChoiceDeadlineUtc.HasValue
            && utcNow >= _pendingChoiceDeadlineUtc.Value)
        {
            FailPendingChoice(
                "GSX did not reopen the question in time. No response was sent; keep the parking brake set and retry.");
        }
        if (_awaitingChoiceAckDeadlineUtc.HasValue
            && utcNow >= _awaitingChoiceAckDeadlineUtc.Value)
        {
            FailChoiceAcknowledgement(
                "GSX did not confirm the selected response. Keep the parking brake set and retry.");
        }

        if (!_pendingActionDeadlineUtc.HasValue
            || utcNow < _pendingActionDeadlineUtc.Value)
        {
            return;
        }

        if (_pendingAction == GsxDepartureAction.Boarding)
        {
            _boardingRequestedThisFlight = false;
        }
        _effects.DashboardLog(
            "GSX did not provide the expected service menu; use GSX manually or retry.");
        _pendingAction = null;
        _pendingActionDeadlineUtc = null;
    }

    public void CancelPendingAction()
    {
        _pendingAction = null;
        _pendingActionDeadlineUtc = null;
    }

    public void CancelGoodEngineStartPrompt() =>
        ClearGoodEngineStartPrompt();

    public void ResetFlightState()
    {
        _boardingRequestedThisFlight = false;
        _boardingCompletedThisFlight = false;
        _departureRequestedThisFlight = false;
        _deboardingRequestedThisFlight = false;
        _departureRequestAccepted = false;
        _departureRequestAcceptedUtc = null;
        _statusTracker.Reset();
        ClearGoodEngineStartPrompt();
        _pendingArrivalStand = null;
        _selectedArrivalStand = null;
        CancelPendingAction();
    }

    public void OnSimConnectDisconnected()
    {
        _ownsRemoteControl = false;
        _remoteControlActive = false;
        _couatlStarted = false;
        _statusTracker.Reset();
        CancelPendingAction();
        ClearPendingChoice();
        ClearChoiceAcknowledgement();
        ClearGoodEngineStartPrompt();
        ClearMenu();
    }

    public void SetBoardingRequestedThisFlight(bool value) =>
        _boardingRequestedThisFlight = value;

    public void SetBoardingCompletedThisFlight(bool value) =>
        _boardingCompletedThisFlight = value;

    public void SetDepartureRequestedThisFlight(bool value) =>
        _departureRequestedThisFlight = value;

    public void SetDeboardingRequestedThisFlight(bool value) =>
        _deboardingRequestedThisFlight = value;

    public void ClearDepartureRequestAcceptedTime() =>
        _departureRequestAcceptedUtc = null;

    public void SetArrivalStandPending(string stand)
    {
        _pendingArrivalStand = stand;
        _selectedArrivalStand = null;
    }

    public void CompleteArrivalStandSelection(string stand)
    {
        _selectedArrivalStand = stand;
        _pendingArrivalStand = null;
    }

    public void SendAutomatedMenuChoice(int choice)
    {
        _effects.SendMenuChoice(choice);
        _menuOpen = false;
    }

    private void SendDirectMenuChoice(int choice, DateTime utcNow)
    {
        _effects.SendMenuChoice(choice);
        _menuOpen = false;
        _menuHidden = false;
        _menuReceivedUtc = DateTime.MinValue;
        if (_pendingAction == GsxDepartureAction.PrepareForDeparture)
        {
            _departureRequestAccepted = true;
            _departureRequestAcceptedUtc = utcNow;
        }
        _pendingAction = null;
        _pendingActionDeadlineUtc = null;
    }

    internal void CacheChoiceForRefresh(
        string expectedTitle,
        string expectedLabel,
        string? requestId,
        DateTime utcNow)
    {
        _pendingChoiceTitle = expectedTitle;
        _pendingChoiceLabel = expectedLabel;
        _pendingChoiceRequestId = requestId;
        _pendingChoiceDeadlineUtc = utcNow.Add(PendingChoiceTimeout);
    }

    private void SetCouatlStarted(bool value)
    {
        _couatlStarted = value;
        if (!value)
        {
            _statusTracker.Reset();
        }
    }

    private void HandleStatusPrompt(DateTime utcNow, bool enginesStabilized)
    {
        var needsConfirmation =
            GsxPromptPolicy.RequiresGoodEngineStartMenu(
                _statusTracker.CurrentNotifications(utcNow));
        if (!needsConfirmation)
        {
            ClearGoodEngineStartPrompt();
            return;
        }

        if (!_ownsRemoteControl)
        {
            if (_remoteControlActive)
            {
                _effects.Log(
                    "GSX is waiting for good-engine-start confirmation, but remote control is owned by another add-on.");
                return;
            }
            ClaimRemoteControl(utcNow);
        }

        _goodEngineStartPromptPending = true;
        if (_menuOpen)
        {
            TryAutoConfirmGoodEngineStart(enginesStabilized, utcNow);
            return;
        }

        OpenMenu();
        if (!_goodEngineStartMenuRequested)
        {
            _effects.DashboardLog(
                "GSX is waiting for good-engine-start confirmation; the First Officer will respond when both engines are stable.");
            _effects.Log(
                "Opened the GSX menu from its good-engine-start status prompt.");
        }
        _goodEngineStartMenuRequested = true;
    }

    private bool InvalidateHiddenMenuAfterStatusChange()
    {
        if (!_menuHidden)
        {
            return false;
        }

        FailPendingChoice(
            "GSX advanced to a new status before the selected response could be submitted.");
        ClearMenu();
        _effects.Log(
            "Cleared the hidden GSX question because GSX published a newer status.");
        return true;
    }

    private bool TrySubmitRefreshedChoice(DateTime utcNow)
    {
        if (_pendingChoiceTitle == null
            || _pendingChoiceLabel == null)
        {
            return false;
        }

        var choice = GsxPromptPolicy.FindMatchingChoice(
            _menu,
            _pendingChoiceTitle,
            _pendingChoiceLabel);
        if (!choice.HasValue)
        {
            FailPendingChoice(
                "The GSX question changed before the selected response could be submitted. Please choose from the current question.");
            return false;
        }

        var label = _menu.Choices[choice.Value];
        var requestId = _pendingChoiceRequestId;
        ClearPendingChoice();
        SubmitLiveChoice(choice.Value, label, requestId, utcNow);
        return true;
    }

    private void TrySelectPendingAction(DateTime utcNow)
    {
        if (!_pendingAction.HasValue || !_menuOpen)
        {
            return;
        }
        if (_pendingActionDeadlineUtc.HasValue
            && utcNow >= _pendingActionDeadlineUtc.Value)
        {
            if (_pendingAction == GsxDepartureAction.Boarding)
            {
                _boardingRequestedThisFlight = false;
            }
            _effects.DashboardLog(
                "GSX did not provide the expected service menu; use GSX manually or retry.");
            CancelPendingAction();
            return;
        }

        var choice = GsxDepartureCoordinator.FindChoice(_menu, _pendingAction.Value);
        if (!choice.HasValue)
        {
            _effects.Log(
                $"GSX menu '{_menu.Title}' did not contain the pending "
                + $"{_pendingAction.Value} action. Choices: "
                + string.Join(" | ", _menu.Choices));
            return;
        }

        var action = _pendingAction.Value;
        SendDirectMenuChoice(choice.Value, utcNow);
        _effects.DashboardLog(
            action switch
            {
                GsxDepartureAction.Boarding => "GSX boarding request accepted.",
                GsxDepartureAction.Deboarding => "GSX deboarding request accepted.",
                _ => "GSX departure preparation request accepted."
            });
    }

    private void SendMenuChoice(int choice, string? label, DateTime utcNow)
    {
        _effects.SendMenuChoice(choice);
        _menuOpen = false;
        _menuHidden = false;
        _menuReceivedUtc = DateTime.MinValue;
        if (_goodEngineStartPromptPending && choice >= 0)
        {
            ClearGoodEngineStartPrompt();
        }
        if (_pendingAction.HasValue)
        {
            if (_pendingAction.Value == GsxDepartureAction.PrepareForDeparture
                && choice >= 0)
            {
                _departureRequestAccepted = true;
                _departureRequestAcceptedUtc = utcNow;
            }
            CancelPendingAction();
        }
        _effects.DashboardLog(
            choice >= 0
                ? $"GSX selection sent: {label ?? $"option {choice + 1}"}."
                : "GSX prompt cancelled.");
        _effects.Log(
            choice >= 0
                ? $"GSX menu choice index {choice} transmitted: {label ?? "unlabelled"}."
                : "GSX menu cancellation transmitted.");
    }

    private void CompleteChoiceAcknowledgement()
    {
        if (_awaitingChoiceAckLabel == null)
        {
            return;
        }

        var label = _awaitingChoiceAckLabel;
        var requestId = _awaitingChoiceAckRequestId;
        ClearChoiceAcknowledgement();
        if (!string.IsNullOrWhiteSpace(requestId))
        {
            _effects.SendCommandResult(
                requestId!,
                true,
                $"GSX accepted '{label}'.");
        }
        _effects.DashboardLog($"GSX accepted: {label}.");
        _effects.Log($"GSX acknowledged menu choice: {label}.");
    }

    private void FailPendingChoice(string message)
    {
        var requestId = _pendingChoiceRequestId;
        if (_pendingChoiceLabel == null)
        {
            return;
        }

        ClearPendingChoice();
        if (!string.IsNullOrWhiteSpace(requestId))
        {
            _effects.SendCommandResult(requestId!, false, message);
        }
        _effects.DashboardLog(message);
        _effects.Log(message);
    }

    private void FailChoiceAcknowledgement(string message)
    {
        var requestId = _awaitingChoiceAckRequestId;
        if (_awaitingChoiceAckLabel == null)
        {
            return;
        }

        ClearChoiceAcknowledgement();
        if (!string.IsNullOrWhiteSpace(requestId))
        {
            _effects.SendCommandResult(requestId!, false, message);
        }
        _effects.DashboardLog(message);
        _effects.Log(message);
    }

    private void ClearPendingChoice()
    {
        _pendingChoiceTitle = null;
        _pendingChoiceLabel = null;
        _pendingChoiceRequestId = null;
        _pendingChoiceDeadlineUtc = null;
    }

    private void ClearChoiceAcknowledgement()
    {
        _awaitingChoiceAckLabel = null;
        _awaitingChoiceAckRequestId = null;
        _awaitingChoiceAckDeadlineUtc = null;
    }

    private void ClearGoodEngineStartPrompt()
    {
        _goodEngineStartMenuRequested = false;
        _goodEngineStartPromptPending = false;
        _goodEngineStartWaitingLogged = false;
    }

    private void ClearMenu()
    {
        _menuOpen = false;
        _menuHidden = false;
        _menuReceivedUtc = DateTime.MinValue;
        _menu = EmptyMenu;
    }
}
