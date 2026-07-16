namespace Msfs2024Ai.Copilot.Checklists;

internal static class FbwA320ChecklistLibrary
{
    public static IReadOnlyList<ChecklistDefinition> GateToGate { get; } =
        new[]
        {
            Checklist("power-up-initial-setup", "Power Up & Initial Setup Verification",
                new ChecklistItem("Batteries", "ON", state => state.Battery1On && state.Battery2On),
                new ChecklistItem("External power", "ON", state => state.ExternalPowerOn),
                new ChecklistItem("NAV & LOGO", "ON", state => state.NavLogoSelectorPosition.HasValue ? Math.Abs(state.NavLogoSelectorPosition.Value) < 0.1 : null),
                new ChecklistItem("ADIRS", "NAV", state => state.AllAdirsNav),
                new ChecklistItem("Crew oxygen", "ON", state => state.CrewOxygenOn),
                new ChecklistItem(
                    "Strobe",
                    "AUTO",
                    state => state.StrobeSelectorPosition.HasValue
                        ? Math.Abs(state.StrobeSelectorPosition.Value - 1) < 0.1
                        : null),
                new ChecklistItem(
                    "Fire tests",
                    "COMPLETE",
                    state => state.ApuFireTestCompleted
                             && state.Engine1FireTestCompleted
                             && state.Engine2FireTestCompleted)),
            Checklist("flight-computer-preflight", "Flight Computer & Pre-Flight Verification",
                new ChecklistItem("Parking brake", "ON", state => state.ParkingBrakeSet),
                new ChecklistItem("Fuel pumps", "ON", state => state.FuelPumpsConfigured),
                Unknown("MCDU setup", "COMPLETE"),
                Unknown("IFR clearance", "RECEIVED"),
                new ChecklistItem("Signs", "SEATBELTS ON / NO SMOKING AUTO", state => state.SeatbeltSignsOn && state.NoSmokingSelectorPosition.HasValue && Math.Abs(state.NoSmokingSelectorPosition.Value - 1) < 0.1),
                new ChecklistItem("Emergency lights", "ARMED", state => state.EmergencyExitSelectorPosition.HasValue && Math.Abs(state.EmergencyExitSelectorPosition.Value - 1) < 0.1)),
            Checklist("apu-start-pushback", "APU Start & Pushback Verification",
                new ChecklistItem("APU", "AVAILABLE", state => state.ApuAvailable),
                new ChecklistItem("APU bleed", "ON", state => state.ApuBleedOn),
                new ChecklistItem("External power", "OFF", state => !state.ExternalPowerOn),
                new ChecklistItem("Beacon", "ON", state => state.BeaconOn),
                Unknown("Pushback/start clearance", "RECEIVED"),
                new ChecklistItem(
                    "Transponder",
                    "AUTO",
                    state => state.TransponderModeSelectorPosition.HasValue
                        ? Math.Abs(state.TransponderModeSelectorPosition.Value - 1) < 0.1
                        : null),
                new ChecklistItem("Cabin/cargo doors", "CLOSED", state => state.RequiredDoorsClosed)),
            Checklist("engine-start-sequence", "Engine Start Verification",
                new ChecklistItem("Engine 2", "STABLE", state => state.Engine2Running),
                new ChecklistItem("Engine 1", "STABLE", state => state.Engine1Running),
                Unknown("Engine mode selector", "NORM")),
            Checklist("after-start-taxi", "After Start & Taxi Verification",
                new ChecklistItem("APU bleed", "OFF", state => !state.ApuBleedOn),
                new ChecklistItem("APU master", "OFF", state => !state.ApuMasterSwitchOn),
                new ChecklistItem("Flaps", "TAKEOFF SET", state => state.FlapsHandleIndex > 0),
                new ChecklistItem("Ground spoilers", "ARMED", state => state.GroundSpoilersArmed),
                new ChecklistItem(
                    "Auto-brake",
                    "MAX",
                    state => state.AutobrakeLevel.HasValue
                        ? Math.Abs(state.AutobrakeLevel.Value - 3) < 0.1
                        : null),
                Unknown("ECAM", "CHECKED")),
            Checklist("before-takeoff", "Before Takeoff Verification",
                Unknown("Takeoff briefing", "COMPLETE"), Unknown("Cabin", "READY"),
                Unknown("TCAS", "TA/RA"), Unknown("Anti-ice", "AS REQUIRED"), Unknown("Exterior lights", "SET")),
            Checklist("takeoff-climb", "Takeoff & Climb Verification",
                new ChecklistItem("Landing gear", "UP", state => !state.GearHandleDown),
                new ChecklistItem("Flaps", "RETRACTED", state => state.FlapsHandleIndex <= 0),
                Unknown("Thrust", "CL")),
            Checklist("cruise", "Cruise Verification",
                new ChecklistItem("Cruise", "ESTABLISHED", state => state.CruiseEstablished),
                new ChecklistItem(
                    "Seatbelt signs",
                    "AS REQUIRED",
                    state => true)),
            Checklist("descent-preparation", "Descent Preparation Verification",
                Unknown("Arrival and approach", "ENTERED"),
                Unknown("PERF APPR data", "ENTERED"),
                Unknown("Descent setup", "REVIEWED")),
            Checklist("approach-landing", "Approach & Landing Verification",
                new ChecklistItem(
                    "Landing auto-brake",
                    "LOW",
                    state => state.AutobrakeLevel.HasValue
                        ? Math.Abs(state.AutobrakeLevel.Value - 1) < 0.1
                        : null),
                new ChecklistItem("Landing gear", "DOWN", state => state.GearHandleDown),
                new ChecklistItem("Flaps", "LANDING SET", state => state.FlapsHandleIndex > 0),
                Unknown("Destination QNH", "SET"), Unknown("Ground spoilers", "ARMED"), Unknown("Signs and lights", "SET")),
            Checklist("after-landing-taxi", "After Landing & Taxi Verification",
                new ChecklistItem("Flaps", "ZERO", state => state.FlapsHandleIndex <= 0),
                new ChecklistItem("APU", "AVAILABLE", state => state.ApuAvailable),
                Unknown("Ground spoilers", "DISARMED"), Unknown("Radar and TCAS", "SET"), Unknown("Exterior lights", "SET")),
            Checklist("parking-shutdown", "Parking & Shutdown Verification",
                new ChecklistItem("Parking brake", "ON", state => state.ParkingBrakeSet),
                new ChecklistItem("Engines", "OFF", state => state.EnginesOff),
                new ChecklistItem("Fuel pumps", "OFF", state => state.AllFuelPumpsOff),
                new ChecklistItem("Beacon", "OFF", state => !state.BeaconOn),
                Unknown("Doors and slides", "SET"))
        };

    public static IReadOnlyList<ChecklistDefinition> ThroughCruise => GateToGate;

    public static ChecklistDefinition? FindForProcedure(string procedureId) =>
        GateToGate.FirstOrDefault(
            checklist => string.Equals(checklist.ProcedureId, procedureId, StringComparison.OrdinalIgnoreCase));

    private static ChecklistDefinition Checklist(string procedureId, string name, params ChecklistItem[] items) =>
        new(procedureId, name, items);

    private static ChecklistItem Unknown(string challenge, string response) =>
        new(challenge, response, _ => null);
}
