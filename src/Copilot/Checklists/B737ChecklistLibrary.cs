namespace Msfs2024Ai.Copilot.Checklists;

internal static class B737ChecklistLibrary
{
    public static IReadOnlyList<ChecklistDefinition> GateToGate { get; } =
        new[]
        {
            Checklist("power-up-initial-setup", "737 Power Up Verification",
                new ChecklistItem("Battery", "ON", state => state.Battery1On),
                new ChecklistItem("Ground power", "ON", state => state.ExternalPowerOn),
                new ChecklistItem("IRS", "NAV", state => Math.Abs(state.Adirs1SelectorState - 2) < 0.1 && Math.Abs(state.Adirs2SelectorState - 2) < 0.1),
                new ChecklistItem("Logo / position", "SET", state => state.LogoLightsOn && state.NavigationLightsOn),
                new ChecklistItem("Emergency lights", "ARMED", state => state.EmergencyExitSelectorPosition.HasValue ? Math.Abs(state.EmergencyExitSelectorPosition.Value - 1) < 0.1 : null)),
            Checklist("flight-computer-preflight", "737 FMC & Pre-Flight Verification",
                new ChecklistItem("Parking brake", "ON", state => state.ParkingBrakeSet),
                new ChecklistItem("Fuel pumps", "ON AS REQUIRED", state => state.FuelPumpsConfigured),
                Unknown("FMC", "COMPLETE"),
                new ChecklistItem("Signs", "AUTO", state => state.SeatbeltSelectorPosition.HasValue && Math.Abs(state.SeatbeltSelectorPosition.Value - 1) < 0.1)),
            Checklist("apu-start-pushback", "737 APU & Pushback Verification",
                new ChecklistItem("APU", "AVAILABLE", state => state.ApuAvailable),
                new ChecklistItem("APU bleed", "ON", state => state.ApuBleedOn),
                new ChecklistItem("Ground power", "OFF", state => state.ApuGeneratorPowerEstablished && !state.ExternalPowerOn),
                new ChecklistItem("Anti-collision", "ON", state => state.BeaconOn),
                Unknown("Clearance", "RECEIVED"),
                new ChecklistItem("Doors", "CLOSED", state => state.RequiredDoorsClosed)),
            Checklist("engine-start-sequence", "737 Engine Start Verification",
                new ChecklistItem("Engine 2", "STABLE", state => state.Engine2StartStabilized),
                new ChecklistItem("Engine 1", "STABLE", state => state.Engine1StartStabilized),
                Unknown("Start switches", "SET")),
            Checklist("after-start-taxi", "737 After Start & Taxi Verification",
                new ChecklistItem("APU bleed", "OFF", state => !state.ApuBleedOn),
                new ChecklistItem("Speedbrake", "DOWN", state => !state.GroundSpoilersArmed),
                new ChecklistItem("Flaps", "TAKEOFF SET", state => state.FlapsHandleIndex > 0),
                new ChecklistItem("Autobrake", "RTO", state => state.AutobrakeLevel.HasValue ? Math.Abs(state.AutobrakeLevel.Value) < 0.1 : null),
                new ChecklistItem("Taxi light", "ON", state => state.NoseLightSelectorPosition.HasValue ? state.NoseLightSelectorPosition.Value < 1.5 : null)),
            Checklist("before-takeoff", "737 Before Takeoff Verification",
                Unknown("Takeoff briefing", "COMPLETE"),
                Unknown("Cabin", "READY"),
                new ChecklistItem("Landing lights", "ON", state => state.LeftLandingLightSelectorPosition == 2 && state.RightLandingLightSelectorPosition == 2),
                new ChecklistItem("Transponder", "TA/RA", state => state.TcasMode.HasValue ? state.TcasMode.Value >= 4 : null)),
            Checklist("takeoff-climb", "737 Takeoff & Climb Verification",
                new ChecklistItem("Landing gear", "UP", state => state.GearHandleUp),
                new ChecklistItem("Flaps", "UP", state => state.FlapsHandleIndex <= 0),
                Unknown("Climb thrust", "SET")),
            Checklist("cruise", "737 Cruise Verification",
                new ChecklistItem("Cruise", "ESTABLISHED", state => !state.OnGround && state.AltitudeAboveGroundFeet >= 10000 && Math.Abs(state.VerticalSpeedFeetPerMinute) < 300)),
            Checklist("descent-preparation", "737 Descent Preparation Verification",
                Unknown("Arrival and approach", "ENTERED"),
                Unknown("Landing data", "SET"),
                Unknown("Briefing", "COMPLETE")),
            Checklist("approach-landing", "737 Approach & Landing Verification",
                new ChecklistItem("Autobrake", "SET", state => state.AutobrakeLevel.HasValue && state.AutobrakeLevel.Value >= 2),
                new ChecklistItem("Gear", "DOWN", state => state.GearHandleDown),
                new ChecklistItem("Flaps", "LANDING SET", state => state.BoeingLandingFlapsSet),
                new ChecklistItem("Speedbrake", "ARMED", state => state.GroundSpoilersArmed),
                Unknown("Landing checklist", "COMPLETE")),
            Checklist("after-landing-taxi", "737 After Landing & Taxi Verification",
                new ChecklistItem("Flaps", "UP", state => state.FlapsHandleIndex <= 0),
                new ChecklistItem("Speedbrake", "DOWN", state => !state.GroundSpoilersArmed),
                new ChecklistItem("APU bleed", "ON", state => state.ApuBleedOn),
                new ChecklistItem("Runway turnoff lights", "ON", state => state.RunwayTurnoffLightsOn)),
            Checklist("parking-shutdown", "737 Parking & Shutdown Verification",
                new ChecklistItem("Parking brake", "ON", state => state.ParkingBrakeSet),
                new ChecklistItem("Engines", "OFF", state => state.EnginesOff),
                new ChecklistItem("Runway turnoff lights", "OFF", state => !state.RunwayTurnoffLightsOn),
                new ChecklistItem("Fuel pumps", "OFF", state => state.AllFuelPumpsOff),
                new ChecklistItem("Anti-collision", "OFF", state => !state.BeaconOn))
        };

    public static ChecklistDefinition? FindForProcedure(string procedureId) =>
        GateToGate.FirstOrDefault(
            checklist => string.Equals(checklist.ProcedureId, procedureId, StringComparison.OrdinalIgnoreCase));

    private static ChecklistDefinition Checklist(string procedureId, string name, params ChecklistItem[] items) =>
        new(procedureId, name, items);

    private static ChecklistItem Unknown(string challenge, string response) =>
        new(challenge, response, _ => null);
}
