using Msfs2024Ai.Copilot.Domain;

namespace Msfs2024Ai.Copilot.Procedures;

/// <summary>
/// PMDG 777-300ER gate-to-gate procedure catalog. The complete catalog is
/// visible during integration, but no automatic 777 command is permitted
/// until its event and independent readback have been validated live.
/// </summary>
internal static class Pmdg777ProcedureLibrary
{
    public static IReadOnlyList<ProcedureDefinition> GateToGate =>
        new[]
        {
            PowerUpAndPreliminaryPreflight,
            FlightComputerAndPreFlight,
            BeforeStartAndPushback,
            EngineStartSequence,
            BeforeTaxiAndTaxi,
            BeforeTakeoff,
            TakeoffAndClimb,
            Cruise,
            DescentPreparation,
            ApproachAndLanding,
            AfterLandingAndTaxi,
            ParkingAndShutdown
        };

    public static ProcedureDefinition? Find(string id)
    {
        var canonicalId = id.ToLowerInvariant() switch
        {
            "cockpit-preparation" => "power-up-initial-setup",
            "before-start" => "apu-start-pushback",
            "engine-start" => "engine-start-sequence",
            "after-start" => "after-start-taxi",
            "taxi" => "after-start-taxi",
            "takeoff-initial-climb" => "takeoff-climb",
            "climb-to-cruise" => "takeoff-climb",
            _ => id
        };

        return GateToGate.FirstOrDefault(procedure =>
            string.Equals(procedure.Id, canonicalId, StringComparison.OrdinalIgnoreCase));
    }

    private static ProcedureStep Observe(
        string id,
        string label,
        Func<AircraftState, bool> complete) =>
        new(id, label, ProcedureStepKind.Observe, complete, CrewRole.FirstOfficer);

    private static ProcedureStep Manual(
        string id,
        string label,
        string instruction,
        CrewRole role = CrewRole.FirstOfficer,
        Func<AircraftState, bool>? complete = null) =>
        new(
            id,
            label,
            ProcedureStepKind.ManualAction,
            complete ?? (_ => false),
            role,
            manualInstruction: instruction);

    public static ProcedureDefinition PowerUpAndPreliminaryPreflight { get; } =
        new(
            "power-up-initial-setup",
            "1. 777 Power Up & Preliminary Preflight",
            new[]
            {
                Observe("sdk-data-ready", "PMDG 777X SDK data received", state => state.Pmdg777SdkDataReady),
                Observe("battery-on", "Battery switch ON", state => state.Pmdg777BatteryOn),
                Observe("hydraulic-starting-state", "C1/C2 primary and demand pumps OFF", state => state.Pmdg777HydraulicPanelSafe),
                Observe("wipers-off", "Windshield wipers OFF", state => state.Pmdg777WipersOff),
                Observe("gear-down", "Landing gear lever DOWN", state => state.Pmdg777GearLeverDown),
                Observe("alternate-flaps-off", "Alternate flaps OFF", state => state.Pmdg777AlternateFlapsOff),
                Observe("external-power-available", "Primary and secondary external power AVAILABLE", state => state.Pmdg777ExternalPowerAvailable),
                Observe("external-power-on", "Primary and secondary external power ON", state => state.Pmdg777ExternalPowerOn),
                Observe("parking-brake-set", "Parking brake SET", state => state.ParkingBrakeSet),
                Observe("nav-light-on", "Navigation light ON", state => state.Pmdg777NavigationLightOn),
                Manual("logo-light-as-required", "Logo light as required", "First Officer: set the logo light for the prevailing light conditions, then press Confirm now."),
                Observe("packs-recirculation-off", "Packs and recirculation fans OFF for ground air", state => state.Pmdg777GroundAirConfigurationSet),
                Manual("ground-air-connected", "Ground air requested and connected", "First Officer: request ground air, wait for connection, then press Confirm now."),
                Manual("adiru-cycle", "ADIRU OFF for 30 seconds, then ON", "First Officer: complete the first-flight ADIRU reset if required, select ON, then press Confirm now.", complete: state => state.Pmdg777AdiruOn),
                Observe("emergency-lights-armed", "Emergency exit lights ARMED; guard closed", state => state.Pmdg777EmergencyLightsArmed),
                Manual("eicas-status-scan", "EICAS and system status checked", "First Officer: review EICAS, oil, hydraulics, oxygen, status messages and checklist resets, then press Confirm now."),
                Manual("documents-equipment-check", "Documents and emergency equipment checked", "First Officer: complete first-flight documents, emergency-equipment, guards and circuit-breaker checks, then press Confirm now.")
            });

