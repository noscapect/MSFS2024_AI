using Msfs2024Ai.Copilot.Domain;

namespace Msfs2024Ai.Copilot.Procedures;

/// <summary>
/// PMDG 777-300ER procedures. Only Flow 1 is published during the read-only
/// SDK validation stage. No 777 automatic command is permitted yet.
/// </summary>
internal static class Pmdg777ProcedureLibrary
{
    public static IReadOnlyList<ProcedureDefinition> GateToGate =>
        new[] { PowerUpAndPreliminaryPreflight };

    public static ProcedureDefinition? Find(string id) =>
        GateToGate.FirstOrDefault(procedure =>
            string.Equals(procedure.Id, id, StringComparison.OrdinalIgnoreCase));

    private static ProcedureStep Observe(
        string id,
        string label,
        Func<AircraftState, bool> complete) =>
        new(id, label, ProcedureStepKind.Observe, complete, CrewRole.FirstOfficer);

    private static ProcedureStep Manual(
        string id,
        string label,
        string instruction,
        Func<AircraftState, bool>? complete = null) =>
        new(
            id,
            label,
            ProcedureStepKind.ManualAction,
            complete ?? (_ => false),
            CrewRole.FirstOfficer,
            manualInstruction: instruction);

    public static ProcedureDefinition PowerUpAndPreliminaryPreflight { get; } =
        new(
            "power-up-initial-setup",
            "1. 777 Power Up & Preliminary Preflight",
            new[]
            {
                Observe(
                    "sdk-data-ready",
                    "PMDG 777X SDK data received",
                    state => state.Pmdg777SdkDataReady),
                Observe(
                    "battery-on",
                    "Battery switch ON",
                    state => state.Pmdg777BatteryOn),
                Observe(
                    "hydraulic-starting-state",
                    "C1/C2 primary and demand pumps OFF",
                    state => state.Pmdg777HydraulicPanelSafe),
                Observe(
                    "wipers-off",
                    "Windshield wipers OFF",
                    state => state.Pmdg777WipersOff),
                Observe(
                    "gear-down",
                    "Landing gear lever DOWN",
                    state => state.Pmdg777GearLeverDown),
                Observe(
                    "alternate-flaps-off",
                    "Alternate flaps OFF",
                    state => state.Pmdg777AlternateFlapsOff),
                Observe(
                    "external-power-available",
                    "Primary and secondary external power AVAILABLE",
                    state => state.Pmdg777ExternalPowerAvailable),
                Observe(
                    "external-power-on",
                    "Primary and secondary external power ON",
                    state => state.Pmdg777ExternalPowerOn),
                Observe(
                    "parking-brake-set",
                    "Parking brake SET",
                    state => state.ParkingBrakeSet),
                Observe(
                    "nav-light-on",
                    "Navigation light ON",
                    state => state.Pmdg777NavigationLightOn),
                Manual(
                    "logo-light-as-required",
                    "Logo light as required",
                    "First Officer: set the logo light ON at night, otherwise as required, then press Confirm now."),
                Observe(
                    "packs-recirculation-off",
                    "Packs and recirculation fans OFF for ground air",
                    state => state.Pmdg777GroundAirConfigurationSet),
                Manual(
                    "ground-air-connected",
                    "Ground air requested and connected",
                    "First Officer: request the air-conditioning unit through Ground Operations, wait for connection, then press Confirm now."),
                Manual(
                    "adiru-cycle",
                    "ADIRU OFF for 30 seconds, then ON",
                    "First Officer: if required for first-flight initialization, hold ADIRU OFF for 30 seconds, select ON, verify the switch remains ON, then press Confirm now.",
                    state => state.Pmdg777AdiruOn),
                Observe(
                    "emergency-lights-armed",
                    "Emergency exit lights ARMED; guard closed",
                    state => state.Pmdg777EmergencyLightsArmed),
                Manual(
                    "eicas-status-scan",
                    "EICAS, oil, hydraulics, oxygen and status checked",
                    "First Officer: verify and clear expected EICAS alerts; check oil quantity, hydraulic quantity, crew oxygen, status messages, checklist resets and COM manager, then press Confirm now."),
                Manual(
                    "documents-equipment-check",
                    "Documents, emergency equipment and circuit breakers checked",
                    "First Officer: complete the first-flight document, PA, emergency-equipment, overhead-guard and circuit-breaker checks, then press Confirm now.")
            });
}
