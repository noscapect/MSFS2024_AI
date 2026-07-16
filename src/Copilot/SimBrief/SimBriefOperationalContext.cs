using Msfs2024Ai.Copilot.AircraftAdapters;
using Msfs2024Ai.Copilot.Settings;

namespace Msfs2024Ai.Copilot.SimBrief;

internal static class SimBriefOperationalContext
{
    private const double PoundsPerKilogram = 2.20462262185;

    public static IReadOnlyList<string> ExpectedAircraftIcaos(AircraftVariant variant) =>
        variant switch
        {
            AircraftVariant.Pmdg737800 => new[] { "B738" },
            AircraftVariant.IniBuildsA321Lr => new[] { "A21N", "A321" },
            AircraftVariant.IniBuildsA330 => new[] { "A333" },
            AircraftVariant.IniBuildsA320NeoV2 or AircraftVariant.FlyByWireA320Neo =>
                new[] { "A20N" },
            _ => Array.Empty<string>()
        };

    public static bool ApplyTakeoffSettings(
        ImportedFlightPlan? plan,
        CopilotSettings settings)
    {
        if (plan == null) return false;

        var changed = false;
        if (plan.TransitionAltitudeFeet is >= 1000 and <= 20000
            && settings.TransitionAltitudeFeet != plan.TransitionAltitudeFeet.Value)
        {
            settings.TransitionAltitudeFeet = plan.TransitionAltitudeFeet.Value;
            changed = true;
        }
        if (plan.TakeoffV1Knots is >= 80 and <= 219
            && settings.TakeoffV1SpeedKnots != plan.TakeoffV1Knots.Value)
        {
            settings.TakeoffV1SpeedKnots = plan.TakeoffV1Knots.Value;
            changed = true;
        }
        if (plan.TakeoffVrKnots is >= 80 and <= 220)
        {
            var reviewedVr = Math.Max(
                settings.TakeoffV1SpeedKnots,
                plan.TakeoffVrKnots.Value);
            if (settings.TakeoffRotateSpeedKnots != reviewedVr)
            {
                settings.TakeoffRotateSpeedKnots = reviewedVr;
                changed = true;
            }
        }
        return changed;
    }

    public static double? BlockFuelKilograms(ImportedFlightPlan? plan)
    {
        var blockFuel = plan?.BlockFuel;
        if (blockFuel is not > 0)
        {
            return null;
        }

        var units = (plan?.Units ?? string.Empty).Trim().ToLowerInvariant();
        return units.StartsWith("lb", StringComparison.Ordinal)
            ? blockFuel.Value / PoundsPerKilogram
            : blockFuel.Value;
    }

    public static int? TakeoffFlapSetting(
        ImportedFlightPlan? plan,
        AircraftVariant variant)
    {
        var value = plan?.TakeoffFlaps?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (variant == AircraftVariant.Pmdg737800)
        {
            var digits = new string(value!.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var boeingFlaps)
                && boeingFlaps is 1 or 2 or 5 or 10 or 15 or 25)
            {
                return boeingFlaps;
            }
            return null;
        }

        if (variant is AircraftVariant.IniBuildsA320NeoV2
            or AircraftVariant.IniBuildsA321Lr
            or AircraftVariant.IniBuildsA330
            or AircraftVariant.FlyByWireA320Neo)
        {
            if (value!.StartsWith("1", StringComparison.Ordinal)) return 1;
            if (value.StartsWith("2", StringComparison.Ordinal)) return 2;
            if (value.StartsWith("3", StringComparison.Ordinal)) return 3;
        }

        return null;
    }

    public static string FuelComparison(
        double? plannedKilograms,
        double actualKilograms)
    {
        if (!plannedKilograms.HasValue || plannedKilograms.Value <= 0)
        {
            return "No SimBrief block fuel";
        }
        if (actualKilograms <= 0)
        {
            return "Live fuel unavailable";
        }

        var difference = actualKilograms - plannedKilograms.Value;
        var tolerance = Math.Max(300, plannedKilograms.Value * 0.03);
        if (Math.Abs(difference) <= tolerance)
        {
            return "Within tolerance";
        }

        return difference < 0 ? "Below SimBrief plan" : "Above SimBrief plan";
    }

    public static string TakeoffComparison(
        ImportedFlightPlan? plan,
        AircraftVariant variant,
        int? cockpitV1,
        int? cockpitVr,
        int? cockpitFlaps)
    {
        if (plan == null)
        {
            return "No active SimBrief flight";
        }

        var differences = new List<string>();
        Compare("V1", plan.TakeoffV1Knots, cockpitV1, differences);
        Compare("VR", plan.TakeoffVrKnots, cockpitVr, differences);
        Compare("flaps", TakeoffFlapSetting(plan, variant), cockpitFlaps, differences);
        return differences.Count == 0
            ? "SimBrief/FMC match"
            : "Difference: " + string.Join(", ", differences);
    }

    private static void Compare(
        string label,
        int? planned,
        int? cockpit,
        ICollection<string> differences)
    {
        if (!planned.HasValue || !cockpit.HasValue || cockpit.Value <= 0)
        {
            return;
        }
        if (planned.Value != cockpit.Value)
        {
            differences.Add($"{label} {planned.Value}/{cockpit.Value}");
        }
    }
}