    public static ProcedureDefinition FlightComputerAndPreFlight { get; } =
        new(
            "flight-computer-preflight",
            "2. 777 Flight Deck & Preflight",
            new[]
            {
                Observe("sdk-data-ready", "PMDG 777X SDK data available", state => state.Pmdg777SdkDataReady),
                Manual("external-inspection", "External inspection complete", "Captain: complete the exterior inspection and verify doors, panels, gear and surfaces, then press Confirm now.", CrewRole.Captain),
                Manual("uft-setup", "Universal Flight Tablet setup complete", "Captain: import or enter the flight, payload, fuel and performance data in the UFT, then press Confirm now.", CrewRole.Captain),
                Manual("cdu-initial-data", "CDU initial data complete", "Captain: complete IDENT, POS INIT and route initialization, then press Confirm now.", CrewRole.Captain),
                Manual("route-performance", "Route and performance data complete", "Captain: verify route, departure, arrival, reserves, cruise altitude, cost index and takeoff reference data, then press Confirm now.", CrewRole.Captain),
                Manual("preflight-overhead", "Preflight overhead scan complete", "First Officer: complete the 777 preflight overhead scan and verify normal guarded-switch positions, then press Confirm now."),
                Manual("instrument-panel", "Instrument panel preflight complete", "First Officer: check displays, flight instruments, MCP, EFIS and standby instruments, then press Confirm now."),
                Manual("console", "Pedestal and console preflight complete", "First Officer: check radios, transponder, trim, speedbrake, flap and fuel-control positions, then press Confirm now."),
                Manual("preflight-checklist", "Preflight checklist complete", "Crew: complete the PMDG 777 electronic PRE-FLIGHT checklist, then press Confirm now."),
                Manual("takeoff-data", "Takeoff data and MCP set", "Captain: verify V-speeds, takeoff flaps, thrust, runway, initial altitude and MCP entries, then press Confirm now.", CrewRole.Captain)
            });

    public static ProcedureDefinition BeforeStartAndPushback { get; } =
        new(
            "apu-start-pushback",
            "3. 777 Before Start & Pushback",
            new[]
            {
                Observe("parked", "Aircraft stationary at the gate", state => state.OnGround && state.GroundSpeedKnots <= 0.5),
                Manual("doors-cargo", "Passenger and cargo doors ready", "First Officer: coordinate boarding completion and verify doors and cargo are ready for closure, then press Confirm now."),
                Manual("fuel-load", "Fuel quantity and load verified", "Captain: compare actual fuel and loading with the operational flight plan, then press Confirm now.", CrewRole.Captain),
                Manual("apu-start", "APU started and available", "First Officer: start the APU in accordance with the 777 procedure and verify AVAIL, then press Confirm now."),
                Manual("apu-power-air", "APU electrical and pneumatic supply established", "First Officer: establish APU generator and bleed supply as required, then press Confirm now."),
                Manual("ground-services-disconnect", "Ground air and external power disconnected", "First Officer: coordinate removal of ground air and external electrical power, then press Confirm now."),
                Manual("before-start-procedure", "Before Start procedure complete", "Crew: complete CDU, MCP, trim, flight-control, hydraulic and door checks, then press Confirm now."),
                Manual("before-start-checklist", "Before Start checklist complete", "Crew: complete the electronic BEFORE START checklist, then press Confirm now."),
                Manual("pushback-clearance", "Pushback and start clearance received", "Captain: obtain and acknowledge pushback/start clearance, then press Confirm now.", CrewRole.Captain),
                Manual("beacon-on", "Beacon light ON", "Captain: switch the beacon ON before pushback or engine start, then press Confirm now.", CrewRole.Captain),
                Manual("pushback-started", "Pushback commenced", "Captain: release the parking brake when cleared and confirm pushback has begun, then press Confirm now.", CrewRole.Captain)
            });

