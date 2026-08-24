namespace Msfs2024Ai.Copilot.Gsx;

internal sealed class GsxRuntimeEffects : IGsxRuntimeEffects
{
    private readonly Action<bool> _setRemoteControl;
    private readonly Action<TimeSpan> _requestMenuOpen;
    private readonly Action<int> _sendMenuChoice;
    private readonly Action<string> _log;
    private readonly Action<string> _dashboardLog;
    private readonly Action<string, bool, string> _sendCommandResult;

    public GsxRuntimeEffects(
        Action<bool> setRemoteControl,
        Action<TimeSpan> requestMenuOpen,
        Action<int> sendMenuChoice,
        Action<string> log,
        Action<string> dashboardLog,
        Action<string, bool, string> sendCommandResult)
    {
        _setRemoteControl = setRemoteControl;
        _requestMenuOpen = requestMenuOpen;
        _sendMenuChoice = sendMenuChoice;
        _log = log;
        _dashboardLog = dashboardLog;
        _sendCommandResult = sendCommandResult;
    }

    public void SetRemoteControl(bool enabled) => _setRemoteControl(enabled);
    public void RequestMenuOpen(TimeSpan delay) => _requestMenuOpen(delay);
    public void SendMenuChoice(int choice) => _sendMenuChoice(choice);
    public void Log(string message) => _log(message);
    public void DashboardLog(string message) => _dashboardLog(message);

    public void SendCommandResult(
        string requestId,
        bool success,
        string message) =>
        _sendCommandResult(requestId, success, message);
}
