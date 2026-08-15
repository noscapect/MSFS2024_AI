namespace Msfs2024Ai.Copilot.Checklists;

internal static class Pmdg777ChecklistLibrary
{
    public static IReadOnlyList<ChecklistDefinition> GateToGate { get; } =
        new[]
        {
            Checklist("power-up-initial-setup", "777 Power Up & Preliminary Preflight Verification",
                Item("777X SDK", "CONNECTED", state => state.Pmdg777SdkDataReady),
                Item("Battery", "ON", state => state.Pmdg777SdkDataReady && state.Pmdg777BatteryOn),
                Item("Primary external power", "ON", state => state.Pmdg777PrimaryExternalPowerOn),
                Item("Secondary external power", "ON IF AVAILABLE", state =>
                    !state.Pmdg777SecondaryExternalPowerAvailable
                    || state.Pmdg777SecondaryExternalPowerOn),
                Item("Bus ties", "AUTO", state => state.Pmdg777BusTiesAuto),
                Item("C1/C2 primary and demand pumps", "OFF", state => state.Pmdg777HydraulicPanelSafe),
                Item("Wipers", "OFF", state => state.Pmdg777WipersOff),
                Item("Landing gear lever", "DOWN", state => state.Pmdg777GearLeverDown),
                Item("Alternate flaps", "OFF", state => state.Pmdg777AlternateFlapsOff),
                Item("ADIRU", "ON AFTER 30 SECONDS OFF", state => state.Pmdg777AdiruOn)),
            Checklist("flight-computer-preflight", "777 Preflight Verification",
                Item("Electrical power", "ESTABLISHED", state => state.Pmdg777SdkDataReady && state.Pmdg777BatteryOn && state.Pmdg777PrimaryExternalPowerOn),
                Unknown("Captain flight instruments", "CHECKED / QNH SET"),
                Item("Parking brake", "SET", state => state.ParkingBrakeSet),
                Unknown("UFT setup", "COMPLETE"),
                Unknown("CDU IDENT / POS INIT", "COMPLETE"),
                Item("CDU route", "COMPLETE", state => state.Pmdg777FmcRouteInitialized),
                Item("CDU performance", "COMPLETE", state => state.Pmdg777FmcPerformanceInputComplete),
                Item("CDU TAKEOFF REF", "COMPLETE", state =>
                    state.Pmdg777FmcTakeoffFlaps > 0
                    && state.Pmdg777FmcV1 > 0
                    && state.Pmdg777FmcVr > 0
                    && state.Pmdg777FmcV2 > 0),
                Item("IFR clearance", "RECEIVED", state => state.AtcClearedIfr),
                Item("IFE / passenger seats", "ON", state => state.Pmdg777IfePassengerSeatsOn),
                Item("Cabin utility", "ON", state => state.Pmdg777CabinUtilityOn),
                Item("Emergency lights selector", "ARMED", state => state.Pmdg777EmergencyLightsArmed),
                Item("Emergency lights guard", "CLOSED", state => state.Pmdg777EmergencyLightsGuardClosed),
                Item("Navigation light", "ON", state => state.Pmdg777NavigationLightOn),
                Item("FO overhead electrical / hydraulic", "PREFLIGHT COMPLETE", state =>
                    state.Pmdg777ThrustAsymmetryCompensationAuto
                    && state.Pmdg777PrimaryFlightComputersAuto
                    && state.Pmdg777PrimaryFlightComputersGuardClosed
                    && state.Pmdg777PassengerOxygenGuardClosed
                    && state.Pmdg777HydraulicPanelSafe),
                Item("FO overhead engine / fuel / fire", "PREFLIGHT COMPLETE", state =>
                    state.Pmdg777FirePanelNormal
                    && state.Pmdg777EngineControlPanelNormal
                    && state.Pmdg777FuelPanelPreflight
                    && state.Pmdg777FuelToRemainSelectorIn
                    && state.Pmdg777FireOverheatTestComplete
                    && state.Pmdg777AntiIceAuto),
                Item("FO overhead lights / signs", "PREFLIGHT COMPLETE", state => state.Pmdg777ExteriorLightsPreflight && state.Pmdg777NoSmokingAuto && state.Pmdg777SeatBeltsOff),
                Item("FO overhead air systems", "PREFLIGHT COMPLETE", state => state.Pmdg777AirPanelPreflight && state.Pmdg777TemperatureControlsPreflight),
                Item("FO flight director", "ON", state => state.Pmdg777FirstOfficerFlightDirectorOn),
                Item("FO display sources", "NORMAL", state => state.Pmdg777FirstOfficerSourcesNormal),
                Item("FO displays", "PFD / ND", state => state.Pmdg777FirstOfficerDisplaysReady),
                Item("FO instruments / displays", "PREFLIGHT COMPLETE", state =>
                    state.Pmdg777FirstOfficerSourcesNormal
                    && state.Pmdg777FirstOfficerDisplaysReady
                    && state.Pmdg777FirstOfficerNdMap
                    && state.Pmdg777AutobrakeRto
                    && state.Pmdg777FirstOfficerOxygenTestComplete),
                Item("Console starting configuration", "SET", state => state.Pmdg777ConsoleStartingConfiguration),
                Item("FO transponder altitude source", "NORM", state => state.Pmdg777TransponderAltitudeSourceNormal),
                Item("IRS", "ALIGNED", state => state.Pmdg777IrsAligned),
                Item("Virtual FO PREFLIGHT verification", "COMPLETE", state => state.Pmdg777FlowTwoFirstOfficerVerified)),
            Checklist("apu-start-pushback", "777 Before Start & Pushback Verification",
                Item("Doors and cargo", "CLOSED", state => state.RequiredDoorsClosed),
                Unknown("Fuel and load", "VERIFIED"),
                Item("APU", "AVAILABLE", state => state.Pmdg777ApuRunning && state.Pmdg777ApuGeneratorPowerEstablished && state.Pmdg777ApuBleedAirAvailable),
                Item("External power", "DISCONNECTED", state => !state.Pmdg777PrimaryExternalPowerOn && !state.Pmdg777SecondaryExternalPowerOn),
                Item("Seat belts", "AUTO", state => state.Pmdg777SeatBeltsAuto),
                Item("Hydraulics", "BEFORE START", state => state.Pmdg777HydraulicsBeforeStart),
                Item("Fuel pumps", "ON AS REQUIRED", state => state.Pmdg777FuelPumpsBeforeStart),
                Item("Beacon", "ON", state => state.Pmdg777BeaconOn),
                Item("Transponder", "XPNDR", state => state.Pmdg777TransponderXpndr),
                Item("Virtual FO BEFORE START verification", "COMPLETE", state => state.Pmdg777FlowThreeFirstOfficerVerified),
                Unknown("Pushback/start clearance", "RECEIVED"),
                Unknown("Captain setup", "COMPLETE")),
            Checklist("engine-start-sequence", "777 Engine Start Verification",
                Item("Secondary engine display", "SELECTED", state => state.Pmdg777SecondaryEngineDisplaySelected),
                Item("Start configuration", "SET", state => state.Pmdg777ApuBleedAirAvailable && state.Pmdg777HydraulicsBeforeStart && state.Pmdg777FuelPumpsBeforeStart),
                Item("Engine 2", "STABLE", state => state.Engine2StartStabilized && state.Pmdg777EngineTwoFuelControlRun && !state.Pmdg777EngineTwoStartValveOpen),
                Item("Engine 1", "STABLE", state => state.Engine1StartStabilized && state.Pmdg777EngineOneFuelControlRun && !state.Pmdg777EngineOneStartValveOpen),
                Item("Engines", "RUNNING", state => state.Engine1Running && state.Engine2Running),
                Item("Start valves", "CLOSED", state => !state.Pmdg777EngineOneStartValveOpen && !state.Pmdg777EngineTwoStartValveOpen)),
            Checklist("after-start-taxi", "777 Before Taxi Verification",
                Item("Engine bleed / packs / APU", "AFTER START", state => state.Pmdg777EngineBleedsAuto && state.Pmdg777PacksAuto && state.Pmdg777ApuBleedOff && state.Pmdg777ApuSelectorOff),
                Item("Hydraulics", "DEPARTURE SET", state => state.Pmdg777HydraulicsBeforeStart),
                Unknown("Flight controls", "CHECKED"),
                Unknown("Recall and trim", "CHECKED / TAKEOFF SET"),
                Item("Flaps", "TAKEOFF SET", state => state.Pmdg777TakeoffFlapsSet),
                Item("Autobrake", "RTO", state => state.Pmdg777AutobrakeRto),
                Item("Ground equipment", "CLEAR", state => !state.Pmdg777WheelChocksSet && state.RequiredDoorsClosed),
                Item("Taxi lights", "ON", state => state.Pmdg777TaxiLightsSet),
                Item("Taxi clearance", "RECEIVED / NOT INTEGRATED", state => !state.SayIntentionsAtcActive || state.TaxiClearanceReceived)),
            Checklist("before-takeoff", "777 Before Takeoff Verification",
                Unknown("Takeoff briefing", "COMPLETE"),
                Unknown("Cabin", "READY"),
                Item("Flaps", "TAKEOFF SET", state => state.Pmdg777TakeoffFlapsSet),
                Item("Transponder / TCAS", "TA/RA", state => state.Pmdg777TransponderTaRa),
                Item("Takeoff clearance", "RECEIVED / NOT INTEGRATED", state => !state.SayIntentionsAtcActive || state.TakeoffClearanceReceived),
                Item("Exterior lights", "TAKEOFF SET", state => state.Pmdg777TakeoffLightsSet)),
            Checklist("takeoff-climb", "777 After Takeoff Verification",
                Item("Takeoff thrust", "SET", state => state.Engine1N1Percent >= 40 && state.Engine2N1Percent >= 40),
                Item("Aircraft", "AIRBORNE", state => !state.OnGround),
                Item("Landing gear", "UP", state => state.Pmdg777GearLeverUp),
                Item("Flaps", "UP", state => state.Pmdg777FlapsUp),
                Item("Climb lights", "SET", state => state.Pmdg777ClimbLightsSet)),
            Checklist("cruise", "777 Cruise Verification",
                Item("Aircraft", "AIRBORNE", state => !state.OnGround),
                Item("Cruise altitude", "ESTABLISHED", state => state.CruiseEstablished),
                Item("Engines", "RUNNING", state => state.Engine1Running && state.Engine2Running),
                Item("Configuration", "CLEAN", state => state.Pmdg777GearLeverUp && state.Pmdg777FlapsUp),
                Item("Fuel quantity", "AVAILABLE", state => state.ActualFuelKilograms > 0)),
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
