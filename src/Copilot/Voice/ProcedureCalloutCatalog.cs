namespace Msfs2024Ai.Copilot.Voice;

internal static class ProcedureCalloutCatalog
{
    public static string? ForStep(
        string stepId,
        AircraftState? state,
        CalloutDetail detail)
    {
        var essential = stepId switch
        {
            "captain-engine-two" => "Engine two on",
            "fo-engine-two-starter" => "Engine two starter valve open",
            "fo-engine-two-fuel" => "Engine two fuel flow",
            "fo-engine-two-stable" => "Engine two stabilized",
            "captain-engine-one" => "Engine one on",
            "fo-engine-one-starter" => "Engine one starter valve open",
            "fo-engine-one-fuel" => "Engine one fuel flow",
            "fo-engine-one-stable" => "Engine one stabilized",
            "fo-cabin-call" or "cabin-ready" => "Cabin crew, prepare for takeoff",
            "fo-cabin-landing-call" => "Cabin crew, prepare for landing",
            "captain-takeoff" or "thrust-set" => "Thrust set",
            "fo-100-knots" or "hundred-knots" => "One hundred knots",
            "fo-v1" or "v1" => "V one",
            "fo-rotate" or "rotate" => "Rotate",
            "positive-climb" or "airborne" => "Positive climb",
            "fo-gear-up" => "Landing gear up",
            "fo-gear-down" => "Landing gear down",
            "fo-approaching-minimums" => "Approaching minimums",
            "fo-minimums" => "Minimums",
            "fo-spoilers-callout" => "Spoilers",
            "fo-reverse-callout" => "Reverse green",
            "fo-decel-callout" => "Decel",
            _ => null
        };
        if (essential != null || detail == CalloutDetail.Minimal)
        {
            return essential;
        }

        var standard = stepId switch
        {
            "fo-ground-spoilers" => state?.IsPmdg737 == true
                ? "Speedbrake armed"
                : "Ground spoilers armed",
            "fo-spoilers-arm" => "Speedbrake armed",
            "fo-takeoff-flaps" => "Flaps one",
            "fo-flaps-takeoff" => BoeingFlaps(state?.BoeingTakeoffFlaps, "Takeoff flaps set"),
            "fo-flaps-one" => "Flaps one",
            "fo-flaps-two" => "Flaps two",
            "fo-flaps-three" => "Flaps three",
            "fo-flaps-full" => "Flaps full",
            "fo-flaps-five" => "Flaps five",
            "fo-flaps-fifteen" => "Flaps fifteen",
            "fo-flaps-landing" => BoeingFlaps(state?.BoeingLandingFlaps, "Landing flaps set"),
            "fo-flaps" or "fo-flaps-zero" => "Flaps zero",
            "fo-flaps-up" => "Flaps up",
            "stable-approach" => "One thousand, stable",
            _ => null
        };
        if (standard != null || detail == CalloutDetail.Standard)
        {
            return standard;
        }

        return stepId switch
        {
            "fo-autobrake-max" => "Autobrake max",
            "fo-autobrake-rto" => "Autobrake R T O",
            "fo-landing-autobrake-low" => "Autobrake low",
            "fo-autobrake" => "Autobrake set",
            "fo-autobrake-off" => "Autobrake off",
            "apu-available" => "APU available",
            _ => null
        };
    }

    public static string? ForCompletedProcedure(
        string procedureId,
        CalloutDetail detail)
    {
        if (detail == CalloutDetail.Minimal)
        {
            return null;
        }

        if (procedureId == "before-takeoff")
        {
            return detail == CalloutDetail.Expanded
                ? "Takeoff configuration normal. Before takeoff checklist complete"
                : "Takeoff configuration normal";
        }

        if (detail != CalloutDetail.Expanded)
        {
            return null;
        }

        return procedureId switch
        {
            "flight-computer-preflight" => "Preflight checklist complete",
            "after-start-taxi" => "After start checklist complete",
            "parking-shutdown" => "Shutdown checklist complete",
            _ => null
        };
    }

    private static string BoeingFlaps(int? setting, string fallback) =>
        setting switch
        {
            1 => "Flaps one",
            2 => "Flaps two",
            5 => "Flaps five",
            10 => "Flaps ten",
            15 => "Flaps fifteen",
            25 => "Flaps twenty five",
            30 => "Flaps thirty",
            40 => "Flaps forty",
            _ => fallback
        };
}
