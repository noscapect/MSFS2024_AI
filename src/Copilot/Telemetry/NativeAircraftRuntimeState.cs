using Msfs2024Ai.Copilot.AircraftAdapters.IniBuildsA320;
using Msfs2024Ai.Copilot.AircraftAdapters.FbwA320;
using System.Collections.ObjectModel;

namespace Msfs2024Ai.Copilot.Telemetry;

internal readonly struct NativeAircraftStateChange
{
    public NativeAircraftStateChange(
        bool handled,
        bool applyToAircraftState,
        string? diagnostic)
    {
        Handled = handled;
        ApplyToAircraftState = applyToAircraftState;
        Diagnostic = diagnostic;
    }

    public bool Handled { get; }
    public bool ApplyToAircraftState { get; }
    public string? Diagnostic { get; }

    public static NativeAircraftStateChange NotHandled => new(false, false, null);
}

/// <summary>
/// Owns transient Airbus native, InputEvent, and MobiFlight readback state.
/// Cockpit command transport and procedure orchestration deliberately remain
/// in <see cref="CopilotService"/>.
/// </summary>
internal sealed class NativeAircraftRuntimeState
{
    public A310RuntimeState A310 { get; } = new();
    public A330RuntimeState A330 { get; } = new();
    public NativeAirbusRuntimeState NativeAirbus { get; } = new();
    public FbwRuntimeState Fbw { get; } = new();

    public bool AirbusNativeStateReady => NativeAirbus.IsReady;

    public NativeAircraftStateChange TryApplyInputEvent(Request request, double value)
    {
        if (request is >= Request.A310ApuMasterInputEvent and <= Request.A310ApuBleedInputEvent)
        {
            var index = (int)request - (int)Request.A310ApuMasterInputEvent;
            var previous = A310.ApuInputStates[index];
            A310.SetApuInput(index, value);
            var label = index switch
            {
                0 => "master",
                1 => "start",
                2 => "generator",
                _ => "bleed"
            };
            return Changed(previous, value, 0.01,
                $"A310 APU {label} InputEvent={value:0.###}.", apply: false);
        }

        if (request is >= Request.A330FuelPump1InputEvent and <= Request.A330FuelPump6InputEvent)
        {
            var index = (int)request - (int)Request.A330FuelPump1InputEvent;
            var previous = A330.FuelPumpInputStates[index];
            var wasOn = previous.HasValue && previous.Value >= 0.5;
            var isOn = value >= 0.5;
            A330.SetFuelPump(index, value);
            return new(true, true, wasOn != isOn
                ? $"A330 fuel pump InputEvent {index + 1}={value:0.###} ({(isOn ? "ON" : "OFF")})."
                : null);
        }

        if (request is >= Request.A330SeatbeltsInputEvent and <= Request.A330EmergencyExitInputEvent)
        {
            var index = (int)request - (int)Request.A330SeatbeltsInputEvent;
            var previous = A330.SignInputStates[index];
            A330.SetSign(index, value);
            return Changed(previous, value, 0.1,
                $"A330 sign InputEvent {index + 1}={value:0.###}.");
        }

        if (request is >= Request.A330Adirs1InputEvent and <= Request.A330Adirs3InputEvent)
        {
            var index = (int)request - (int)Request.A330Adirs1InputEvent;
            var previous = A330.AdirsInputStates[index];
            A330.SetAdirs(index, value);
            return Changed(previous, value, 0.1,
                $"A330 ADIRS {index + 1} InputEvent={value:0.###}.");
        }

        if (request is >= Request.A330ApuMasterInputEvent and <= Request.A330ApuBleedInputEvent)
        {
            var index = (int)request - (int)Request.A330ApuMasterInputEvent;
            var previous = A330.ApuInputStates[index];
            A330.SetApu(index, value);
            return Changed(previous, value, 0.1,
                $"A330 APU InputEvent {index + 1}={value:0.###}.");
        }

        if (request is >= Request.A330AutobrakeLowInputEvent and <= Request.A330AutobrakeHighInputEvent)
        {
            var index = (int)request - (int)Request.A330AutobrakeLowInputEvent;
            var previous = A330.Autobrake.GetState(index);
            A330.Autobrake.Update(index, value);
            return Changed(previous, value, 0.1,
                $"A330 autobrake InputEvent {index + 1}={value:0.###}.");
        }

        return request switch
        {
            Request.A330StrobeInputEvent => UpdateA330Scalar(
                A330.StrobeInputState, value, A330.SetStrobe, 0.1,
                $"A330 strobe InputEvent={value:0.###}."),
            Request.A330NavLogoInputEvent => UpdateA330Scalar(
                A330.NavLogoInputState, value, A330.SetNavLogo, 0.1,
                $"A330 NAV/LOGO InputEvent={value:0.###}."),
            Request.A330TransponderModeInputEvent => UpdateA330Scalar(
                A330.TransponderModeInputState, value, A330.SetTransponderMode, 0.1,
                $"A330 transponder mode InputEvent={value:0.###}."),
            Request.A330CrewOxygenInputEvent => UpdateA330Scalar(
                A330.CrewOxygenInputState, value, A330.SetCrewOxygen, 0.1,
                $"A330 crew oxygen InputEvent={value:0.###}."),
            Request.A330SpoilerLeverInputEvent => UpdateA330Scalar(
                A330.SpoilerLeverInputState, value, A330.SetSpoilerLever, 0.01,
                $"A330 spoiler lever InputEvent={value:0.###}.", apply: false),
            Request.A330FlapsInputEvent => UpdateA330Scalar(
                A330.FlapsInputState, value, A330.SetFlaps, 0.01,
                $"A330 flaps InputEvent={value:0.###}."),
            Request.A330WeatherRadarPwsInputEvent => UpdateA330Scalar(
                A330.WeatherRadarPwsInputState, value, A330.SetWeatherRadarPws, 0.1,
                $"A330 WXR/PWS InputEvent={value:0.###}."),
            Request.A330NoseLightInputEvent => UpdateA330Scalar(
                A330.NoseLightInputState, value, A330.SetNoseLight, 0.1,
                $"A330 nose light InputEvent={value:0.###}."),
            Request.A330LandingLightInputEvent => UpdateA330Scalar(
                A330.LandingLightInputState, value, A330.SetLandingLight, 0.1,
                $"A330 landing light InputEvent={value:0.###} ({(value >= 0.5 ? "ON" : "OFF")})."),
            Request.A330TcasTrafficInputEvent => UpdateA330Scalar(
                A330.TcasTrafficInputState, value, A330.SetTcasTraffic, 0.1,
                $"A330 TCAS traffic InputEvent={value:0.###}."),
            Request.A330TcasAltitudeInputEvent => UpdateA330Scalar(
                A330.TcasAltitudeInputState, value, A330.SetTcasAltitude, 0.1,
                $"A330 TCAS altitude InputEvent={value:0.###}."),
            Request.A330ApuBatteryInputEvent => UpdateA330ApuBattery(value),
            _ => NativeAircraftStateChange.NotHandled
        };
    }

