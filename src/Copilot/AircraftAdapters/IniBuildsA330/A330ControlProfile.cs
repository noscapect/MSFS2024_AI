namespace Msfs2024Ai.Copilot.AircraftAdapters.IniBuildsA330;

/// <summary>
/// iniBuilds A330 cockpit mappings. The A330 sign InputEvents use the physical
/// order OFF/AUTO(or ARM)/ON, which is the reverse of the app's normalized
/// ON/AUTO(or ARM)/OFF convention.
/// </summary>
internal static class A330ControlProfile
{
    public const ulong RunwayTurnoffInputEventHash = 346751915044149355UL;
    public const int FlapStepIntervalMilliseconds = 700;
    public const string FlapsRetractOneDetentCommand =
        "MF.SimVars.Set.16384 (A:FLAPS NUM HANDLE POSITIONS, Number) / (>B:AIRLINER_Flaps_Dec)";

    public static double NormalizeSignPosition(double physicalPosition) =>
        2.0 - physicalPosition;

    public static double? NormalizeSignPosition(double? physicalPosition) =>
        physicalPosition.HasValue
            ? NormalizeSignPosition(physicalPosition.Value)
            : null;

    public static double ToPhysicalSignPosition(int normalizedPosition) =>
        2.0 - normalizedPosition;

    public static int FlapRetractionStepCount(double currentHandleIndex) =>
        Math.Max(1, (int)Math.Ceiling(currentHandleIndex));
}
