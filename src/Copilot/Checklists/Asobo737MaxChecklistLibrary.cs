namespace Msfs2024Ai.Copilot.Checklists;

internal static class Asobo737MaxChecklistLibrary
{
    public static IReadOnlyList<ChecklistDefinition> GateToGate { get; } =
        new[]
        {
            Checklist("power-up-initial-setup", "737 MAX Power Up Verification",
                new ChecklistItem("Battery", "ON", state => state.Battery1On),
                new ChecklistItem("Ground power", "ON", state => state.ExternalPowerOn),
                Unknown("Fire tests", "COMPLETE"),
                Unknown("IRS", "NAV"),
                Unknown("Logo / position", "SET"),
                Unknown("Emergency lights", "ARMED")),
            Checklist("flight-computer-preflight", "737 MAX FMC & Pre-Flight Verification",
                new ChecklistItem("Parking brake", "ON", state => state.ParkingBrakeSet),
                Unknown("Fuel pumps", "ON AS REQUIRED"),
                Unknown("FMC TAKEOFF REF", "COMPLETE"),
                Unknown("IFR clearance", "RECEIVED"),
                Unknown("Signs", "SET")),
            Checklist("apu-start-pushback", "737 MAX APU & Pushback Verification",
                new ChecklistItem("APU", "AVAILABLE", state => state.ApuAvailable),
                new ChecklistItem("APU bleed", "ON", state => state.ApuBleedOn),
                Unknown("Ground power", "OFF"),
                new ChecklistItem("Anti-collision", "ON", state => state.BeaconOn),
                Unknown("Pushback/start clearance", "RECEIVED"),
                new ChecklistItem("Doors", "CLOSED", state => state.RequiredDoorsClosed)),
            Checklist("engine-start-sequence", "737 MAX Engine Start Verification",
                new ChecklistItem("Engine 2", "STABLE", state => state.Engine2StartStabilized),
                new ChecklistItem("Engine 1", "STABLE", state => state.Engine1StartStabilized),
                Unknown("Start switches", "SET")),
            Checklist("after-start-taxi", "737 MAX After Start & Taxi Verification",
                Unknown("Hydraulic pumps", "ON"),
                Unknown("APU bleed", "OFF"),
                Unknown("Speedbrake", "DOWN"),
                Unknown("Flaps", "TAKEOFF SET"),
                Unknown("Autobrake", "RTO"),
                Unknown("Taxi light", "ON")),
            Checklist("before-takeoff", "737 MAX Before Takeoff Verification",
                Unknown("Takeoff briefing", "COMPLETE"),
                Unknown("Cabin", "READY"),
                Unknown("Landing lights", "ON"),
                Unknown("Transponder", "TA/RA")),
            Checklist("takeoff-climb", "737 MAX Takeoff & Climb Verification",
                new ChecklistItem("Landing gear", "UP", state => state.GearHandleUp),
                new ChecklistItem("Flaps", "UP", state => state.FlapsHandleIndex <= 0),
                Unknown("Climb thrust", "SET")),
            Checklist("cruise", "737 MAX Cruise Verification",
                new ChecklistItem("Cruise", "ESTABLISHED", state => state.CruiseEstablished)),
            Checklist("descent-preparation", "737 MAX Descent Preparation Verification",
                Unknown("Arrival and approach", "ENTERED"),
                Unknown("Landing data", "SET"),
                Unknown("Briefing", "COMPLETE")),
            Checklist("approach-landing", "737 MAX Approach & Landing Verification",
                Unknown("Autobrake", "SET"),
                new ChecklistItem("Gear", "DOWN", state => state.GearHandleDown),
                Unknown("Flaps", "LANDING SET"),
                Unknown("Speedbrake", "ARMED"),
                Unknown("Landing checklist", "COMPLETE")),
            Checklist("after-landing-taxi", "737 MAX After Landing & Taxi Verification",
                Unknown("Taxi light", "ON"),
                new ChecklistItem("Flaps", "UP", state => state.FlapsHandleIndex <= 0),
                Unknown("Speedbrake", "DOWN"),
                new ChecklistItem("APU", "AVAILABLE", state => state.ApuAvailable || state.ApuSpoolingOrAvailable)),
            Checklist("parking-shutdown", "737 MAX Parking & Shutdown Verification",
                new ChecklistItem("Parking brake", "ON", state => state.ParkingBrakeSet),
                new ChecklistItem("Engines", "OFF", state => state.EnginesOff),
                Unknown("Fuel pumps", "OFF"),
                Unknown("Anti-collision", "OFF"))
        };

    public static ChecklistDefinition? FindForProcedure(string procedureId) =>
        GateToGate.FirstOrDefault(
            checklist => string.Equals(checklist.ProcedureId, procedureId, StringComparison.OrdinalIgnoreCase));

    private static ChecklistDefinition Checklist(string procedureId, string name, params ChecklistItem[] items) =>
        new(procedureId, name, items);

    private static ChecklistItem Unknown(string challenge, string response) =>
        new(challenge, response, _ => null);
}