    public NativeAircraftStateChange TryApplyMobiFlightReadback(Request request, float value)
    {
        if (request is >= Request.A310NavLogoLight and <= Request.A310RightRunwayTurnoffLight)
        {
            var labels = new[] { "NAV/LOGO", "beacon", "nose", "left landing", "right landing", "wing", "left runway-turnoff", "right runway-turnoff" };
            var index = (int)request - (int)Request.A310NavLogoLight;
            var previous = A310.InitialLightStates[index];
            A310.SetInitialLight(index, value);
            return Changed(previous, value, 0.01, $"A310 {labels[index]} light selector changed to {value:F2}.");
        }

        if (request is >= Request.A310Flow2Seatbelts and <= Request.A310Flow2CargoSmokeBulk)
        {
            var labels = new[]
            {
                "seat-belt selector", "no-smoking selector", "ATS 1", "ATS 2", "pitch-trim computer 1", "pitch-trim computer 2", "yaw damper 1", "yaw damper 2",
                "window heat 1", "window heat 2", "window heat 3", "window heat 4", "captain probe heat", "first-officer probe heat", "standby probe heat",
                "emergency-exit lights", "cargo-smoke test", "EGPWS test", "autobrake", "rudder trim", "TCAS pedestal mode", "forward cargo SMOKE indication",
                "aft cargo SMOKE indication", "bulk cargo SMOKE indication"
            };
            var index = (int)request - (int)Request.A310Flow2Seatbelts;
            var previous = A310.Flow2States[index];
            A310.SetFlow2(index, value, request);
            return Changed(previous, value, 0.01, $"A310 {labels[index]} changed to {value:F2}.");
        }

        if (request is >= Request.A310Flow3ApuMaster and <= Request.A310Flow3ApuGenerator)
        {
            var labels = new[] { "APU master", "APU start", "APU available", "APU bleed", "APU generator" };
            var index = (int)request - (int)Request.A310Flow3ApuMaster;
            var previous = A310.Flow3ApuStates[index];
            A310.SetFlow3Apu(index, value);
            return Changed(previous, value, 0.01, $"A310 {labels[index]} changed to {value:F2}.");
        }

        if (request is >= Request.A310Flow4Ignition and <= Request.A310Flow4Engine2FuelLever)
        {
            var labels = new[] { "engine ignition selector", "pack 1 flow", "pack 2 flow", "Engine 1 starter", "Engine 2 starter", "Engine 1 fuel lever", "Engine 2 fuel lever" };
            var index = (int)request - (int)Request.A310Flow4Ignition;
            var previous = A310.Flow4EngineStartStates[index];
            A310.SetFlow4(index, value);
            return Changed(previous, value, 0.01, $"A310 {labels[index]} changed to {value:F2}.");
        }

        if (request is >= Request.A310FuelPump1 and <= Request.A310FuelPump12)
        {
            var index = (int)request - (int)Request.A310FuelPump1;
            var previous = A310.FuelPumpStates[index];
            A310.SetFuelPump(index, value);
            return Changed(previous, value, 0.01, $"A310 fuel pump {index + 1} changed to {value:F2}.");
        }

        if (request is >= Request.A310Flow5WeatherRadarMode and <= Request.A310Flow5SpoilersArmed)
        {
            var labels = new[] { "weather-radar mode", "autobrake MAX", "speedbrake armed" };
            var index = (int)request - (int)Request.A310Flow5WeatherRadarMode;
            var previous = A310.Flow5States[index];
            A310.SetFlow5(index, value);
            return Changed(previous, value, 0.01, $"A310 {labels[index]} changed to {value:F2}.");
        }

        if (request is >= Request.A310CaptainAltimeterStandard and <= Request.A310StandbyAltimeterStandard)
        {
            var labels = new[] { "captain altimeter STD", "first-officer altimeter STD", "standby altimeter STD" };
            var index = (int)request - (int)Request.A310CaptainAltimeterStandard;
            var previous = A310.AltimeterStandardStates[index];
            A310.SetAltimeterStandard(index, value);
            return Changed(previous, value, 0.01, $"A310 {labels[index]} changed to {value:F2}.");
        }

        if (request == Request.A310GearHandleStatus)
        {
            var previous = A310.GearHandleStatus;
            A310.SetGearHandleStatus(value);
            return Changed(previous, value, 0.01, $"A310 gear-handle status changed to {value:F2}.");
        }

        if (request is >= Request.A310Battery1Auto and <= Request.A310AnnunciatorTest)
        {
            return ApplyA310PrimaryReadback(request, value);
        }

        if (request is >= Request.NativeBattery1 and <= Request.FbwA380ExternalPower4OnTyped
            || request is Request.NativeEngineModeSelector or Request.NativeA320RunwayTurnoffSelector)
        {
            return ApplyNativeOrFbwReadback(request, value);
        }

        return NativeAircraftStateChange.NotHandled;
    }

    public void RecordFbwBatteryCommand(int battery, bool desiredAuto) =>
        Fbw.SetCommandedBattery(battery, desiredAuto);

    public void RecordFbwAdirsCommand(int adirs, float position, DateTime utcNow) =>
        Fbw.SetCommandedAdirs(adirs, position, utcNow);

    public void RecordFbwCrewOxygenCommand(bool desiredOn, DateTime utcNow) =>
        Fbw.SetCommandedCrewOxygen(desiredOn, utcNow);

    public void RecordFbwSpoilersCommand(bool armed, DateTime utcNow) =>
        Fbw.SetCommandedSpoilers(armed, utcNow);

    public void RecordA330SpoilersCommand(bool armed) => A330.SetCommandedSpoilers(armed);

    public void RecordFbwAutobrakeCommand(float level, DateTime utcNow) =>
        Fbw.SetCommandedAutobrake(level, utcNow);

    public void RecordFbwWeatherRadarPwsCommand(float position, DateTime utcNow) =>
        Fbw.SetCommandedWeatherRadarPws(position, utcNow);

    public void RecordFbwNoseLightCommand(float position, DateTime utcNow) =>
        Fbw.SetCommandedNoseLight(position, utcNow);

    public void RecordFbwLandingLightCommand(float position, DateTime utcNow) =>
        Fbw.SetCommandedLandingLight(position, utcNow);

    public void RecordFbwTcasAltitudeCommand(bool enabled, DateTime utcNow) =>
        Fbw.SetCommandedTcasAltitude(enabled, utcNow);

    public void RecordFbwTcasModeCommand(float mode, DateTime utcNow) =>
        Fbw.SetCommandedTcasMode(mode, utcNow);

    public bool ResolveFbwBattery(int battery, double genericMasterBattery) =>
        FbwStateResolvers.ResolveBattery(
            battery == 1 ? Fbw.CommandedBattery1Auto : Fbw.CommandedBattery2Auto,
            battery == 1 ? Fbw.Battery1AutoTyped : Fbw.Battery2AutoTyped,
            battery == 1 ? Fbw.Battery1Auto : Fbw.Battery2Auto,
            genericMasterBattery);

