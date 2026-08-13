namespace Msfs2024Ai.Copilot.Checklists;

internal static class Pmdg777ChecklistLibrary
{
    public static IReadOnlyList<ChecklistDefinition> GateToGate { get; } =
        new[]
        {
            new ChecklistDefinition(
                "power-up-initial-setup",
                "777 Power Up & Preliminary Preflight Verification",
                new[]
                {
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
                    Item("Emergency lights", "ARMED", state => state.Pmdg777EmergencyLightsArmed)
                })
        };

    public static ChecklistDefinition? FindForProcedure(string procedureId) =>
        GateToGate.FirstOrDefault(checklist =>
            string.Equals(checklist.ProcedureId, procedureId, StringComparison.OrdinalIgnoreCase));

    private static ChecklistItem Item(
        string challenge,
        string response,
        Func<AircraftState, bool> verify) =>
        new(challenge, response, state => verify(state));
}
