namespace Msfs2024Ai.Copilot.Checklists;

internal sealed class ChecklistItem
{
    public ChecklistItem(
        string challenge,
        string expectedResponse,
        Func<AircraftState, bool?> verify)
    {
        Challenge = challenge;
        ExpectedResponse = expectedResponse;
        Verify = verify;
    }

    public string Challenge { get; }
    public string ExpectedResponse { get; }
    public Func<AircraftState, bool?> Verify { get; }
}