    public double ResolveFbwAdirsSelector(int selector, DateTime utcNow)
    {
        var commanded = selector == 1
            ? Fbw.CommandedAdirs1Selector
            : selector == 2 ? Fbw.CommandedAdirs2Selector : Fbw.CommandedAdirs3Selector;
        var commandedUtc = selector == 1
            ? Fbw.CommandedAdirs1SelectorUtc
            : selector == 2 ? Fbw.CommandedAdirs2SelectorUtc : Fbw.CommandedAdirs3SelectorUtc;
        var typed = selector == 1
            ? Fbw.Adirs1SelectorTyped
            : selector == 2 ? Fbw.Adirs2SelectorTyped : Fbw.Adirs3SelectorTyped;
        var untyped = selector == 1
            ? Fbw.Adirs1Selector
            : selector == 2 ? Fbw.Adirs2Selector : Fbw.Adirs3Selector;
        return FbwStateResolvers.ResolveSelector(
            commanded,
            commandedUtc,
            typed,
            untyped,
            utcNow);
    }

    public bool ResolveFbwSpoilersArmed(double genericSpoilersArmed, DateTime utcNow)
    {
        if (Fbw.SpoilersArmed.HasValue) return Fbw.SpoilersArmed.Value;
        if (genericSpoilersArmed != 0) return true;
        return IsCurrent(Fbw.CommandedSpoilersArmedUtc, utcNow)
            && Fbw.CommandedSpoilersArmed == true;
    }

    public double? ResolveFbwAutobrake(DateTime utcNow) =>
        ResolveLiveThenCommanded(
            Fbw.AutobrakeLevel,
            Fbw.CommandedAutobrakeLevel,
            Fbw.CommandedAutobrakeLevelUtc,
            utcNow);

    public double? ResolveFbwWeatherRadarPws(DateTime utcNow) =>
        ResolveLiveThenCommanded(
            Fbw.WeatherRadarPwsSelector,
            Fbw.CommandedWeatherRadarPwsSelector,
            Fbw.CommandedWeatherRadarPwsSelectorUtc,
            utcNow);

    public bool? ResolveFbwTcasAltitudeReporting(DateTime utcNow)
    {
        if (Fbw.TcasAltitudeReporting.HasValue) return Fbw.TcasAltitudeReporting;
        return IsCurrent(Fbw.CommandedTcasAltitudeReportingUtc, utcNow)
            ? Fbw.CommandedTcasAltitudeReporting
            : null;
    }

    public double? ResolveFbwTcasMode(DateTime utcNow) =>
        ResolveLiveThenCommanded(
            Fbw.TcasMode,
            Fbw.CommandedTcasMode,
            Fbw.CommandedTcasModeUtc,
            utcNow);

    public double ResolveFbwLandingLight(double circuitOn, DateTime utcNow)
    {
        if (circuitOn != 0) return 0;
        return IsCurrent(Fbw.CommandedLandingLightSelectorUtc, utcNow)
            ? Fbw.CommandedLandingLightSelector!.Value
            : 2;
    }

    public void ResetA310ApuFireObservation() => A310.ResetApuFireObservation();
    public void ResetA310AnnunciatorObservation() => A310.ResetAnnunciatorObservation();
    public void ResetA310CargoSmokeObservation() => A310.ResetCargoSmokeObservation();
    public void ResetA310EgpwsObservation() => A310.ResetEgpwsObservation();
    public void ResetA330AutobrakeReadback() => A330.Autobrake.Reset();

    public double? A330AutobrakeLevel => A330.Autobrake.Level;

    public void ClearCommandedState()
    {
        Fbw.ClearCommandedState();
        A330.ClearCommandedState();
    }

    public void ResetConnectionState()
    {
        A310.Reset();
        A330.Reset();
        NativeAirbus.Reset();
        Fbw.Reset();
    }

    public void ResetAircraftState() => ResetConnectionState();

    private NativeAircraftStateChange ApplyA310PrimaryReadback(Request request, float value)
    {
        var label = request switch
        {
            Request.A310Battery1Auto => "A310 BAT 1 AUTO",
            Request.A310Battery2Auto => "A310 BAT 2 AUTO",
            Request.A310Battery3Auto => "A310 BAT 3 AUTO",
            Request.A310HydraulicEngine1 => "A310 hydraulic ENG 1 selector",
            Request.A310HydraulicEngine1A => "A310 hydraulic ENG 1 A selector",
            Request.A310HydraulicEngine2 => "A310 hydraulic ENG 2 selector",
            Request.A310HydraulicEngine2B => "A310 hydraulic ENG 2 B selector",
            Request.A310HydraulicElectric => "A310 hydraulic electric pumps",
            Request.A310CaptainWiper => "A310 captain wiper selector",
            Request.A310FirstOfficerWiper => "A310 first-officer wiper selector",
            Request.A310WeatherRadarSystem => "A310 weather-radar system selector",
            Request.A310Irs1 => "A310 IRS 1 selector",
            Request.A310Irs2 => "A310 IRS 2 selector",
            Request.A310Irs3 => "A310 IRS 3 selector",
            Request.A310OxygenSupply => "A310 crew oxygen supply",
            Request.A310ApuFireTest => "A310 APU SQUIB test",
            Request.A310ApuLoopTest => "A310 APU LOOP test",
            _ => "A310 annunciator test"
        };
        var previous = A310.GetPrimary(request);
        A310.SetPrimary(request, value);
        var diagnostic = request is >= Request.A310Battery1Auto and <= Request.A310Battery3Auto
            ? $"{label} changed to {(value != 0 ? "ON" : "OFF")}."
            : $"{label} changed to {value:F2}.";
        return Changed(previous, value, 0.01, diagnostic);
    }

    private NativeAircraftStateChange ApplyNativeOrFbwReadback(Request request, float value)
    {
        if (request is >= Request.FbwBattery1Auto and <= Request.FbwA380ExternalPower4OnTyped)
        {
            var previous = Fbw.GetReadback(request);
            Fbw.SetReadback(request, value);
            var threshold = request is Request.FbwBattery1Potential or Request.FbwBattery2Potential ? 0.1 : 0.01;
            return Changed(
                previous,
                value,
                threshold,
                FbwRuntimeState.FormatDiagnostic(request, value));
        }

        var old = NativeAirbus.GetReadback(request);
        NativeAirbus.SetReadback(request, value);
        var diagnostic = request switch
        {
            Request.NativeCrewOxygen => $"Native INI_CREW_SUPPLY changed to {value:F0}.",
            Request.NativeSpoilersArmed => $"Native INI_SPOILERS_ARMED changed to {value:F0}.",
            Request.NativeGearHandlePosition => $"Native INI_GEAR_HANDLE_STATUS_ANIMATION changed to {value:F2}.",
            Request.NativeEngineModeSelector => $"Native INI_IGNITION_KNOB changed to {value:F0}.",
            Request.NativeA320RunwayTurnoffSelector => $"Native {A320RunwayTurnoffProfile.ReadbackLVar} changed to {value:F0}.",
            _ => null
        };
        return new(true, true,
            diagnostic != null && (!old.HasValue || Math.Abs(old.Value - value) >= 0.01)
                ? diagnostic
                : null);
    }

