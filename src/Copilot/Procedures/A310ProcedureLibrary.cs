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
                Automatic(
                    "takeoff-flaps",
                    "Slats 15 / Flaps 0 set for takeoff",
                    state => state.FlapsHandleIndex >= 1,
                    "takeoff-flaps 15-0"),
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
                Advisory("brake-fans", "Brake fans OFF and temperatures acceptable; delay takeoff if brake temperature is excessive"),
                Automatic("takeoff-lights", "Takeoff exterior lights set", state => state.A310TakeoffExteriorLightsSet, "takeoff-lights"),
                Automatic("ignition-takeoff", "Ignition CONT RELIGHT", state => state.A310IgnitionContinuousRelight, "ignition takeoff"),
                Automatic("packs-takeoff", "Packs ON for takeoff", state => state.A310PacksOn, "packs on"),
                Automatic("tcas-tara", "TCAS TA/RA", state => state.A310TcasTaRaSet, "tcas tara"),
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
                Advisory("clock-start", "Takeoff announced and elapsed-time clock started"),
                Observe("thrust-set", "Takeoff thrust established", state => state.Engine1N1Percent >= 70 && state.Engine2N1Percent >= 70, CrewRole.Captain),
                Observe("fo-100-knots", "One hundred knots", state => state.HundredKnotsCalloutReached),
                Observe("v1", "V1", state => state.V1CalloutReached),
                Observe("rotate", "Rotate", state => state.RotateCalloutReached),
                Observe("positive-climb", "Positive climb", state => !state.OnGround && state.VerticalSpeedFeetPerMinute > 100),
                // The A310's generic three-wheel position SimVars can remain stale
                // after retraction. Its native handle readback is authoritative:
                // 0=UP, 1=transit and 2=DOWN.
                Automatic("fo-gear-up", "Landing gear UP", state => state.GearHandleUp, "gear up"),
                Advisory("autopilot", "Autopilot as required"),
                Observe("thrust-reduction", "Thrust-reduction altitude reached", state => state.AltitudeAboveGroundFeet >= 1000),
                Advisory("climb-thrust", "Climb thrust established; TRP CL and symmetric thrust checked"),
                Advisory("flaps-zero", "Flaps remain zero; retract slats at the displayed S speed"),
                FirstOfficer("slats-zero", "Slats zero at or above S speed", "retract slats to zero at or above S speed.", state => state.FlapsHandleIndex <= 0),
                Automatic("fo-ground-spoilers-disarm", "Spoilers DISARMED", state => !state.GroundSpoilersArmed, "speedbrake disarm"),
                Advisory("gear-off", "Landing-gear lever retained in the normal retracted position"),
                Automatic("packs-on", "Packs ON", state => state.A310PacksOn, "packs on"),
                Automatic("climb-lights", "Nose and runway-turnoff lights OFF", state => state.A310ClimbLightsSet, "climb-lights"),
                Automatic("apu-climb", "APU OFF", state => !state.ApuBleedOn && !state.ApuMasterSwitchOn, "apu off"),
                Observe("transition-altitude", "Transition altitude reached", state => state.IndicatedAltitudeFeet >= state.TransitionAltitudeFeet),
                Automatic("altimeters-standard", "Altimeters STANDARD", state => state.CaptainAltimeterStandard && state.FirstOfficerAltimeterStandard, "altimeters standard"),
                Observe("ten-thousand", "10,000 feet passed", state => state.IndicatedAltitudeFeet >= 10000),
                Automatic("landing-lights-retract", "Landing lights RETRACT/OFF", state => state.A310LandingLightsRetracted, "landing-lights retract"),
                Advisory("seatbelts-climb", "Seat-belt signs retained or set as conditions require"),
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
                Advisory("descent-anti-ice", "Anti-ice and CONT RELIGHT as required for conditions"),
                Advisory("qnh", "Set and cross-check destination QNH below transition level"),
                Observe("below-ten-thousand", "At or below 10,000 feet", state => state.IndicatedAltitudeFeet <= 10000),
                Automatic("approach-signs-lights", "Seat belts ON and approach lights set", state => state.A310ApproachLightsSet, "approach-lights"),
                Observe("slats-point", "Slats 15 point", state => ApproachGate(state, state.ApproachFlaps1DistanceNm, state.IndicatedAltitudeFeet <= state.ApproachFlaps1AltitudeFeet)),
                Observe("slats-speed", "Slats 15 speed safe", state => state.IndicatedAirspeedKnots <= 245),
                Automatic("slats-15", "Slats 15 / Flaps 0", state => state.FlapsHandleIndex >= 1, "flaps 15-0"),
                Advisory("land-mode", "Arm LAND when cleared and monitor LOC/G/S capture"),
                Observe(
                    "flaps-15-point",
                    "Flaps 15 point",
                    state => state.IndicatedAirspeedKnots <= 210
                             || ApproachGate(
                                 state,
                                 state.ApproachFlaps2DistanceNm,
                                 state.AltitudeAboveGroundFeet <= 2000)),
                Observe("flaps-15-speed", "Flaps 15 speed safe", state => state.IndicatedAirspeedKnots <= 210),
                Automatic("flaps-15", "Slats 15 / Flaps 15", state => state.FlapsHandleIndex >= 2, "flaps 15-15"),
                Automatic("speedbrakes-retracted", "Speedbrakes retracted", state => !state.GroundSpoilersArmed, "speedbrake disarm"),
                Observe("gear-point", "Latest gear-down point", state => ApproachGate(state, 5, state.AltitudeAboveGroundFeet <= 1800)),
                Automatic("fo-gear-down", "Landing gear DOWN", state => state.GearHandleDown, "gear down"),
                Automatic("fo-spoilers-arm", "Ground spoilers ARMED", state => state.GroundSpoilersArmed, "speedbrake arm"),
                Automatic("nose-to", "Nose light T.O.", state => state.A310NoseLightTakeoff, "nose-light takeoff"),
                Observe("flaps-20-speed", "Flaps 20 speed safe", state => state.IndicatedAirspeedKnots <= 195),
                Automatic("flaps-20", "Slats 15 / Flaps 20", state => state.FlapsHandleIndex >= 3, "flaps 15-20"),
                Observe("flaps-40-speed", "Landing flap speed safe", state => state.IndicatedAirspeedKnots <= 180),
                Automatic("flaps-40", "Slats 30 / Flaps 40", state => state.FlapsHandleIndex >= 4, "flaps 30-40"),
                Observe("configured-1000", "Fully configured by 1,000 feet AGL", state => state.AltitudeAboveGroundFeet <= 1000 && state.GearHandleDown && state.FlapsHandleIndex >= 4),
                Advisory("stable-500", "Stable by 500 feet AGL; go around if unstable"),
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
                Automatic("after-landing-lights", "After-landing lights set", state => state.A310AfterLandingLightsSet, "after-landing-lights"),
                Advisory("anti-ice-after-landing", "Anti-ice OFF unless required for taxi"),
                Automatic("ignition-off", "Ignition OFF", state => state.A310IgnitionOff, "ignition off"),
                Automatic("apu-start", "APU started", state => state.ApuAvailable, "apu start"),
                Automatic("spoilers-disarm", "Ground spoilers retracted and disarmed", state => !state.GroundSpoilersArmed, "speedbrake disarm"),
                Automatic("transponder-standby", "Transponder/TCAS STBY", state => state.A310TransponderStandby, "transponder-radar standby"),
                Automatic("radar-off", "Weather radar OFF", state => state.A310WeatherRadarOff, "transponder-radar standby"),
                Advisory("pitch-trim-one", "Pitch trim set to 1 degree nose up when practical"),
                Automatic("flaps-retract", "Slats/flaps retracted", state => state.FlapsHandleIndex <= 0, "flaps retract"),
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
                Automatic("nose-off", "Nose light OFF approaching stand", state => state.A310NoseLightOff, "nose-light off"),
                Observe("parked", "Aircraft stationary at the gate with parking brake set", state => state.OnGround && state.GroundSpeedKnots <= 0.5 && state.ParkingBrakeSet),
                Automatic("apu-bleed", "APU bleed and electrical power established", state => state.ExternalPowerOn || state.A310ApuPowerAndBleedSet, "apu power-bleed"),
                Captain("fuel-levers-off", "Engine fuel levers OFF", "move both engine fuel levers OFF.", state => !state.A310Engine1FuelLeverOn && !state.A310Engine2FuelLeverOn),
                Observe("engines-off", "Both engines spooled down", state => state.EnginesOff),
                Automatic("clock-beacon", "Elapsed time stopped and beacon OFF", state => !state.BeaconOn, "beacon off"),
                Advisory("cabin-pressure", "Cabin differential pressure check due before doors open"),
                Automatic("seatbelts-off", "Seat-belt signs OFF", state => state.A310SeatbeltsOff, "seatbelts off"),
                Automatic("fuel-pumps-parking", "Fuel pumps set for parking", state => state.A310FuelPumpsParkingSet, "fuel-pumps parking"),
                Automatic("probe-heat-off", "Probe heat OFF", state => state.A310ProbeHeatOff, "probe-heat off"),
                Advisory("irs-brakes", "IRS error and brake-fan review as required"),
                Manual("secure-decision", "Choose final secure or follow-up flight", "Captain and First Officer: press Confirm now to continue to final cold-and-dark secure. For a follow-up flight, press Cancel to keep the aircraft on APU or external power.", CrewRole.Either),
                Automatic("irs-off", "IRS units OFF", state => state.A310IrsOff, "irs off"),
                Automatic("oxygen-off", "Crew oxygen OFF", state => state.A310OxygenOff, "oxygen off"),
                Automatic("lights-displays-off", "Exterior lights OFF; CRTs as required", state => state.A310ExteriorLightsOff, "exterior-lights off"),
                Automatic("apu-bleed-off", "APU bleed OFF", state => !state.ApuBleedOn, "apu-bleed off"),
                FirstOfficer("external-power-secure", "External power established as required", "connect external power before APU shutdown when available."),
                Automatic("apu-off", "APU OFF", state => !state.ApuMasterSwitchOn, "apu off"),
                Automatic("apu-fuel-pump-off", "Retained APU fuel pump OFF", state => state.A310FuelPumpsParkingSet, "fuel-pumps parking"),
                Automatic("emergency-lights-disarm", "Emergency exit lights DISARMED", state => state.A310EmergencyExitDisarmed, "emergency-exit disarm"),
                Automatic("batteries-off", "BAT 1, BAT 2 and BAT 3 OFF", state => state.A310BatteriesOff, "batteries off")
            });
}
