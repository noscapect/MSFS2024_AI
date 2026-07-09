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
        ApproachDistanceReached(state, state.ApproachGearDistanceNm)
            || (!state.ApproachDistanceToTouchdownNm.HasValue
                && state.AltitudeAboveGroundFeet <= state.ApproachGearAltitudeAglFeet);

    private static bool ApproachLandingConfigGateReached(AircraftState state) =>
        ApproachDistanceReached(state, state.ApproachLandingConfigDistanceNm)
            || (!state.ApproachDistanceToTouchdownNm.HasValue
                && state.AltitudeAboveGroundFeet <= state.ApproachLandingConfigAltitudeAglFeet);

    private static bool BoeingApproachSpeedSafe(
        AircraftState state,
        int scheduledSpeedKnots,
        int absoluteLimitKnots) =>
        state.IsSupportedBoeing737
            ? state.IndicatedAirspeedKnots <= absoluteLimitKnots
            : state.IndicatedAirspeedKnots <= scheduledSpeedKnots;

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
                Manual("captain-ground-power-available", "Ground power available", "Captain: connect ground power through PMDG ground services/EFB if GRD POWER AVAILABLE is not shown.", CrewRole.Captain, state => state.ExternalPowerAvailable),
                Manual("captain-external-power", "Ground power ON", "Captain: switch GRD POWER ON and verify the aircraft is powered.", CrewRole.Captain, state => state.ExternalPowerOn),
                Automatic("fo-irs-left", "Left IRS selector NAV", state => Math.Abs(state.Adirs1SelectorState - 2) < 0.1, "pmdg irs left nav"),
                Automatic("fo-irs-right", "Right IRS selector NAV", state => Math.Abs(state.Adirs2SelectorState - 2) < 0.1, "pmdg irs right nav"),
                Observe("irs-on-dc-extinguished", "IRS ON DC lights extinguished", state => state.PmdgIrsOnDcExtinguished),
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
                Observe("electrical-power", "Electrical power established", state => state.Battery1On && state.ExternalPowerOn && state.CockpitDisplaysReady),
                Manual("captain-fd-qnh", "Flight Directors ON and local QNH set", "Captain: turn both Flight Directors ON and set local QNH.", CrewRole.Captain),
                Manual("captain-displays", "PFD/ND/EICAS checked", "Captain: verify displays and annunciations.", CrewRole.Captain),
                Manual("captain-parking-brake", "Parking brake ON", "Captain: verify parking brake ON.", CrewRole.Captain, state => state.ParkingBrakeSet),
                Manual("fmc-pos-init", "FMC POS INIT / IRS position set", "Captain: on the FMC POS INIT page, enter or copy the present position to SET IRS POS.", CrewRole.Captain),
                Manual("fmc-route", "FMC route complete", "Captain: enter route, departure, arrival and performance data.", CrewRole.Captain),
                Manual("fmc-perf", "FMC TAKEOFF REF complete", "Captain: enter V-speeds, transition altitude and takeoff flap setting.", CrewRole.Captain),
                Automatic("fo-fuel-pumps", "Fuel pumps ON as required", state => state.FuelPumpsConfigured, "pmdg fuel-pumps on"),
                Automatic("fo-seatbelts-auto", "Fasten belts AUTO", state => state.SeatbeltSelectorPosition.HasValue && Math.Abs(state.SeatbeltSelectorPosition.Value - 1) < 0.1, "pmdg seatbelts auto"),
                Automatic("fo-no-smoking-auto", "No smoking AUTO", state => state.NoSmokingSelectorPosition.HasValue && Math.Abs(state.NoSmokingSelectorPosition.Value - 1) < 0.1, "pmdg no-smoking auto"),
                Observe("irs-aligned", "IRS alignment complete", state => state.PmdgIrsReady)
            });

    public static ProcedureDefinition ApuStartAndPushback { get; } =
        new(
            "apu-start-pushback",
            "3. 737 APU Start & Pushback",
            new[]
            {
                Observe("stationary", "Aircraft stationary with parking brake set", state => state.OnGround && state.GroundSpeedKnots <= 0.5 && state.ParkingBrakeSet),
                Observe("irs-aligned", "IRS alignment complete", state => state.PmdgIrsReady),
                Manual("captain-apu-on", "APU selector ON", "Captain: move APU selector to ON.", CrewRole.Captain, state => state.ApuMasterSwitchOn),
                Manual("captain-apu-start", "APU selector START", "Captain: hold APU selector to START, then release to ON.", CrewRole.Captain, state => state.ApuStartButtonOn || state.ApuAvailable),
                Observe("apu-available", "APU available", state => state.ApuAvailable, CrewRole.Captain),
                Automatic("fo-apu-generators", "APU generators ON", state => state.ApuGeneratorPowerEstablished, "pmdg apu-generators on", requireCommandExecution: true),
                Observe("apu-generator-power", "APU generator power established", state => state.ApuGeneratorPowerEstablished, CrewRole.FirstOfficer),
                Observe("apu-bleed-warmup", "APU bleed warm-up complete", state => state.ApuBleedWarmupComplete || state.ApuBleedOn, CrewRole.FirstOfficer),
                Automatic("fo-apu-bleed", "APU bleed ON", state => state.ApuBleedOn, "pmdg apu-bleed on"),
                Automatic("fo-isolation-open", "Isolation valve OPEN", state => state.IsolationValveOpen, "pmdg isolation open"),
                Automatic("fo-packs-auto", "PACK switches AUTO", state => state.PacksAuto, "pmdg packs auto"),
                Automatic("fo-ground-power-off", "Ground power switch OFF", state => state.ApuGeneratorPowerEstablished && !state.ExternalPowerOn, "pmdg ground-power off", requireCommandExecution: true),
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
                Automatic("fo-packs-off", "PACK switches OFF", state => state.PacksOffForEngineStart, "pmdg packs off"),
                Automatic("fo-isolation-open", "Isolation valve OPEN", state => state.IsolationValveOpen, "pmdg isolation open"),
                Observe("fo-start-air", "Start air available", state => state.EngineStartAirAvailable),
                Manual("captain-engine-two-start", "Engine 2 start switch GRD", "Captain: move Engine 2 start switch to GRD.", CrewRole.Captain, state => state.Engine2StartSwitchGround || state.Engine2StarterActive || state.Engine2Running),
                Observe("fo-engine-two-starter", "Engine 2 — Starter Valve Open", state => state.Engine2StarterActive || state.Engine2StartStabilized, recoveryComplete: state => state.Engine2StartStabilized),
                Manual("captain-engine-two-start-lever", "Engine 2 start lever IDLE", "Captain: at 25% N2, move Engine 2 start lever to IDLE.", CrewRole.Captain, state => state.Engine2FuelFlowDetected || state.Engine2Running, recoveryComplete: state => state.Engine2StartStabilized),
                Observe("fo-engine-two-fuel", "Engine 2 — Fuel Flow", state => state.Engine2FuelFlowDetected || state.Engine2StartStabilized, recoveryComplete: state => state.Engine2StartStabilized),
                Observe("fo-engine-two-stable", "Engine 2 — Stabilized", state => state.Engine2StartStabilized),
                Manual("captain-engine-one-start", "Engine 1 start switch GRD", "Captain: move Engine 1 start switch to GRD.", CrewRole.Captain, state => state.Engine1StartSwitchGround || state.Engine1StarterActive || state.Engine1Running),
                Observe("fo-engine-one-starter", "Engine 1 — Starter Valve Open", state => state.Engine1StarterActive || state.Engine1StartStabilized, recoveryComplete: state => state.Engine1StartStabilized),
                Manual("captain-engine-one-start-lever", "Engine 1 start lever IDLE", "Captain: at 25% N2, move Engine 1 start lever to IDLE.", CrewRole.Captain, state => state.Engine1FuelFlowDetected || state.Engine1Running, recoveryComplete: state => state.Engine1StartStabilized),
                Observe("fo-engine-one-fuel", "Engine 1 — Fuel Flow", state => state.Engine1FuelFlowDetected || state.Engine1StartStabilized, recoveryComplete: state => state.Engine1StartStabilized),
                Observe("fo-engine-one-stable", "Engine 1 — Stabilized", state => state.Engine1StartStabilized),
                Manual("captain-start-switches-cont", "Engine start switches CONT", "Captain: set engine start switches CONT as required.", CrewRole.Captain, state => state.EngineStartSwitchesContinuous, recoveryComplete: state => state.Engine1Running && state.Engine2Running)
            });

    public static ProcedureDefinition AfterStartAndTaxi { get; } =
        new(
            "after-start-taxi",
            "5. 737 After Start & Taxi",
            new[]
            {
                Automatic("fo-engine-generators", "Engine generators ON", state => state.EngineGeneratorPowerEstablished, "pmdg engine-generators on", requireCommandExecution: true),
                Observe("fo-engine-generator-power", "Engine generator power established", state => state.EngineGeneratorPowerEstablished),
                Automatic("fo-electric-hydraulic-pumps", "Electric hydraulic pumps ON", state => state.BoeingElectricHydraulicPumpsOn, "pmdg electric-hydraulic-pumps on"),
                Automatic("fo-apu-bleed-off", "APU bleed OFF", state => !state.ApuBleedOn, "pmdg apu-bleed off"),
                Automatic("fo-packs-auto", "PACK switches AUTO", state => state.PacksAuto, "pmdg packs auto"),
                Automatic("fo-isolation-auto", "Isolation valve AUTO", state => state.IsolationValveAuto, "pmdg isolation auto"),
                Automatic("fo-apu-off", "APU selector OFF", state => !state.ApuMasterSwitchOn, "pmdg apu off"),
                Observe("fo-speedbrake-down", "Speedbrake DOWN verified", _ => true),
                Automatic("fo-flaps-takeoff", "Flaps takeoff setting", state => state.BoeingTakeoffFlapsSet, "pmdg flaps takeoff"),
                Automatic("fo-autobrake-rto", "Autobrake RTO", state => state.AutobrakeLevel.HasValue && Math.Abs(state.AutobrakeLevel.Value) < 0.1, "pmdg autobrake rto"),
                Automatic("fo-taxi-light", "Taxi light ON", state => state.NoseLightSelectorPosition.HasValue && state.NoseLightSelectorPosition.Value < 1.5, "pmdg taxi-light on"),
                Automatic("fo-runway-turnoff-on", "Runway turnoff lights ON", state => state.RunwayTurnoffLightsOn, "pmdg runway-turnoff on", requireCommandExecution: true),
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
                Manual("captain-trim-green-band", "Stabilizer trim set for takeoff", "Captain: verify stabilizer trim is set in the green takeoff range.", CrewRole.Captain),
                Automatic("fo-autothrottle-arm", "Autothrottle ARM", _ => true, "pmdg mcp autothrottle arm", requireCommandExecution: true),
                Automatic("fo-lnav-arm", "LNAV armed", _ => true, "pmdg mcp lnav arm", requireCommandExecution: true),
                Automatic("fo-vnav-arm", "VNAV armed", _ => true, "pmdg mcp vnav arm", requireCommandExecution: true),
                Automatic("fo-landing-lights", "Landing lights ON", state => state.LeftLandingLightSelectorPosition == 2 && state.RightLandingLightSelectorPosition == 2, "pmdg landing-lights on", requireCommandExecution: true),
                Automatic("fo-taxi-light-off", "Taxi light OFF", state => state.NoseLightSelectorPosition.HasValue && state.NoseLightSelectorPosition.Value >= 1.5, "pmdg taxi-light off"),
                Automatic("fo-strobes", "Position/strobe STROBE & STEADY", state => state.StrobeSelectorPosition.HasValue && Math.Abs(state.StrobeSelectorPosition.Value) < 0.1, "pmdg strobes on"),
                Automatic("fo-transponder-tara", "Transponder TA/RA", state => state.TcasMode.HasValue && state.TcasMode.Value >= 4, "pmdg transponder tara", requireCommandExecution: true),
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
                Automatic("fo-autobrake-off", "Autobrake OFF", state => state.AutobrakeLevel.HasValue && Math.Abs(state.AutobrakeLevel.Value - 1) < 0.1, "pmdg autobrake off", requireCommandExecution: true),
                Automatic("fo-gear-up", "Landing gear UP", state => state.GearHandleUp, "pmdg gear up"),
                Automatic("fo-taxi-light-off", "Taxi light OFF", state => state.NoseLightSelectorPosition.HasValue && state.NoseLightSelectorPosition.Value >= 1.5, "pmdg taxi-light off"),
                Observe("fo-spoilers-down", "Speedbrake DOWN verified", _ => true),
                Observe("acceleration-altitude", "Acceleration altitude passed", state => !state.OnGround && state.AltitudeAboveGroundFeet >= 1000),
                Automatic("fo-flaps-up", "Flaps retracted on schedule", state => state.FlapsHandleIndex <= 0, "pmdg flaps clean"),
                Observe("ten-thousand-feet", "10,000 feet passed", state => state.IndicatedAltitudeFeet >= 10000),
                Automatic("fo-runway-turnoff-off", "Runway turnoff lights OFF above 10,000 feet", state => !state.RunwayTurnoffLightsOn, "pmdg runway-turnoff off", requireCommandExecution: true),
                Automatic("fo-landing-lights-off", "Landing lights RETRACT above 10,000 feet", state => state.LeftLandingLightSelectorPosition == 0 && state.RightLandingLightSelectorPosition == 0, "pmdg landing-lights off", requireCommandExecution: true)
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
                Manual("captain-vref", "Landing data and VREF set", "Captain: select landing flap and VREF.", CrewRole.Captain, state => state.BoeingLandingDataSet),
                Manual("captain-ils-autoland", "ILS/autoland setup checked", "Captain: if using autoland, verify both NAV radios are tuned to the same ILS, both course selectors match the runway course, both FDs are ON, APP is armed/captured, LOC/GS are valid, and CMD B remains engaged for dual-channel approach.", CrewRole.Captain),
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
                Observe("landing-data-set", "Landing data and VREF available", state => state.BoeingLandingDataSet, CrewRole.Captain),
                Automatic("fo-autobrake", "Autobrake set for landing", state => state.AutobrakeLevel.HasValue && state.AutobrakeLevel.Value >= 2, "pmdg autobrake landing"),
                Automatic("fo-seatbelts-on", "Fasten belts ON", state => state.SeatbeltSignsOn, "pmdg seatbelts on"),
                Automatic("fo-runway-turnoff-on", "Runway turnoff lights ON", state => state.RunwayTurnoffLightsOn, "pmdg runway-turnoff on"),
                Automatic("fo-landing-lights-on", "Landing lights ON", state => state.LeftLandingLightSelectorPosition == 2 && state.RightLandingLightSelectorPosition == 2, "pmdg landing-lights on", requireCommandExecution: true),
                Observe("cabin-landing", "Cabin crew, prepare for landing", _ => true),
                Observe("flaps-one-gate", "Flaps 1 point reached", state => ApproachDistanceReached(state, state.ApproachFlaps1DistanceNm) || state.IndicatedAltitudeFeet <= state.ApproachFlaps1AltitudeFeet),
                Automatic("fo-flaps-one", "Flaps 1", state => state.BoeingFlapsAtSetting(1), "pmdg flaps 1"),
                Observe("flaps-five-speed", "Safe speed for flaps 5", state => BoeingApproachSpeedSafe(state, state.EffectiveApproachFlaps2SpeedKnots, 250)),
                Automatic("fo-flaps-five", "Flaps 5", state => state.BoeingFlapsAtSetting(5), "pmdg flaps 5"),
                Observe("gear-gate", "Gear-down point reached", ApproachGearGateReached),
                Automatic("fo-gear-down", "Landing gear DOWN", state => state.GearHandleDown, "pmdg gear down", requireCommandExecution: true),
                Observe("flaps-fifteen-speed", "Safe speed for flaps 15", state => BoeingApproachSpeedSafe(state, state.EffectiveApproachFlaps3SpeedKnots, 230)),
                Automatic("fo-flaps-fifteen", "Flaps 15", state => state.BoeingFlapsAtSetting(15), "pmdg flaps 15"),
                Automatic("fo-spoilers-arm", "Speedbrake armed", state => state.GroundSpoilersArmed, "pmdg spoilers arm"),
                Observe("landing-config-point", "Landing-configuration point reached", ApproachLandingConfigGateReached),
                Observe("landing-config-speed", "Safe speed for landing flaps", state => BoeingApproachSpeedSafe(state, state.EffectiveApproachFlapsFullSpeedKnots, 195)),
                Automatic("fo-flaps-landing", "Landing flaps set", state => state.BoeingLandingFlapsSet, "pmdg flaps landing"),
                Observe("stable-approach", "Stable approach by 1,000 feet AGL", state => state.RadioHeightFeet > 0 && state.RadioHeightFeet <= 1000 && state.BoeingApproachStable, CrewRole.FirstOfficer),
                Observe("approaching-minimums", "Approaching Minimums", state => state.RadioHeightFeet > 0 && state.RadioHeightFeet <= state.DecisionHeightFeet + 100),
                Observe("minimums", "Minimums", state => state.RadioHeightFeet > 0 && state.RadioHeightFeet <= state.DecisionHeightFeet),
                Observe("touchdown", "Touchdown", state => state.OnGround && state.GroundSpeedKnots > 30),
                Observe("landing-rollout", "Spoilers, Reverse Green, Decel", state => state.OnGround && state.GroundSpoilersDeployed && state.ReverseThrustEngaged && state.GroundSpeedKnots <= 120)
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
                Automatic("fo-landing-lights-off", "Landing lights RETRACT", state => state.LeftLandingLightSelectorPosition == 0 && state.RightLandingLightSelectorPosition == 0, "pmdg landing-lights off", requireCommandExecution: true),
                Automatic("fo-strobes-off", "Position lights STEADY", state => state.StrobeSelectorPosition.HasValue && Math.Abs(state.StrobeSelectorPosition.Value - 1) < 0.1, "pmdg strobes off"),
                Automatic("fo-runway-turnoff-on", "Runway turnoff lights ON for taxi", state => state.RunwayTurnoffLightsOn, "pmdg runway-turnoff on"),
                Automatic("fo-spoilers-down", "Speedbrake down", state => !state.GroundSpoilersArmed && !state.GroundSpoilersDeployed, "pmdg spoilers down"),
                Automatic("fo-flaps-up", "Flaps UP", state => state.FlapsHandleIndex <= 0, "pmdg flaps clean"),
                Automatic("fo-transponder-standby", "Transponder STBY", _ => true, "pmdg transponder stby", requireCommandExecution: true),
                Automatic("fo-apu-on", "APU selector ON", state => state.ApuMasterSwitchOn || state.ApuAvailable, "pmdg apu on"),
                Automatic("fo-apu-start", "APU start initiated", state => state.ApuSpoolingOrAvailable, "pmdg apu start"),
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
                Automatic("fo-runway-turnoff-off", "Runway turnoff lights OFF", state => !state.RunwayTurnoffLightsOn, "pmdg runway-turnoff off"),
                Automatic("fo-fuel-pumps-off", "Fuel pumps OFF", state => state.AllFuelPumpsOff, "pmdg fuel-pumps off"),
                Manual("captain-secure", "Aircraft secure as required", "Captain: complete secure checklist as required for turnaround or cold-and-dark.", CrewRole.Captain)
            });
}