    private NativeAircraftStateChange UpdateA330ApuBattery(double value)
    {
        var isOn = value >= 0.5;
        var changed = A330.ApuBatteryInputEventOn != isOn;
        A330.SetApuBattery(isOn);
        return new(true, true, changed
            ? $"A330 AIRLINER_ELEC_APU_BAT InputEvent={value:0.###} ({(isOn ? "ON" : "OFF")})."
            : null);
    }

    private static NativeAircraftStateChange UpdateA330Scalar(
        double? previous,
        double value,
        Action<double> setter,
        double threshold,
        string diagnostic,
        bool apply = true)
    {
        setter(value);
        return Changed(previous, value, threshold, diagnostic, apply);
    }

    private static NativeAircraftStateChange Changed(
        double? previous,
        double value,
        double threshold,
        string diagnostic,
        bool apply = true) =>
        new(true, apply,
            !previous.HasValue || Math.Abs(previous.Value - value) >= threshold
                ? diagnostic
                : null);

    private static double? ResolveLiveThenCommanded(
        float? live,
        float? commanded,
        DateTime? commandedUtc,
        DateTime utcNow) =>
        live.HasValue
            ? live.Value
            : commanded.HasValue && IsCurrent(commandedUtc, utcNow)
                ? commanded.Value
                : null;

    private static bool IsCurrent(DateTime? commandedUtc, DateTime utcNow) =>
        commandedUtc.HasValue
        && utcNow - commandedUtc.Value < TimeSpan.FromSeconds(10);

    internal sealed class A310RuntimeState
    {
        private readonly double?[] _apuInputStates = new double?[4];
        private readonly float?[] _initialLightStates = new float?[8];
        private readonly float?[] _flow2States = new float?[24];
        private readonly float?[] _flow3ApuStates = new float?[5];
        private readonly float?[] _flow4EngineStartStates = new float?[7];
        private readonly float?[] _fuelPumpStates = new float?[12];
        private readonly float?[] _flow5States = new float?[3];
        private readonly float?[] _altimeterStandardStates = new float?[3];

        public ReadOnlyCollection<double?> ApuInputStates { get; }
        public ReadOnlyCollection<float?> InitialLightStates { get; }
        public ReadOnlyCollection<float?> Flow2States { get; }
        public ReadOnlyCollection<float?> Flow3ApuStates { get; }
        public ReadOnlyCollection<float?> Flow4EngineStartStates { get; }
        public ReadOnlyCollection<float?> FuelPumpStates { get; }
        public ReadOnlyCollection<float?> Flow5States { get; }
        public ReadOnlyCollection<float?> AltimeterStandardStates { get; }

        public bool? Battery1Auto { get; private set; }
        public bool? Battery2Auto { get; private set; }
        public bool? Battery3Auto { get; private set; }
        public float? HydraulicEngine1 { get; private set; }
        public float? HydraulicEngine1A { get; private set; }
        public float? HydraulicEngine2 { get; private set; }
        public float? HydraulicEngine2B { get; private set; }
        public float? HydraulicElectric { get; private set; }
        public float? CaptainWiper { get; private set; }
        public float? FirstOfficerWiper { get; private set; }
        public float? WeatherRadarSystem { get; private set; }
        public float? Irs1 { get; private set; }
        public float? Irs2 { get; private set; }
        public float? Irs3 { get; private set; }
        public float? OxygenSupply { get; private set; }
        public float? ApuFireTest { get; private set; }
        public float? ApuLoopTest { get; private set; }
        public float? AnnunciatorTest { get; private set; }
        public bool ApuFireTestObserved { get; private set; }
        public bool ApuLoopTestObserved { get; private set; }
        public bool AnnunciatorTestObserved { get; private set; }
        public bool CargoSmokeTestObserved { get; private set; }
        public bool CargoSmokeIndicationsObserved { get; private set; }
        public bool EgpwsTestObserved { get; private set; }
        public float? GearHandleStatus { get; private set; }

        public A310RuntimeState()
        {
            ApuInputStates = Array.AsReadOnly(_apuInputStates);
            InitialLightStates = Array.AsReadOnly(_initialLightStates);
            Flow2States = Array.AsReadOnly(_flow2States);
            Flow3ApuStates = Array.AsReadOnly(_flow3ApuStates);
            Flow4EngineStartStates = Array.AsReadOnly(_flow4EngineStartStates);
            FuelPumpStates = Array.AsReadOnly(_fuelPumpStates);
            Flow5States = Array.AsReadOnly(_flow5States);
            AltimeterStandardStates = Array.AsReadOnly(_altimeterStandardStates);
        }

        internal double? GetPrimary(Request request) => request switch
        {
            Request.A310Battery1Auto => Battery1Auto.HasValue ? (Battery1Auto.Value ? 1 : 0) : null,
            Request.A310Battery2Auto => Battery2Auto.HasValue ? (Battery2Auto.Value ? 1 : 0) : null,
            Request.A310Battery3Auto => Battery3Auto.HasValue ? (Battery3Auto.Value ? 1 : 0) : null,
            Request.A310HydraulicEngine1 => HydraulicEngine1,
            Request.A310HydraulicEngine1A => HydraulicEngine1A,
            Request.A310HydraulicEngine2 => HydraulicEngine2,
            Request.A310HydraulicEngine2B => HydraulicEngine2B,
            Request.A310HydraulicElectric => HydraulicElectric,
            Request.A310CaptainWiper => CaptainWiper,
            Request.A310FirstOfficerWiper => FirstOfficerWiper,
            Request.A310WeatherRadarSystem => WeatherRadarSystem,
            Request.A310Irs1 => Irs1,
            Request.A310Irs2 => Irs2,
            Request.A310Irs3 => Irs3,
            Request.A310OxygenSupply => OxygenSupply,
            Request.A310ApuFireTest => ApuFireTest,
            Request.A310ApuLoopTest => ApuLoopTest,
            _ => AnnunciatorTest
        };

        internal void SetPrimary(Request request, float value)
        {
            switch (request)
            {
                case Request.A310Battery1Auto: Battery1Auto = value != 0; break;
                case Request.A310Battery2Auto: Battery2Auto = value != 0; break;
                case Request.A310Battery3Auto: Battery3Auto = value != 0; break;
                case Request.A310HydraulicEngine1: HydraulicEngine1 = value; break;
                case Request.A310HydraulicEngine1A: HydraulicEngine1A = value; break;
                case Request.A310HydraulicEngine2: HydraulicEngine2 = value; break;
                case Request.A310HydraulicEngine2B: HydraulicEngine2B = value; break;
                case Request.A310HydraulicElectric: HydraulicElectric = value; break;
                case Request.A310CaptainWiper: CaptainWiper = value; break;
                case Request.A310FirstOfficerWiper: FirstOfficerWiper = value; break;
                case Request.A310WeatherRadarSystem: WeatherRadarSystem = value; break;
                case Request.A310Irs1: Irs1 = value; break;
                case Request.A310Irs2: Irs2 = value; break;
                case Request.A310Irs3: Irs3 = value; break;
                case Request.A310OxygenSupply: OxygenSupply = value; break;
                case Request.A310ApuFireTest: ApuFireTest = value; ApuFireTestObserved |= value > 0.5f; break;
                case Request.A310ApuLoopTest: ApuLoopTest = value; ApuLoopTestObserved |= Math.Abs(value) > 0.5f; break;
                default: AnnunciatorTest = value; AnnunciatorTestObserved |= value > 0.5f; break;
            }
        }

