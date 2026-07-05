using Msfs2024Ai.Copilot.Domain;

namespace Msfs2024Ai.Copilot.Procedures;

internal static class B737ProcedureLibrary
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
        bool requireCommandExecution = false) =>
        new(
            id,
            label,
            ProcedureStepKind.AutomaticAction,
            complete,
            role,
            command,
            isCompleteWhenRecovering: recoveryComplete,
            requireCommandExecution: requireCommandExecution);

    private static bool ApproachDistanceReached(
        AircraftState state,
        int maximumDistanceNm) =>
        state.ApproachDistanceToTouchdownNm.HasValue
        && state.ApproachDistanceToTouchdownNm.Value > 0
        && state.ApproachDistanceToTouchdownNm.Value <= maximumDistanceNm;

    private static bool ApproachGearGateReached(AircraftState state) =>
        state.IndicatedAirspeedKnots <= state.ApproachGearSpeedKnots
        && (ApproachDistanceReached(state, state.ApproachGearDistanceNm)
            || (!state.ApproachDistanceToTouchdownNm.HasValue
                && state.AltitudeAboveGroundFeet <= state.ApproachGearAltitudeAglFeet));

    public static ProcedureDefinition PowerUpAndInitialSetup { get; } =
        new(
            "power-up-initial-setup",
            "1. 737 Power Up & Initial Setup",
            new[]
            {
                Observe("aircraft", "Supported PMDG 737-800 loaded", state => state.IsSupportedBoeing737),
                Observe("stationary", "Aircraft stationary on the ground", state => state.OnGround && state.GroundSpeedKnots <= 0.5),
                Observe("engines-off", "Engines off", state => state.EnginesOff),
                Manual("captain-battery", "BATTERY switch ON", "Captain: set the BATTERY switch ON.", CrewRole.Captain, state => state.Battery1On),
                Manual("captain-standby-power", "STANDBY POWER AUTO", "Captain: verify STANDBY POWER is AUTO.", CrewRole.Captain),
                Manual("captain-external-power", "Ground power ON when available", "Captain: when GRD POWER is available, switch ground power ON.", CrewRole.Captain, state => state.ExternalPowerOn),
                Automatic("fo-irs-left", "Left IRS selector NAV", state => Math.Abs(state.Adirs1SelectorState - 2) < 0.1, "pmdg irs left nav"),
                Automatic("fo-irs-right", "Right IRS selector NAV", state => Math.Abs(state.Adirs2SelectorState - 2) < 0.1, "pmdg irs right nav"),
                Automatic("fo-logo", "Logo light ON", state => state.LogoLightsOn, "pmdg logo on"),
                Automatic("fo-position", "Position lights STEADY", state => state.NavigationLightsOn, "pmdg position steady"),
                Automatic("fo-emergency-lights-armed", "Emergency exit lights ARMED", state => state.EmergencyExitSelectorPosition.HasValue && Math.Abs(state.EmergencyExitSelectorPosition.Value - 1) < 0.1, "pmdg emergency-exit arm")
            });

    public static ProcedureDefinition FlightComputerAndPreFlight { get; } =
        new(
            "flight-computer-preflight",
            "2. 737 FMC & Pre-Flight",
            new[]
            {
                Manual("captain-fd-qnh", "Flight Directors ON and local QNH set", "Captain: turn both Flight Directors ON and set local QNH.", CrewRole.Captain),
                Manual("captain-displays", "PFD/ND/EICAS checked", "Captain: verify displays and annunciations.", CrewRole.Captain),
                Manual("captain-parking-brake", "Parking brake ON", "Captain: verify parking brake ON.", CrewRole.Captain, state => state.ParkingBrakeSet),
                Manual("fmc-route", "FMC route complete", "Captain: enter route, departure, arrival and performance data.", CrewRole.Captain),
                Manual("fmc-perf", "FMC TAKEOFF REF complete", "Captain: enter V-speeds, transition altitude and takeoff flap setting.", CrewRole.Captain),
                Automatic("fo-fuel-pumps", "Fuel pumps ON as required", state => state.FuelPumpsConfigured, "pmdg fuel-pumps on"),
                Automatic("fo-seatbelts-auto", "Fasten belts AUTO", state => state.SeatbeltSelectorPosition.HasValue && Math.Abs(state.SeatbeltSelectorPosition.Value - 1) < 0.1, "pmdg seatbelts auto"),
                Automatic("fo-no-smoking-auto", "No smoking AUTO", state => state.NoSmokingSelectorPosition.HasValue && Math.Abs(state.NoSmokingSelectorPosition.Value - 1) < 0.1, "pmdg no-smoking auto")
            });

    public static ProcedureDefinition ApuStartAndPushback { get; } =
        new(
            "apu-start-pushback",
            "3. 737 APU Start & Pushback",
            new[]
            {
                Observe("stationary", "Aircraft stationary with parking brake set", state => state.OnGround && state.GroundSpeedKnots <= 0.5 && state.ParkingBrakeSet),
                Manual("captain-apu-on", "APU selector ON", "Captain: move APU selector to ON.", CrewRole.Captain, state => state.ApuMasterSwitchOn),
                Manual("captain-apu-start", "APU selector START", "Captain: hold APU selector to START, then release to ON.", CrewRole.Captain, state => state.ApuStartButtonOn || state.ApuAvailable),
                Observe("apu-available", "APU available", state => state.ApuAvailable, CrewRole.Captain),
                Automatic("fo-apu-generators", "APU generators ON", state => state.ApuGeneratorSwitchOn, "pmdg apu-generators on"),
                Automatic("fo-apu-bleed", "APU bleed ON", state => state.ApuBleedOn, "pmdg apu-bleed on"),
                Manual("captain-ground-power-off", "Ground power OFF", "Captain: switch ground power OFF after APU power is established.", CrewRole.Captain, state => !state.ExternalPowerOn),
                Manual("captain-beacon", "Anti-collision light ON", "Captain: turn anti-collision light ON.", CrewRole.Captain, state => state.BeaconOn),
                Manual("captain-ifr-clearance", "IFR clearance received", "Pilot: use the MSFS ATC system to request and acknowledge IFR clearance.", CrewRole.Captain, state => state.AtcClearedIfr),
                Manual("captain-pushback-clearance", "Pushback and engine-start clearance received", "Pilot: use the MSFS ATC system to request and acknowledge pushback and engine-start clearance.", CrewRole.Captain),
                Observe("fo-doors", "Cabin and cargo doors closed", state => state.RequiredDoorsClosed, CrewRole.FirstOfficer)
            });

    public static ProcedureDefinition EngineStartSequence { get; } =
        new(
            "engine-start-sequence",
            "4. 737 Engine Start Sequence",
            new[]
            {
                Observe("start-condition", "Aircraft on ground with anti-collision ON", state => state.OnGround && state.BeaconOn),
                Manual("captain-packs-off", "PACK switches OFF", "Captain: set both PACK switches OFF for engine start.", CrewRole.Captain),
                Manual("captain-isolation-open", "Isolation valve OPEN", "Captain: set isolation valve OPEN.", CrewRole.Captain),
                Manual("captain-engine-two-start", "Engine 2 start switch GRD", "Captain: move Engine 2 start switch to GRD.", CrewRole.Captain, state => state.Engine2StarterActive || state.Engine2Running),
                Observe("fo-engine-two-starter", "Engine 2 — Starter Valve Open", state => state.Engine2StarterActive, recoveryComplete: state => state.Engine2StartStabilized),
                Observe("fo-engine-two-fuel", "Engine 2 — Fuel Flow", state => state.Engine2FuelFlowDetected, recoveryComplete: state => state.Engine2StartStabilized),
                Observe("fo-engine-two-stable", "Engine 2 — Stabilized", state => state.Engine2StartStabilized),
                Manual("captain-engine-one-start", "Engine 1 start switch GRD", "Captain: move Engine 1 start switch to GRD.", CrewRole.Captain, state => state.Engine1StarterActive || state.Engine1Running),
                Observe("fo-engine-one-starter", "Engine 1 — Starter Valve Open", state => state.Engine1StarterActive, recoveryComplete: state => state.Engine1StartStabilized),
                Observe("fo-engine-one-fuel", "Engine 1 — Fuel Flow", state => state.Engine1FuelFlowDetected, recoveryComplete: state => state.Engine1StartStabilized),
                Observe("fo-engine-one-stable", "Engine 1 — Stabilized", state => state.Engine1StartStabilized),
                Manual("captain-start-switches-cont", "Engine start switches CONT", "Captain: set engine start switches CONT as required.", CrewRole.Captain, recoveryComplete: state => state.Engine1Running && state.Engine2Running)
            });

    public static ProcedureDefinition AfterStartAndTaxi { get; } =
        new(
            "after-start-taxi",
            "5. 737 After Start & Taxi",
            new[]
            {
                Automatic("fo-apu-bleed-off", "APU bleed OFF", state => !state.ApuBleedOn, "pmdg apu-bleed off"),
                Automatic("fo-apu-off", "APU selector OFF", state => !state.ApuMasterSwitchOn, "pmdg apu off"),
                Automatic("fo-ground-spoilers-arm", "Speedbrake armed", state => state.GroundSpoilersArmed, "pmdg spoilers arm"),
                Automatic("fo-flaps-takeoff", "Flaps takeoff setting", state => state.FlapsHandleIndex > 0, "pmdg flaps takeoff"),
                Automatic("fo-autobrake-rto", "Autobrake RTO", state => state.AutobrakeLevel.HasValue && Math.Abs(state.AutobrakeLevel.Value) < 0.1, "pmdg autobrake rto"),
                Automatic("fo-taxi-light", "Taxi light ON", state => state.NoseLightSelectorPosition.HasValue && state.NoseLightSelectorPosition.Value < 1.5, "pmdg taxi-light on"),
                Observe("captain-taxi-started", "Captain started taxi", state => state.OnGround && state.GroundSpeedKnots > 1)
            });

    public static ProcedureDefinition BeforeTakeoff { get; } =
        new(
            "before-takeoff",
            "6. 737 Before Takeoff",
            new[]
            {
                Observe("holding-short", "Aircraft stopped near runway", state => state.OnGround && state.GroundSpeedKnots <= 1),
                Manual("captain-takeoff-briefing", "Takeoff briefing complete", "Captain: complete takeoff briefing.", CrewRole.Captain),
                Automatic("fo-landing-lights", "Landing lights ON", state => state.LeftLandingLightSelectorPosition == 0 && state.RightLandingLightSelectorPosition == 0, "pmdg landing-lights on"),
                Automatic("fo-strobes", "Position/strobe STROBE & STEADY", state => state.StrobeSelectorPosition.HasValue && Math.Abs(state.StrobeSelectorPosition.Value) < 0.1, "pmdg strobes on"),
                Automatic("fo-transponder-tara", "Transponder TA/RA", state => state.TcasMode.HasValue && state.TcasMode.Value >= 4, "pmdg transponder tara"),
                Observe("cabin-ready", "Cabin crew, prepare for takeoff", _ => true)
            });

    public static ProcedureDefinition TakeoffAndClimb { get; } =
        new(
            "takeoff-climb",
            "7. 737 Takeoff & Climb",
            new[]
            {
                Observe("thrust-set", "Thrust set", state => state.Engine1N1Percent >= 40 && state.Engine2N1Percent >= 40),
                Observe("hundred-knots", "100 knots", state => state.IndicatedAirspeedKnots >= 100),
                Observe("v1", "V1", state => state.IndicatedAirspeedKnots >= state.TakeoffV1SpeedKnots),
                Observe("rotate", "Rotate", state => state.IndicatedAirspeedKnots >= state.TakeoffRotateSpeedKnots),
                Observe("airborne", "Positive climb", state => !state.OnGround && state.AltitudeAboveGroundFeet >= 35),
                Automatic("fo-gear-up", "Landing gear UP", state => !state.GearHandleDown, "pmdg gear up"),
                Observe("acceleration-altitude", "Acceleration altitude passed", state => !state.OnGround && state.AltitudeAboveGroundFeet >= 1000),
                Automatic("fo-flaps-up", "Flaps retracted on schedule", state => state.FlapsHandleIndex <= 0, "pmdg flaps clean"),
                Automatic("fo-landing-lights-off", "Landing lights OFF above 10,000 feet", state => state.IndicatedAltitudeFeet < 10000 || state.LeftLandingLightSelectorPosition == 2 && state.RightLandingLightSelectorPosition == 2, "pmdg landing-lights off")
            });

    public static ProcedureDefinition Cruise { get; } =
        new(
            "cruise",
            "8. 737 Cruise",
            new[]
            {
                Observe("cruise-established", "Cruise established", state => !state.OnGround && state.AltitudeAboveGroundFeet >= 10000 && Math.Abs(state.VerticalSpeedFeetPerMinute) < 300),
                Observe("systems-monitor", "Systems monitored", _ => true)
            });

    public static ProcedureDefinition DescentPreparation { get; } =
        new(
            "descent-preparation",
            "9. 737 Descent Preparation",
            new[]
            {
                Manual("captain-fmc-arrival", "FMC arrival and approach entered", "Captain: set arrival, approach, landing runway and descent forecast.", CrewRole.Captain),
                Manual("captain-vref", "Landing data and VREF set", "Captain: select landing flap and VREF.", CrewRole.Captain),
                Manual("captain-briefing", "Approach briefing complete", "Captain: complete approach briefing.", CrewRole.Captain)
            });

    public static ProcedureDefinition ApproachAndLanding { get; } =
        new(
            "approach-landing",
            "10. 737 Approach & Landing",
            new[]
            {
                Observe("descent-established", "Descent established", state => !state.OnGround && (state.VerticalSpeedFeetPerMinute <= -300 || state.IndicatedAltitudeFeet <= 10000)),
                Observe("below-ten-thousand", "10,000 feet passed", state => !state.OnGround && state.IndicatedAltitudeFeet <= 10000),
                Automatic("fo-autobrake", "Autobrake set for landing", state => state.AutobrakeLevel.HasValue && state.AutobrakeLevel.Value >= 2, "pmdg autobrake landing"),
                Automatic("fo-seatbelts-on", "Fasten belts ON", state => state.SeatbeltSignsOn, "pmdg seatbelts on"),
                Automatic("fo-landing-lights-on", "Landing lights ON", state => state.LeftLandingLightSelectorPosition == 0 && state.RightLandingLightSelectorPosition == 0, "pmdg landing-lights on"),
                Observe("cabin-landing", "Cabin crew, prepare for landing", _ => true),
                Observe("flaps-one-gate", "Flaps 1 point reached", state => ApproachDistanceReached(state, state.ApproachFlaps1DistanceNm) || state.IndicatedAltitudeFeet <= state.ApproachFlaps1AltitudeFeet),
                Automatic("fo-flaps-one", "Flaps 1", state => state.FlapsAtDetent(1), "pmdg flaps 1"),
                Observe("flaps-five-speed", "Safe speed for flaps 5", state => state.IndicatedAirspeedKnots <= state.EffectiveApproachFlaps2SpeedKnots),
                Automatic("fo-flaps-five", "Flaps 5", state => state.FlapsAtDetent(2), "pmdg flaps 5"),
                Observe("gear-gate", "Gear-down point reached", ApproachGearGateReached),
                Automatic("fo-gear-down", "Landing gear DOWN", state => state.GearHandleDown, "pmdg gear down"),
                Automatic("fo-spoilers-arm", "Speedbrake armed", state => state.GroundSpoilersArmed, "pmdg spoilers arm"),
                Observe("landing-config-speed", "Safe speed for landing flaps", state => state.IndicatedAirspeedKnots <= state.EffectiveApproachFlapsFullSpeedKnots),
                Automatic("fo-flaps-landing", "Landing flaps set", state => state.FlapsAtDetent(4), "pmdg flaps landing"),
                Observe("approaching-minimums", "Approaching Minimums", state => state.RadioHeightFeet > 0 && state.RadioHeightFeet <= state.DecisionHeightFeet + 100),
                Observe("minimums", "Minimums", state => state.RadioHeightFeet > 0 && state.RadioHeightFeet <= state.DecisionHeightFeet),
                Observe("touchdown", "Touchdown", state => state.OnGround && state.GroundSpeedKnots > 30),
                Observe("landing-rollout", "Spoilers, Reverse Green, Decel", state => state.OnGround && (state.GroundSpoilersDeployed || state.GroundSpeedKnots <= 80))
            });

    public static ProcedureDefinition AfterLandingAndTaxi { get; } =
        new(
            "after-landing-taxi",
            "11. 737 After Landing & Taxi",
            new[]
            {
                Observe("on-ground", "Aircraft on the ground", state => state.OnGround),
                Observe("reverse-stowed", "Reverse thrust stowed below 70 knots", state => state.OnGround && state.GroundSpeedKnots <= 70 && !state.ReverseThrustEngaged),
                Automatic("fo-autobrake-off", "Autobrake OFF", state => state.AutobrakeLevel.HasValue && Math.Abs(state.AutobrakeLevel.Value - 1) < 0.1, "pmdg autobrake off"),
                Observe("taxi-speed", "Taxi speed at or below 30 knots", state => state.OnGround && state.GroundSpeedKnots <= 30),
                Automatic("fo-landing-lights-off", "Landing lights OFF", state => state.LeftLandingLightSelectorPosition == 2 && state.RightLandingLightSelectorPosition == 2, "pmdg landing-lights off"),
                Automatic("fo-strobes-off", "Position lights STEADY", state => state.StrobeSelectorPosition.HasValue && Math.Abs(state.StrobeSelectorPosition.Value - 1) < 0.1, "pmdg strobes off"),
                Automatic("fo-spoilers-down", "Speedbrake down", state => !state.GroundSpoilersArmed, "pmdg spoilers down"),
                Automatic("fo-flaps-up", "Flaps UP", state => state.FlapsHandleIndex <= 0, "pmdg flaps clean"),
                Automatic("fo-transponder-standby", "Transponder STBY", state => state.TransponderStandby, "pmdg transponder stby"),
                Automatic("fo-apu-on", "APU started for gate", state => state.ApuMasterSwitchOn || state.ApuAvailable, "pmdg apu start"),
                Observe("apu-available", "APU available", state => state.ApuAvailable),
                Automatic("fo-apu-bleed-on", "APU bleed ON", state => state.ApuBleedOn, "pmdg apu-bleed on")
            });

    public static ProcedureDefinition ParkingAndShutdown { get; } =
        new(
            "parking-shutdown",
            "12. 737 Parking & Shutdown",
            new[]
            {
                Observe("parked", "Aircraft parked at gate", state => state.OnGround && state.GroundSpeedKnots <= 0.5),
                Observe("parking-brake", "Parking brake ON", state => state.ParkingBrakeSet),
                Observe("power-source", "APU available or ground power connected", state => state.ApuAvailable || state.ExternalPowerOn),
                Manual("captain-engine-masters", "Fuel cutoff levers CUTOFF", "Captain: move both fuel cutoff levers to CUTOFF.", CrewRole.Captain, state => state.EnginesOff),
                Automatic("fo-beacon-off", "Anti-collision OFF", state => !state.BeaconOn, "pmdg beacon off"),
                Automatic("fo-fuel-pumps-off", "Fuel pumps OFF", state => state.AllFuelPumpsOff, "pmdg fuel-pumps off"),
                Manual("captain-secure", "Aircraft secure as required", "Captain: complete secure checklist as required for turnaround or cold-and-dark.", CrewRole.Captain)
            });
}