    public static ProcedureDefinition EngineStartSequence { get; } =
        new(
            "engine-start-sequence",
            "4. 777 Engine Start",
            new[]
            {
                Observe("on-ground", "Aircraft on the ground", state => state.OnGround),
                Manual("start-configuration", "Pneumatic and hydraulic start configuration set", "First Officer: verify packs, isolation and hydraulic demand-pump configuration for engine start, then press Confirm now."),
                Manual("engine-two-start", "Engine 2 start initiated", "Captain: select the right engine START selector and move its fuel control switch to RUN at the required indication, then press Confirm now.", CrewRole.Captain),
                Manual("engine-two-stable", "Engine 2 stabilized", "First Officer: monitor oil pressure, EGT, N2 and start-valve indications until stable, then press Confirm now."),
                Manual("engine-one-start", "Engine 1 start initiated", "Captain: select the left engine START selector and move its fuel control switch to RUN at the required indication, then press Confirm now.", CrewRole.Captain),
                Manual("engine-one-stable", "Engine 1 stabilized", "First Officer: monitor oil pressure, EGT, N2 and start-valve indications until stable, then press Confirm now."),
                Observe("engines-running", "Both engines running", state => state.Engine1Running && state.Engine2Running),
                Manual("start-abnormal-review", "No engine-start abnormal indications", "Crew: verify both starts are normal and review any EICAS messages before continuing, then press Confirm now.")
            });

    public static ProcedureDefinition BeforeTaxiAndTaxi { get; } =
        new(
            "after-start-taxi",
            "5. 777 Before Taxi & Taxi",
            new[]
            {
                Observe("engines-running", "Both engines running", state => state.Engine1Running && state.Engine2Running),
                Manual("generators-air", "Engine electrical and pneumatic configuration normal", "First Officer: verify engine generators, packs and bleed configuration, and shut down the APU when appropriate, then press Confirm now."),
                Manual("hydraulics", "Hydraulic panel configured", "First Officer: configure and verify primary and demand pumps for taxi, then press Confirm now."),
                Manual("flight-controls", "Flight controls checked", "Crew: complete the full flight-control check and verify EICAS indications, then press Confirm now."),
                Manual("flaps", "Takeoff flaps set", "Captain: set the calculated takeoff flap position and verify the indication, then press Confirm now.", CrewRole.Captain),
                Manual("trim", "Takeoff trim set", "Captain: set stabilizer trim from the takeoff data and verify rudder/aileron trim centered, then press Confirm now.", CrewRole.Captain),
                Manual("before-taxi-checklist", "Before Taxi checklist complete", "Crew: complete the electronic BEFORE TAXI checklist, then press Confirm now."),
                Manual("taxi-clearance", "Taxi clearance received", "Captain: obtain taxi clearance and verify the route, then press Confirm now.", CrewRole.Captain),
                Manual("taxi-lights", "Taxi and runway-turnoff lights set", "First Officer: set taxi and runway-turnoff lights as required, then press Confirm now."),
                Manual("brake-check", "Brakes and steering checked", "Captain: check brakes immediately after movement and verify steering during taxi, then press Confirm now.", CrewRole.Captain)
            });

    public static ProcedureDefinition BeforeTakeoff { get; } =
        new(
            "before-takeoff",
            "6. 777 Before Takeoff",
            new[]
            {
                Observe("on-ground", "Aircraft on the ground", state => state.OnGround),
                Manual("takeoff-briefing", "Takeoff briefing reviewed", "Captain: review runway, performance, initial routing and reject/engine-failure plan, then press Confirm now.", CrewRole.Captain),
                Manual("cabin-ready", "Cabin ready received", "Crew: confirm the cabin is secure for takeoff, then press Confirm now."),
                Manual("takeoff-config", "Takeoff configuration verified", "Crew: verify flaps, trim, speedbrake, autobrake, flight controls and takeoff data, then press Confirm now."),
                Manual("transponder", "Transponder and TCAS set", "First Officer: select the required transponder and TCAS mode, then press Confirm now."),
                Manual("lights", "Exterior lights set for runway entry", "First Officer: set strobes, landing and runway lights when cleared onto the runway, then press Confirm now."),
                Manual("before-takeoff-checklist", "Before Takeoff checklist complete", "Crew: complete the electronic BEFORE TAKEOFF checklist, then press Confirm now."),
                Manual("takeoff-clearance", "Takeoff clearance received", "Captain: acknowledge takeoff clearance and verify the correct runway, then press Confirm now.", CrewRole.Captain)
            });