        internal void SetApuInput(int index, double value) => _apuInputStates[index] = value;
        internal void SetInitialLight(int index, float value) => _initialLightStates[index] = value;
        internal void SetFlow2(int index, float value, Request request)
        {
            _flow2States[index] = value;
            if (request == Request.A310Flow2CargoSmokeTest) CargoSmokeTestObserved |= Math.Abs(value) > 0.5f;
            else if (request == Request.A310Flow2EgpwsTest) EgpwsTestObserved |= Math.Abs(value) > 0.5f;
            if (request is >= Request.A310Flow2CargoSmokeForward and <= Request.A310Flow2CargoSmokeBulk)
                CargoSmokeIndicationsObserved |= value > 0.5f;
        }
        internal void SetFlow3Apu(int index, float value) => _flow3ApuStates[index] = value;
        internal void SetFlow4(int index, float value) => _flow4EngineStartStates[index] = value;
        internal void SetFuelPump(int index, float value) => _fuelPumpStates[index] = value;
        internal void SetFlow5(int index, float value) => _flow5States[index] = value;
        internal void SetGearHandleStatus(float value) => GearHandleStatus = value;
        internal void SetAltimeterStandard(int index, float value) => _altimeterStandardStates[index] = value;
        internal void ResetApuFireObservation() { ApuFireTestObserved = false; ApuLoopTestObserved = false; }
        internal void ResetAnnunciatorObservation() => AnnunciatorTestObserved = false;
        internal void ResetCargoSmokeObservation() { CargoSmokeTestObserved = false; CargoSmokeIndicationsObserved = false; }
        internal void ResetEgpwsObservation() => EgpwsTestObserved = false;

        internal void Reset()
        {
            Battery1Auto = Battery2Auto = Battery3Auto = null;
            HydraulicEngine1 = HydraulicEngine1A = HydraulicEngine2 = HydraulicEngine2B = HydraulicElectric = null;
            CaptainWiper = FirstOfficerWiper = WeatherRadarSystem = null;
            Irs1 = Irs2 = Irs3 = OxygenSupply = null;
            ApuFireTest = ApuLoopTest = AnnunciatorTest = GearHandleStatus = null;
            ApuFireTestObserved = ApuLoopTestObserved = AnnunciatorTestObserved = false;
            CargoSmokeTestObserved = CargoSmokeIndicationsObserved = EgpwsTestObserved = false;
            Array.Clear(_apuInputStates, 0, _apuInputStates.Length);
            Array.Clear(_initialLightStates, 0, _initialLightStates.Length);
            Array.Clear(_flow2States, 0, _flow2States.Length);
            Array.Clear(_flow3ApuStates, 0, _flow3ApuStates.Length);
            Array.Clear(_flow4EngineStartStates, 0, _flow4EngineStartStates.Length);
            Array.Clear(_fuelPumpStates, 0, _fuelPumpStates.Length);
            Array.Clear(_flow5States, 0, _flow5States.Length);
            Array.Clear(_altimeterStandardStates, 0, _altimeterStandardStates.Length);
        }
    }

    internal sealed class A330RuntimeState
    {
        private readonly double?[] _fuelPumpInputStates = new double?[6];
        private readonly double?[] _signInputStates = new double?[3];
        private readonly double?[] _adirsInputStates = new double?[3];
        private readonly double?[] _apuInputStates = new double?[3];
        public ReadOnlyCollection<double?> FuelPumpInputStates { get; }
        public ReadOnlyCollection<double?> SignInputStates { get; }
        public ReadOnlyCollection<double?> AdirsInputStates { get; }
        public ReadOnlyCollection<double?> ApuInputStates { get; }
        public double? StrobeInputState { get; private set; }
        public double? NavLogoInputState { get; private set; }
        public double? TransponderModeInputState { get; private set; }
        public double? CrewOxygenInputState { get; private set; }
        public double? SpoilerLeverInputState { get; private set; }
        public bool? CommandedSpoilersArmed { get; private set; }
        public double? FlapsInputState { get; private set; }
        public A330AutobrakeReadback Autobrake { get; } = new();
        public double? WeatherRadarPwsInputState { get; private set; }
        public double? NoseLightInputState { get; private set; }
        public double? LandingLightInputState { get; private set; }
        public double? TcasTrafficInputState { get; private set; }
        public double? TcasAltitudeInputState { get; private set; }
        public bool? ApuBatteryInputEventOn { get; private set; }

        public A330RuntimeState()
        {
            FuelPumpInputStates = Array.AsReadOnly(_fuelPumpInputStates);
            SignInputStates = Array.AsReadOnly(_signInputStates);
            AdirsInputStates = Array.AsReadOnly(_adirsInputStates);
            ApuInputStates = Array.AsReadOnly(_apuInputStates);
        }
        internal void SetFuelPump(int index, double value) => _fuelPumpInputStates[index] = value;
        internal void SetSign(int index, double value) => _signInputStates[index] = value;
        internal void SetAdirs(int index, double value) => _adirsInputStates[index] = value;
        internal void SetApu(int index, double value) => _apuInputStates[index] = value;
        internal void SetStrobe(double value) => StrobeInputState = value;
        internal void SetNavLogo(double value) => NavLogoInputState = value;
        internal void SetTransponderMode(double value) => TransponderModeInputState = value;
        internal void SetCrewOxygen(double value) => CrewOxygenInputState = value;
        internal void SetSpoilerLever(double value) => SpoilerLeverInputState = value;
        internal void SetCommandedSpoilers(bool value) => CommandedSpoilersArmed = value;
        internal void SetFlaps(double value) => FlapsInputState = value;
        internal void SetWeatherRadarPws(double value) => WeatherRadarPwsInputState = value;
        internal void SetNoseLight(double value) => NoseLightInputState = value;
        internal void SetLandingLight(double value) => LandingLightInputState = value;
        internal void SetTcasTraffic(double value) => TcasTrafficInputState = value;
        internal void SetTcasAltitude(double value) => TcasAltitudeInputState = value;
        internal void SetApuBattery(bool value) => ApuBatteryInputEventOn = value;
        internal void ClearCommandedState() => CommandedSpoilersArmed = null;
        internal void Reset()
        {
            Array.Clear(_fuelPumpInputStates, 0, _fuelPumpInputStates.Length);
            Array.Clear(_signInputStates, 0, _signInputStates.Length);
            Array.Clear(_adirsInputStates, 0, _adirsInputStates.Length);
            Array.Clear(_apuInputStates, 0, _apuInputStates.Length);
            StrobeInputState = NavLogoInputState = TransponderModeInputState = CrewOxygenInputState = null;
            SpoilerLeverInputState = FlapsInputState = WeatherRadarPwsInputState = NoseLightInputState = null;
            LandingLightInputState = TcasTrafficInputState = TcasAltitudeInputState = null;
            ApuBatteryInputEventOn = null;
            ClearCommandedState();
            Autobrake.Reset();
        }
    }

