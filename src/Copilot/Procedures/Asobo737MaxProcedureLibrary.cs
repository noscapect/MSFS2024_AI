using Msfs2024Ai.Copilot.Domain;

namespace Msfs2024Ai.Copilot.Procedures;

internal static class Asobo737MaxProcedureLibrary
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
            "after-start" => "after-start-taxi",
            "taxi" => "after-start-taxi",
            "takeoff-initial-climb" => "takeoff-climb",
            "climb-to-cruise" => "takeoff-climb",
            _ => id
        };

        return GateToGate.FirstOrDefault(
            procedure => string.Equals(procedure.Id, canonicalId, StringComparison.OrdinalIgnoreCase));
    }

    private static ProcedureStep Observe(
        string id,
        string label,
        Func<AircraftState, bool> complete,
        CrewRole role = CrewRole.FirstOfficer,
        Func<AircraftState, bool>? recoveryComplete = null) =>
        new(
            id,
            label,
            ProcedureStepKind.Observe,
            complete,
            role,
            isCompleteWhenRecovering: recoveryComplete);

    private static ProcedureStep Manual(
        string id,
        string label,
        string instruction,
        CrewRole role,
        Func<AircraftState, bool>? complete = null,
        Func<AircraftState, bool>? recoveryComplete = null) =>
        new(
            id,
            label,
            ProcedureStepKind.ManualAction,
            complete ?? (_ => false),
            role,
            manualInstruction: instruction,
            isCompleteWhenRecovering: recoveryComplete);

    private static ProcedureStep Automatic(
        string id,
        string label,
        Func<AircraftState, bool> complete,
        string command,
        CrewRole role = CrewRole.FirstOfficer,
        Func<AircraftState, bool>? recoveryComplete = null,
        bool requireCommandExecution = true) =>
        new(
            id,
            label,
            ProcedureStepKind.AutomaticAction,
            complete,
            role,
            command,
            isCompleteWhenRecovering: recoveryComplete,
            requireCommandExecution: requireCommandExecution);

    private static bool ApproachDistanceReached(AircraftState state, int maximumDistanceNm) =>
        state.ApproachDistanceToTouchdownNm.HasValue
        && state.ApproachDistanceToTouchdownNm.Value > 0
        && state.ApproachDistanceToTouchdownNm.Value <= maximumDistanceNm;

    public static ProcedureDefinition PowerUpAndInitialSetup { get; } =
        new(
            "power-up-initial-setup",
            "1. 737 MAX Power Up & Initial Setup",
            new[]
            {
                Observe("aircraft", "Supported Asobo 737 MAX 8 loaded", state => state.IsAsobo737Max8),
                Observe("stationary", "Aircraft stationary on the ground", state => state.OnGround && state.GroundSpeedKnots <= 0.5),
                Observe("engines-off", "Engines off", state => state.EnginesOff),
                Manual("captain-battery", "BATTERY switch ON", "Captain: set the BATTERY switch ON.", CrewRole.Captain, state => state.Battery1On),
                Manual("captain-standby-power", "STANDBY POWER AUTO", "Captain: verify STANDBY POWER is AUTO.", CrewRole.Captain),
                Manual("captain-ground-power-available", "Ground power available", "Captain: connect ground power if GRD POWER AVAILABLE is not shown.", CrewRole.Captain, state => state.ExternalPowerAvailable),
                Manual("captain-external-power", "Ground power ON", "Captain: switch GRD POWER ON and verify the aircraft is powered.", CrewRole.Captain, state => state.ExternalPowerOn),
                Automatic(
                    "fo-fire-tests",
                    "Fire detection/extinguisher tests",
                    state => state.ApuFireTestCompleted
                             && state.Engine1FireTestCompleted
                             && state.Engine2FireTestCompleted,
                    "asobo737max fire-tests"),
                Automatic("fo-irs-left", "Left IRS selector NAV", state => state.Adirs1SelectorState >= 2, "asobo737max irs left nav"),
                Automatic("fo-irs-right", "Right IRS selector NAV", state => state.Adirs2SelectorState >= 2, "asobo737max irs right nav"),
                Automatic("fo-position", "Position lights STEADY", state => state.NavigationLightsOn, "asobo737max position steady"),
                Automatic("fo-logo", "Logo light ON", state => state.LogoLightsOn, "asobo737max logo on"),
                Automatic("fo-emergency-lights-armed", "Emergency exit lights ARMED", state => state.EmergencyExitSelectorPosition.HasValue && Math.Abs(state.EmergencyExitSelectorPosition.Value - 1) < 0.1, "asobo737max emergency-exit arm")
            });

    public static ProcedureDefinition FlightComputerAndPreFlight { get; } =
        new(
            "flight-computer-preflight",
            "2. 737 MAX FMC & Pre-Flight",
            new[]
            {
                Observe("electrical-power", "Electrical power established", state => state.Battery1On && state.CockpitDisplaysReady),
                Manual("captain-fd-qnh", "Flight Directors ON and local QNH set", "Captain: turn both Flight Directors ON and set local QNH.", CrewRole.Captain),
                Manual("captain-displays", "PFD/ND/EICAS checked", "Captain: verify displays and annunciations.", CrewRole.Captain),
                Manual("captain-parking-brake", "Parking brake ON", "Captain: verify parking brake ON.", CrewRole.Captain, state => state.ParkingBrakeSet),
                Manual("fmc-pos-init", "FMC POS INIT / IRS position set", "Captain: on the FMC POS INIT page, enter or copy the present position to SET IRS POS.", CrewRole.Captain),
                Manual("fmc-route", "FMC route complete", "Captain: enter route, departure, arrival and performance data.", CrewRole.Captain),
                Manual("fmc-perf", "FMC TAKEOFF REF complete", "Captain: enter takeoff performance, V-speeds and takeoff flaps.", CrewRole.Captain),
                Manual("captain-ifr-clearance", "IFR clearance received", "Pilot: after completing FMC setup, request and acknowledge IFR clearance.", CrewRole.Captain, state => state.AtcClearedIfr),
                Automatic("fo-fuel-pumps", "Fuel pumps ON as required", state => state.FuelPumpsConfigured, "asobo737max fuel-pumps on"),
                Automatic("fo-seatbelts-auto", "Fasten belts AUTO/ON", state => state.SeatbeltSelectorPosition.HasValue && Math.Abs(state.SeatbeltSelectorPosition.Value - 1) < 0.1, "asobo737max seatbelts set"),
                Automatic("fo-no-smoking-auto", "No smoking AUTO/ON", state => state.NoSmokingSelectorPosition.HasValue && Math.Abs(state.NoSmokingSelectorPosition.Value - 1) < 0.1, "asobo737max no-smoking set"),
                Manual("irs-aligned", "FMC POS INIT / IRS alignment verified", "Captain: verify the FMC present position is set and IRS alignment is complete with no unexpected IRS messages.", CrewRole.Captain)
            });

    public static ProcedureDefinition ApuStartAndPushback { get; } =
        new(
            "apu-start-pushback",
            "3. 737 MAX APU Start & Pushback",
            new[]
            {
                Observe("stationary", "Aircraft stationary with parking brake set", state => state.OnGround && state.GroundSpeedKnots <= 0.5 && state.ParkingBrakeSet),
                Manual("captain-apu-on", "APU selector ON", "Captain: move APU selector to ON.", CrewRole.Captain, state => state.ApuMasterSwitchOn),
                Manual("captain-apu-start", "APU selector START", "Captain: hold APU selector to START, then release to ON.", CrewRole.Captain, state => state.ApuStartButtonOn || state.ApuAvailable),
                Observe("apu-available", "APU available", state => state.ApuAvailable, CrewRole.Captain),
                Manual("fo-apu-generators", "APU generators ON", "First Officer: connect APU generators to the busses.", CrewRole.FirstOfficer),
                Manual("fo-apu-bleed", "APU bleed ON", "First Officer: set APU bleed ON.", CrewRole.FirstOfficer, state => state.ApuBleedOn),
                Manual("fo-isolation-open", "Isolation valve OPEN", "First Officer: set isolation valve OPEN for engine start.", CrewRole.FirstOfficer),
                Manual("fo-packs-auto", "PACK switches AUTO", "First Officer: set PACK switches AUTO until start flow.", CrewRole.FirstOfficer),
                Manual("fo-ground-power-off", "Ground power switch OFF", "First Officer: remove ground power after APU generator power is established.", CrewRole.FirstOfficer),
                Manual("captain-beacon", "Anti-collision light ON", "Captain: turn anti-collision light ON.", CrewRole.Captain, state => state.BeaconOn),
                Manual("captain-pushback-clearance", "Pushback and engine-start clearance received", "Pilot: request and acknowledge pushback and engine-start clearance.", CrewRole.Captain),
                Observe("fo-doors", "Cabin and cargo doors closed", state => state.RequiredDoorsClosed, CrewRole.FirstOfficer)
            });

    public static ProcedureDefinition EngineStartSequence { get; } =
        new(
            "engine-start-sequence",
            "4. 737 MAX Engine Start Sequence",
            new[]
            {
                Observe("start-condition", "Aircraft on ground with anti-collision ON", state => state.OnGround && state.BeaconOn),
                Manual("fo-packs-off", "PACK switches OFF", "First Officer: set PACK switches OFF for engine start.", CrewRole.FirstOfficer),
                Manual("fo-isolation-open", "Isolation valve OPEN", "First Officer: verify isolation valve OPEN.", CrewRole.FirstOfficer),
                Manual("captain-engine-two-start", "Engine 2 start switch GRD", "Captain: move Engine 2 start switch to GRD.", CrewRole.Captain, state => state.Engine2StarterActive || state.Engine2Running),
                Observe("fo-engine-two-starter", "Engine 2 - Starter Valve Open", state => state.Engine2StarterActive || state.Engine2StartStabilized, recoveryComplete: state => state.Engine2StartStabilized),
                Manual("captain-engine-two-start-lever", "Engine 2 start lever IDLE", "Captain: at 25% N2, move Engine 2 start lever to IDLE.", CrewRole.Captain, state => state.Engine2FuelFlowDetected || state.Engine2Running, recoveryComplete: state => state.Engine2StartStabilized),
                Observe("fo-engine-two-stable", "Engine 2 - Stabilized", state => state.Engine2StartStabilized),
                Manual("captain-engine-one-start", "Engine 1 start switch GRD", "Captain: move Engine 1 start switch to GRD.", CrewRole.Captain, state => state.Engine1StarterActive || state.Engine1Running),
                Observe("fo-engine-one-starter", "Engine 1 - Starter Valve Open", state => state.Engine1StarterActive || state.Engine1StartStabilized, recoveryComplete: state => state.Engine1StartStabilized),
                Manual("captain-engine-one-start-lever", "Engine 1 start lever IDLE", "Captain: at 25% N2, move Engine 1 start lever to IDLE.", CrewRole.Captain, state => state.Engine1FuelFlowDetected || state.Engine1Running, recoveryComplete: state => state.Engine1StartStabilized),
                Observe("fo-engine-one-stable", "Engine 1 - Stabilized", state => state.Engine1StartStabilized),
                Manual("captain-start-switches-cont", "Engine start switches CONT", "Captain: set engine start switches CONT as required.", CrewRole.Captain)
            });

    public static ProcedureDefinition AfterStartAndTaxi { get; } =
        new(
            "after-start-taxi",
            "5. 737 MAX After Start & Taxi",
            new[]
            {
                Manual("fo-engine-generators", "Engine generators ON", "First Officer: connect engine generators.", CrewRole.FirstOfficer),
                Manual("fo-electric-hydraulic-pumps", "Electric hydraulic pumps ON", "First Officer: set electric hydraulic pumps ON.", CrewRole.FirstOfficer),
                Manual("fo-apu-bleed-off", "APU bleed OFF", "First Officer: set APU bleed OFF.", CrewRole.FirstOfficer),
                Manual("fo-packs-auto", "PACK switches AUTO", "First Officer: set PACK switches AUTO.", CrewRole.FirstOfficer),
                Manual("fo-isolation-auto", "Isolation valve AUTO", "First Officer: set isolation valve AUTO.", CrewRole.FirstOfficer),
                Manual("fo-apu-off", "APU selector OFF", "First Officer: set APU selector OFF.", CrewRole.FirstOfficer),
                Observe("fo-speedbrake-down", "Speedbrake DOWN verified", _ => true),
                Manual("fo-flaps-takeoff", "Flaps takeoff setting", "First Officer: set takeoff flaps.", CrewRole.FirstOfficer),
                Manual("fo-autobrake-rto", "Autobrake RTO", "First Officer: set autobrake RTO.", CrewRole.FirstOfficer),
                Manual("fo-taxi-light", "Taxi light ON", "First Officer: set taxi light ON.", CrewRole.FirstOfficer),
                Manual("fo-runway-turnoff-on", "Runway turnoff lights ON", "First Officer: set runway turnoff lights ON when cleared to taxi.", CrewRole.FirstOfficer),
                Manual("fo-taxi-clearance", "Taxi clearance received", "First Officer: press Confirm now to request taxi clearance through SayIntentions.", CrewRole.FirstOfficer, state => !state.SayIntentionsAtcActive),
                Observe("captain-taxi-started", "Captain started taxi", state => state.OnGround && state.GroundSpeedKnots > 1)
            });

    public static ProcedureDefinition BeforeTakeoff { get; } =
        new(
            "before-takeoff",
            "6. 737 MAX Before Takeoff",
            new[]
            {
                Observe("holding-short", "Aircraft stopped near runway", state => state.OnGround && state.GroundSpeedKnots <= 1),
                Manual("captain-takeoff-briefing", "Takeoff briefing complete", "Captain: complete takeoff briefing.", CrewRole.Captain),
                Manual("captain-trim-green-band", "Stabilizer trim set for takeoff", "Captain: verify stabilizer trim is set in the green takeoff range.", CrewRole.Captain),
                Manual("fo-autothrottle-arm", "Autothrottle ARM", "First Officer: arm autothrottle.", CrewRole.FirstOfficer),
                Manual("fo-lnav-vnav", "LNAV/VNAV armed as required", "First Officer: arm LNAV/VNAV as briefed.", CrewRole.FirstOfficer),
                Manual("fo-landing-lights", "Landing lights ON", "First Officer: set landing lights ON.", CrewRole.FirstOfficer),
                Manual("fo-taxi-light-off", "Taxi light OFF", "First Officer: set taxi light OFF for takeoff.", CrewRole.FirstOfficer),
                Manual("fo-strobes", "Position/strobe STROBE & STEADY", "First Officer: set strobes ON.", CrewRole.FirstOfficer),
                Manual("fo-transponder-tara", "Transponder TA/RA", "First Officer: set transponder TA/RA.", CrewRole.FirstOfficer),
                Observe("cabin-ready", "Cabin crew, prepare for takeoff", _ => true),
                Manual("fo-takeoff-clearance", "Takeoff clearance received", "First Officer: while holding short, press Confirm now to report ready for departure and request takeoff clearance through SayIntentions.", CrewRole.FirstOfficer, state => !state.SayIntentionsAtcActive)
            });

    public static ProcedureDefinition TakeoffAndClimb { get; } =
        new(
            "takeoff-climb",
            "7. 737 MAX Takeoff & Climb",
            new[]
            {
                Observe("thrust-set", "Thrust set", state => state.Engine1N1Percent >= 40 && state.Engine2N1Percent >= 40),
                Observe("hundred-knots", "100 knots", state => state.IndicatedAirspeedKnots >= 100),
                Observe("v1", "V1", state => state.IndicatedAirspeedKnots >= state.TakeoffV1SpeedKnots),
                Observe("rotate", "Rotate", state => state.IndicatedAirspeedKnots >= state.TakeoffRotateSpeedKnots),
                Observe("airborne", "Positive climb", state => !state.OnGround && state.AltitudeAboveGroundFeet >= 35),
                Manual("fo-gear-up", "Landing gear UP", "First Officer: set landing gear UP.", CrewRole.FirstOfficer, state => state.GearHandleUp),
                Observe("acceleration-altitude", "Acceleration altitude passed", state => !state.OnGround && state.AltitudeAboveGroundFeet >= 1000),
                Manual("fo-flaps-up", "Flaps retracted on schedule", "First Officer: retract flaps on schedule.", CrewRole.FirstOfficer, state => state.FlapsHandleIndex <= 0),
                Observe("ten-thousand-feet", "10,000 feet passed", state => state.IndicatedAltitudeFeet >= 10000),
                Manual("fo-lights-above-ten", "Exterior lights set above 10,000 feet", "First Officer: set landing/runway turnoff/logo lights for climb above 10,000 feet.", CrewRole.FirstOfficer)
            });

    public static ProcedureDefinition Cruise { get; } =
        new(
            "cruise",
            "8. 737 MAX Cruise",
            new[]
            {
                Observe("cruise-established", "Cruise established", state => state.CruiseEstablished),
                Observe("systems-monitor", "Systems monitored", _ => true)
            });

    public static ProcedureDefinition DescentPreparation { get; } =
        new(
            "descent-preparation",
            "9. 737 MAX Descent Preparation",
            new[]
            {
                Manual("captain-fmc-arrival", "FMC arrival and approach entered", "Captain: set arrival, approach, landing runway and descent forecast.", CrewRole.Captain),
                Manual("captain-vref", "Landing data and VREF set", "Captain: select landing flap and VREF.", CrewRole.Captain),
                Manual("captain-ils-approach", "ILS approach setup checked", "Captain: verify NAV radios, course selectors, flight directors and APP/LOC/GS guidance are set as required for the planned approach.", CrewRole.Captain),
                Manual("captain-briefing", "Approach briefing complete", "Captain: complete approach briefing.", CrewRole.Captain)
            });

    public static ProcedureDefinition ApproachAndLanding { get; } =
        new(
            "approach-landing",
            "10. 737 MAX Approach & Landing",
            new[]
            {
                Observe("descent-established", "Descent established", state => !state.OnGround && (state.VerticalSpeedFeetPerMinute <= -300 || state.IndicatedAltitudeFeet <= 10000)),
                Observe("below-ten-thousand", "10,000 feet passed", state => !state.OnGround && state.IndicatedAltitudeFeet <= 10000),
                Manual("fo-approach-lights", "Exterior lights ON below 10,000 feet", "First Officer: set logo, runway turnoff and landing lights ON.", CrewRole.FirstOfficer),
                Manual("fo-autobrake", "Autobrake set for landing", "First Officer: set landing autobrake.", CrewRole.FirstOfficer),
                Manual("fo-seatbelts-on", "Fasten belts ON", "First Officer: set fasten belts ON.", CrewRole.FirstOfficer),
                Observe("cabin-landing", "Cabin crew, prepare for landing", _ => true),
                Observe("flaps-one-gate", "Flaps 1 point reached", state => ApproachDistanceReached(state, state.ApproachFlaps1DistanceNm) || state.IndicatedAirspeedKnots <= state.EffectiveApproachFlaps1SpeedKnots),
                Manual("fo-flaps-one", "Flaps 1", "First Officer: set flaps 1.", CrewRole.FirstOfficer),
                Observe("flaps-five-gate", "Flaps 5 point reached", state => ApproachDistanceReached(state, state.ApproachFlaps2DistanceNm) || state.IndicatedAirspeedKnots <= state.EffectiveApproachFlaps2SpeedKnots),
                Manual("fo-flaps-five", "Flaps 5", "First Officer: set flaps 5.", CrewRole.FirstOfficer),
                Observe("gear-gate", "Gear-down point reached", state => ApproachDistanceReached(state, state.ApproachGearDistanceNm) || state.AltitudeAboveGroundFeet <= state.ApproachGearAltitudeAglFeet),
                Manual("fo-gear-down", "Landing gear DOWN", "First Officer: set landing gear DOWN.", CrewRole.FirstOfficer, state => state.GearHandleDown),
                Manual("fo-flaps-fifteen", "Flaps 15", "First Officer: set flaps 15.", CrewRole.FirstOfficer),
                Manual("fo-spoilers-arm", "Speedbrake armed", "First Officer: arm speedbrake.", CrewRole.FirstOfficer),
                Observe("landing-config-point", "Landing-configuration point reached", state => ApproachDistanceReached(state, state.ApproachLandingConfigDistanceNm) || state.AltitudeAboveGroundFeet <= state.ApproachLandingConfigAltitudeAglFeet),
                Manual("fo-flaps-landing", "Landing flaps set", "First Officer: set landing flaps.", CrewRole.FirstOfficer),
                Observe("approaching-minimums", "Approaching Minimums", state => state.RadioHeightFeet > 0 && state.RadioHeightFeet <= state.DecisionHeightFeet + 100),
                Observe("minimums", "Minimums", state => state.RadioHeightFeet > 0 && state.RadioHeightFeet <= state.DecisionHeightFeet),
                Observe("touchdown", "Touchdown", state => state.OnGround && state.GroundSpeedKnots > 30),
                Observe("landing-rollout", "Spoilers, Reverse Green, Decel", state => state.OnGround && state.GroundSpeedKnots <= 120)
            });

    public static ProcedureDefinition AfterLandingAndTaxi { get; } =
        new(
            "after-landing-taxi",
            "11. 737 MAX After Landing & Taxi",
            new[]
            {
                Observe("on-ground", "Aircraft on the ground", state => state.OnGround),
                Observe("reverse-stowed", "Reverse thrust stowed below 70 knots", state => state.OnGround && state.GroundSpeedKnots <= 70 && !state.ReverseThrustEngaged),
                Manual("fo-after-landing", "After landing items complete", "First Officer: set autobrake OFF, lights for taxi, transponder standby, speedbrake down and flaps up.", CrewRole.FirstOfficer),
                Manual("fo-apu-start", "APU started for taxi-in", "First Officer: start APU as required for arrival.", CrewRole.FirstOfficer, state => state.ApuAvailable || state.ApuSpoolingOrAvailable)
            });

    public static ProcedureDefinition ParkingAndShutdown { get; } =
        new(
            "parking-shutdown",
            "12. 737 MAX Parking & Shutdown",
            new[]
            {
                Observe("parked", "Aircraft parked at gate", state => state.OnGround && state.GroundSpeedKnots <= 0.5),
                Observe("parking-brake", "Parking brake ON", state => state.ParkingBrakeSet),
                Manual("captain-engine-masters", "Fuel cutoff levers CUTOFF", "Captain: move both fuel cutoff levers to CUTOFF.", CrewRole.Captain, state => state.EnginesOff),
                Manual("fo-shutdown", "Shutdown items complete", "First Officer: set beacon off, runway turnoff lights off and fuel pumps off.", CrewRole.FirstOfficer),
                Manual("captain-secure", "Choose final secure or follow-up flight", "Captain: complete the cold-and-dark secure checklist and press Confirm now, or press Cancel for a follow-up flight and remain on APU or ground power.", CrewRole.Captain)
            });
}
