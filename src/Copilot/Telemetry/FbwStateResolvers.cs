namespace Msfs2024Ai.Copilot.Telemetry;

internal static class FbwStateResolvers
{
    public static double ResolveNoseLightSelectorPosition(
        double selectorPosition,
        float? commandedValue,
        DateTime? commandedUtc,
        double takeoffCircuitOn,
        double taxiCircuitOn,
        double taxiLightOn)
    {
        // LIGHTING_LANDING_1 is the FBW A32NX cockpit selector itself:
        // 0 = T.O., 1 = TAXI, 2 = OFF.  Generic LIGHT TAXI and the
        // individual circuits describe emitted light state and can remain on
        // or be changed automatically with the landing gear, so they must not
        // override a valid selector readback.
        if (selectorPosition >= -0.1 && selectorPosition <= 2.1)
        {
            return Math.Round(selectorPosition);
        }

        if (commandedValue.HasValue
            && commandedUtc.HasValue
            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromSeconds(30))
        {
            return commandedValue.Value;
        }

        if (takeoffCircuitOn != 0)
        {
            return 0;
        }

        return taxiCircuitOn != 0 || taxiLightOn != 0 ? 1 : 2;
    }

    public static bool ResolveBattery(
        bool? commandedPushbuttonAuto,
        bool? typedPushbuttonAuto,
        bool? untypedPushbuttonAuto,
        double genericMasterBattery)
    {
        if (typedPushbuttonAuto.HasValue)
        {
            return typedPushbuttonAuto.Value;
        }

        if (untypedPushbuttonAuto.HasValue)
        {
            return untypedPushbuttonAuto.Value;
        }

        if (commandedPushbuttonAuto.HasValue)
        {
            return commandedPushbuttonAuto.Value;
        }

        return genericMasterBattery != 0;
    }

    public static bool ResolveBool(
        bool? commandedValue,
        bool? typedValue,
        bool? untypedValue)
    {
        if (typedValue.HasValue)
        {
            return typedValue.Value;
        }

        if (untypedValue.HasValue)
        {
            return untypedValue.Value;
        }

        return commandedValue == true;
    }

    public static double ResolveSelector(
        float? commandedValue,
        DateTime? commandedUtc,
        float? typedValue,
        float? untypedValue)
    {
        var recentCommand = commandedValue.HasValue
                            && commandedUtc.HasValue
                            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromSeconds(10);
        if (recentCommand
            && ((typedValue.HasValue && Math.Abs(typedValue.Value - commandedValue!.Value) < 0.1f)
                || (untypedValue.HasValue && Math.Abs(untypedValue.Value - commandedValue!.Value) < 0.1f)))
        {
            return commandedValue!.Value;
        }

        if (typedValue.HasValue)
        {
            return typedValue.Value;
        }

        if (untypedValue.HasValue)
        {
            return untypedValue.Value;
        }

        if (recentCommand)
        {
            return commandedValue!.Value;
        }

        return commandedValue ?? 0;
    }

    public static bool ResolveCrewOxygen(
        bool? commandedValue,
        DateTime? commandedUtc,
        bool? typedValue,
        bool? untypedValue)
    {
        // FBW exposes PUSH_OVHD_OXYGEN_CREW as the pushbutton/OFF-side state.
        // In cockpit terms this is inverted for the checklist:
        // raw true  = crew oxygen supply OFF
        // raw false = crew oxygen supply ON
        if (typedValue.HasValue)
        {
            return !typedValue.Value;
        }

        if (untypedValue.HasValue)
        {
            return !untypedValue.Value;
        }

        if (commandedValue.HasValue
            && commandedUtc.HasValue
            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromSeconds(10))
        {
            return commandedValue.Value;
        }

        return false;
    }
}