    internal sealed class NativeAirbusRuntimeState
    {
        private readonly float?[] _values = new float?[48];
        private static int Index(Request request) => request switch
        {
            Request.NativeEngineModeSelector => 46,
            Request.NativeA320RunwayTurnoffSelector => 47,
            _ => (int)request - (int)Request.NativeBattery1
        };
        internal float? GetReadback(Request request) => _values[Index(request)];
        internal void SetReadback(Request request, float value) => _values[Index(request)] = value;
        public float? this[Request request] => GetReadback(request);
        public bool? Battery1On => AsBool(Request.NativeBattery1);
        public bool? Battery2On => AsBool(Request.NativeBattery2);
        public float? FuelPump1 => this[Request.NativeFuelPump1]; public float? FuelPump2 => this[Request.NativeFuelPump2];
        public float? FuelPump3 => this[Request.NativeFuelPump3]; public float? FuelPump4 => this[Request.NativeFuelPump4];
        public float? FuelPump5 => this[Request.NativeFuelPump5]; public float? FuelPump6 => this[Request.NativeFuelPump6];
        public float? NavLogoSelectorPosition => this[Request.NativeNavLogoSelector];
        public float? ApuAvailable => this[Request.NativeApuAvailable]; public float? ApuMasterSwitch => this[Request.NativeApuMasterSwitch];
        public float? ApuStartButton => this[Request.NativeApuStartButton]; public float? ApuBleedButton => this[Request.NativeApuBleedButton];
        public float? ApuGeneratorOn => this[Request.NativeApuGeneratorOn]; public float? ApuFlapPercent => this[Request.NativeApuFlapPercent];
        public float? Adirs1State => this[Request.NativeAdirs1State]; public float? Adirs2State => this[Request.NativeAdirs2State]; public float? Adirs3State => this[Request.NativeAdirs3State];
        public float? AdirsOnBattery => this[Request.NativeAdirsOnBattery]; public float? CrewOxygen => this[Request.NativeCrewOxygen]; public float? StrobeSelector => this[Request.NativeStrobeSelector];
        public float? ApuFireTest => this[Request.NativeApuFireTest]; public float? Engine1FireTest => this[Request.NativeEngine1FireTest]; public float? Engine2FireTest => this[Request.NativeEngine2FireTest];
        public float? ApuFireWarningLit => this[Request.NativeApuFireWarningLit]; public float? ApuFireSound => this[Request.NativeApuFireSound];
        public float? Engine1FireWarningLit => this[Request.NativeEngine1FireWarningLit]; public float? Engine1FireSound => this[Request.NativeEngine1FireSound];
        public float? Engine2FireWarningLit => this[Request.NativeEngine2FireWarningLit]; public float? Engine2FireSound => this[Request.NativeEngine2FireSound];
        public float? SeatbeltSelector => this[Request.NativeSeatbeltSelector]; public float? SeatbeltSignsOn => this[Request.NativeSeatbeltSignsOn];
        public float? NoSmokingSelector => this[Request.NativeNoSmokingSelector]; public float? NoSmokingSignsOn => this[Request.NativeNoSmokingSignsOn];
        public float? EmergencyExitSelector => this[Request.NativeEmergencyExitSelector]; public float? SpoilersArmed => this[Request.NativeSpoilersArmed];
        public float? AutobrakeLevel => this[Request.NativeAutobrakeLevel]; public float? TcasAltitudeReporting => this[Request.NativeTcasAltitudeReporting];
        public float? GearHandlePosition => this[Request.NativeGearHandlePosition]; public float? WeatherRadarPwsSelector => this[Request.NativeWeatherRadarPwsSelector];
        public float? NoseLightSelector => this[Request.NativeNoseLightSelector]; public float? LeftLandingLightSelector => this[Request.NativeLeftLandingLightSelector];
        public float? RightLandingLightSelector => this[Request.NativeRightLandingLightSelector]; public float? TransponderAtcState => this[Request.NativeTransponderAtcState];
        public float? TcasMode => this[Request.NativeTcasMode]; public float? TransponderStandby => this[Request.NativeTransponderStandby];
        public float? EngineModeSelector => this[Request.NativeEngineModeSelector]; public float? A320RunwayTurnoffSelector => this[Request.NativeA320RunwayTurnoffSelector];
        public bool IsReady => Enumerable.Range((int)Request.NativeBattery1, 46).All(value => this[(Request)value].HasValue);
        private bool? AsBool(Request request) => this[request].HasValue ? this[request]!.Value != 0 : null;
        internal void Reset() => Array.Clear(_values, 0, _values.Length);
    }

