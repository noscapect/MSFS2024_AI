using Msfs2024Ai.Copilot.Domain;

namespace Msfs2024Ai.Copilot.Procedures;

internal static class A320ProcedureLibrary
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

    public static IReadOnlyList<ProcedureDefinition> ThroughCruise => GateToGate;

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

    private static bool ApproachGateReached(
        AircraftState state,
        int maximumDistanceNm,
        bool fallbackReached,
        int maximumSpeedKnots) =>
        state.IndicatedAirspeedKnots <= maximumSpeedKnots
        && (ApproachDistanceReached(state, maximumDistanceNm)
            || fallbackReached);

    public static ProcedureDefinition PowerUpAndInitialSetup { get; } =
        new(
            "power-up-initial-setup",
            "1. Power Up & Initial Setup",
            new[]
            {
                Observe("aircraft", "A320neo V2 loaded", state => state.IsA320NeoV2),
                Observe("stationary", "Aircraft stationary on the ground", state => state.OnGround && state.GroundSpeedKnots <= 0.5),
                Observe("engines-off", "Engines off", state => state.EnginesOff),
                Manual("captain-batteries", "BAT 1 and BAT 2 ON", "Captain: turn BAT 1 and BAT 2 ON.", CrewRole.Captain, state => state.Battery1On && state.Battery2On),
                Manual("captain-external-power", "External power ON when available", "Captain: when EXT PWR shows AVAIL, turn external power ON.", CrewRole.Captain, state => state.ExternalPowerOn),
                Automatic("fo-adirs-1", "ADIRS 1 set to NAV", state => Math.Abs(state.Adirs1SelectorState - 1) < 0.1, "adirs-1 nav"),
                Observe("fo-adirs-1-on-bat", "ADIRS ON BAT extinguished after selector 1", state => !state.AdirsOnBattery),
                Automatic("fo-adirs-2", "ADIRS 2 set to NAV", state => Math.Abs(state.Adirs2SelectorState - 1) < 0.1, "adirs-2 nav"),
                Observe("fo-adirs-2-on-bat", "ADIRS ON BAT extinguished after selector 2", state => !state.AdirsOnBattery),
                Automatic("fo-adirs-3", "ADIRS 3 set to NAV", state => Math.Abs(state.Adirs3SelectorState - 1) < 0.1, "adirs-3 nav"),
                Automatic(
                    "fo-oxygen",
                    "Crew oxygen ON",
                    state => state.CrewOxygenOn,
                    "crew-oxygen on"),
                Automatic("fo-nav-logo", "NAV & LOGO lights ON", state => state.NavLogoSelectorPosition.HasValue && Math.Abs(state.NavLogoSelectorPosition.Value) < 0.1, "nav-logo 2"),
                Automatic(
                    "fo-strobe-auto",
                    "Strobe set to AUTO",
                    state => state.StrobeSelectorPosition.HasValue
                             && Math.Abs(state.StrobeSelectorPosition.Value - 1) < 0.1,
                    "strobe auto"),
                Observe(
                    "fo-display-initialization",
                    "Cockpit displays and warning systems initialized",
                    state => state.CockpitDisplaysReady,
                    CrewRole.FirstOfficer),
                Automatic(
                    "fo-apu-fire-test",
                    "APU fire test complete",
                    state => state.ApuFireTestCompleted,
                    "fire-test apu"),
                Automatic(
                    "fo-engine-one-fire-test",
                    "Engine 1 fire test complete",
                    state => state.Engine1FireTestCompleted,
                    "fire-test engine-1"),
                Automatic(
                    "fo-engine-two-fire-test",
                    "Engine 2 fire test complete",
                    state => state.Engine2FireTestCompleted,
                    "fire-test engine-2")
            });

    public static ProcedureDefinition FlightComputerAndPreFlight { get; } =
        new(
            "flight-computer-preflight",
            "2. Flight Computer & Pre-Flight",
            new[]
            {
                Manual("captain-fd-qnh", "Flight Director ON and local QNH set", "Captain: turn the Flight Director ON and set local QNH, then confirm.", CrewRole.Captain),
                Manual("captain-displays", "PFD and ND checked", "Captain: verify the PFD and ND are aligning with no unexpected errors, then confirm.", CrewRole.Captain),
                Manual("captain-parking-brake", "Parking brake ON with pressure indicated", "Captain: verify the parking brake is ON and brake pressure is indicated.", CrewRole.Captain, state => state.ParkingBrakeSet),
                Manual("mcdu-data", "MCDU DATA checked", "Captain: check aircraft database validity on the MCDU DATA page, then confirm.", CrewRole.Captain),
                Manual("mcdu-init-a", "MCDU INIT A complete", "Captain: enter FROM/TO, flight number, cost index and cruise altitude, then confirm.", CrewRole.Captain),
                Manual("mcdu-flight-plan", "MCDU flight plan complete", "Captain: enter the SID, route and STAR, then confirm.", CrewRole.Captain),
                Manual("mcdu-rad-nav", "MCDU RAD NAV checked", "Captain: verify navigation radios are auto-tuned, then confirm.", CrewRole.Captain),
                Manual("mcdu-init-b", "MCDU INIT B complete", "Captain: enter zero fuel weight and block fuel, then confirm.", CrewRole.Captain),
                Manual("mcdu-perf", "MCDU PERF complete", "Captain: enter V1, VR, V2, transition altitude and takeoff flap setting, then confirm.", CrewRole.Captain),
                Automatic("fo-fuel-pumps", "All six fuel pumps ON", state => state.FuelPumpsConfigured, "fuel-pumps on"),
                Automatic("fo-seatbelts-auto", "Seatbelt signs AUTO", state => state.SeatbeltSelectorPosition.HasValue && Math.Abs(state.SeatbeltSelectorPosition.Value - 1) < 0.1, "seatbelts auto"),
                Automatic("fo-no-smoking-auto", "No smoking signs AUTO", state => state.NoSmokingSelectorPosition.HasValue && Math.Abs(state.NoSmokingSelectorPosition.Value - 1) < 0.1, "no-smoking auto"),
                Automatic("fo-emergency-lights-armed", "Emergency exit lights ARMED", state => state.EmergencyExitSelectorPosition.HasValue && Math.Abs(state.EmergencyExitSelectorPosition.Value - 1) < 0.1, "emergency-exit arm")
            });

    public static ProcedureDefinition ApuStartAndPushback { get; } =
        new(
            "apu-start-pushback",
            "3. APU Start & Pushback",
            new[]
            {
                Observe("stationary", "Aircraft stationary with parking brake set", state => state.OnGround && state.GroundSpeedKnots <= 0.5 && state.ParkingBrakeSet),
                Manual("captain-apu-master", "APU MASTER ON", "Captain: press APU MASTER ON.", CrewRole.Captain, state => state.ApuMasterSwitchOn),
                Manual("captain-apu-start", "APU START selected after 3 seconds", "Captain: wait at least 3 seconds after APU MASTER, then press APU START.", CrewRole.Captain, state => state.ApuStartButtonOn),
                Observe("apu-available", "APU AVAIL", state => state.ApuAvailable, CrewRole.Captain),
                Manual("captain-apu-bleed", "APU BLEED ON", "Captain: once APU AVAIL is shown, turn APU BLEED ON.", CrewRole.Captain, state => state.ApuBleedOn),
                Manual("captain-external-power-off", "External power disconnected", "Captain: disconnect external power after APU power is established.", CrewRole.Captain, state => !state.ExternalPowerOn),
                Manual("captain-beacon", "Beacon ON", "Captain: turn the beacon ON.", CrewRole.Captain, state => state.BeaconOn),
                Manual(
                    "captain-ifr-clearance",
                    "IFR clearance received",
                    "Pilot: use the MSFS ATC system to request and acknowledge IFR clearance.",
                    CrewRole.Captain,
                    state => state.AtcClearedIfr),
                Manual(
                    "captain-pushback-clearance",
                    "Pushback and engine-start clearance received",
                    "Pilot: use the MSFS ATC system to request and acknowledge pushback and engine-start clearance, then confirm.",
                    CrewRole.Captain),
                Automatic(
                    "fo-transponder",
                    "Transponder AUTO",
                    state => state.TransponderModeSelectorPosition.HasValue
                             && Math.Abs(state.TransponderModeSelectorPosition.Value - 1) < 0.1,
                    "transponder auto"),
                Observe(
                    "fo-doors",
                    "All configured cabin and cargo doors closed",
                    state => state.RequiredDoorsClosed,
                    CrewRole.FirstOfficer)
            });

    public static ProcedureDefinition EngineStartSequence { get; } =
        new(
            "engine-start-sequence",
            "4. Engine Start Sequence",
            new[]
            {
                Observe("start-condition", "Aircraft on ground with beacon ON", state => state.OnGround && state.BeaconOn),
                Manual(
                    "captain-engine-mode-start",
                    "Engine mode selector IGN/START",
                    "Captain: set the engine mode selector to IGN/START, then confirm.",
                    CrewRole.Captain,
                    recoveryComplete: state => state.Engine1Running || state.Engine2Running),
                Manual("captain-engine-two", "Engine 2 master ON", "Captain: set Engine 2 Master ON.", CrewRole.Captain, state => state.Engine2StarterActive || state.Engine2Running),
                Observe(
                    "fo-engine-two-starter",
                    "Engine 2 — Starter Valve Open",
                    state => state.Engine2StarterActive,
                    recoveryComplete: state => state.Engine2StartStabilized),
                Observe(
                    "fo-engine-two-fuel",
                    "Engine 2 — Fuel Flow",
                    state => state.Engine2FuelFlowPph > 0,
                    recoveryComplete: state => state.Engine2StartStabilized),
                Observe("fo-engine-two-stable", "Engine 2 — Stabilized", state => state.Engine2StartStabilized),
                Manual("captain-engine-one", "Engine 1 master ON", "Captain: once Engine 2 is stable, set Engine 1 Master ON.", CrewRole.Captain, state => state.Engine1StarterActive || state.Engine1Running),
                Observe(
                    "fo-engine-one-starter",
                    "Engine 1 — Starter Valve Open",
                    state => state.Engine1StarterActive,
                    recoveryComplete: state => state.Engine1StartStabilized),
                Observe(
                    "fo-engine-one-fuel",
                    "Engine 1 — Fuel Flow",
                    state => state.Engine1FuelFlowPph > 0,
                    recoveryComplete: state => state.Engine1StartStabilized),
                Observe("fo-engine-one-stable", "Engine 1 — Stabilized", state => state.Engine1StartStabilized),
                Manual("captain-engine-mode-normal", "Engine mode selector NORM", "Captain: return the engine mode selector to NORM, then confirm.", CrewRole.Captain)
            });

    public static ProcedureDefinition AfterStartAndTaxi { get; } =
        new(
            "after-start-taxi",
            "5. After Start & Taxi",
            new[]
            {
                Observe("engines-running", "Both engines running", state => state.Engine1Running && state.Engine2Running),
                Automatic("fo-apu-bleed-off", "APU BLEED OFF", state => !state.ApuBleedOn, "apu-bleed off"),
                Automatic("fo-apu-master-off", "APU MASTER OFF", state => !state.ApuMasterSwitchOn, "apu-master off"),
                Automatic("fo-ground-spoilers", "Ground spoilers ARMED", state => state.GroundSpoilersArmed, "ground-spoilers arm"),
                Automatic("fo-takeoff-flaps", "Takeoff flaps CONFIG 1", state => state.FlapsAtDetent(1), "flaps config-1"),
                Automatic("fo-autobrake-max", "Auto-brake MAX", state => state.AutobrakeLevel.HasValue && Math.Abs(state.AutobrakeLevel.Value - 3) < 0.1, "autobrake max"),
                Automatic(
                    "fo-wxr-pws",
                    "WXR/PWS selector 1",
                    state => state.WeatherRadarPwsSelectorPosition.HasValue
                             && Math.Abs(
                                 state.WeatherRadarPwsSelectorPosition.Value - 0) < 0.1,
                    "wxr-pws 1",
                    requireCommandExecution: true),
                Automatic(
                    "fo-nose-light-taxi",
                    "Nose light TAXI",
                    state => state.NoseLightSelectorPosition.HasValue
                             && Math.Abs(state.NoseLightSelectorPosition.Value - 1) < 0.1,
                    "nose-light taxi"),
                Manual("fo-ecam", "ECAM checked", "First Officer: check for remaining memos or system warnings, then confirm.", CrewRole.FirstOfficer),
                Observe(
                    "captain-taxi",
                    "Captain commenced taxi",
                    state => state.OnGround
                             && !state.ParkingBrakeSet
                             && state.GroundSpeedKnots > 0.5,
                    CrewRole.FirstOfficer)
            });

    public static ProcedureDefinition BeforeTakeoff { get; } =
        new(
            "before-takeoff",
            "6. Before Takeoff",
            new[]
            {
                Manual("captain-runway-lights", "Runway turnoff lights ON", "Captain: when cleared to line up, turn runway turnoff lights ON, then confirm.", CrewRole.Captain),
                Manual("captain-briefing", "Takeoff briefing complete", "Captain: confirm the takeoff briefing is complete.", CrewRole.Captain),
                Observe(
                    "fo-cabin-call",
                    "Cabin crew, prepare for takeoff",
                    _ => true,
                    CrewRole.FirstOfficer),
                Automatic(
                    "fo-altitude-reporting",
                    "TCAS altitude reporting ON",
                    state => state.TcasAltitudeReportingOn == true,
                    "tcas altitude-reporting on"),
                Automatic(
                    "fo-tcas",
                    "TCAS TA/RA",
                    state => state.TcasMode.HasValue
                             && Math.Abs(state.TcasMode.Value - 2) < 0.1,
                    "tcas traffic tara"),
                Manual("fo-engine-anti-ice", "Engine anti-ice set as required", "First Officer: configure engine anti-ice for conditions, then confirm.", CrewRole.FirstOfficer),
                Automatic(
                    "fo-nose-light-takeoff",
                    "Nose light T.O.",
                    state => state.NoseLightSelectorPosition.HasValue
                             && Math.Abs(state.NoseLightSelectorPosition.Value) < 0.1,
                    "nose-light takeoff"),
                Automatic(
                    "fo-landing-lights-on",
                    "Landing lights ON",
                    state => state.LeftLandingLightSelectorPosition.HasValue
                             && state.RightLandingLightSelectorPosition.HasValue
                             && Math.Abs(state.LeftLandingLightSelectorPosition.Value) < 0.1
                             && Math.Abs(state.RightLandingLightSelectorPosition.Value) < 0.1,
                    "landing-lights on")
            });

    public static ProcedureDefinition TakeoffAndClimb { get; } =
        new(
            "takeoff-climb",
            "7. Takeoff & Climb",
            new[]
            {
                Observe(
                    "captain-takeoff",
                    "Thrust Set",
                    state => state.OnGround
                             && state.IndicatedAirspeedKnots > 20
                             && state.Engine1N1Percent >= 80
                             && state.Engine2N1Percent >= 80,
                    recoveryComplete: state => !state.OnGround),
                Observe(
                    "takeoff-roll",
                    "Takeoff roll",
                    state => state.OnGround && state.IndicatedAirspeedKnots > 40,
                    recoveryComplete: state => !state.OnGround),
                Observe(
                    "fo-100-knots",
                    "100 Knots",
                    state => state.OnGround && state.IndicatedAirspeedKnots >= 100,
                    recoveryComplete: state => !state.OnGround),
                Observe(
                    "fo-v1",
                    "V1",
                    state => state.OnGround
                             && state.IndicatedAirspeedKnots
                                >= state.TakeoffV1SpeedKnots,
                    recoveryComplete: state => !state.OnGround),
                Observe(
                    "fo-rotate",
                    "Rotate",
                    state => state.IndicatedAirspeedKnots
                             >= state.TakeoffRotateSpeedKnots,
                    recoveryComplete: state => !state.OnGround),
                Observe(
                    "positive-climb",
                    "Positive Climb",
                    state => !state.OnGround && state.VerticalSpeedFeetPerMinute > 100,
                    recoveryComplete: state => !state.OnGround
                                               && state.AltitudeAboveGroundFeet >= 100),
                Automatic(
                    "fo-ground-spoilers-disarm",
                    "Ground spoilers DISARMED",
                    state => !state.GroundSpoilersArmed,
                    "ground-spoilers disarm"),
                Automatic(
                    "fo-gear-up",
                    "Landing gear UP",
                    state => !state.GearHandleDown,
                    "gear up"),
                Automatic(
                    "fo-nose-light-off",
                    "Nose light OFF",
                    state => state.NoseLightSelectorPosition.HasValue
                             && Math.Abs(state.NoseLightSelectorPosition.Value - 2) < 0.1,
                    "nose-light off"),
                Observe(
                    "captain-autopilot",
                    "AP1 engaged when selected",
                    state => state.AltitudeAboveGroundFeet >= 400
                             || state.AutopilotMasterOn,
                    CrewRole.FirstOfficer),
                Observe(
                    "captain-climb-detent",
                    "Thrust reduction altitude passed",
                    state => state.AltitudeAboveGroundFeet >= 1500,
                    CrewRole.FirstOfficer),
                Automatic(
                    "fo-flaps",
                    "Flaps retracted on schedule",
                    state => state.FlapsAtDetent(0),
                    "flaps clean"),
                Observe(
                    "above-ten-thousand",
                    "10,000 feet passed",
                    state => state.IndicatedAltitudeFeet >= 10000,
                    CrewRole.FirstOfficer),
                Automatic(
                    "fo-landing-lights-off",
                    "Landing lights RETRACTED",
                    state => state.LeftLandingLightSelectorPosition.HasValue
                             && state.RightLandingLightSelectorPosition.HasValue
                             && Math.Abs(state.LeftLandingLightSelectorPosition.Value - 2) < 0.1
                             && Math.Abs(state.RightLandingLightSelectorPosition.Value - 2) < 0.1,
                    "landing-lights retract")
            });

    public static ProcedureDefinition Cruise { get; } =
        new(
            "cruise",
            "8. Cruise",
            new[]
            {
                Observe("cruise-established", "Cruise established", state => !state.OnGround && state.AltitudeAboveGroundFeet >= 10000 && Math.Abs(state.VerticalSpeedFeetPerMinute) < 300),
                Observe(
                    "smooth-cruise",
                    "Smooth cruise conditions",
                    state => state.GForce >= 0.85
                             && state.GForce <= 1.15
                             && Math.Abs(state.VerticalSpeedFeetPerMinute) < 300),
                Automatic(
                    "fo-seatbelts-cruise",
                    "Seatbelt signs OFF in smooth cruise",
                    state => state.SeatbeltSelectorPosition.HasValue
                             && Math.Abs(state.SeatbeltSelectorPosition.Value - 2) < 0.1,
                    "seatbelts off")
            });

    public static ProcedureDefinition DescentPreparation { get; } =
        new(
            "descent-preparation",
            "9. Descent Preparation",
            new[]
            {
                Manual(
                    "captain-arrival-data",
                    "Arrival and approach entered in the flight computer",
                    "Captain: select and verify the arrival, approach, STAR and transition in the MCDU, then confirm.",
                    CrewRole.Captain),
                Manual(
                    "captain-destination-data",
                    "Destination weather and approach data entered",
                    "Captain: enter destination QNH, temperature, wind and minima in the MCDU PERF APPR page, then confirm.",
                    CrewRole.Captain),
                Manual(
                    "captain-descent-review",
                    "Descent flight-computer setup reviewed",
                    "Captain: review the descent profile, constraints and landing configuration data, then confirm.",
                    CrewRole.Captain)
            });

    public static ProcedureDefinition ApproachAndLanding { get; } =
        new(
            "approach-landing",
            "10. Approach & Landing",
            new[]
            {
                Automatic(
                    "fo-landing-autobrake-low",
                    "Landing auto-brake LOW",
                    state => state.AutobrakeLevel.HasValue
                             && Math.Abs(state.AutobrakeLevel.Value - 1) < 0.1,
                    "autobrake low"),
                Observe(
                    "captain-descent",
                    "Descent established or below 10,000 feet",
                    state => !state.OnGround
                             && (state.VerticalSpeedFeetPerMinute < -300
                                 || state.IndicatedAltitudeFeet <= 10000),
                    CrewRole.FirstOfficer),
                Observe(
                    "below-ten-thousand",
                    "Below 10,000 feet",
                    state => state.IndicatedAltitudeFeet <= 10000,
                    CrewRole.FirstOfficer),
                Automatic(
                    "fo-seatbelts-on",
                    "Seatbelt signs ON",
                    state => state.SeatbeltSelectorPosition.HasValue
                             && Math.Abs(state.SeatbeltSelectorPosition.Value) < 0.1,
                    "seatbelts on"),
                Automatic(
                    "fo-landing-lights-on",
                    "Landing lights ON",
                    state => state.LeftLandingLightSelectorPosition.HasValue
                             && state.RightLandingLightSelectorPosition.HasValue
                             && Math.Abs(state.LeftLandingLightSelectorPosition.Value) < 0.1
                             && Math.Abs(state.RightLandingLightSelectorPosition.Value) < 0.1,
                    "landing-lights on"),
                Automatic(
                    "fo-nose-light-taxi",
                    "Nose light TAXI",
                    state => state.NoseLightSelectorPosition.HasValue
                             && Math.Abs(state.NoseLightSelectorPosition.Value - 1) < 0.1,
                    "nose-light taxi"),
                Observe(
                    "approach-config-start",
                    "Approach configuration point",
                    state => ApproachGateReached(
                        state,
                        state.ApproachFlaps1DistanceNm,
                        state.IndicatedAltitudeFeet
                            <= state.ApproachFlaps1AltitudeFeet,
                        state.ApproachFlaps1SpeedKnots),
                    CrewRole.FirstOfficer),
                Observe(
                    "fo-cabin-landing-call",
                    "Cabin crew, prepare for landing",
                    _ => true,
                    CrewRole.FirstOfficer),
                Automatic(
                    "fo-flaps-one",
                    "Flaps CONFIG 1",
                    state => state.FlapsAtDetent(1),
                    "flaps config-1"),
                Observe(
                    "flaps-two-point",
                    "Flaps 2 point",
                    state => ApproachGateReached(
                        state,
                        state.ApproachFlaps2DistanceNm,
                        state.AltitudeAboveGroundFeet
                            <= state.ApproachFlaps2AltitudeAglFeet,
                        state.ApproachFlaps2SpeedKnots),
                    CrewRole.FirstOfficer),
                Automatic(
                    "fo-flaps-two",
                    "Flaps CONFIG 2",
                    state => state.FlapsAtDetent(2),
                    "flaps config-2"),
                Observe(
                    "gear-down-point",
                    "Gear-down point",
                    state => ApproachGateReached(
                        state,
                        state.ApproachGearDistanceNm,
                        state.AltitudeAboveGroundFeet
                            <= state.ApproachGearAltitudeAglFeet,
                        state.ApproachGearSpeedKnots),
                    CrewRole.FirstOfficer),
                Automatic(
                    "fo-gear-down",
                    "Landing gear DOWN",
                    state => state.GearHandleDown,
                    "gear down"),
                Automatic(
                    "fo-ground-spoilers",
                    "Ground spoilers ARMED",
                    state => state.GroundSpoilersArmed,
                    "ground-spoilers arm"),
                Observe(
                    "landing-config-point",
                    "Landing configuration point",
                    state => ApproachGateReached(
                        state,
                        state.ApproachLandingConfigDistanceNm,
                        state.AltitudeAboveGroundFeet
                            <= state.ApproachLandingConfigAltitudeAglFeet,
                        state.ApproachLandingConfigSpeedKnots),
                    CrewRole.FirstOfficer),
                Automatic(
                    "fo-flaps-three",
                    "Flaps CONFIG 3",
                    state => state.FlapsAtDetent(3),
                    "flaps config-3"),
                Automatic(
                    "fo-flaps-full",
                    "Flaps FULL",
                    state => state.FlapsAtDetent(4),
                    "flaps full"),
                Observe(
                    "fo-approaching-minimums",
                    "Approaching minimums",
                    state => state.DecisionHeightFeet > 0
                             && state.RadioHeightFeet
                                <= state.DecisionHeightFeet + 100,
                    CrewRole.FirstOfficer),
                Observe(
                    "fo-minimums",
                    "Minimums",
                    state => state.DecisionHeightFeet > 0
                             && state.RadioHeightFeet
                                <= state.DecisionHeightFeet,
                    CrewRole.FirstOfficer),
                Observe(
                    "captain-landing",
                    "Touchdown",
                    state => state.OnGround,
                    CrewRole.FirstOfficer),
                Observe(
                    "fo-spoilers-callout",
                    "Spoilers",
                    state => state.OnGround
                             && state.GroundSpoilersDeployed,
                    CrewRole.FirstOfficer),
                Observe(
                    "fo-reverse-callout",
                    "Reverse green",
                    state => state.OnGround
                             && (state.ReverseThrustEngaged
                                 || state.GroundSpeedKnots < 40),
                    CrewRole.FirstOfficer),
                Observe(
                    "fo-decel-callout",
                    "Decel",
                    state => state.OnGround
                             && (state.AutobrakesActive
                                 || state.GroundSpeedKnots < 80),
                    CrewRole.FirstOfficer)
            });

    public static ProcedureDefinition AfterLandingAndTaxi { get; } =
        new(
            "after-landing-taxi",
            "11. After Landing & Taxi",
            new[]
            {
                Observe("on-ground", "Aircraft on the ground", state => state.OnGround),
                Observe(
                    "captain-deceleration",
                    "Reverse stowed by 70 knots",
                    state => state.OnGround
                             && state.GroundSpeedKnots <= 70
                             && !state.ReverseThrustEngaged,
                    CrewRole.FirstOfficer),
                Automatic(
                    "fo-autobrake-off",
                    "Autobrake OFF",
                    state => state.AutobrakeLevel.HasValue
                             && Math.Abs(state.AutobrakeLevel.Value) < 0.1,
                    "autobrake off"),
                Automatic(
                    "fo-apu-master-on",
                    "APU MASTER ON",
                    state => state.ApuMasterSwitchOn,
                    "apu-master on"),
                Observe("fo-apu-flap-open", "APU intake flap open", state => state.ApuFlapPercent >= 0.95),
                Automatic("fo-apu-start-on", "APU START selected", state => state.ApuStartButtonOn, "apu-start on"),
                Observe("apu-available", "APU AVAIL", state => state.ApuAvailable),
                Observe(
                    "captain-runway-exit",
                    "After-landing taxi speed reached",
                    state => state.OnGround
                             && state.GroundSpeedKnots <= 30,
                    CrewRole.FirstOfficer),
                Automatic(
                    "fo-landing-lights-retract",
                    "Landing lights RETRACTED",
                    state => state.LeftLandingLightSelectorPosition.HasValue
                             && state.RightLandingLightSelectorPosition.HasValue
                             && Math.Abs(state.LeftLandingLightSelectorPosition.Value - 2) < 0.1
                             && Math.Abs(state.RightLandingLightSelectorPosition.Value - 2) < 0.1,
                    "landing-lights retract"),
                Automatic(
                    "fo-strobes-off",
                    "Strobes OFF",
                    state => state.StrobeSelectorPosition.HasValue
                             && Math.Abs(state.StrobeSelectorPosition.Value - 2) < 0.1,
                    "strobe off"),
                Automatic(
                    "fo-nose-light-taxi",
                    "Nose light TAXI",
                    state => state.NoseLightSelectorPosition.HasValue
                             && Math.Abs(state.NoseLightSelectorPosition.Value - 1) < 0.1,
                    "nose-light taxi"),
                Automatic(
                    "fo-spoilers-disarm",
                    "Ground spoilers DISARMED",
                    state => !state.GroundSpoilersArmed,
                    "ground-spoilers disarm"),
                Automatic(
                    "fo-flaps-zero",
                    "Flaps retracted to zero",
                    state => state.FlapsAtDetent(0),
                    "flaps clean"),
                Automatic(
                    "fo-transponder-stby",
                    "Transponder STBY",
                    state => state.TransponderModeSelectorPosition.HasValue
                             && Math.Abs(state.TransponderModeSelectorPosition.Value) < 0.1,
                    "transponder stby")
            });

    public static ProcedureDefinition ParkingAndShutdown { get; } =
        new(
            "parking-shutdown",
            "12. Parking & Shutdown",
            new[]
            {
                Observe(
                    "captain-park",
                    "Aircraft parked at the gate",
                    state => state.OnGround && state.GroundSpeedKnots <= 0.5,
                    CrewRole.FirstOfficer),
                Observe(
                    "captain-parking-brake",
                    "Parking brake ON",
                    state => state.ParkingBrakeSet,
                    CrewRole.FirstOfficer),
                Observe("shutdown-power", "APU available or external power connected", state => state.ApuAvailable || state.ExternalPowerOn),
                Observe(
                    "captain-engine-shutdown",
                    "Engine masters OFF",
                    state => state.EnginesOff,
                    CrewRole.FirstOfficer),
                Automatic(
                    "fo-nose-light-off",
                    "Nose taxi light OFF",
                    state => state.NoseLightSelectorPosition.HasValue
                             && Math.Abs(state.NoseLightSelectorPosition.Value - 2) < 0.1,
                    "nose-light off"),
                Automatic(
                    "fo-beacon-off",
                    "Beacon OFF after engine spool-down",
                    state => !state.BeaconOn,
                    "beacon off"),
                Automatic("fo-fuel-pumps-off", "All six fuel pumps OFF", state => state.AllFuelPumpsOff, "fuel-pumps off"),
                Automatic("fo-seatbelts-off", "Seatbelt signs OFF", state => state.SeatbeltSelectorPosition.HasValue && Math.Abs(state.SeatbeltSelectorPosition.Value - 2) < 0.1, "seatbelts off"),
                Automatic("fo-apu-bleed-on", "APU BLEED ON", state => state.ApuBleedOn, "apu-bleed on"),
                Observe(
                    "fo-doors",
                    "A required cabin or cargo door opened",
                    state => state.AnyRequiredDoorOpen,
                    CrewRole.FirstOfficer),
                Manual("secure-decision", "Cold-and-dark secure requested", "Captain and First Officer: confirm continuation to final cold-and-dark secure.", CrewRole.Either),
                Automatic(
                    "secure-oxygen",
                    "Crew oxygen OFF",
                    state => !state.CrewOxygenOn,
                    "crew-oxygen off",
                    CrewRole.Either),
                Automatic("secure-emergency-lights", "Emergency lights OFF", state => state.EmergencyExitSelectorPosition.HasValue && Math.Abs(state.EmergencyExitSelectorPosition.Value - 2) < 0.1, "emergency-exit off", CrewRole.Either),
                Automatic("secure-nav-logo-off", "NAV & LOGO lights OFF", state => state.NavLogoSelectorPosition.HasValue && Math.Abs(state.NavLogoSelectorPosition.Value - 2) < 0.1, "nav-logo off", CrewRole.Either),
                Automatic("secure-adirs-one-off", "ADIRS 1 OFF", state => Math.Abs(state.Adirs1SelectorState) < 0.1, "adirs-1 off", CrewRole.Either),
                Automatic("secure-adirs-two-off", "ADIRS 2 OFF", state => Math.Abs(state.Adirs2SelectorState) < 0.1, "adirs-2 off", CrewRole.Either),
                Automatic("secure-adirs-three-off", "ADIRS 3 OFF", state => Math.Abs(state.Adirs3SelectorState) < 0.1, "adirs-3 off", CrewRole.Either),
                Automatic("secure-apu-bleed-off", "APU BLEED OFF", state => !state.ApuBleedOn, "apu-bleed off", CrewRole.Either),
                Observe(
                    "secure-apu-power",
                    "APU power available before external-power disconnect",
                    state => !state.ExternalPowerOn
                             || (state.ApuAvailable && state.ApuGeneratorSwitchOn),
                    CrewRole.Either),
                Automatic("secure-external-power-off", "External power OFF", state => !state.ExternalPowerOn, "external-power off", CrewRole.Either),
                Automatic("secure-apu-master-off", "APU MASTER OFF", state => !state.ApuMasterSwitchOn, "apu-master off", CrewRole.Either),
                Observe("secure-apu-flap", "APU exhaust/intake flap closed", state => state.ApuFlapPercent <= 0.05, CrewRole.Either),
                Automatic("secure-battery-one-off", "BAT 1 OFF", state => !state.Battery1On, "battery-1 off", CrewRole.Either),
                Automatic("secure-battery-two-off", "BAT 2 OFF", state => !state.Battery2On, "battery-2 off", CrewRole.Either)
            });

    public static ProcedureDefinition CockpitPreparation => PowerUpAndInitialSetup;
    public static ProcedureDefinition BeforeStart => ApuStartAndPushback;
    public static ProcedureDefinition EngineStart => EngineStartSequence;
    public static ProcedureDefinition AfterStart => AfterStartAndTaxi;
    public static ProcedureDefinition Taxi => AfterStartAndTaxi;
    public static ProcedureDefinition TakeoffAndInitialClimb => TakeoffAndClimb;
    public static ProcedureDefinition ClimbToCruise => TakeoffAndClimb;
}
