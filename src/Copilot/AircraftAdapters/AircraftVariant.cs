namespace Msfs2024Ai.Copilot.AircraftAdapters;

/// <summary>
/// Stable aircraft implementation boundary. New aircraft must receive a new
/// value and an explicit procedure/command route; they must never inherit an
/// existing aircraft implementation through a generic Airbus fallback.
/// </summary>
internal enum AircraftVariant
{
    Unsupported,
    IniBuildsA320NeoV2,
    IniBuildsA321Lr,
    IniBuildsA330,
    FlyByWireA320Neo,
    FlyByWireA380XExperimental,
    Pmdg737800
}

internal static class AircraftVariantResolver
{
    public static AircraftVariant Resolve(
        string? title,
        bool enableExperimentalFlyByWireA380X = false)
    {
        var value = title ?? string.Empty;
        var hasA380Signature = HasFlyByWireA380XSignature(value);
        if (hasA380Signature)
        {
            return enableExperimentalFlyByWireA380X
                ? AircraftVariant.FlyByWireA380XExperimental
                : AircraftVariant.Unsupported;
        }

        if (value.IndexOf("A32NX", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("A320", StringComparison.OrdinalIgnoreCase) >= 0
            && value.IndexOf("FlyByWire", StringComparison.OrdinalIgnoreCase) >= 0
            || string.Equals(value, "FlyByWire A32NX", StringComparison.OrdinalIgnoreCase))
        {
            return AircraftVariant.FlyByWireA320Neo;
        }

        var isPmdg737 =
            value.IndexOf("PMDG", StringComparison.OrdinalIgnoreCase) >= 0
            && value.IndexOf("737", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("737-800", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("738", StringComparison.OrdinalIgnoreCase) >= 0;
        if (isPmdg737
            && (value.IndexOf("737-800", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("738", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return AircraftVariant.Pmdg737800;
        }

        if (value.IndexOf("A330-300 (GE)", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("iniBuilds A330", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return AircraftVariant.IniBuildsA330;
        }

        if (string.Equals(value, "A321", StringComparison.OrdinalIgnoreCase)
            || value.IndexOf("A321", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return AircraftVariant.IniBuildsA321Lr;
        }

        return string.Equals(value, "A320neo V2", StringComparison.OrdinalIgnoreCase)
            ? AircraftVariant.IniBuildsA320NeoV2
            : AircraftVariant.Unsupported;
    }

    public static bool HasFlyByWireA380XSignature(string? title)
    {
        var value = title ?? string.Empty;
        return value.IndexOf("A380X", StringComparison.OrdinalIgnoreCase) >= 0
               || value.IndexOf("A380-842", StringComparison.OrdinalIgnoreCase) >= 0
               || value.IndexOf("A380", StringComparison.OrdinalIgnoreCase) >= 0
               && value.IndexOf("FlyByWire", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