    internal sealed class FbwRuntimeState
    {
        private readonly Dictionary<Request, float> _readbacks = new();
        public float? this[Request request] => _readbacks.TryGetValue(request, out var value) ? value : null;
        internal float? GetReadback(Request request) => this[request];
        internal void SetReadback(Request request, float value) => _readbacks[request] = value;
        private bool? Bool(Request request) => this[request].HasValue ? this[request]!.Value != 0 : null;
        public bool? Battery1Auto => Bool(Request.FbwBattery1Auto); public bool? Battery2Auto => Bool(Request.FbwBattery2Auto);
        public bool? Battery1AutoTyped => Bool(Request.FbwBattery1AutoTyped); public bool? Battery2AutoTyped => Bool(Request.FbwBattery2AutoTyped);
        public bool? CommandedBattery1Auto { get; private set; } public bool? CommandedBattery2Auto { get; private set; }
        public bool? ExternalPowerAvailable => Bool(Request.FbwExternalPowerAvailable); public bool? ExternalPowerOn => Bool(Request.FbwExternalPowerOn);
        public bool? ExternalPowerAvailableTyped => Bool(Request.FbwExternalPowerAvailableTyped); public bool? ExternalPowerOnTyped => Bool(Request.FbwExternalPowerOnTyped);
        public bool? A380ExternalPower1AvailableTyped => Bool(Request.FbwA380ExternalPower1AvailableTyped); public bool? A380ExternalPower1OnTyped => Bool(Request.FbwA380ExternalPower1OnTyped);
        public bool? A380ExternalPower2AvailableTyped => Bool(Request.FbwA380ExternalPower2AvailableTyped); public bool? A380ExternalPower2OnTyped => Bool(Request.FbwA380ExternalPower2OnTyped);
        public bool? A380ExternalPower3AvailableTyped => Bool(Request.FbwA380ExternalPower3AvailableTyped); public bool? A380ExternalPower3OnTyped => Bool(Request.FbwA380ExternalPower3OnTyped);
        public bool? A380ExternalPower4AvailableTyped => Bool(Request.FbwA380ExternalPower4AvailableTyped); public bool? A380ExternalPower4OnTyped => Bool(Request.FbwA380ExternalPower4OnTyped);
        public float? Battery1Potential => this[Request.FbwBattery1Potential]; public float? Battery2Potential => this[Request.FbwBattery2Potential];
        public bool? ApuMasterSwitch => Bool(Request.FbwApuMasterSwitch); public bool? ApuStartButton => Bool(Request.FbwApuStartButton);
        public bool? ApuStartAvailable => Bool(Request.FbwApuStartAvailable); public bool? ApuBleedButton => Bool(Request.FbwApuBleedButton);
        public float? TransponderMode => this[Request.FbwTransponderMode]; public bool? ParkingBrake => Bool(Request.FbwParkingBrake);
        public float? Engine1State => this[Request.FbwEngine1State]; public float? Engine2State => this[Request.FbwEngine2State];
        public float? Engine1N1 => this[Request.FbwEngine1N1]; public float? Engine2N1 => this[Request.FbwEngine2N1];
        public bool? Engine1StarterValveOpen => Bool(Request.FbwEngine1StarterValveOpen); public bool? Engine2StarterValveOpen => Bool(Request.FbwEngine2StarterValveOpen);
        public bool? SpoilersArmed => Bool(Request.FbwSpoilersArmed); public float? FlapsHandleIndex => this[Request.FbwFlapsHandleIndex];
        public float? AutobrakeLevel => this[Request.FbwAutobrakeLevel]; public float? WeatherRadarPwsSelector => this[Request.FbwWeatherRadarPwsSelector];
        public bool? TcasAltitudeReporting => Bool(Request.FbwTcasAltitudeReporting); public float? TcasMode => this[Request.FbwTcasMode];
        public float? Adirs1Selector => this[Request.FbwAdirs1Selector]; public float? Adirs2Selector => this[Request.FbwAdirs2Selector]; public float? Adirs3Selector => this[Request.FbwAdirs3Selector];
        public float? Adirs1SelectorTyped => this[Request.FbwAdirs1SelectorTyped]; public float? Adirs2SelectorTyped => this[Request.FbwAdirs2SelectorTyped]; public float? Adirs3SelectorTyped => this[Request.FbwAdirs3SelectorTyped];
        public bool? AdirsOnBattery => Bool(Request.FbwAdirsOnBattery); public bool? CrewOxygen => Bool(Request.FbwCrewOxygen); public bool? CrewOxygenTyped => Bool(Request.FbwCrewOxygenTyped);
        public float? NavLogoSelector => this[Request.FbwNavLogoSelector]; public float? NavLogoSelectorTyped => this[Request.FbwNavLogoSelectorTyped];
        public bool? StrobeAuto => Bool(Request.FbwStrobeAuto); public float? StrobeLightState => this[Request.FbwStrobeLightState];
        public float? SeatbeltSelector => this[Request.FbwSeatbeltSelector]; public float? NoSmokingSelector => this[Request.FbwNoSmokingSelector]; public float? EmergencyExitSelector => this[Request.FbwEmergencyExitSelector];
        public bool? CommandedSpoilersArmed { get; private set; } public DateTime? CommandedSpoilersArmedUtc { get; private set; }
        public float? CommandedAutobrakeLevel { get; private set; } public DateTime? CommandedAutobrakeLevelUtc { get; private set; }
        public float? CommandedWeatherRadarPwsSelector { get; private set; } public DateTime? CommandedWeatherRadarPwsSelectorUtc { get; private set; }
        public float? CommandedNoseLightSelector { get; private set; } public DateTime? CommandedNoseLightSelectorUtc { get; private set; }
        public bool? CommandedTcasAltitudeReporting { get; private set; } public DateTime? CommandedTcasAltitudeReportingUtc { get; private set; }
        public float? CommandedTcasMode { get; private set; } public DateTime? CommandedTcasModeUtc { get; private set; }
        public float? CommandedLandingLightSelector { get; private set; } public DateTime? CommandedLandingLightSelectorUtc { get; private set; }
        public float? CommandedAdirs1Selector { get; private set; } public DateTime? CommandedAdirs1SelectorUtc { get; private set; }
        public float? CommandedAdirs2Selector { get; private set; } public DateTime? CommandedAdirs2SelectorUtc { get; private set; }
        public float? CommandedAdirs3Selector { get; private set; } public DateTime? CommandedAdirs3SelectorUtc { get; private set; }
        public bool? CommandedCrewOxygen { get; private set; } public DateTime? CommandedCrewOxygenUtc { get; private set; }
        internal void SetCommandedBattery(int battery, bool value) { if (battery == 1) CommandedBattery1Auto = value; else CommandedBattery2Auto = value; }
        internal void SetCommandedAdirs(int adirs, float value, DateTime utc) { if (adirs == 1) { CommandedAdirs1Selector = value; CommandedAdirs1SelectorUtc = utc; } else if (adirs == 2) { CommandedAdirs2Selector = value; CommandedAdirs2SelectorUtc = utc; } else { CommandedAdirs3Selector = value; CommandedAdirs3SelectorUtc = utc; } }
        internal void SetCommandedCrewOxygen(bool value, DateTime utc) { CommandedCrewOxygen = value; CommandedCrewOxygenUtc = utc; }
        internal void SetCommandedSpoilers(bool value, DateTime utc) { CommandedSpoilersArmed = value; CommandedSpoilersArmedUtc = utc; }
        internal void SetCommandedAutobrake(float value, DateTime utc) { CommandedAutobrakeLevel = value; CommandedAutobrakeLevelUtc = utc; }
        internal void SetCommandedWeatherRadarPws(float value, DateTime utc) { CommandedWeatherRadarPwsSelector = value; CommandedWeatherRadarPwsSelectorUtc = utc; }
        internal void SetCommandedNoseLight(float value, DateTime utc) { CommandedNoseLightSelector = value; CommandedNoseLightSelectorUtc = utc; }
        internal void SetCommandedLandingLight(float value, DateTime utc) { CommandedLandingLightSelector = value; CommandedLandingLightSelectorUtc = utc; }
        internal void SetCommandedTcasAltitude(bool value, DateTime utc) { CommandedTcasAltitudeReporting = value; CommandedTcasAltitudeReportingUtc = utc; }
        internal void SetCommandedTcasMode(float value, DateTime utc) { CommandedTcasMode = value; CommandedTcasModeUtc = utc; }
        internal void ClearCommandedState()
        {
            CommandedBattery1Auto = CommandedBattery2Auto = null;
            CommandedSpoilersArmed = null; CommandedSpoilersArmedUtc = null;
            CommandedAutobrakeLevel = null; CommandedAutobrakeLevelUtc = null;
            CommandedWeatherRadarPwsSelector = null; CommandedWeatherRadarPwsSelectorUtc = null;
            CommandedNoseLightSelector = null; CommandedNoseLightSelectorUtc = null;
            CommandedTcasAltitudeReporting = null; CommandedTcasAltitudeReportingUtc = null;
            CommandedTcasMode = null; CommandedTcasModeUtc = null;
            CommandedLandingLightSelector = null; CommandedLandingLightSelectorUtc = null;
            CommandedAdirs1Selector = CommandedAdirs2Selector = CommandedAdirs3Selector = null;
            CommandedAdirs1SelectorUtc = CommandedAdirs2SelectorUtc = CommandedAdirs3SelectorUtc = null;
            CommandedCrewOxygen = null; CommandedCrewOxygenUtc = null;
        }
        internal void Reset() { _readbacks.Clear(); ClearCommandedState(); }

