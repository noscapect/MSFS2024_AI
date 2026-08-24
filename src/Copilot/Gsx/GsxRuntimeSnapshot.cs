namespace Msfs2024Ai.Copilot.Gsx;

internal sealed class GsxRuntimeSnapshot
{
    public GsxRuntimeSnapshot(
        bool couatlStarted,
        bool remoteControlActive,
        bool ownsRemoteControl,
        bool menuOpen,
        GsxMenuSnapshot currentMenu,
        DateTime menuReceivedUtc,
        bool menuHidden,
        bool pendingChoice,
        bool awaitingChoiceAcknowledgement,
        GsxDepartureAction? pendingAction,
        bool boardingRequestedThisFlight,
        bool boardingCompletedThisFlight,
        bool departureRequestedThisFlight,
        bool deboardingRequestedThisFlight,
        bool departureRequestAccepted,
        DateTime? departureRequestAcceptedUtc,
        string? pendingArrivalStand,
        string? selectedArrivalStand)
    {
        CouatlStarted = couatlStarted;
        RemoteControlActive = remoteControlActive;
        OwnsRemoteControl = ownsRemoteControl;
        MenuOpen = menuOpen;
        CurrentMenu = currentMenu;
        MenuReceivedUtc = menuReceivedUtc;
        MenuHidden = menuHidden;
        PendingChoice = pendingChoice;
        AwaitingChoiceAcknowledgement = awaitingChoiceAcknowledgement;
        PendingAction = pendingAction;
        BoardingRequestedThisFlight = boardingRequestedThisFlight;
        BoardingCompletedThisFlight = boardingCompletedThisFlight;
        DepartureRequestedThisFlight = departureRequestedThisFlight;
        DeboardingRequestedThisFlight = deboardingRequestedThisFlight;
        DepartureRequestAccepted = departureRequestAccepted;
        DepartureRequestAcceptedUtc = departureRequestAcceptedUtc;
        PendingArrivalStand = pendingArrivalStand;
        SelectedArrivalStand = selectedArrivalStand;
    }

    public bool CouatlStarted { get; }
    public bool RemoteControlActive { get; }
    public bool OwnsRemoteControl { get; }
    public bool MenuOpen { get; }
    public GsxMenuSnapshot CurrentMenu { get; }
    public DateTime MenuReceivedUtc { get; }
    public bool MenuHidden { get; }
    public bool PendingChoice { get; }
    public bool AwaitingChoiceAcknowledgement { get; }
    public GsxDepartureAction? PendingAction { get; }
    public bool BoardingRequestedThisFlight { get; }
    public bool BoardingCompletedThisFlight { get; }
    public bool DepartureRequestedThisFlight { get; }
    public bool DeboardingRequestedThisFlight { get; }
    public bool DepartureRequestAccepted { get; }
    public DateTime? DepartureRequestAcceptedUtc { get; }
    public string? PendingArrivalStand { get; }
    public string? SelectedArrivalStand { get; }
}
