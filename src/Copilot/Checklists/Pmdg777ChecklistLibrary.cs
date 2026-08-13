namespace Msfs2024Ai.Copilot.Checklists;

internal static class Pmdg777ChecklistLibrary
{
    public static IReadOnlyList<ChecklistDefinition> GateToGate { get; } =
        new[]
        {
            Checklist("power-up-initial-setup", "777 Power Up & Preliminary Preflight Verification",
                Item("777X SDK", "CONNECTED", state => state.Pmdg777SdkDataReady),
                Item("Battery", "ON", state => state.Pmdg777BatteryOn),
                Item("Hydraulic starting state", "SAFE", state => state.Pmdg777HydraulicPanelSafe),
                Item("Wipers", "OFF", state => state.Pmdg777WipersOff),
                Item("Landing gear", "DOWN", state => state.Pmdg777GearLeverDown),
                Item("Alternate flaps", "OFF", state => state.Pmdg777AlternateFlapsOff),
                Item("External power", "ON", state => state.Pmdg777ExternalPowerOn),
                Item("Parking brake", "SET", state => state.ParkingBrakeSet),
                Item("Navigation light", "ON", state => state.Pmdg777NavigationLightOn),
                Item("Packs / recirculation", "OFF FOR GROUND AIR", state => state.Pmdg777GroundAirConfigurationSet),
                Item("ADIRU", "ON", state => state.Pmdg777AdiruOn),
                Item("Emergency lights", "ARMED", state => state.Pmdg777EmergencyLightsArmed)),
            Checklist("flight-computer-preflight", "777 Preflight Verification",
                Unknown("External inspection", "COMPLETE"),
                Unknown("UFT and CDU", "COMPLETE"),
                Unknown("Route and performance", "VERIFIED"),
                Unknown("Overhead / instruments / console", "CHECKED"),
                Unknown("Preflight checklist", "COMPLETE"),
                Unknown("Takeoff data", "SET")),
            Checklist("apu-start-pushback", "777 Before Start & Pushback Verification",
                Unknown("Doors and cargo", "READY"),
                Unknown("Fuel and load", "VERIFIED"),
                Unknown("APU", "AVAILABLE"),
                Unknown("Ground services", "DISCONNECTED"),
                Unknown("Before Start checklist", "COMPLETE"),
                Unknown("Pushback/start clearance", "RECEIVED"),
                Unknown("Beacon", "ON")),
            Checklist("engine-start-sequence", "777 Engine Start Verification",
                Unknown("Start configuration", "SET"),
                Unknown("Engine 2", "STABLE"),
                Unknown("Engine 1", "STABLE"),
                Item("Engines", "RUNNING", state => state.Engine1Running && state.Engine2Running),
                Unknown("EICAS", "NORMAL")),
            Checklist("after-start-taxi", "777 Before Taxi Verification",
                Unknown("Generators and pneumatics", "SET"),
                Unknown("Hydraulics", "SET"),
                Unknown("Flight controls", "CHECKED"),
                Unknown("Flaps and trim", "TAKEOFF SET"),
                Unknown("Before Taxi checklist", "COMPLETE"),
                Unknown("Taxi clearance", "RECEIVED")),
            Checklist("before-takeoff", "777 Before Takeoff Verification",
                Unknown("Takeoff briefing", "COMPLETE"),
                Unknown("Cabin", "READY"),
                Unknown("Takeoff configuration", "VERIFIED"),
                Unknown("Transponder / TCAS", "SET"),
                Unknown("Exterior lights", "SET"),
                Unknown("Before Takeoff checklist", "COMPLETE")),
            Checklist("takeoff-climb", "777 After Takeoff Verification",
                Unknown("Takeoff thrust", "SET"),
                Item("Aircraft", "AIRBORNE", state => !state.OnGround),
                Unknown("Landing gear", "UP"),
                Unknown("Flaps", "UP"),
                Unknown("After Takeoff checklist", "COMPLETE")),
            Checklist("cruise", "777 Cruise Verification",
                Item("Aircraft", "AIRBORNE", state => !state.OnGround),
                Unknown("Cruise altitude", "ESTABLISHED"),
                Unknown("Fuel and engines", "MONITORED"),
                Unknown("Route and weather", "REVIEWED")),
            Checklist("descent-preparation", "777 Descent Verification",
                Unknown("Arrival and approach", "ENTERED"),
                Unknown("Landing performance", "COMPLETE"),
                Unknown("Minimums and references", "SET"),
                Unknown("Approach briefing", "COMPLETE"),
                Unknown("Descent checklist", "COMPLETE")),
            Checklist("approach-landing", "777 Approach & Landing Verification",
                Unknown("Approach checklist", "COMPLETE"),
                Unknown("Landing gear", "DOWN"),
                Unknown("Landing flaps", "SET"),
                Unknown("Speedbrake / autobrake", "SET"),
                Unknown("Landing checklist", "COMPLETE"),
                Unknown("Stable approach", "CONFIRMED")),
            Checklist("after-landing-taxi", "777 After Landing Verification",
                Item("Aircraft", "ON GROUND", state => state.OnGround),
                Unknown("Exterior lights", "TAXI"),
                Unknown("Flaps / speedbrake", "UP / DOWN"),
                Unknown("Radar / transponder", "SET"),
                Unknown("APU", "AS REQUIRED")),
            Checklist("parking-shutdown", "777 Shutdown & Secure Verification",
                Item("Aircraft", "PARKED", state => state.OnGround && state.GroundSpeedKnots <= 0.5),
                Unknown("Parking brake / chocks", "SET / IN"),
                Unknown("Electrical power", "ESTABLISHED"),
                Item("Engines", "OFF", state => state.EnginesOff),
                Unknown("Beacon", "OFF"),
                Unknown("Shutdown checklist", "COMPLETE"),
                Unknown("Secure checklist", "AS REQUIRED"))
        };

    public static ChecklistDefinition? FindForProcedure(string procedureId) =>
        GateToGate.FirstOrDefault(checklist =>
            string.Equals(checklist.ProcedureId, procedureId, StringComparison.OrdinalIgnoreCase));

    private static ChecklistDefinition Checklist(
        string procedureId,
        string name,
        params ChecklistItem[] items) =>
        new(procedureId, name, items);

    private static ChecklistItem Item(
        string challenge,
        string response,
        Func<AircraftState, bool> verify) =>
        new(challenge, response, state => verify(state));

    private static ChecklistItem Unknown(string challenge, string response) =>
        new(challenge, response, _ => null);
}
