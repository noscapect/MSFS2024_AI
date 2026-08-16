using Msfs2024Ai.Copilot.Domain;
using Msfs2024Ai.Copilot.AircraftAdapters.Pmdg777;

namespace Msfs2024Ai.Copilot.Procedures;

/// <summary>
/// PMDG 777-300ER gate-to-gate procedure catalog. The complete catalog is
/// visible during integration. Automatic 777 commands use aircraft-specific
/// PMDG events and advance only after an independent aircraft-state readback.
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
        Func<AircraftState, bool> complete,
        TimeSpan? minimumDuration = null,
        CrewRole role = CrewRole.FirstOfficer,
        Func<AircraftState, bool>? recoveryComplete = null) =>
        new(id, label, ProcedureStepKind.Observe, complete, role,
            isCompleteWhenRecovering: recoveryComplete,
            minimumDuration: minimumDuration);

    private static ProcedureStep Manual(
        string id,
        string label,
        string instruction,
        CrewRole role = CrewRole.FirstOfficer,
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
        TimeSpan? minimumDuration = null,
        Func<AircraftState, bool>? recoveryComplete = null) =>
        new(
            id,
            label,
            ProcedureStepKind.AutomaticAction,
            complete,
            CrewRole.FirstOfficer,
            command,
            isCompleteWhenRecovering: recoveryComplete,
            minimumDuration: minimumDuration);

    private static bool ApproachGateReached(
        AircraftState state,
        double distanceNm,
        bool altitudeFallback) =>
        state.ApproachDistanceToTouchdownNm.HasValue
        && state.ApproachDistanceToTouchdownNm.Value > 0
            ? state.ApproachDistanceToTouchdownNm.Value <= distanceNm
            : altitudeFallback;

    public static ProcedureDefinition PowerUpAndPreliminaryPreflight { get; } =
        new(
            "power-up-initial-setup",
            "1. 777 Power Up & Preliminary Preflight",
            new[]
            {
                Automatic(
                    "battery-on",
                    "First Officer: BATTERY switch ON",
                    state => state.Pmdg777SdkDataReady && state.Pmdg777BatteryOn,
                    "pmdg777 battery on",
                    TimeSpan.FromSeconds(3)),
                Observe(
                    "primary-external-power-available",
                    "Waiting for PRIMARY EXTERNAL POWER AVAIL after the GPU is connected",
                    state => state.Pmdg777PrimaryExternalPowerAvailable),
                Automatic(
                    "primary-external-power-on",
                    "First Officer: PRIMARY EXTERNAL POWER switch PUSH; ON light illuminated",
                    state => state.Pmdg777PrimaryExternalPowerOn,
                    "pmdg777 primary external power on",
                    TimeSpan.FromSeconds(5)),
                Observe(
                    "secondary-external-power-available",
                    "Waiting for SECONDARY EXTERNAL POWER AVAIL after the GPU is connected",
                    state => state.Pmdg777SecondaryExternalPowerAvailable),
                Automatic(
                    "secondary-external-power-on",
                    "First Officer: SECONDARY EXTERNAL POWER switch PUSH when available; ON light illuminated",
                    state => state.Pmdg777SecondaryExternalPowerOn,
                    "pmdg777 secondary external power on",
                    TimeSpan.FromSeconds(5)),
                Observe(
                    "bus-ties-auto",
                    "First Officer verifies both BUS TIE switches AUTO",
                    state => state.Pmdg777BusTiesAuto,
                    TimeSpan.FromSeconds(3)),
                Observe(
                    "hydraulic-starting-state",
                    "First Officer verifies C1/C2 PRIMARY and all DEMAND pump selectors OFF",
                    state => state.Pmdg777HydraulicPanelSafe,
                    TimeSpan.FromSeconds(4)),
                Observe(
                    "wipers-off",
                    "First Officer verifies both WIPER selectors OFF",
                    state => state.Pmdg777WipersOff,
                    TimeSpan.FromSeconds(3)),
                Observe(
                    "gear-down",
                    "First Officer verifies landing gear lever DOWN",
                    state => state.Pmdg777GearLeverDown,
                    TimeSpan.FromSeconds(3)),
                Observe(
                    "alternate-flaps-off",
                    "First Officer verifies ALTERNATE FLAPS selector OFF",
                    state => state.Pmdg777AlternateFlapsOff,
                    TimeSpan.FromSeconds(3)),
                Automatic(
                    "adiru-on",
                    "First Officer: ADIRU OFF for at least 30 seconds, then ON",
                    state => state.Pmdg777AdiruOn,
                    "pmdg777 adiru on",
                    TimeSpan.FromSeconds(5))
            });

    public static ProcedureDefinition FlightComputerAndPreFlight { get; } =
        new(
            "flight-computer-preflight",
            "2. 777 Flight Deck & Preflight",
            new[]
            {
                Observe("electrical-power", "Electrical power established and PMDG 777X data available", state => state.Pmdg777SdkDataReady && state.Pmdg777BatteryOn && state.Pmdg777PrimaryExternalPowerOn),
                Manual("captain-fd-qnh", "Captain flight director ON and local QNH set", "Captain: set the left FLIGHT DIRECTOR switch ON and set the local altimeter reference, then press Confirm now.", CrewRole.Captain),
                Manual("captain-displays", "Captain PFD, ND, EICAS and flight instruments checked", "Captain: verify the PFD, ND, EICAS and standby instruments are correctly displayed with no unexpected indications, then press Confirm now.", CrewRole.Captain),
                Manual("captain-parking-brake", "Captain parking brake SET", "Captain: set the parking brake and verify the PARKING BRAKE SET message, then press Confirm now.", CrewRole.Captain, state => state.ParkingBrakeSet),
                Manual("captain-uft-setup", "Captain UFT flight and load data prepared", "Captain: load or enter the flight plan in the UFT and verify payload, fuel and operational flight-plan data, then press Confirm now.", CrewRole.Captain),
                Manual("captain-cdu-ident-pos-init", "Captain CDU IDENT and POS INIT complete", "Captain: verify the aircraft/navigation database on IDENT and enter or copy the present position on POS INIT, then press Confirm now.", CrewRole.Captain),
                Manual("captain-cdu-route", "Captain CDU route complete", "Captain: enter or import origin, destination, flight number, runway, departure, route and arrival; resolve discontinuities and execute the route, then press Confirm now.", CrewRole.Captain, state => state.Pmdg777FmcRouteInitialized),
                Manual("captain-cdu-performance", "Captain CDU performance initialization complete", "Captain: enter gross-weight and fuel data, reserves, cost index, cruise altitude and required performance entries, then press Confirm now.", CrewRole.Captain, state => state.Pmdg777FmcPerformanceInputComplete),
                Manual("captain-cdu-takeoff-reference", "Captain CDU TAKEOFF REF complete", "Captain: enter takeoff flaps and thrust data, select or enter V1, VR and V2, and verify the takeoff reference pages, then press Confirm now.", CrewRole.Captain, state => state.Pmdg777FmcTakeoffFlaps > 0 && state.Pmdg777FmcV1 > 0 && state.Pmdg777FmcVr > 0 && state.Pmdg777FmcV2 > 0),
                Manual("captain-ifr-clearance", "IFR clearance received and reviewed", "Pilot: after completing the CDU setup, request, acknowledge and review the IFR clearance using the available ATC system, then press Confirm now.", CrewRole.Captain, state => state.AtcClearedIfr),
                Automatic("fo-overhead-electrical-hydraulic", "First Officer: electrical and hydraulic panels set for preflight", state => state.Pmdg777ElectricalHydraulicPreflight, "pmdg777 electrical hydraulic preflight", TimeSpan.FromSeconds(6)),
                Automatic("fo-overhead-engine-fuel-fire", "First Officer: engine, fuel, fire and anti-ice panels set for preflight", state => state.Pmdg777FirePanelNormal && state.Pmdg777EngineControlPanelNormal && state.Pmdg777FuelPanelPreflight && state.Pmdg777FuelToRemainSelectorIn && state.Pmdg777AntiIceAuto, "pmdg777 engine fuel fire preflight", TimeSpan.FromSeconds(6)),
                Automatic("fo-fire-overheat-test", "First Officer: OVHT/FIRE test completed", state => state.Pmdg777FireOverheatTestComplete, "pmdg777 fire overheat test", TimeSpan.FromSeconds(3)),
                Automatic("fo-overhead-lights", "First Officer: exterior lights, no-smoking AUTO and seat belts OFF for preflight", state => state.Pmdg777ExteriorLightsPreflight && state.Pmdg777NoSmokingAuto && state.Pmdg777SeatBeltsOff, "pmdg777 exterior lights preflight", TimeSpan.FromSeconds(4)),
                Automatic("fo-overhead-air", "First Officer: air-conditioning, bleed, temperature and pressurization panels set for preflight", state => state.Pmdg777AirPanelPreflight && state.Pmdg777TemperatureControlsPreflight, "pmdg777 air panel preflight", TimeSpan.FromSeconds(6)),
                Automatic("fo-flight-director-on", "First Officer: FLIGHT DIRECTOR ON", state => state.Pmdg777FirstOfficerFlightDirectorOn, "pmdg777 fo flight director on", TimeSpan.FromSeconds(3)),
                Automatic("fo-oxygen-test", "First Officer: oxygen test completed", state => state.Pmdg777FirstOfficerOxygenTestComplete, "pmdg777 fo oxygen test", TimeSpan.FromSeconds(3)),
                Automatic("fo-instruments", "First Officer: displays, ND MAP and AUTOBRAKE RTO set and verified", state => state.Pmdg777FirstOfficerSourcesNormal && state.Pmdg777FirstOfficerDisplaysReady && state.Pmdg777FirstOfficerNdMap && state.Pmdg777AutobrakeRto, "pmdg777 instruments preflight", TimeSpan.FromSeconds(6)),
                Automatic("transponder-standby", "First Officer: transponder mode selector STBY", state => state.Pmdg777TransponderStandby, "pmdg777 transponder standby", TimeSpan.FromSeconds(3)),
                Observe("console-starting-configuration", "First Officer verifies speedbrake DOWN, flaps UP, fuel controls CUTOFF and transponder STBY", state => state.Pmdg777ConsoleStartingConfiguration, TimeSpan.FromSeconds(6)),
                Observe("fo-radios-audio", "Radio/audio panels left untouched when SayIntentions owns communications; transponder altitude source verified NORM", state => state.Pmdg777TransponderAltitudeSourceNormal, TimeSpan.FromSeconds(3)),
                Observe("irs-aligned", "IRS alignment complete", state => state.Pmdg777IrsAligned, TimeSpan.FromSeconds(3)),
                Observe("preflight-checklist", "Virtual First Officer completes the PREFLIGHT verification from independent aircraft readbacks", state => state.Pmdg777FlowTwoFirstOfficerVerified, TimeSpan.FromSeconds(5))
            });

    public static ProcedureDefinition BeforeStartAndPushback { get; } =
        new(
            "apu-start-pushback",
            "3. 777 Before Start & Pushback",
            new[]
            {
                Observe("parked", "Aircraft stationary at the gate", state => state.OnGround && state.GroundSpeedKnots <= 0.5),
                Manual("fuel-load", "Fuel quantity and load verified", "Captain: compare actual fuel and loading with the operational flight plan, then press Confirm now.", CrewRole.Captain),
                Automatic("apu-start", "First Officer starts the APU and verifies it running", state => state.Pmdg777ApuRunning, "pmdg777 apu start", TimeSpan.FromSeconds(8)),
                Automatic("apu-power-air", "First Officer establishes APU electrical and pneumatic supply", state => state.Pmdg777ApuGeneratorPowerEstablished && state.Pmdg777ApuBleedAirAvailable, "pmdg777 apu power air", TimeSpan.FromSeconds(6)),
                Automatic("ground-services-disconnect", "First Officer disconnects external electrical power after APU supply is established", state => state.Pmdg777ApuGeneratorPowerEstablished && !state.Pmdg777PrimaryExternalPowerOn && !state.Pmdg777SecondaryExternalPowerOn, "pmdg777 external power off", TimeSpan.FromSeconds(5)),
                Manual("before-start-procedure", "Captain Before Start setup complete", "Captain: complete CDU, MCP, trim and flight-control setup, then press Confirm now.", CrewRole.Captain),
                Automatic("fo-seatbelts-auto", "First Officer: seat-belt selector AUTO for Before Start", state => state.Pmdg777SeatBeltsAuto, "pmdg777 seatbelts auto", TimeSpan.FromSeconds(3)),
                Manual("captain-pushback-clearance", "Pushback and start clearance received", "Captain: press Confirm to instruct the SayIntentions First Officer to request pushback and engine-start clearance; the flow advances only after the matching clearance is received.", CrewRole.Captain),
                Observe("doors-cargo", "First Officer monitors GSX departure preparation and verifies all required passenger and cargo doors closed", state => state.RequiredDoorsClosed),
                Automatic("fo-hydraulics-before-start", "First Officer pressurizes the hydraulic systems in the Before Start configuration", state => state.Pmdg777HydraulicsBeforeStart, "pmdg777 hydraulics before start", TimeSpan.FromSeconds(10)),
                Automatic("fo-fuel-pumps-before-start", "First Officer sets main and required center fuel pumps ON", state => state.Pmdg777FuelPumpsBeforeStart, "pmdg777 fuel pumps before start", TimeSpan.FromSeconds(10)),
                Manual("beacon-on", "Beacon light ON", "Captain: switch the beacon ON before pushback or engine start.", CrewRole.Captain, state => state.Pmdg777BeaconOn),
                Automatic("fo-transponder-xpndr", "First Officer sets the transponder mode selector XPNDR", state => state.Pmdg777TransponderXpndr, "pmdg777 transponder xpndr", TimeSpan.FromSeconds(3)),
                Observe("before-start-checklist", "Virtual First Officer completes the BEFORE START verification from independent aircraft readbacks", state => state.Pmdg777FlowThreeFirstOfficerVerified, TimeSpan.FromSeconds(5)),
                Manual("captain-remove-wheel-chocks", "PMDG wheel chocks removed", "Captain: remove the wheel chocks from the PMDG tablet Ground Connections page. PMDG exposes a chock readback but no SDK removal command; no confirmation is needed because the flow advances from that readback.", CrewRole.Captain, state => !state.Pmdg777WheelChocksSet),
                Observe("pushback-underway", "GSX pushback underway; parking brake released and aircraft moving", state => state.OnGround && !state.ParkingBrakeSet && state.GroundSpeedKnots >= 0.1)
            });

    public static ProcedureDefinition EngineStartSequence { get; } =
        new(
            "engine-start-sequence",
            "4. 777 Engine Start",
            new[]
            {
                Observe("pushback-underway", "Pushback underway", state => state.OnGround && !state.ParkingBrakeSet && state.GroundSpeedKnots >= 0.1, recoveryComplete: state => state.Engine1StarterActive || state.Engine2StarterActive || state.Engine1Running || state.Engine2Running),
                Automatic("secondary-engine-display", "First Officer selects the secondary engine display", state => state.Pmdg777SecondaryEngineDisplaySelected, "pmdg777 secondary engine display", TimeSpan.FromSeconds(3)),
                Observe("start-configuration", "First Officer verifies the pneumatic, hydraulic and fuel configuration for start", state => state.Pmdg777ApuBleedAirAvailable && state.Pmdg777HydraulicsBeforeStart && state.Pmdg777FuelPumpsBeforeStart, TimeSpan.FromSeconds(3)),
                Manual("engine-two-selector", "Engine 2 START selector selected", "Captain: call for and select the right engine START selector. The flow advances from the selector readback.", CrewRole.Captain, state => state.Pmdg777EngineTwoStartSelectorStart || state.Engine2StarterActive, state => state.Engine2StartStabilized),
                Observe("engine-two-start-valve", "First Officer verifies Engine 2 start valve open", state => state.Pmdg777EngineTwoStartValveOpen || state.Engine2StarterActive || state.Engine2StartStabilized),
                Automatic("engine-two-fuel-control", "First Officer moves Engine 2 fuel control switch to RUN", state => state.Pmdg777EngineTwoFuelControlRun, "pmdg777 engine two fuel control run", TimeSpan.FromSeconds(2)),
                Observe("engine-two-stable", "First Officer monitors Engine 2 until stable and the start valve closes", state => state.Engine2StartStabilized, TimeSpan.FromSeconds(5)),
                Manual("engine-one-selector", "Engine 1 START selector selected", "Captain: call for and select the left engine START selector. The flow advances from the selector readback.", CrewRole.Captain, state => state.Pmdg777EngineOneStartSelectorStart || state.Engine1StarterActive, state => state.Engine1StartStabilized),
                Observe("engine-one-start-valve", "First Officer verifies Engine 1 start valve open", state => state.Pmdg777EngineOneStartValveOpen || state.Engine1StarterActive || state.Engine1StartStabilized),
                Automatic("engine-one-fuel-control", "First Officer moves Engine 1 fuel control switch to RUN", state => state.Pmdg777EngineOneFuelControlRun, "pmdg777 engine one fuel control run", TimeSpan.FromSeconds(2)),
                Observe("engine-one-stable", "First Officer monitors Engine 1 until stable and the start valve closes", state => state.Engine1StartStabilized, TimeSpan.FromSeconds(5)),
                Observe("engines-running", "Both engines running", state => state.Engine1Running && state.Engine2Running),
                Observe("start-normal", "Both starts normal; fuel controls RUN and start valves closed", state => state.Engine1StartStabilized && state.Engine2StartStabilized && state.Pmdg777EngineOneFuelControlRun && state.Pmdg777EngineTwoFuelControlRun && !state.Pmdg777EngineOneStartValveOpen && !state.Pmdg777EngineTwoStartValveOpen, TimeSpan.FromSeconds(3))
            });

    public static ProcedureDefinition BeforeTaxiAndTaxi { get; } =
        new(
            "after-start-taxi",
            "5. 777 Before Taxi & Taxi",
            new[]
            {
                Observe("engines-running", "Both engines running", state => state.Engine1Running && state.Engine2Running),
                Automatic("fo-after-start-air-apu", "First Officer establishes engine bleed/pack configuration, leaves APU bleed AUTO and selects APU OFF", state => state.Pmdg777EngineBleedsAuto && state.Pmdg777PacksAuto && state.Pmdg777ApuBleedAuto && state.Pmdg777ApuSelectorOff, "pmdg777 after start air apu", TimeSpan.FromSeconds(6)),
                Observe("fo-hydraulics", "First Officer verifies the departure hydraulic configuration", state => state.Pmdg777HydraulicsBeforeStart, TimeSpan.FromSeconds(3)),
                Automatic("flaps", "First Officer sets the FMC takeoff flap position", state => state.Pmdg777TakeoffFlapsSet, "pmdg777 takeoff flaps", TimeSpan.FromSeconds(5)),
                Automatic("fo-autobrake-rto", "First Officer verifies AUTOBRAKE RTO", state => state.Pmdg777AutobrakeRto, "pmdg777 autobrake rto", TimeSpan.FromSeconds(3)),
                Manual("captain-flight-controls", "Flight controls checked", "Captain: complete the full flight-control check. Continue through the full travel of each control; this step records the physical crew check.", CrewRole.Captain),
                Manual("captain-recall-trim", "Recall and takeoff trim checked", "Captain: check EICAS recall and verify stabilizer trim is set for takeoff with aileron and rudder trim neutral.", CrewRole.Captain),
                Observe("ground-equipment-clear", "Ground equipment clear", state => !state.Pmdg777WheelChocksSet && state.RequiredDoorsClosed),
                Automatic("taxi-lights", "First Officer sets taxi and runway-turnoff lights", state => state.Pmdg777TaxiLightsCommandedThisFlow && state.Pmdg777TaxiLightsSet, "pmdg777 taxi lights", TimeSpan.FromSeconds(3)),
                Automatic("fo-taxi-clearance", "SayIntentions First Officer obtains taxi clearance", state => !state.SayIntentionsAtcActive || state.TaxiClearanceReceived, "sayintentions taxi clearance"),
                Manual("brake-check", "Brakes and steering checked", "Captain: check brakes immediately after movement and verify steering during taxi, then press Confirm now.", CrewRole.Captain)
            });

    public static ProcedureDefinition BeforeTakeoff { get; } =
        new(
            "before-takeoff",
            "6. 777 Before Takeoff",
            new[]
            {
                Observe("holding-short", "Aircraft stopped at the runway holding point", state => state.BeforeTakeoffHoldEligible, recoveryComplete: state => !state.OnGround),
                Manual("captain-takeoff-briefing", "Takeoff briefing reviewed", "Captain: review runway, performance, initial routing and reject/engine-failure plan.", CrewRole.Captain, recoveryComplete: state => !state.OnGround),
                Manual("captain-cabin-ready", "Cabin ready received", "Captain: verify the cabin-ready indication or report has been received.", CrewRole.Captain, recoveryComplete: state => !state.OnGround),
                Observe("takeoff-flaps", "First Officer verifies flaps set for takeoff", state => state.Pmdg777TakeoffFlapsSet, TimeSpan.FromSeconds(3), recoveryComplete: state => !state.OnGround),
                Automatic("fo-transponder-tara", "First Officer sets transponder TA/RA", state => state.Pmdg777TransponderTaRa, "pmdg777 transponder tara", TimeSpan.FromSeconds(3), state => !state.OnGround),
                Automatic("fo-lnav-arm", "First Officer arms LNAV", state => state.Pmdg777LnavArmed, "pmdg777 lnav arm", TimeSpan.FromSeconds(3), state => !state.OnGround),
                Automatic("fo-vnav-arm", "First Officer arms VNAV", state => state.Pmdg777VnavArmed, "pmdg777 vnav arm", TimeSpan.FromSeconds(3), state => !state.OnGround),
                Automatic("fo-takeoff-clearance", "SayIntentions First Officer obtains takeoff clearance", state => !state.SayIntentionsAtcActive || state.TakeoffClearanceReceived, "sayintentions takeoff clearance", recoveryComplete: state => !state.OnGround),
                Automatic("fo-takeoff-lights", "First Officer sets exterior lights for runway entry", state => state.Pmdg777TakeoffLightsSet, "pmdg777 takeoff lights", TimeSpan.FromSeconds(4), state => !state.OnGround),
                Observe("before-takeoff-checklist", "PMDG BEFORE TAKEOFF electronic checklist complete", state => state.Pmdg777BeforeTakeoffChecklistComplete && state.Pmdg777TakeoffFlapsSet && state.Pmdg777TransponderTaRa && state.Pmdg777LnavArmed && state.Pmdg777VnavArmed && state.Pmdg777TakeoffLightsSet, TimeSpan.FromSeconds(4), recoveryComplete: state => !state.OnGround)
            });

    public static ProcedureDefinition TakeoffAndClimb { get; } =
        new(
            "takeoff-climb",
            "7. 777 Takeoff & Climb",
            new[]
            {
                Observe("thrust-set", "Takeoff thrust set", state => state.Engine1N1Percent >= 40 && state.Engine2N1Percent >= 40),
                Observe("hundred-knots", "100 knots", state => state.HundredKnotsCalloutReached),
                Observe("v1", "V1", state => state.V1CalloutReached),
                Observe("rotate", "Rotate", state => state.RotateCalloutReached),
                Observe("positive-climb", "Positive climb", state => !state.OnGround && state.AltitudeAboveGroundFeet >= 35 && state.VerticalSpeedFeetPerMinute > 100, recoveryComplete: state => !state.OnGround && state.AltitudeAboveGroundFeet >= 400),
                Automatic("gear-up", "First Officer selects landing gear UP", state => state.Pmdg777GearLeverUp, "pmdg777 gear up", TimeSpan.FromSeconds(3)),
                Observe("acceleration-altitude", "Acceleration altitude passed", state => !state.OnGround && state.AltitudeAboveGroundFeet >= 1000),
                Observe("flap-retraction-speed", "Flap retraction speed reached", state => state.TakeoffV2SpeedKnots.HasValue && state.IndicatedAirspeedKnots >= state.TakeoffV2SpeedKnots.Value + 40),
                Automatic("flap-retraction", "First Officer retracts flaps on schedule", state => state.Pmdg777FlapsUp, "pmdg777 flaps up", TimeSpan.FromSeconds(5)),
                Observe("after-takeoff-checklist", "Virtual First Officer verifies gear UP and flaps UP", state => state.Pmdg777GearLeverUp && state.Pmdg777FlapsUp, TimeSpan.FromSeconds(4)),
                Observe("ten-thousand-feet", "10,000 feet passed", state => state.IndicatedAltitudeFeet >= 10000),
                Automatic("fo-climb-lights", "First Officer sets exterior lights for climb", state => state.Pmdg777ClimbLightsSet, "pmdg777 climb lights", TimeSpan.FromSeconds(4))
            });

    public static ProcedureDefinition Cruise { get; } =
        new(
            "cruise",
            "8. 777 Cruise",
            new[]
            {
                Observe("cruise-established", "Cruise altitude captured and vertical speed stabilized", state => state.CruiseEstablished, TimeSpan.FromSeconds(10)),
                Observe("systems-monitor", "First Officer verifies engines running, clean configuration and fuel quantity available", state => state.Engine1Running && state.Engine2Running && state.Pmdg777GearLeverUp && state.Pmdg777FlapsUp && state.ActualFuelKilograms > 0, TimeSpan.FromSeconds(5))
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
                Observe("descent-established", "Descent established", state => !state.OnGround && (state.VerticalSpeedFeetPerMinute <= -300 || state.IndicatedAltitudeFeet <= 10000)),
                Observe("below-ten-thousand", "10,000 feet passed", state => !state.OnGround && state.IndicatedAltitudeFeet <= 10000),
                Observe("landing-data-set", "FMC landing flaps and VREF available", state => state.Pmdg777FmcLandingFlaps is 20 or 25 or 30 && state.Pmdg777FmcLandingVref > 0, TimeSpan.FromSeconds(3)),
                Automatic("fo-approach-lights", "First Officer switches landing lights ON", state => state.Pmdg777LandingLightsOn, "pmdg777 approach lights", TimeSpan.FromSeconds(4)),
                Automatic("fo-autobrake-landing", "First Officer sets AUTOBRAKE 2", state => state.Pmdg777AutobrakeSelector == 4, "pmdg777 autobrake landing", TimeSpan.FromSeconds(3)),
                Observe("flaps-one-gate", "Flaps 1 point reached", state => ApproachGateReached(state, state.ApproachFlaps1DistanceNm, state.IndicatedAltitudeFeet <= state.ApproachFlaps1AltitudeFeet)),
                Observe("flaps-one-speed", "Safe speed for flaps 1", state => state.IndicatedAirspeedKnots <= Pmdg777ControlProfile.ApproachFlapsOneCommandSpeedKnots),
                Automatic("fo-flaps-one", "First Officer selects flaps 1", state => state.Pmdg777FlapsLever >= 1, "pmdg777 flaps one", TimeSpan.FromSeconds(4)),
                Observe("flaps-five-gate", "Flaps 5 point reached", state => ApproachGateReached(state, state.ApproachFlaps2DistanceNm, state.AltitudeAboveGroundFeet <= state.ApproachFlaps2AltitudeAglFeet)),
                Observe("flaps-five-speed", "Safe speed for flaps 5", state => state.IndicatedAirspeedKnots <= Pmdg777ControlProfile.ApproachFlapsFiveCommandSpeedKnots),
                Automatic("fo-flaps-five", "First Officer selects flaps 5", state => state.Pmdg777FlapsLever >= 2, "pmdg777 flaps five", TimeSpan.FromSeconds(4)),
                Observe("gear-gate", "Gear-down point reached", state => ApproachGateReached(state, state.ApproachGearDistanceNm, state.AltitudeAboveGroundFeet <= state.ApproachGearAltitudeAglFeet)),
                Observe("gear-speed", "Safe speed for landing gear", state => state.IndicatedAirspeedKnots <= state.ApproachGearSpeedKnots),
                Automatic("fo-gear-down", "First Officer selects landing gear DOWN", state => state.Pmdg777GearLeverDown, "pmdg777 gear down", TimeSpan.FromSeconds(4)),
                Automatic("fo-speedbrake-arm", "First Officer arms the speedbrake", state => state.Pmdg777SpeedbrakeArmed, "pmdg777 speedbrake arm", TimeSpan.FromSeconds(3)),
                Observe("flaps-fifteen-speed", "Safe speed for flaps 15", state => state.IndicatedAirspeedKnots <= Pmdg777ControlProfile.ApproachFlapsFifteenCommandSpeedKnots),
                Automatic("fo-flaps-fifteen", "First Officer selects flaps 15", state => state.Pmdg777FlapsLever >= 3, "pmdg777 flaps fifteen", TimeSpan.FromSeconds(4)),
                Observe("flaps-twenty-speed", "Safe speed for flaps 20", state => state.IndicatedAirspeedKnots <= Pmdg777ControlProfile.ApproachFlapsTwentyCommandSpeedKnots),
                Automatic("fo-flaps-twenty", "First Officer selects flaps 20", state => state.Pmdg777FlapsLever >= 4, "pmdg777 flaps twenty", TimeSpan.FromSeconds(4)),
                Observe("landing-config-gate", "Landing-configuration point reached", state => ApproachGateReached(state, state.ApproachLandingConfigDistanceNm, state.AltitudeAboveGroundFeet <= state.ApproachLandingConfigAltitudeAglFeet)),
                Observe("landing-flaps-speed", "Safe speed for the FMC landing flap setting", state => Pmdg777ControlProfile.LandingFlapsCommandSpeedKnots(state.Pmdg777FmcLandingFlaps) > 0 && state.IndicatedAirspeedKnots <= Pmdg777ControlProfile.LandingFlapsCommandSpeedKnots(state.Pmdg777FmcLandingFlaps)),
                Automatic("fo-landing-flaps", "First Officer selects the FMC landing flap setting", state => state.Pmdg777LandingFlapsSet, "pmdg777 landing flaps", TimeSpan.FromSeconds(5)),
                Observe("landing-checklist", "Virtual First Officer verifies landing configuration", state => state.Pmdg777GearLeverDown && state.Pmdg777LandingFlapsSet && state.Pmdg777SpeedbrakeArmed && state.Pmdg777AutobrakeSelector == 4, TimeSpan.FromSeconds(4)),
                Observe("stable-approach", "Stable approach by 1,000 feet AGL", state => state.RadioHeightFeet > 0 && state.RadioHeightFeet <= 1000 && state.Pmdg777GearLeverDown && state.Pmdg777LandingFlapsSet && state.Pmdg777SpeedbrakeArmed && state.IndicatedAirspeedKnots <= state.Pmdg777FmcLandingVref + 15),
                Observe("approaching-minimums", "Approaching minimums", state => state.DecisionHeightFeet > 0 && state.RadioHeightFeet <= state.DecisionHeightFeet + 100),
                Observe("minimums", "Minimums", state => state.DecisionHeightFeet > 0 && state.RadioHeightFeet <= state.DecisionHeightFeet),
                Observe("touchdown", "Touchdown", state => state.OnGround && state.GroundSpeedKnots > 30),
                Observe("landing-roll", "Spoilers, reverse and deceleration monitored", state => state.OnGround && (state.ReverseThrustEngaged || state.GroundSpeedKnots <= 80))
            });

    public static ProcedureDefinition AfterLandingAndTaxi { get; } =
        new(
            "after-landing-taxi",
            "11. 777 After Landing & Taxi In",
            new[]
            {
                Observe("on-ground", "Aircraft on the ground", state => state.OnGround),
                Observe("reverse-stowed", "Reverse thrust stowed below 70 knots", state => state.OnGround && state.GroundSpeedKnots <= 70 && !state.ReverseThrustEngaged),
                Observe("taxi-speed", "After-landing taxi speed reached", state => state.OnGround && state.GroundSpeedKnots <= 30),
                Automatic("fo-autobrake-off", "First Officer sets AUTOBRAKE OFF", state => state.Pmdg777AutobrakeSelector == 1, "pmdg777 autobrake off", TimeSpan.FromSeconds(3)),
                Automatic("fo-after-landing-lights", "First Officer sets exterior lights for taxi", state => state.Pmdg777AfterLandingLightsSet, "pmdg777 after landing lights", TimeSpan.FromSeconds(5)),
                Automatic("fo-speedbrake-down", "First Officer lowers the speedbrake", state => state.Pmdg777SpeedbrakeDown, "pmdg777 speedbrake down", TimeSpan.FromSeconds(3)),
                Automatic("fo-flaps-up", "First Officer retracts flaps", state => state.Pmdg777FlapsUp, "pmdg777 flaps up", TimeSpan.FromSeconds(5)),
                Automatic("fo-transponder-standby", "First Officer sets transponder STBY", state => state.Pmdg777TransponderStandby, "pmdg777 transponder standby", TimeSpan.FromSeconds(3)),
                Automatic("fo-apu-start", "First Officer starts the APU", state => state.Pmdg777ApuRunning, "pmdg777 apu start", TimeSpan.FromSeconds(5)),
                Observe("apu-running", "APU running", state => state.Pmdg777ApuRunning),
                Automatic("fo-apu-power-air", "First Officer establishes APU electrical and pneumatic supply", state => state.Pmdg777ApuGeneratorPowerEstablished && state.Pmdg777ApuBleedAirAvailable, "pmdg777 apu power air", TimeSpan.FromSeconds(5)),
                Observe("after-landing-procedure", "After Landing configuration verified", state => state.Pmdg777AfterLandingLightsSet && state.Pmdg777SpeedbrakeDown && state.Pmdg777FlapsUp && state.Pmdg777TransponderStandby && state.Pmdg777ApuGeneratorPowerEstablished, TimeSpan.FromSeconds(4))
            });

    public static ProcedureDefinition ParkingAndShutdown { get; } =
        new(
            "parking-shutdown",
            "12. 777 Parking, Shutdown & Secure",
            new[]
            {
                Observe("parked", "Aircraft stationary at the gate", state => state.OnGround && state.GroundSpeedKnots <= 0.5),
                Observe("parking-brake", "Captain sets the parking brake", state => state.ParkingBrakeSet, role: CrewRole.Captain),
                Automatic("gate-power", "First Officer verifies APU electrical and pneumatic supply", state => state.Pmdg777ApuGeneratorPowerEstablished && state.Pmdg777ApuBleedAirAvailable, "pmdg777 apu power air", TimeSpan.FromSeconds(4)),
                Observe("fuel-controls-cutoff", "Captain moves both fuel control switches to CUTOFF", state => state.Pmdg777FuelControlsCutoff, role: CrewRole.Captain),
                Observe("engines-off", "Both engines shut down", state => state.EnginesOff),
                Automatic("fo-beacon-off", "First Officer switches the beacon OFF", state => !state.Pmdg777BeaconOn, "pmdg777 beacon off", TimeSpan.FromSeconds(3)),
                Automatic("fo-shutdown-pumps", "First Officer secures hydraulic and fuel pumps", state => state.Pmdg777FuelPumpsOff && state.Pmdg777HydraulicsShutdown, "pmdg777 shutdown pumps", TimeSpan.FromSeconds(8)),
                Observe("doors-ground", "Aircraft safe for doors and ground services", state => state.EnginesOff && !state.Pmdg777BeaconOn, TimeSpan.FromSeconds(3)),
                Observe("shutdown-checklist", "Shutdown configuration verified", state => state.EnginesOff && !state.Pmdg777BeaconOn && state.Pmdg777FuelPumpsOff && state.Pmdg777HydraulicsShutdown && state.Pmdg777ApuGeneratorPowerEstablished, TimeSpan.FromSeconds(5)),
                Manual("secure-decision", "Choose final secure or turnaround state", "Captain: press Confirm now only for final secure; cancel to retain the normal APU-powered turnaround state.", CrewRole.Captain)
            });
}
