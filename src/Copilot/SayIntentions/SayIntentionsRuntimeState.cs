namespace Msfs2024Ai.Copilot.SayIntentions;

/// <summary>
/// Passive SayIntentions session and readback state. API calls, polling,
/// procedure decisions, logging, GSX coordination, and voice remain owned by
/// CopilotService.
/// </summary>
internal sealed class SayIntentionsRuntimeState
{
    private readonly SayIntentionsCommunicationTracker _communicationTracker = new();

    public SayIntentionsFlightContext? Flight { get; private set; }
    public bool CopilotModeApplied { get; private set; }
    public string? CopilotModeSessionKey { get; private set; }
    public string? CommunicationSessionKey { get; private set; }
    public long LastCommunicationId { get; private set; }
    public string ApproachRunway { get; private set; } = "";
    public bool ApproachIsIls { get; private set; }
    public double? PushbackTargetHeadingDegrees { get; private set; }
    public DateTime PushbackTargetCapturedUtc { get; private set; } = DateTime.MinValue;

    public void SetFlight(SayIntentionsFlightContext? flight) => Flight = flight;

    public bool IsCopilotModeCurrent(string sessionKey, bool desired) =>
        string.Equals(CopilotModeSessionKey, sessionKey, StringComparison.Ordinal)
        && CopilotModeApplied == desired;

    public void RecordCopilotModeApplied(string sessionKey, bool enabled)
    {
        CopilotModeSessionKey = sessionKey;
        CopilotModeApplied = enabled;
    }

    public void ClearCopilotModeState()
    {
        CopilotModeApplied = false;
        CopilotModeSessionKey = null;
    }

    public bool IsNewCommunicationSession(string sessionKey) =>
        !string.Equals(CommunicationSessionKey, sessionKey, StringComparison.Ordinal);

    public void BeginCommunicationSession(
        string sessionKey,
        IReadOnlyList<SayIntentionsCommunication> communications)
    {
        CommunicationSessionKey = sessionKey;
        ApproachRunway = "";
        ApproachIsIls = false;
        LastCommunicationId = communications.Count == 0
            ? 0
            : communications.Max(item => item.Id);
        _communicationTracker.Reset();
        _communicationTracker.Prime(communications);
    }

    public void EstablishCommunicationBaseline(
        string sessionKey,
        IReadOnlyList<SayIntentionsCommunication> communications)
    {
        CommunicationSessionKey = sessionKey;
        LastCommunicationId = communications.Count == 0
            ? 0
            : communications.Max(item => item.Id);
        _communicationTracker.Prime(communications);
    }

    public SayIntentionsCommunicationChange ObserveCommunication(
        SayIntentionsCommunication communication)
    {
        LastCommunicationId = Math.Max(LastCommunicationId, communication.Id);
        return _communicationTracker.Observe(communication);
    }

    public bool RecordApproachAssignment(SayIntentionsApproachAssignment assignment)
    {
        if (string.Equals(ApproachRunway, assignment.Runway, StringComparison.OrdinalIgnoreCase)
            && ApproachIsIls == assignment.IsIls)
        {
            return false;
        }

        ApproachRunway = assignment.Runway;
        ApproachIsIls = assignment.IsIls;
        return true;
    }

    public void RecordPushbackTargetHeading(double headingDegrees, DateTime utcNow)
    {
        PushbackTargetHeadingDegrees = headingDegrees;
        PushbackTargetCapturedUtc = utcNow;
    }

    public void ClearPushbackTargetHeading()
    {
        PushbackTargetHeadingDegrees = null;
        PushbackTargetCapturedUtc = DateTime.MinValue;
    }

    public void ResetDiscoverySession()
    {
        Flight = null;
        ClearCopilotModeState();
        CommunicationSessionKey = null;
        LastCommunicationId = 0;
        _communicationTracker.Reset();
    }
}
