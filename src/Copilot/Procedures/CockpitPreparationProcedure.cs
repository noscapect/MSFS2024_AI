namespace Msfs2024Ai.Copilot.Procedures;

internal static class CockpitPreparationProcedure
{
    public static IReadOnlyList<ProcedureStepResult> Evaluate(AircraftState state)
    {
        return new[]
        {
            new ProcedureStepResult("aircraft", "A320neo V2 loaded", state.IsA320NeoV2),
            new ProcedureStepResult("stationary", "Aircraft stationary on ground", state.OnGround && state.GroundSpeedKnots <= 0.5),
            new ProcedureStepResult("parking-brake", "Parking brake set", state.ParkingBrakeSet),
            new ProcedureStepResult("engines", "Engines off", state.EnginesOff),
            new ProcedureStepResult("battery-1", "BAT 1 on", state.Battery1On, "MobiFlight iniBuilds adapter"),
            new ProcedureStepResult("battery-2", "BAT 2 on", state.Battery2On, "manual adapter pending"),
            new ProcedureStepResult("external-available", "External power available", state.ExternalPowerAvailable),
            new ProcedureStepResult("external-connected", "External power connected", state.ExternalPowerOn, "external-power on")
        };
    }
}
