using Msfs2024Ai.Copilot.AircraftAdapters.Asobo737Max;
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
                Automatic("fo-irs-left", "Left IRS selector NAV", state => state.Adirs1SelectorState >= 2, "asobo737max irs left nav"),
                Automatic("fo-irs-right", "Right IRS selector NAV", state => state.Adirs2SelectorState >= 2, "asobo737max irs right nav"),
                Automatic(
                    "fo-fire-tests",
                    "Fire detection/extinguisher tests",
                    state => state.ApuFireTestCompleted
                             && state.Engine1FireTestCompleted
                             && state.Engine2FireTestCompleted,
                    "asobo737max fire-tests"),
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
                Automatic("fo-apu-generators", "APU generators ON", state => state.ApuGeneratorPowerEstablished, "asobo737max apu-generator force-on", requireCommandExecution: true),
                Automatic("fo-apu-bleed", "APU bleed ON", state => state.ApuBleedOn, "asobo737max apu-bleed on", requireCommandExecution: true),
                Automatic("fo-isolation-open", "Isolation valve OPEN", state => state.IsolationValveOpen, "asobo737max isolation force-open", requireCommandExecution: true),
                Automatic("fo-packs-auto", "PACK switches AUTO", state => state.PacksAuto, "asobo737max packs force-auto", requireCommandExecution: true),
                Automatic("fo-ground-power-off", "Ground power switch OFF", state => !state.ExternalPowerOn, "asobo737max ground-power off", requireCommandExecution: true),
                Automatic("fo-beacon", "Anti-collision light ON", state => state.BeaconOn, "asobo737max beacon on", requireCommandExecution: true),
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
                Automatic("fo-engine-bleeds-on", "Engine bleed switches ON", state => state.BoeingEngineBleedsOn, "asobo737max engine-bleeds on"),
                Automatic("fo-packs-off", "PACK switches OFF", state => state.PacksOffForEngineStart, "asobo737max packs off"),
                Automatic("fo-isolation-open", "Isolation valve OPEN", state => state.IsolationValveOpen, "asobo737max isolation open"),
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
                Automatic("fo-engine-generators", "Engine generators ON", state => state.EngineGeneratorsOn, "asobo737max engine-generators on"),
                Automatic("fo-electric-hydraulic-pumps", "Electric hydraulic pumps ON", state => state.BoeingElectricHydraulicPumpsOn, "asobo737max electric-hydraulic-pumps on"),
                Automatic("fo-apu-bleed-off", "APU bleed OFF", state => !state.ApuBleedOn, "asobo737max apu-bleed off"),
                Automatic("fo-packs-auto", "PACK switches AUTO", state => state.PacksAuto, "asobo737max packs auto"),
                Automatic("fo-isolation-auto", "Isolation valve AUTO", state => state.IsolationValveAuto, "asobo737max isolation auto"),
                Automatic("fo-apu-off", "APU selector OFF", state => !state.ApuMasterSwitchOn, "asobo737max apu off"),
                Observe("fo-speedbrake-down", "Speedbrake DOWN verified", _ => true),
                Automatic("fo-flaps-takeoff", "Flaps takeoff setting", state => state.BoeingTakeoffFlapsSet, "asobo737max flaps takeoff"),
                Automatic("fo-autobrake-rto", "Autobrake RTO", state => state.AutobrakeLevel.HasValue && Math.Abs(state.AutobrakeLevel.Value - 1) < 0.1, "asobo737max autobrake rto"),
                Automatic("fo-taxi-light", "Taxi light AUTO", state => state.NoseLightSelectorPosition.HasValue && Math.Abs(state.NoseLightSelectorPosition.Value - 1) < 0.1, "asobo737max taxi-light auto"),
                Automatic("fo-runway-turnoff-on", "Runway turnoff lights ON", state => state.RunwayTurnoffLightsOn, "asobo737max runway-turnoff on"),
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
                Automatic("fo-autothrottle-arm", "Autothrottle ARM", state => state.BoeingAutothrottleArmed, "asobo737max autothrottle arm"),
                Automatic("fo-lnav-arm", "LNAV armed", _ => true, "asobo737max lnav arm", requireCommandExecution: true),
                Automatic("fo-vnav-arm", "VNAV armed", _ => true, "asobo737max vnav arm", requireCommandExecution: true),
                Automatic("fo-landing-lights", "Landing lights ON", state => state.LeftLandingLightSelectorPosition.HasValue && state.RightLandingLightSelectorPosition.HasValue && Math.Abs(state.LeftLandingLightSelectorPosition.Value) < 0.1 && Math.Abs(state.RightLandingLightSelectorPosition.Value) < 0.1, "asobo737max landing-lights on"),
                Automatic("fo-taxi-light-off", "Taxi light OFF", state => state.NoseLightSelectorPosition.HasValue && Math.Abs(state.NoseLightSelectorPosition.Value - 2) < 0.1, "asobo737max taxi-light off"),
                Automatic("fo-strobes", "Position/strobe STROBE & STEADY", state => state.StrobeSelectorPosition.HasValue && Math.Abs(state.StrobeSelectorPosition.Value - 2) < 0.1, "asobo737max strobes on"),
                Automatic("fo-transponder-auto", "XPDR AUTO", state => state.TransponderModeSelectorPosition.HasValue && Asobo737MaxControlProfile.IsTransponderAuto(state.TransponderModeSelectorPosition.Value), "asobo737max transponder auto"),
                Automatic("fo-transponder-tara", "Transponder TA/RA", state => state.BoeingTransponderOperatingMode.HasValue && Asobo737MaxControlProfile.IsTransponderTaRa(state.BoeingTransponderOperatingMode.Value), "asobo737max transponder tara"),
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
                Observe("airborne", "Positive climb", state => !state.OnGround && state.AltitudeAboveGroundFeet >= 35 && state.VerticalSpeedFeetPerMinute > 100),
                Automatic("fo-gear-up", "Landing gear UP", state => state.GearHandleUp, "asobo737max gear up"),
                Observe("acceleration-altitude", "Acceleration altitude passed", state => !state.OnGround && state.AltitudeAboveGroundFeet >= 1500),
                Observe("flap-retraction-speed", "Flap retraction speed reached", state => !state.OnGround && state.TakeoffV2SpeedKnots.HasValue && state.IndicatedAirspeedKnots >= state.TakeoffV2SpeedKnots.Value + 40),
                Automatic("fo-flaps-up", "Flaps retracted on schedule", state => state.BoeingFlapsAtSetting(0), "asobo737max flaps clean"),
                Observe("ten-thousand-feet", "10,000 feet passed", state => state.IndicatedAltitudeFeet >= 10000),
                Automatic("fo-landing-lights-above-ten", "Landing lights OFF above 10,000 feet", state => state.LeftLandingLightSelectorPosition.HasValue && state.RightLandingLightSelectorPosition.HasValue && Math.Abs(state.LeftLandingLightSelectorPosition.Value - 1) < 0.1 && Math.Abs(state.RightLandingLightSelectorPosition.Value - 1) < 0.1, "asobo737max landing-lights off"),
                Automatic("fo-runway-turnoff-above-ten", "Runway turnoff lights OFF above 10,000 feet", state => !state.RunwayTurnoffLightsOn, "asobo737max runway-turnoff off")
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
                Automatic("fo-landing-lights-below-ten", "Landing lights ON below 10,000 feet", state => state.LeftLandingLightSelectorPosition.HasValue && state.RightLandingLightSelectorPosition.HasValue && Math.Abs(state.LeftLandingLightSelectorPosition.Value) < 0.1 && Math.Abs(state.RightLandingLightSelectorPosition.Value) < 0.1, "asobo737max landing-lights on"),
                Automatic("fo-runway-turnoff-below-ten", "Runway turnoff lights ON below 10,000 feet", state => state.RunwayTurnoffLightsOn, "asobo737max runway-turnoff on"),
                Manual("fo-autobrake", "Autobrake set for landing", "First Officer: set landing autobrake.", CrewRole.FirstOfficer),
                Manual("fo-seatbelts-on", "Fasten belts ON", "First Officer: set fasten belts ON.", CrewRole.FirstOfficer),
                Observe("cabin-landing", "Cabin crew, prepare for landing", _ => true),
                Observe("flaps-one-gate", "Flaps 1 point reached", state => ApproachDistanceReached(state, state.ApproachFlaps1DistanceNm) || state.IndicatedAirspeedKnots <= state.EffectiveApproachFlaps1SpeedKnots),
                Automatic("fo-flaps-one", "Flaps 1", state => state.BoeingFlapsAtSetting(1), "asobo737max flaps 1"),
                Observe("flaps-five-gate", "Flaps 5 point reached", state => ApproachDistanceReached(state, state.ApproachFlaps2DistanceNm) || state.IndicatedAirspeedKnots <= state.EffectiveApproachFlaps2SpeedKnots),
                Automatic("fo-flaps-five", "Flaps 5", state => state.BoeingFlapsAtSetting(5), "asobo737max flaps 5"),
                Observe("gear-gate", "Gear-down point reached", state => ApproachDistanceReached(state, state.ApproachGearDistanceNm) || state.AltitudeAboveGroundFeet <= state.ApproachGearAltitudeAglFeet),
                Automatic("fo-gear-down", "Landing gear DOWN", state => state.GearHandleDown, "asobo737max gear down"),
                Automatic("fo-flaps-fifteen", "Flaps 15", state => state.BoeingFlapsAtSetting(15), "asobo737max flaps 15"),
                Manual("fo-spoilers-arm", "Speedbrake armed", "First Officer: arm speedbrake.", CrewRole.FirstOfficer),
                Observe("landing-config-point", "Landing-configuration point reached", state => ApproachDistanceReached(state, state.ApproachLandingConfigDistanceNm) || state.AltitudeAboveGroundFeet <= state.ApproachLandingConfigAltitudeAglFeet),
                Automatic("fo-flaps-landing", "Landing flaps set", state => state.BoeingLandingFlapsSet, "asobo737max flaps landing"),
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
