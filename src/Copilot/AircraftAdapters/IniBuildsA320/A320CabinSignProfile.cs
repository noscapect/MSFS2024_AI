namespace Msfs2024Ai.Copilot.AircraftAdapters.IniBuildsA320;

/// <summary>
/// iniBuilds A320neo V2 cabin-sign policy. AUTO is retained throughout the
/// flight so the aircraft controls the illuminated signs for each phase.
/// This profile is intentionally independent from A321, A330 and FBW logic.
/// </summary>
internal static class A320CabinSignProfile
{
    public const int AutoPosition = 1;
    public const string SeatbeltsAutoCommand = "seatbelts auto";
    public const string NoSmokingAutoCommand = "no-smoking auto";

    public static bool IsAuto(double? selectorPosition) =>
        selectorPosition.HasValue
        && Math.Abs(selectorPosition.Value - AutoPosition) < 0.1;
}
