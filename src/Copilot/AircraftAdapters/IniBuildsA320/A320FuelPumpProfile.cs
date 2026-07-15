namespace Msfs2024Ai.Copilot.AircraftAdapters.IniBuildsA320;

/// <summary>
/// Live-verified iniBuilds A320neo V2 fuel-pump controls and physical
/// readbacks. Keep this profile independent from the A321, A330 and FBW
/// implementations so work on those aircraft cannot change A320 behavior.
/// </summary>
internal static class A320FuelPumpProfile
{
    public static IReadOnlyList<A320FuelPumpMapping> Pumps { get; } =
        new[]
        {
            new A320FuelPumpMapping(
                "L1",
                "INI_OUTER_TANK_LEFT",
                "__FUEL_ENG1_L1IsPressed",
                "INI_OUTER_TANK_LEFT_PUMP_ON"),
            new A320FuelPumpMapping(
                "L2",
                "INI_INNER_TANK_LEFT",
                "__FUEL_ENG1_L2IsPressed",
                "INI_INNER_TANK_LEFT_PUMP_ON"),
            new A320FuelPumpMapping(
                "C1",
                "INI_CENTER_TANK_LEFT",
                "__FUEL_CTR_1IsPressed",
                "INI_CENTER_TANK_LEFT_PUMP_ON"),
            new A320FuelPumpMapping(
                "C2",
                "INI_CENTER_TANK_RIGHT",
                "__FUEL_CTR_2IsPressed",
                "INI_CENTER_TANK_RIGHT_PUMP_ON"),
            new A320FuelPumpMapping(
                "R1",
                "INI_INNER_TANK_RIGHT",
                "__FUEL_ENG2_R1IsPressed",
                "INI_INNER_TANK_RIGHT_PUMP_ON"),
            new A320FuelPumpMapping(
                "R2",
                "INI_OUTER_TANK_RIGHT",
                "__FUEL_ENG2_R2IsPressed",
                "INI_OUTER_TANK_RIGHT_PUMP_ON")
        };

    public static string BuildToggleCommand(int pumpIndex)
    {
        var pump = Pumps[pumpIndex];
        return
            $"(L:{pump.SelectorLVar}) ! (>L:{pump.SelectorLVar}) " +
            $"(L:{pump.PressAnimationLVar}) ! (>L:{pump.PressAnimationLVar})";
    }

    public static bool IsOn(double value) => Math.Abs(value) >= 0.1;

    public static bool AreConfigured(IReadOnlyList<double> states) =>
        states.Count == Pumps.Count && states.All(IsOn);

    public static bool AreAllOff(IReadOnlyList<double> states) =>
        states.Count == Pumps.Count && states.All(state => !IsOn(state));
}

internal sealed class A320FuelPumpMapping
{
    public A320FuelPumpMapping(
        string name,
        string selectorLVar,
        string pressAnimationLVar,
        string readbackLVar)
    {
        Name = name;
        SelectorLVar = selectorLVar;
        PressAnimationLVar = pressAnimationLVar;
        ReadbackLVar = readbackLVar;
    }

    public string Name { get; }
    public string SelectorLVar { get; }
    public string PressAnimationLVar { get; }
    public string ReadbackLVar { get; }
}