        internal static string FormatDiagnostic(Request request, float value)
        {
            if (request is Request.FbwBattery1Auto or Request.FbwBattery2Auto
                or Request.FbwBattery1AutoTyped or Request.FbwBattery2AutoTyped)
            {
                var battery = request is Request.FbwBattery1Auto or Request.FbwBattery1AutoTyped ? 1 : 2;
                var typed = request is Request.FbwBattery1AutoTyped or Request.FbwBattery2AutoTyped
                    ? " typed"
                    : string.Empty;
                return $"FBW A32NX BAT {battery} AUTO{typed} changed to {value:F0}.";
            }
            if (request is Request.FbwBattery1Potential or Request.FbwBattery2Potential)
            {
                var battery = request == Request.FbwBattery1Potential ? 1 : 2;
                return $"FBW A32NX BAT {battery} potential changed to {value:F1} V.";
            }

            var label = request switch
            {
                Request.FbwExternalPowerAvailable => "FBW A32NX EXT PWR available",
                Request.FbwExternalPowerOn => "FBW A32NX EXT PWR ON",
                Request.FbwExternalPowerAvailableTyped => "FBW A32NX EXT PWR available typed",
                Request.FbwExternalPowerOnTyped => "FBW A32NX EXT PWR ON typed",
                Request.FbwA380ExternalPower1AvailableTyped => "FBW A380X EXT PWR 1 available typed",
                Request.FbwA380ExternalPower1OnTyped => "FBW A380X EXT PWR 1 ON typed",
                Request.FbwA380ExternalPower2AvailableTyped => "FBW A380X EXT PWR 2 available typed",
                Request.FbwA380ExternalPower2OnTyped => "FBW A380X EXT PWR 2 ON typed",
                Request.FbwA380ExternalPower3AvailableTyped => "FBW A380X EXT PWR 3 available typed",
                Request.FbwA380ExternalPower3OnTyped => "FBW A380X EXT PWR 3 ON typed",
                Request.FbwA380ExternalPower4AvailableTyped => "FBW A380X EXT PWR 4 available typed",
                Request.FbwA380ExternalPower4OnTyped => "FBW A380X EXT PWR 4 ON typed",
                Request.FbwAdirs1Selector => "FBW A32NX ADIRS 1 selector",
                Request.FbwAdirs2Selector => "FBW A32NX ADIRS 2 selector",
                Request.FbwAdirs3Selector => "FBW A32NX ADIRS 3 selector",
                Request.FbwAdirs1SelectorTyped => "FBW A32NX ADIRS 1 selector typed",
                Request.FbwAdirs2SelectorTyped => "FBW A32NX ADIRS 2 selector typed",
                Request.FbwAdirs3SelectorTyped => "FBW A32NX ADIRS 3 selector typed",
                Request.FbwAdirsOnBattery => "FBW A32NX ADIRS ON BAT",
                Request.FbwCrewOxygen => "FBW A32NX crew oxygen",
                Request.FbwCrewOxygenTyped => "FBW A32NX crew oxygen typed",
                Request.FbwNavLogoSelector => "FBW A32NX NAV/LOGO selector",
                Request.FbwNavLogoSelectorTyped => "FBW A32NX NAV/LOGO selector typed",
                Request.FbwStrobeAuto => "FBW A32NX strobe auto",
                Request.FbwStrobeLightState => "FBW A32NX strobe light state",
                Request.FbwSeatbeltSelector => "FBW A32NX seatbelt selector",
                Request.FbwNoSmokingSelector => "FBW A32NX no-smoking selector",
                Request.FbwEmergencyExitSelector => "FBW A32NX emergency-exit selector",
                Request.FbwApuMasterSwitch => "FBW A32NX APU master",
                Request.FbwApuStartButton => "FBW A32NX APU start",
                Request.FbwApuStartAvailable => "FBW A32NX APU available",
                Request.FbwApuBleedButton => "FBW A32NX APU bleed",
                Request.FbwTransponderMode => "FBW A32NX transponder mode",
                Request.FbwParkingBrake => "FBW A32NX parking brake",
                Request.FbwEngine1State => "FBW A32NX engine 1 state",
                Request.FbwEngine2State => "FBW A32NX engine 2 state",
                Request.FbwEngine1N1 => "FBW A32NX engine 1 N1",
                Request.FbwEngine2N1 => "FBW A32NX engine 2 N1",
                Request.FbwEngine1StarterValveOpen => "FBW A32NX engine 1 starter valve",
                Request.FbwEngine2StarterValveOpen => "FBW A32NX engine 2 starter valve",
                Request.FbwSpoilersArmed => "FBW A32NX spoilers armed",
                Request.FbwFlapsHandleIndex => "FBW A32NX flaps handle",
                Request.FbwAutobrakeLevel => "FBW A32NX autobrake mode",
                Request.FbwWeatherRadarPwsSelector => "FBW A32NX WXR/PWS selector",
                Request.FbwTcasAltitudeReporting => "FBW A32NX TCAS altitude reporting",
                Request.FbwTcasMode => "FBW A32NX TCAS mode",
                _ => request.ToString()
            };
            var isBool = request is Request.FbwExternalPowerAvailable
                or Request.FbwExternalPowerOn
                or Request.FbwExternalPowerAvailableTyped
                or Request.FbwExternalPowerOnTyped
                or Request.FbwA380ExternalPower1AvailableTyped
                or Request.FbwA380ExternalPower1OnTyped
                or Request.FbwA380ExternalPower2AvailableTyped
                or Request.FbwA380ExternalPower2OnTyped
                or Request.FbwA380ExternalPower3AvailableTyped
                or Request.FbwA380ExternalPower3OnTyped
                or Request.FbwA380ExternalPower4AvailableTyped
                or Request.FbwA380ExternalPower4OnTyped
                or Request.FbwAdirsOnBattery
                or Request.FbwCrewOxygen
                or Request.FbwCrewOxygenTyped
                or Request.FbwStrobeAuto
                or Request.FbwApuMasterSwitch
                or Request.FbwApuStartButton
                or Request.FbwApuStartAvailable
                or Request.FbwApuBleedButton
                or Request.FbwParkingBrake
                or Request.FbwEngine1StarterValveOpen
                or Request.FbwEngine2StarterValveOpen
                or Request.FbwSpoilersArmed
                or Request.FbwTcasAltitudeReporting;
            return isBool
                ? $"{label} changed to {(value != 0 ? "ON" : "OFF")}."
                : $"{label} changed to {value:F2}.";
        }
    }
}
