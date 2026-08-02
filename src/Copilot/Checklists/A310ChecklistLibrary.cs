namespace Msfs2024Ai.Copilot.Checklists;

internal static class A310ChecklistLibrary
{
    public static IReadOnlyList<ChecklistDefinition> GateToGate { get; } =
        new[]
        {
            Checklist("power-up-initial-setup", "A310 Preliminary Cockpit Preparation",
                Known("Aircraft", "A310-300", state => state.IsIniBuildsA310),
                Known("Engines", "OFF", state => state.EnginesOff),
                Unknown("Batteries", "AUTO / OFF lights extinguished"),
                Unknown("Hydraulic electric pumps", "OFF"),
                Unknown("Gear / slats-flaps / reversers", "CHECKED"),
                Unknown("APU fire test", "COMPLETE"),
                Unknown("IRS selectors", "NAV"),
                Unknown("Crew oxygen", "ON"),
                Unknown("Initial exterior lights", "SET")),
            Checklist("flight-computer-preflight", "A310 Before Start to the Line",
                Unknown("Cockpit preparation", "COMPLETE"),
                Unknown("Yaw dampers", "ON"),
                Unknown("Signs", "ON / AUTO"),
                Unknown("Fuel quantity and balance", "CHECKED"),
                Unknown("Navigation and FMS", "SET"),
                Unknown("Landing elevation", "SET"),
                Unknown("Altimeters", "SET AND CROSS-CHECKED"),
                Unknown("Takeoff warning", "CHECKED"),
                Unknown("Brakes / anti-skid", "ON")),
            Checklist("apu-start-pushback", "A310 Before Start Below the Line",
                Known("APU", "AVAILABLE", state => state.ApuAvailable),
                Unknown("Windows / doors / slides", "CLOSED / ARMED"),
                Known("Beacon", "ON", state => state.BeaconOn),
                Known("Parking brake", "ON", state => state.ParkingBrakeSet),
                Unknown("Elapsed time", "STARTED"),
                Unknown("Transponder", "XPDR"),
                Known("External power", "DISCONNECTED", state => !state.ExternalPowerOn)),
            Checklist("engine-start-sequence", "A310 Engine Start",
                Unknown("Area", "CLEAR"),
                Unknown("Ignition", "A OR B"),
                Unknown("Pack valves", "CLOSED FOR START"),
                Known("Engine 2", "STABLE", state => state.Engine2Running),
                Known("Engine 1", "STABLE", state => state.Engine1Running)),
            Checklist("after-start-taxi", "A310 After Start",
                Unknown("Ignition", "AS REQUIRED"),
                Unknown("APU bleed / master", "AS REQUIRED"),
                Unknown("Anti-ice", "AS REQUIRED"),
                Known("Speedbrake", "ARMED", state => state.GroundSpoilersArmed),
                Unknown("Rudder trim", "ZERO"),
                Known("Slats / flaps", "TAKEOFF SET", state => state.FlapsHandleIndex > 0),
                Unknown("Pitch trim", "SET FROM ECAM CG"),
                Unknown("Flight controls", "CHECKED")),
            Checklist("before-takeoff", "A310 Before Takeoff",
                Unknown("Runway", "VERIFIED"),
                Unknown("Brake fans / temperatures", "OFF / CHECKED"),
                Unknown("Exterior lights", "TAKEOFF SET"),
                Unknown("Ignition", "AS REQUIRED"),
                Unknown("Packs", "AS REQUIRED"),
                Unknown("TCAS", "TA/RA"),
                Known("Engines", "STABLE", state => state.Engine1Running && state.Engine2Running)),
            Checklist("takeoff-climb", "A310 After Takeoff",
                Known("Landing gear", "UP", state => !state.GearHandleDown),
                Known("Slats / flaps", "0 / 0", state => state.FlapsHandleIndex <= 0),
                Known("Spoilers", "DISARMED", state => !state.GroundSpoilersArmed),
                Unknown("Landing-gear lever", "OFF"),
                Unknown("Packs", "ON"),
                Unknown("APU", "OFF"),
                Unknown("Lights", "SET"),
                Unknown("Altimeters", "STANDARD")),
            Checklist("cruise", "A310 Cruise",
                Known("Cruise", "ESTABLISHED", state => state.CruiseEstablished),
                Unknown("TRP", "CR"),
                Unknown("ECAM memo / status", "REVIEWED"),
                Unknown("Fuel and flight progress", "CHECKED")),
            Checklist("descent-preparation", "A310 Descent Preparation",
                Unknown("Weather and runway", "OBTAINED"),
                Unknown("ECAM status", "REVIEWED"),
                Unknown("Landing elevation", "SET"),
                Unknown("Fuel", "CHECKED"),
                Unknown("FMS arrival / approach", "SET"),
                Unknown("DH and autobrake", "SET"),
                Unknown("Approach briefing", "COMPLETE")),
            Checklist("approach-landing", "A310 Landing",
                Unknown("Altimeters", "QNH SET"),
                Unknown("Signs and lights", "SET"),
                Known("Landing gear", "DOWN / THREE GREEN", state => state.GearHandleDown),
                Known("Slats / flaps", "LANDING SET", state => state.FlapsHandleIndex >= 4),
                Known("Ground spoilers", "ARMED", state => state.GroundSpoilersArmed),
                Unknown("Approach", "STABLE")),
            Checklist("after-landing-taxi", "A310 After Landing",
                Unknown("Exterior lights", "SET"),
                Unknown("Anti-ice / ignition", "AS REQUIRED / OFF"),
                Known("APU", "AVAILABLE", state => state.ApuAvailable),
                Known("Ground spoilers", "DISARMED", state => !state.GroundSpoilersArmed),
                Unknown("Transponder / radar", "SET / OFF"),
                Unknown("Pitch trim", "1 DEGREE NOSE UP"),
                Known("Slats / flaps", "0 / 0", state => state.FlapsHandleIndex <= 0),
                Unknown("Brake temperature", "CHECKED")),
            Checklist("parking-shutdown", "A310 Parking & Securing",
                Known("Parking brake", "ON", state => state.ParkingBrakeSet),
                Known("Electrical power", "ESTABLISHED", state => state.ExternalPowerOn || state.ApuAvailable),
                Known("Engines", "OFF", state => state.EnginesOff),
                Known("Beacon", "OFF", state => !state.BeaconOn),
                Unknown("Cabin differential pressure", "ZERO"),
                Unknown("Fuel pumps", "PARKING CONFIGURATION"),
                Unknown("Probe heat", "OFF"),
                Unknown("IRS / oxygen / lights / CRTs", "OFF"),
                Unknown("Emergency exit lights", "DISARMED"),
                Unknown("Batteries", "OFF"))
        };

    public static ChecklistDefinition? FindForProcedure(string procedureId) =>
        GateToGate.FirstOrDefault(item =>
            string.Equals(item.ProcedureId, procedureId, StringComparison.OrdinalIgnoreCase));

    private static ChecklistDefinition Checklist(
        string procedureId,
        string name,
        params ChecklistItem[] items) =>
        new(procedureId, name, items);

    private static ChecklistItem Known(
        string challenge,
        string response,
        Func<AircraftState, bool> complete) =>
        new(challenge, response, state => complete(state));

    private static ChecklistItem Unknown(string challenge, string response) =>
        new(challenge, response, _ => null);
}