    public static ProcedureDefinition TakeoffAndClimb { get; } =
        new(
            "takeoff-climb",
            "7. 777 Takeoff & Climb",
            new[]
            {
                Manual("runway-verification", "Runway and heading verified", "Crew: verify runway, heading and takeoff mode annunciations before thrust application, then press Confirm now."),
                Manual("takeoff-thrust", "Takeoff thrust set", "Captain: set takeoff thrust and verify thrust reference and engine indications, then press Confirm now.", CrewRole.Captain),
                Manual("takeoff-callouts", "Takeoff callouts complete", "Crew: complete airspeed, V1, rotation and positive-rate callouts, then press Confirm now."),
                Manual("gear-up", "Landing gear UP", "Captain: command gear UP after positive rate and verify retraction, then press Confirm now.", CrewRole.Captain),
                Observe("airborne", "Aircraft airborne", state => !state.OnGround),
                Manual("flap-retraction", "Flaps retracted on schedule", "Crew: retract flaps on the FMC speed schedule and verify a clean configuration, then press Confirm now."),
                Manual("after-takeoff-checklist", "After Takeoff checklist complete", "Crew: complete the electronic AFTER TAKEOFF checklist, then press Confirm now."),
                Manual("climb-configuration", "Climb configuration established", "Crew: set climb thrust, lights and altimeters as required and monitor the departure, then press Confirm now.")
            });

    public static ProcedureDefinition Cruise { get; } =
        new(
            "cruise",
            "8. 777 Climb & Cruise",
            new[]
            {
                Observe("airborne", "Aircraft airborne", state => !state.OnGround),
                Manual("top-of-climb", "Top of climb checks complete", "Crew: verify cruise altitude capture, thrust mode, pressurization and fuel state, then press Confirm now."),
                Manual("fuel-monitoring", "Fuel and engine trend monitoring established", "Crew: compare fuel against the flight plan and monitor engine/system synoptics, then press Confirm now."),
                Manual("route-weather", "Route and weather reviewed", "Crew: review route changes, winds, weather, alternates and destination status, then press Confirm now."),
                Manual("step-climbs", "Step climbs managed as required", "Captain: verify optimum/maximum altitude and execute cleared step climbs as appropriate, then press Confirm now.", CrewRole.Captain),
                Manual("cruise-check", "Cruise check complete", "Crew: complete the company cruise check and prepare for descent planning, then press Confirm now.")
            });

    public static ProcedureDefinition DescentPreparation { get; } =
        new(
            "descent-preparation",
            "9. 777 Descent Preparation",
            new[]
            {
                Manual("arrival-weather", "Arrival weather and runway reviewed", "Crew: obtain destination weather, runway, approach and airport information, then press Confirm now."),
                Manual("fmc-arrival", "FMC arrival and approach entered", "Captain: verify STAR, approach, constraints and route discontinuities, then press Confirm now.", CrewRole.Captain),
                Manual("landing-performance", "Landing performance complete", "Crew: calculate landing distance, flap setting, autobrake and VREF additives, then press Confirm now."),
                Manual("minimums", "Approach minimums and references set", "Crew: set barometric/radio minimums, courses, bugs and reference speeds, then press Confirm now."),
                Manual("approach-briefing", "Approach and landing briefing complete", "Captain: brief approach, missed approach, threats, taxi-in and landing configuration, then press Confirm now.", CrewRole.Captain),
                Manual("descent-checklist", "Descent checklist complete", "Crew: complete the electronic DESCENT checklist before top of descent, then press Confirm now."),
                Manual("descent-clearance", "Descent clearance received and set", "Captain: acknowledge the clearance and verify MCP/FMC constraints, then press Confirm now.", CrewRole.Captain)
            });

