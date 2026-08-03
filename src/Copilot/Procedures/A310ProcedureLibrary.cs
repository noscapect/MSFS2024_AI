using Msfs2024Ai.Copilot.Domain;

namespace Msfs2024Ai.Copilot.Procedures;

/// <summary>
/// iniBuilds A310-300 normal-operation flow framework. The ordering follows
/// the official iniBuilds A310 MSFS manual. Cockpit actions remain explicit
/// manual actions until their native command and independent readback have
/// been captured and live-validated in MSFS 2024.
/// </summary>
internal static class A310ProcedureLibrary
{
    public static IReadOnlyList<ProcedureDefinition> GateToGate =>
        new[]
        {
            PowerUpAndInitialSetup,
            FlightComputerAndPreFlight,
            ApuStartAndPushback,
            EngineStartSequence,
            AfterStartAndTaxi,
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
            "after-start" or "taxi" => "after-start-taxi",
            "takeoff-initial-climb" or "climb-to-cruise" => "takeoff-climb",
            _ => id
        };
        return GateToGate.FirstOrDefault(item =>
            string.Equals(item.Id, canonicalId, StringComparison.OrdinalIgnoreCase));
    }

    private static ProcedureStep Observe(
        string id,
        string label,
        Func<AircraftState, bool> complete,
        CrewRole role = CrewRole.FirstOfficer) =>
        new(id, label, ProcedureStepKind.Observe, complete, role);

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

    private static ProcedureStep Automatic(
        string id,
        string label,
        Func<AircraftState, bool> complete,
        string command,
        CrewRole role = CrewRole.FirstOfficer) =>
        new(
            id,
            label,
            ProcedureStepKind.AutomaticAction,
            complete,
            role,
            "a310 " + command);

    private static ProcedureStep Advisory(string id, string label) =>
        Observe(id, label, _ => true);

    private static ProcedureStep Captain(
        string id,
        string label,
        string instruction,
        Func<AircraftState, bool>? complete = null) =>
        Manual(id, label, "Captain: " + instruction, CrewRole.Captain, complete);

    private static ProcedureStep FirstOfficer(
        string id,
        string label,
        string instruction,
        Func<AircraftState, bool>? complete = null) =>
        Manual(id, label, "First Officer: " + instruction, CrewRole.FirstOfficer, complete);

    private static bool ApproachDistanceReached(AircraftState state, int distanceNm) =>
        state.ApproachDistanceToTouchdownNm is > 0
        && state.ApproachDistanceToTouchdownNm.Value <= distanceNm;

    private static bool ApproachGate(
        AircraftState state,
        int distanceNm,
        bool altitudeFallback) =>
        ApproachDistanceReached(state, distanceNm)
        || (state.ApproachDistanceToTouchdownNm is not > 0 && altitudeFallback);

    public static ProcedureDefinition PowerUpAndInitialSetup { get; } =
        new(
            "power-up-initial-setup",
            "1. A310 Preliminary Cockpit Preparation",
            new[]
            {
                Observe("aircraft", "iniBuilds A310-300 loaded", state => state.IsIniBuildsA310),
                Observe("stationary", "Aircraft stationary on the ground", state => state.OnGround && state.GroundSpeedKnots <= 0.5),
                Observe("engines-off", "Both engines shut down", state => state.EnginesOff),
                Captain("gear-flaps-reversers", "Gear, slat/flap and reverser controls checked", "verify gear DOWN, the slat/flap handle agrees with surface position, and both reverser levers are down; then confirm."),
                Automatic(
                    "batteries-auto",
                    "BAT 1, BAT 2 and BAT 3 AUTO",
                    state => state.Battery1On && state.Battery2On && state.Battery3On,
                    "batteries auto"),
                Observe(
                    "hydraulic-safe",
                    "Hydraulic engine pumps AUTO and electric pumps OFF",
                    state => state.A310HydraulicPanelSafe),
                Automatic(
                    "wipers-radar-off",
                    "Wipers and weather radar OFF",
                    state => state.A310WipersAndWeatherRadarOff,
                    "wipers-radar off"),
                Observe("fuel-levers-off", "Engine fuel levers OFF", state => state.EnginesOff),
                Captain("external-power", "External electrical power established when available", "connect and establish external power, or confirm that APU power will be used.", state => state.ExternalPowerOn || state.ApuAvailable),
                Automatic("apu-fire-test", "APU fire system test complete", state => state.A310ApuFireTestCompleted, "apu-fire-test"),
                Observe("apu-as-required", "Electrical source available; APU only as required", state => state.ExternalPowerOn || state.ApuAvailable),
                Automatic("irs-nav", "IRS 1, 2 and 3 NAV", state => state.AllAdirsNav, "irs nav"),
                Automatic("oxygen-on", "Crew oxygen low-pressure supply ON", state => state.CrewOxygenOn, "oxygen on"),
                Automatic("annunciator-test", "Annunciator light test complete", state => state.A310AnnunciatorTestCompleted, "annunciator-test"),
                Automatic("initial-lights", "Initial exterior lights set", state => state.A310InitialExteriorLightsSet, "initial-lights"),
                Advisory("vhf-radios", "VHF radios retained/set as required")
            });

    public static ProcedureDefinition FlightComputerAndPreFlight { get; } =
        new(
            "flight-computer-preflight",
            "2. A310 Flight Deck Preparation & Pre-Flight",
            new[]
            {
                Captain("fmc-init", "FMC initialization complete", "enter FROM/TO, alternate, cost index, cruise level, flight ID, route, SID/STAR and required NAVAIDs."),
                Captain("loadsheet", "Loadsheet and fuel verified", "complete the EFB loadsheet, compare fuel with the plan, and enter INIT B data."),
                Captain("takeoff-performance", "Takeoff performance entered", "calculate takeoff performance; enter flap/slat setting, V-speeds, flex temperature if used, and set V2 and TRP AUTO."),
                Automatic("signs", "No smoking AUTO and seat belts ON", state => state.A310PreflightSignsSet, "preflight-signs"),
                Advisory("hyd-servo-panels", "Hydraulic and servo-control indications checked"),
                Automatic("recorder-and-autoflight", "ATS and flight-control computers ON", state => state.A310AutoflightComputersSet, "autoflight-computers"),
                Advisory("flight-recorder", "Flight recorder ground-control status checked"),
                Advisory("overhead-system-scan", "Electrical, engine, fuel and pneumatic indications checked"),
                Automatic("heat", "Window and probe heat ON", state => state.A310PreflightHeatSet, "preflight-heat"),
                Automatic("cargo-smoke-test", "Cargo smoke detection test complete", state => state.A310CargoSmokeTestCompleted, "cargo-smoke-test"),
                Advisory("cargo-smoke-indications", "Cargo LOOP/SMOKE lights, ECAM and warnings checked during test"),
                Advisory("ventilation", "Ventilation indications checked"),
                Automatic("emergency-exit", "Emergency exit lights ARMED", state => state.A310EmergencyExitArmed, "emergency-exit arm"),
                Advisory("instruments", "EFIS, FCP and flight instruments scan complete"),
                Automatic("egpws-test", "EGPWS test complete", state => state.A310EgpwsTestCompleted, "egpws-test"),
                Captain("parking-brake", "Parking brake and brake pressure checked", "set the parking brake and verify accumulator pressure is in the green band.", state => state.ParkingBrakeSet),
                Advisory("brakes-autobrake", "Brakes and anti-skid indications checked"),
                Observe("speedbrake", "Speedbrake retracted and disarmed", state => !state.GroundSpoilersArmed),
                Captain("takeoff-warning", "Takeoff warning system tested", "perform the takeoff-warning test with each throttle and clear the warning."),
                Automatic("atc-radar-rudder", "Preflight pedestal configured", state => state.A310PreflightPedestalSet, "preflight-pedestal"),
                Automatic("fuel-pumps-on", "All tank fuel pumps ON", state => state.A310FuelPumpsOn, "fuel-pumps on"),
                Advisory("adf-radar-check", "ADF and weather-radar indications checked"),
                Manual(
                    "captain-ifr-clearance",
                    "IFR clearance received and reviewed",
                    "Pilot: after completing the FMC setup, use the available ATC system to request, acknowledge and review IFR clearance.",
                    CrewRole.Captain,
                    state => state.AtcClearedIfr)
            });

    public static ProcedureDefinition ApuStartAndPushback { get; } =
        new(
            "apu-start-pushback",
            "3. A310 Before Start & Pushback",
            new[]
            {
                Automatic("apu-start", "APU started and available", state => state.ApuAvailable, "apu start"),
                Automatic("apu-generator-bleed", "APU generator and bleed established", state => state.A310ApuPowerAndBleedSet, "apu power-bleed"),
                Advisory("before-start-to-line", "Before Start checklist to the line reviewed"),
                Manual(
                    "captain-pushback-clearance",
                    "Pushback and engine-start clearance received",
                    "Pilot: request and acknowledge pushback and engine-start clearance through the available ATC system.",
                    CrewRole.Captain),
                Captain("doors-slides", "Windows and doors closed; slides armed", "verify all windows and doors closed, slides armed, and the cockpit door locked.", state => state.RequiredDoorsClosed),
                Automatic("beacon-on", "Beacon ON", state => state.BeaconOn, "beacon on"),
                Observe("parking-brake", "Parking brake ON before tug movement", state => state.ParkingBrakeSet),
                Advisory("elapsed-time", "Elapsed-time clock started; pushback/start time noted"),
                Automatic("transponder-xpdr", "Transponder XPDR", state => state.A310TransponderXpdrSet, "transponder xpdr"),
                Automatic("external-power-off", "External power disconnected", state => !state.ExternalPowerOn, "external-power off")
            });

    public static ProcedureDefinition EngineStartSequence { get; } =
        new(
            "engine-start-sequence",
            "4. A310 Engine Start Sequence",
            new[]
            {
                Captain("area-clear", "Area clear for engine start", "confirm the start area is clear and coordinate with the tug/ground crew."),
                Automatic("ignition-a-b", "Ignition selector A", state => state.A310IgnitionSelectedForStart, "ignition a"),
                Observe("packs-closed", "Pack valves closed for start", state => state.A310PacksClosedForStart),
                Automatic("fo-engine-two-starter", "Engine 2 start switch pressed", state => state.A310Engine2StarterSelected || state.Engine2StarterActive, "engine-2 starter"),
                Observe("engine-two-rotation", "Engine 2 N2 reaches 20 percent", state => state.Engine2N2Percent >= 20),
                Captain("fo-engine-two-fuel", "Engine 2 fuel lever ON at 20 percent N2", "move Engine 2 fuel lever ON and monitor EGT, oil pressure and acceleration.", state => state.A310Engine2FuelLeverOn || state.Engine2FuelFlowDetected || state.Engine2Running),
                Observe("fo-engine-two-stable", "Engine 2 stable and start valve closed", state => state.Engine2Running && state.Engine2N2Percent >= 45 && !state.Engine2StarterActive),
                Automatic("fo-engine-one-starter", "Engine 1 start switch pressed", state => state.A310Engine1StarterSelected || state.Engine1StarterActive, "engine-1 starter"),
                Observe("engine-one-rotation", "Engine 1 N2 reaches 20 percent", state => state.Engine1N2Percent >= 20),
                Captain("fo-engine-one-fuel", "Engine 1 fuel lever ON at 20 percent N2", "move Engine 1 fuel lever ON and monitor EGT, oil pressure and acceleration.", state => state.A310Engine1FuelLeverOn || state.Engine1FuelFlowDetected || state.Engine1Running),
                Observe("fo-engine-one-stable", "Engine 1 stable and start valve closed", state => state.Engine1Running && state.Engine1N2Percent >= 45 && !state.Engine1StarterActive),
                Observe("both-engines", "Both engines stable", state => state.Engine1Running && state.Engine2Running)
            });

    public static ProcedureDefinition AfterStartAndTaxi { get; } =
        new(
            "after-start-taxi",
            "5. A310 After Start & Taxi",
            new[]
            {
                Observe("both-engines", "Both engines running", state => state.Engine1Running && state.Engine2Running),
                Automatic("ignition-normal", "Ignition OFF for normal taxi", state => state.A310IgnitionOff, "ignition off"),
                Automatic("apu-off", "APU bleed OFF, then master OFF", state => !state.ApuBleedOn && !state.ApuMasterSwitchOn, "apu off"),
                Advisory("anti-ice", "Anti-ice as required for conditions"),
                Automatic("speedbrake-arm", "Speedbrake ARMED", state => state.GroundSpoilersArmed, "speedbrake arm"),
                Automatic("rudder-trim", "Rudder trim reset to zero", state => state.A310RudderTrimCentered, "rudder-trim reset"),
                FirstOfficer("takeoff-flaps", "Slats/flaps set for takeoff", "set the slat/flap position calculated by takeoff performance.", state => state.FlapsHandleIndex > 0),
                Captain("pitch-trim", "Pitch trim set from actual takeoff CG", "set pitch trim for takeoff using the CG shown on ECAM; verify the physical trim indication."),
                Advisory("after-start-checklist", "After Start checklist reviewed"),
                Manual("fo-taxi-clearance", "Taxi clearance received", "First Officer: press Confirm now to request taxi clearance through SayIntentions, or confirm the clearance received through another ATC source.", CrewRole.FirstOfficer, state => !state.SayIntentionsAtcActive),
                Automatic("nose-taxi", "Nose light TAXI", state => state.A310TaxiLightTaxi, "nose-light taxi"),
                Captain("brakes-release-check", "Brakes released and checked", "release the parking brake and check toe-brake operation at the first safe opportunity.", state => !state.ParkingBrakeSet),
                Captain("flight-controls", "Flight controls full and free", "select the F/CTL page and check full, free and correctly indicated yoke and rudder movement."),
                Captain("fcp-takeoff", "FCP and takeoff modes set", "set preselected speed 250, arm PROF and NAV as appropriate, and verify both flight directors ON."),
                Automatic("autobrake-max", "Autobrake MAX", state => state.A310AutobrakeMax, "autobrake max"),
                Automatic("transponder-weather", "Transponder XPDR and weather radar ON", state => state.A310TransponderXpdrSet && state.A310WeatherRadarOn, "transponder-weather on"),
                Captain("takeoff-config", "Takeoff configuration test passed", "perform the takeoff-configuration test and resolve every warning."),
                Observe("taxi-underway", "Forward taxi established", state => state.ForwardTaxiDetected || state.BeforeTakeoffHoldEligible)
            });

    public static ProcedureDefinition BeforeTakeoff { get; } =
        new(
            "before-takeoff",
            "6. A310 Before Takeoff",
            new[]
            {
                Observe("holding-short", "Aircraft at the departure runway holding point", state => state.BeforeTakeoffHoldEligible),
                Manual("fo-takeoff-clearance", "Takeoff clearance received", "First Officer: while holding short, press Confirm now to report ready for departure and request takeoff clearance through SayIntentions, or confirm clearance received through another ATC source.", CrewRole.FirstOfficer, state => !state.SayIntentionsAtcActive),
                Captain("runway-verified", "Runway and approach path verified", "verify the runway and approach path, and enter only when cleared."),
                FirstOfficer("brake-fans", "Brake fans OFF and temperatures acceptable", "set brake fans OFF; delay takeoff above the published brake-temperature limit."),
                FirstOfficer("takeoff-lights", "Takeoff exterior lights set", "set STROBE ON, BEACON ON, runway-turnoff lights ON, NAV 1/2, NOSE T.O., LAND ON and WING as required."),
                FirstOfficer("ignition-takeoff", "Ignition as required for takeoff", "use CONT RELIGHT for standing water, heavy rain or expected turbulence; otherwise use the normal setting."),
                FirstOfficer("packs-takeoff", "Packs set for takeoff performance", "leave packs ON unless the performance calculation specifically requires pack valves OFF."),
                FirstOfficer("tcas-tara", "TCAS TA/RA", "select TCAS TA/RA."),
                Captain("below-line-checklist", "Before Takeoff checklist below the line complete", "verify the final runway, lights, ignition, packs and TCAS configuration."),
                Observe("both-engines", "Both engines stable for takeoff", state => state.Engine1Running && state.Engine2Running)
            });

    public static ProcedureDefinition TakeoffAndClimb { get; } =
        new(
            "takeoff-climb",
            "7. A310 Takeoff & Climb",
            new[]
            {
                Observe("takeoff-roll", "Takeoff roll commenced", state => state.OnGround && state.GroundSpeedKnots >= 20),
                FirstOfficer("clock-start", "Takeoff announced and clock started", "announce takeoff and start the elapsed-time clock."),
                Captain("thrust-set", "Thrust stabilized and go-levers triggered", "stabilize both engines at or above 40% N1, advance to takeoff thrust, and trigger the go-levers."),
                Observe("fo-100-knots", "One hundred knots", state => state.HundredKnotsCalloutReached),
                Observe("v1", "V1", state => state.V1CalloutReached),
                Observe("rotate", "Rotate", state => state.RotateCalloutReached),
                Observe("positive-climb", "Positive climb", state => !state.OnGround && state.VerticalSpeedFeetPerMinute > 100),
                FirstOfficer("fo-gear-up", "Landing gear UP", "select the landing-gear lever UP.", state => !state.GearHandleDown),
                Captain("autopilot", "Autopilot as required", "engage AP1 or AP2 for the pilot flying when appropriate."),
                Observe("thrust-reduction", "Thrust-reduction altitude reached", state => state.AltitudeAboveGroundFeet >= 1000),
                Captain("climb-thrust", "Climb thrust established", "verify TRP changes to CL in AUTO and the throttles reduce symmetrically."),
                FirstOfficer("flaps-zero", "Flaps zero at or above F speed", "retract the flap portion to zero at or above F speed."),
                FirstOfficer("slats-zero", "Slats zero at or above S speed", "retract slats to zero at or above S speed.", state => state.FlapsHandleIndex <= 0),
                FirstOfficer("fo-ground-spoilers-disarm", "Spoilers DISARMED", "disarm the spoilers.", state => !state.GroundSpoilersArmed),
                FirstOfficer("gear-off", "Landing-gear lever OFF", "after retraction, move the landing-gear lever from UP to OFF."),
                FirstOfficer("packs-on", "Packs ON sequentially", "set Pack 1 ON, wait approximately 10 seconds, then set Pack 2 ON."),
                FirstOfficer("climb-lights", "Nose and runway-turnoff lights OFF", "set nose and runway-turnoff lights OFF; leave landing lights ON until 10,000 feet."),
                FirstOfficer("apu-climb", "APU OFF", "if used for departure, set APU BLEED OFF and then APU MASTER OFF."),
                Observe("ten-thousand", "10,000 feet passed", state => state.IndicatedAltitudeFeet >= 10000),
                FirstOfficer("altimeters-standard", "Altimeters STANDARD", "at transition altitude set and cross-check 1013 hPa / 29.92 inHg."),
                FirstOfficer("landing-lights-retract", "Landing lights RETRACT/OFF", "retract and switch off the landing lights."),
                FirstOfficer("seatbelts-climb", "Seat-belt signs as required", "set seat-belt signs as conditions permit."),
                Advisory("after-takeoff-checklist", "After Takeoff checklist reviewed")
            });

    public static ProcedureDefinition Cruise { get; } =
        new(
            "cruise",
            "8. A310 Cruise",
            new[]
            {
                Observe("cruise-established", "Cruise established", state => state.CruiseEstablished),
                Captain("trp-cruise", "TRP cruise rating checked", "verify TRP LIM MODE indicates CR, setting it manually if PROFILE has not done so."),
                Advisory("ecam-review", "ECAM memo, status and system pages reviewed"),
                Advisory("flight-progress", "Flight progress and fuel cross-check due"),
                Advisory("cruise-signs", "Seat-belt signs retained/set as conditions require")
            });

    public static ProcedureDefinition DescentPreparation { get; } =
        new(
            "descent-preparation",
            "9. A310 Descent Preparation",
            new[]
            {
                Captain("weather", "Destination and alternate weather obtained", "obtain runway, weather, QNH, minima and landing information approximately 80–100 NM before top of descent."),
                Advisory("ecam-status", "ECAM memo/status reviewed"),
                FirstOfficer("landing-elevation", "Landing elevation set", "set and cross-check destination landing elevation."),
                Advisory("fuel-check", "Fuel prediction and reserves cross-checked"),
                Captain("arrival-fms", "Arrival and approach programmed", "enter and verify STAR, transition, approach, missed approach, NAVAIDs and constraints in the FMS."),
                Captain("approach-page", "Approach data entered", "enter configuration, VAPP, MDA/DH and required FINAL path data on the approach page."),
                FirstOfficer("dh-autobrake", "Decision height and autobrake set", "set DH on the FCP and select the planned landing autobrake."),
                Captain("approach-briefing", "Approach briefing complete", "brief weather, terrain, NAVAIDs, flight plan, minima, runway, deceleration and go-around."),
                FirstOfficer("gpws-flaps", "GPWS landing slats/flaps switch as required", "select the alternate 20/20 setting only when landing with that configuration."),
                Captain("descent-clearance", "Descent clearance received", "obtain and acknowledge descent clearance, then initiate the cleared descent.")
            });

    public static ProcedureDefinition ApproachAndLanding { get; } =
        new(
            "approach-landing",
            "10. A310 Approach & Landing",
            new[]
            {
                Observe("descent", "Descent established", state => !state.OnGround && state.VerticalSpeedFeetPerMinute < -200),
                FirstOfficer("descent-anti-ice", "Anti-ice and ignition as required", "use CONT RELIGHT before engine anti-ice; select anti-ice in visible moisture below 10°C."),
                FirstOfficer("qnh", "Altimeters set to destination pressure", "below transition level set and cross-check destination QNH/QFE."),
                Observe("below-ten-thousand", "At or below 10,000 feet", state => state.IndicatedAltitudeFeet <= 10000),
                FirstOfficer("approach-signs-lights", "Seat belts ON and approach lights set", "set seat belts ON, runway-turnoff lights ON, and landing lights as required."),
                Observe("slats-point", "Slats 15 point", state => ApproachGate(state, state.ApproachFlaps1DistanceNm, state.IndicatedAltitudeFeet <= state.ApproachFlaps1AltitudeFeet)),
                Observe("slats-speed", "Slats 15 speed safe", state => state.IndicatedAirspeedKnots <= 245),
                FirstOfficer("slats-15", "Slats 15 / Flaps 0", "select 15/0 and verify indicated deployment.", state => state.FlapsHandleIndex >= 1),
                Captain("land-mode", "LAND mode armed when cleared", "when cleared for the ILS, press LAND and monitor LOC and G/S capture."),
                Observe("flaps-15-point", "Flaps 15 point", state => ApproachGate(state, state.ApproachFlaps2DistanceNm, state.AltitudeAboveGroundFeet <= 2000)),
                Observe("flaps-15-speed", "Flaps 15 speed safe", state => state.IndicatedAirspeedKnots <= 210),
                FirstOfficer("flaps-15", "Slats 15 / Flaps 15", "select 15/15 and verify indicated deployment.", state => state.FlapsHandleIndex >= 2),
                FirstOfficer("speedbrakes-retracted", "Speedbrakes retracted", "verify speedbrakes retracted; do not use them at 15/15 or greater."),
                Observe("gear-point", "Latest gear-down point", state => ApproachGate(state, 5, state.AltitudeAboveGroundFeet <= 1800)),
                FirstOfficer("fo-gear-down", "Landing gear DOWN", "select gear DOWN and verify three green indications.", state => state.GearHandleDown),
                FirstOfficer("fo-spoilers-arm", "Ground spoilers ARMED", "arm the ground spoilers.", state => state.GroundSpoilersArmed),
                FirstOfficer("nose-to", "Nose light T.O.", "set the nose light to T.O."),
                Observe("flaps-20-speed", "Flaps 20 speed safe", state => state.IndicatedAirspeedKnots <= 195),
                FirstOfficer("flaps-20", "Slats 15 / Flaps 20", "select 15/20 and verify indicated deployment.", state => state.FlapsHandleIndex >= 3),
                Observe("flaps-40-speed", "Landing flap speed safe", state => state.IndicatedAirspeedKnots <= 180),
                FirstOfficer("flaps-40", "Slats 30 / Flaps 40", "select 30/40 and verify indicated deployment.", state => state.FlapsHandleIndex >= 4),
                Observe("configured-1000", "Fully configured by 1,000 feet AGL", state => state.AltitudeAboveGroundFeet <= 1000 && state.GearHandleDown && state.FlapsHandleIndex >= 4),
                Captain("stable-500", "Stable by 500 feet AGL", "verify the approach remains stable; go around if it is not."),
                Observe("fo-approaching-minimums", "Approaching minimums", state => state.DecisionHeightFeet > 0 && state.RadioHeightFeet <= state.DecisionHeightFeet + 100),
                Observe("fo-minimums", "Minimums", state => state.DecisionHeightFeet > 0 && state.RadioHeightFeet <= state.DecisionHeightFeet),
                Observe("touchdown", "Touchdown", state => state.OnGround),
                Observe("fo-spoilers-callout", "Ground spoilers deployed", state => state.OnGround && state.GroundSpoilersDeployed),
                Observe("fo-reverse-callout", "Reverse thrust established", state => state.OnGround && (state.ReverseThrustEngaged || state.GroundSpeedKnots < 40)),
                Observe("eighty", "80 knots; reverse idle", state => state.OnGround && state.GroundSpeedKnots <= 80)
            });

    public static ProcedureDefinition AfterLandingAndTaxi { get; } =
        new(
            "after-landing-taxi",
            "11. A310 After Landing & Taxi",
            new[]
            {
                Observe("on-ground", "Aircraft on the ground", state => state.OnGround),
                Observe("taxi-speed", "Taxi speed reached", state => state.OnGround && state.GroundSpeedKnots <= 30),
                FirstOfficer("after-landing-lights", "After-landing lights set", "set landing, strobe and runway-entry lights for taxi; retain the nose light at TAXI."),
                FirstOfficer("anti-ice-after-landing", "Anti-ice OFF or as required", "switch anti-ice OFF unless required for taxi."),
                FirstOfficer("ignition-off", "Ignition OFF", "set ignition OFF."),
                FirstOfficer("apu-start", "APU started", "start the APU and wait for availability.", state => state.ApuAvailable),
                FirstOfficer("spoilers-disarm", "Ground spoilers retracted and disarmed", "retract and disarm the ground spoilers.", state => !state.GroundSpoilersArmed),
                FirstOfficer("transponder-standby", "Transponder/TCAS STBY or OFF", "set transponder and TCAS to the airport-required ground mode or standby."),
                FirstOfficer("radar-off", "Weather radar OFF", "set weather radar OFF."),
                FirstOfficer("pitch-trim-one", "Pitch trim 1 degree nose up", "set pitch trim to 1° nose up."),
                FirstOfficer("flaps-retract", "Slats/flaps retracted in stages", "retract to 0/0 in stages unless icing or contamination requires inspection after shutdown.", state => state.FlapsHandleIndex <= 0),
                Advisory("brake-temperature", "Brake temperatures reviewed; fans as required"),
                Captain("taxi-gate", "Taxi clearance and assigned gate confirmed", "obtain taxi clearance, confirm the assigned stand, and taxi to the gate."),
                Advisory("after-landing-checklist", "After Landing checklist reviewed")
            });

    public static ProcedureDefinition ParkingAndShutdown { get; } =
        new(
            "parking-shutdown",
            "12. A310 Parking & Securing",
            new[]
            {
                FirstOfficer("nose-off", "Nose light OFF approaching stand", "switch the nose light OFF before turning onto the stand."),
                Observe("parked", "Aircraft stationary at the gate with parking brake set", state => state.OnGround && state.GroundSpeedKnots <= 0.5 && state.ParkingBrakeSet),
                FirstOfficer("apu-bleed", "APU bleed and electrical power established", "establish APU bleed and electrical power, or verify external power is established.", state => state.ExternalPowerOn || state.ApuAvailable),
                Captain("fuel-levers-off", "Engine fuel levers OFF", "move both engine fuel levers OFF."),
                Observe("engines-off", "Both engines spooled down", state => state.EnginesOff),
                FirstOfficer("clock-beacon", "Elapsed time stopped and beacon OFF", "stop elapsed time and switch the beacon OFF after both engines spool below 20% N2.", state => !state.BeaconOn),
                Advisory("cabin-pressure", "Cabin differential pressure check due before doors open"),
                FirstOfficer("seatbelts-off", "Seat-belt signs OFF", "set seat-belt signs OFF."),
                FirstOfficer("fuel-pumps-parking", "Fuel pumps set for parking", "switch fuel pumps OFF, retaining only left inner tank Pump 2 when required to feed a running APU."),
                FirstOfficer("probe-heat-off", "Probe heat OFF", "switch probe heat OFF."),
                Advisory("irs-brakes", "IRS error and brake-fan review as required"),
                Manual("secure-decision", "Choose final secure or follow-up flight", "Captain and First Officer: press Confirm now to continue to final cold-and-dark secure. For a follow-up flight, press Cancel to keep the aircraft on APU or external power.", CrewRole.Either),
                FirstOfficer("irs-off", "IRS units OFF", "set all three IRS units OFF and wait at least 10 seconds before removing electrical power."),
                FirstOfficer("oxygen-off", "Crew oxygen OFF", "switch crew oxygen supply OFF."),
                FirstOfficer("lights-displays-off", "Exterior lights and CRTs OFF", "switch all exterior lights and CRT displays OFF."),
                FirstOfficer("apu-bleed-off", "APU bleed OFF", "switch APU BLEED OFF."),
                FirstOfficer("external-power-secure", "External power established as required", "connect external power before APU shutdown when available."),
                FirstOfficer("apu-off", "APU OFF", "switch APU MASTER OFF, then switch the retained left-inner Pump 2 OFF."),
                FirstOfficer("emergency-lights-disarm", "Emergency exit lights DISARMED", "disarm the emergency exit lights."),
                FirstOfficer("batteries-off", "BAT 1, BAT 2 and BAT 3 OFF", "switch all three batteries OFF.")
            });
}
