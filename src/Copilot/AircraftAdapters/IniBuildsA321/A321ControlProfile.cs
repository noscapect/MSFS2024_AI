namespace Msfs2024Ai.Copilot.AircraftAdapters.IniBuildsA321;

/// <summary>
/// Locked iniBuilds A321LR cockpit mappings. Keep these mappings independent
/// from other Airbus profiles: the A321 generic flap-surface SimVars are not
/// reliable enough to replace the physical handle readback.
/// </summary>
internal static class A321ControlProfile
{
    public const ulong SeatbeltInputEventHash = 12887035727064807174UL;
    public const ulong NoSmokingInputEventHash = 12889273306186432835UL;
    public const ulong EmergencyExitInputEventHash = 15249578372676866282UL;
    public const ulong RunwayTurnoffInputEventHash = 346751915044149355UL;

    public static ulong GetSignInputEventHash(int selectorIndex) =>
        selectorIndex switch
        {
            0 => SeatbeltInputEventHash,
            1 => NoSmokingInputEventHash,
            2 => EmergencyExitInputEventHash,
            _ => throw new ArgumentOutOfRangeException(nameof(selectorIndex))
        };

    public static bool SignSelectorAtPosition(double? position, int desiredPosition) =>
        position.HasValue && Math.Abs(position.Value - desiredPosition) < 0.1;

    public static bool FlapsAtDetent(double handleIndex, int detent) =>
        Math.Abs(handleIndex - detent) < 0.1;

    public static string BuildTakeoffFlapsCommand() =>
        "MF.SimVars.Set.16384 (A:FLAPS NUM HANDLE POSITIONS, Number) / " +
        "(>B:HANDLING_Flaps_Inc)";

    public static string BuildFlapsExtensionCommand() =>
        "MF.SimVars.Set.16384 (A:FLAPS NUM HANDLE POSITIONS, Number) / " +
        "(>B:HANDLING_Flaps_Inc)";

    public static string BuildFlapsCleanCommand(bool onGround) =>
        onGround
            ? "MF.SimVars.Set.0 (>B:HANDLING_Flaps_Set)"
            : "MF.SimVars.Set.16384 (A:FLAPS NUM HANDLE POSITIONS, Number) / " +
              "(>B:HANDLING_Flaps_Dec)";
}
