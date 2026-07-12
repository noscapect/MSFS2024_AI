namespace Msfs2024Ai.Copilot.Telemetry;

internal static class FbwStateResolvers
{
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
