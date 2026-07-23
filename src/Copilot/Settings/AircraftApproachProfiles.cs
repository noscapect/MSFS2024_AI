namespace Msfs2024Ai.Copilot.Settings;

public sealed class ApproachScheduleSettings
{
    public int Flaps1DistanceNm { get; set; }
    public int Flaps1AltitudeFeet { get; set; }
    public int Flaps1SpeedKnots { get; set; }
    public int Flaps2DistanceNm { get; set; }
    public int Flaps2AltitudeAglFeet { get; set; }
    public int Flaps2SpeedKnots { get; set; }
    public int GearDistanceNm { get; set; }
    public int GearAltitudeAglFeet { get; set; }
    public int GearSpeedKnots { get; set; }
    public int LandingConfigDistanceNm { get; set; }
    public int LandingConfigAltitudeAglFeet { get; set; }
    public int LandingConfigSpeedKnots { get; set; }
    public int FlapsFullSpeedKnots { get; set; }

    public ApproachScheduleSettings Clone() => (ApproachScheduleSettings)MemberwiseClone();
}

public sealed class AircraftApproachOverride
{
    public string ProfileKey { get; set; } = "";
    public ApproachScheduleSettings Schedule { get; set; } = new();
}

internal sealed class AircraftApproachProfile
{
    public AircraftApproachProfile(
        string key,
        string displayName,
        ApproachScheduleSettings standardSchedule)
    {
        Key = key;
        DisplayName = displayName;
        StandardSchedule = standardSchedule;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public ApproachScheduleSettings StandardSchedule { get; }
}

internal static class AircraftApproachProfiles
{
    private static readonly AircraftApproachProfile IniA320 = Profile(
        "inibuilds-a320neo-v2", "iniBuilds A320neo V2",
        15, 10000, 230, 10, 4000, 200, 7, 2500, 210, 5, 1800, 185, 185);
    private static readonly AircraftApproachProfile FbwA320 = Profile(
        "fbw-a32nx", "FlyByWire A32NX",
        15, 10000, 230, 10, 4000, 200, 7, 2500, 210, 5, 1800, 185, 185);
    private static readonly AircraftApproachProfile IniA321 = Profile(
        "inibuilds-a321lr", "iniBuilds A321LR",
        15, 10000, 230, 11, 4000, 215, 7, 2500, 210, 5, 1800, 195, 186);
    private static readonly AircraftApproachProfile IniA330 = Profile(
        "inibuilds-a330", "iniBuilds A330",
        16, 10000, 230, 11, 3500, 195, 8, 2500, 210, 5, 1800, 185, 177);
    private static readonly AircraftApproachProfile Pmdg737 = Profile(
        "pmdg-737-800", "PMDG 737-800",
        15, 10000, 230, 12, 4000, 190, 7, 2500, 200, 5, 1800, 190, 190);
    private static readonly AircraftApproachProfile Asobo737Max = Profile(
        "asobo-737-max-8", "Asobo 737 MAX 8",
        15, 10000, 230, 12, 4000, 190, 7, 2500, 200, 5, 1800, 190, 190);
    private static readonly AircraftApproachProfile Generic = Profile(
        "generic", "Generic standard",
        15, 10000, 230, 10, 4000, 200, 7, 2500, 210, 5, 1800, 185, 185);

    public static AircraftApproachProfile Resolve(string? aircraftTitle)
    {
        var title = aircraftTitle ?? "";
        if (title.IndexOf("A321", StringComparison.OrdinalIgnoreCase) >= 0) return IniA321;
        if (title.IndexOf("A330", StringComparison.OrdinalIgnoreCase) >= 0) return IniA330;
        if (title.IndexOf("737-800", StringComparison.OrdinalIgnoreCase) >= 0
            || title.IndexOf("738", StringComparison.OrdinalIgnoreCase) >= 0) return Pmdg737;
        if (title.IndexOf("737 Max", StringComparison.OrdinalIgnoreCase) >= 0
            || title.IndexOf("737 MAX", StringComparison.OrdinalIgnoreCase) >= 0
            || title.IndexOf("B38M", StringComparison.OrdinalIgnoreCase) >= 0) return Asobo737Max;
        if (title.IndexOf("A32NX", StringComparison.OrdinalIgnoreCase) >= 0
            || title.IndexOf("FlyByWire", StringComparison.OrdinalIgnoreCase) >= 0
            && title.IndexOf("A320", StringComparison.OrdinalIgnoreCase) >= 0) return FbwA320;
        if (string.Equals(title, "A320neo V2", StringComparison.OrdinalIgnoreCase)) return IniA320;
        return Generic;
    }

    public static ApproachScheduleSettings EffectiveSchedule(
        string? aircraftTitle,
        IEnumerable<AircraftApproachOverride>? overrides)
    {
        var profile = Resolve(aircraftTitle);
        var saved = overrides?.LastOrDefault(item =>
            string.Equals(item.ProfileKey, profile.Key, StringComparison.OrdinalIgnoreCase));
        return (saved?.Schedule ?? profile.StandardSchedule).Clone();
    }

    private static AircraftApproachProfile Profile(
        string key, string name,
        int f1Distance, int f1Altitude, int f1Speed,
        int f2Distance, int f2Altitude, int f2Speed,
        int gearDistance, int gearAltitude, int gearSpeed,
        int landingDistance, int landingAltitude, int landingSpeed,
        int flapsFullSpeed) =>
        new(key, name, new ApproachScheduleSettings
        {
            Flaps1DistanceNm = f1Distance,
            Flaps1AltitudeFeet = f1Altitude,
            Flaps1SpeedKnots = f1Speed,
            Flaps2DistanceNm = f2Distance,
            Flaps2AltitudeAglFeet = f2Altitude,
            Flaps2SpeedKnots = f2Speed,
            GearDistanceNm = gearDistance,
            GearAltitudeAglFeet = gearAltitude,
            GearSpeedKnots = gearSpeed,
            LandingConfigDistanceNm = landingDistance,
            LandingConfigAltitudeAglFeet = landingAltitude,
            LandingConfigSpeedKnots = landingSpeed,
            FlapsFullSpeedKnots = flapsFullSpeed
        });
}