    public static ProcedureDefinition ApproachAndLanding { get; } =
        new(
            "approach-landing",
            "10. 777 Approach & Landing",
            new[]
            {
                Observe("airborne", "Aircraft airborne", state => !state.OnGround),
                Manual("approach-procedure", "Approach procedure established", "Crew: verify lateral/vertical path, altimeters, approach mode and navigation source, then press Confirm now."),
                Manual("approach-checklist", "Approach checklist complete", "Crew: complete the electronic APPROACH checklist, then press Confirm now."),
                Manual("flaps-schedule", "Flaps extended on speed schedule", "Captain: command flap extension at the PMDG/FMC placard schedule and verify each position, then press Confirm now.", CrewRole.Captain),
                Manual("gear-down", "Landing gear DOWN", "Captain: lower the landing gear at the planned point and verify three green indications, then press Confirm now.", CrewRole.Captain),
                Manual("speedbrake-autobrake", "Speedbrake armed and autobrake set", "First Officer: verify speedbrake ARMED and the planned autobrake selection, then press Confirm now."),
                Manual("landing-flaps", "Landing flaps and VREF set", "Crew: establish final landing flaps and approach speed, then press Confirm now."),
                Manual("landing-checklist", "Landing checklist complete", "Crew: complete the electronic LANDING checklist, then press Confirm now."),
                Manual("stable-approach", "Stable approach criteria met", "Crew: verify landing configuration, speed, path, thrust and checklist completion by the stabilization gate, then press Confirm now."),
                Manual("landing-roll", "Landing and rollout complete", "Crew: monitor spoilers, reverse, autobrake/deceleration and manual braking through runway exit, then press Confirm now."),
                Observe("touchdown", "Aircraft on the ground", state => state.OnGround)
            });

    public static ProcedureDefinition AfterLandingAndTaxi { get; } =
        new(
            "after-landing-taxi",
            "11. 777 After Landing & Taxi In",
            new[]
            {
                Observe("on-ground", "Aircraft on the ground", state => state.OnGround),
                Manual("runway-vacated", "Runway vacated", "Captain: clear the runway and comply with taxi clearance, then press Confirm now.", CrewRole.Captain),
                Manual("landing-lights-strobes", "Landing lights and strobes set for taxi", "First Officer: set landing lights OFF and position/strobe lights for taxi, then press Confirm now."),
                Manual("flaps-speedbrake", "Flaps UP and speedbrake DOWN", "First Officer: retract flaps, lower the speedbrake and verify indications, then press Confirm now."),
                Manual("weather-radar-transponder", "Weather radar and transponder set after landing", "First Officer: set weather radar and transponder/TCAS for taxi, then press Confirm now."),
                Manual("apu-start", "APU started as required", "First Officer: start the APU early enough for gate electrical and pneumatic supply, then press Confirm now."),
                Manual("after-landing-procedure", "After Landing procedure complete", "Crew: complete the after-landing flow and verify no abnormal EICAS messages, then press Confirm now."),
                Manual("gate-approach", "Gate approach and marshaller guidance verified", "Captain: identify the assigned stand and follow docking guidance at walking pace, then press Confirm now.", CrewRole.Captain)
            });

    public static ProcedureDefinition ParkingAndShutdown { get; } =
        new(
            "parking-shutdown",
            "12. 777 Parking, Shutdown & Secure",
            new[]
            {
                Observe("parked", "Aircraft stationary at the gate", state => state.OnGround && state.GroundSpeedKnots <= 0.5),
                Manual("parking-brake", "Parking brake and chocks coordinated", "Captain: set the parking brake until chocks are confirmed, then press Confirm now.", CrewRole.Captain),
                Manual("gate-power", "APU or external power established", "First Officer: verify a stable electrical source before engine shutdown, then press Confirm now."),
                Manual("fuel-controls-cutoff", "Fuel control switches CUTOFF", "Captain: move both fuel control switches to CUTOFF and verify shutdown, then press Confirm now.", CrewRole.Captain),
                Observe("engines-off", "Both engines shut down", state => state.EnginesOff),
                Manual("beacon-off", "Beacon OFF", "First Officer: switch the beacon OFF when engines have stopped, then press Confirm now."),
                Manual("hydraulic-fuel", "Hydraulic and fuel panels secured", "First Officer: set pumps for shutdown and verify the shutdown configuration, then press Confirm now."),
                Manual("doors-ground", "Doors and ground services coordinated", "Crew: release doors and connect required ground services only when safe, then press Confirm now."),
                Manual("shutdown-checklist", "Shutdown checklist complete", "Crew: complete the electronic SHUTDOWN checklist, then press Confirm now."),
                Manual("secure-procedure", "Secure procedure complete", "Crew: for final termination, complete the SECURE procedure and checklist; otherwise leave the aircraft in turnaround state, then press Confirm now."),
                Manual("maintenance-handover", "Maintenance handover complete as required", "Captain: record defects and hand over relevant aircraft status, then press Confirm now.", CrewRole.Captain)
            });
}
