namespace Msfs2024Ai.Copilot.Gsx;

internal interface IGsxRuntimeEffects
{
    void SetRemoteControl(bool enabled);
    void RequestMenuOpen(TimeSpan delay);
    void SendMenuChoice(int choice);
    void Log(string message);
    void DashboardLog(string message);
    void SendCommandResult(string requestId, bool success, string message);
}
