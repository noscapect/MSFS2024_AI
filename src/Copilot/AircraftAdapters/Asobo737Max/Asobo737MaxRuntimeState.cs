namespace Msfs2024Ai.Copilot.AircraftAdapters.Asobo737Max;

internal readonly struct Asobo737MaxRuntimeChange
{
    public Asobo737MaxRuntimeChange(bool handled, string? diagnostic)
    {
        Handled = handled;
        Diagnostic = diagnostic;
    }

    public bool Handled { get; }
    public string? Diagnostic { get; }
}

internal sealed class Asobo737MaxRuntimeState
{
    private static readonly string[] FuelPumpInputEventNames =
    {
        "FUEL_PUMP_AFT_1", "FUEL_PUMP_FWD_1", "FUEL_PUMP_CTR_L",
        "FUEL_PUMP_CTR_R", "FUEL_PUMP_FWD_2", "FUEL_PUMP_AFT_2"
    };

    private readonly ulong?[] _apuGeneratorInputEventHashes = new ulong?[2];
    private readonly ulong?[] _fuelPumpInputEventHashes = new ulong?[6];
    private readonly double?[] _apuGeneratorInputStates = new double?[2];
    private readonly double?[] _fuelPumpInputStates = new double?[6];
    private readonly double?[] _engineBleedInputStates = new double?[2];
    private readonly double?[] _engineGeneratorInputStates = new double?[2];
    private readonly double?[] _electricHydraulicPumpInputStates = new double?[2];
    private readonly double?[] _runwayTurnoffInputStates = new double?[2];
    private readonly double?[] _landingLightInputStates = new double?[2];

    public bool? BatteryInputEventOn { get; private set; }
    public bool? BatteryCoverInputEventOn { get; private set; }
    public ulong? BatteryInputEventHash { get; private set; }
    public ulong? BatteryCoverInputEventHash { get; private set; }
    public ulong? LeftIrsInputEventHash { get; private set; }
    public ulong? RightIrsInputEventHash { get; private set; }
    public ulong? PositionLightInputEventHash { get; private set; }
    public ulong? LogoLightInputEventHash { get; private set; }
    public ulong? EmergencyExitInputEventHash { get; private set; }
    public ulong? EmergencyExitCoverInputEventHash { get; private set; }
    public ulong? SeatbeltsInputEventHash { get; private set; }
    public ulong? NoSmokingInputEventHash { get; private set; }
    public ulong? ApuInputEventHash { get; private set; }
    public ulong? ApuBleedInputEventHash { get; private set; }
    public IReadOnlyList<ulong?> ApuGeneratorInputEventHashes => _apuGeneratorInputEventHashes;
    public IReadOnlyList<ulong?> FuelPumpInputEventHashes => _fuelPumpInputEventHashes;
    private double? _leftIrsInputState, _rightIrsInputState, _positionLightInputState, _logoLightInputState;
    private double? _emergencyExitInputState, _emergencyExitCoverInputState, _seatbeltsInputState, _noSmokingInputState;
    private double? _apuInputState, _apuBleedInputState, _isolationValveInputState, _leftPackInputState, _rightPackInputState;
    private double? _taxiLightInputState, _antiCollisionInputState, _flapsInputState, _autobrakeInputState;
    private double? _autothrottleInputState, _transponderModeInputState, _transponderOperatingModeInputState;
    public double? LeftIrsInputState => _leftIrsInputState;
    public double? RightIrsInputState => _rightIrsInputState;
    public double? PositionLightInputState => _positionLightInputState;
    public double? LogoLightInputState => _logoLightInputState;
    public double? EmergencyExitInputState => _emergencyExitInputState;
    public double? EmergencyExitCoverInputState => _emergencyExitCoverInputState;
    public double? SeatbeltsInputState => _seatbeltsInputState;
    public double? NoSmokingInputState => _noSmokingInputState;
    public double? ApuInputState => _apuInputState;
    public double? ApuBleedInputState => _apuBleedInputState;
    public IReadOnlyList<double?> ApuGeneratorInputStates => _apuGeneratorInputStates;
    public IReadOnlyList<double?> FuelPumpInputStates => _fuelPumpInputStates;
    public double? IsolationValveInputState => _isolationValveInputState;
    public double? LeftPackInputState => _leftPackInputState;
    public double? RightPackInputState => _rightPackInputState;
    public IReadOnlyList<double?> EngineBleedInputStates => _engineBleedInputStates;
    public IReadOnlyList<double?> EngineGeneratorInputStates => _engineGeneratorInputStates;
    public IReadOnlyList<double?> ElectricHydraulicPumpInputStates => _electricHydraulicPumpInputStates;
    public double? TaxiLightInputState => _taxiLightInputState;
    public IReadOnlyList<double?> RunwayTurnoffInputStates => _runwayTurnoffInputStates;
    public IReadOnlyList<double?> LandingLightInputStates => _landingLightInputStates;
    public double? AntiCollisionInputState => _antiCollisionInputState;
    public double? FlapsInputState => _flapsInputState;
    public double? AutobrakeInputState => _autobrakeInputState;
    public double? AutothrottleInputState => _autothrottleInputState;
    public double? TransponderModeInputState => _transponderModeInputState;
    public double? TransponderOperatingModeInputState => _transponderOperatingModeInputState;
    public bool InputEventsEnumerated { get; private set; }

    private double? _commandedLeftIrsState;
    private double? _commandedRightIrsState;
    private DateTime? _commandedLeftIrsUtc;
    private DateTime? _commandedRightIrsUtc;

    public void MarkInputEventsEnumerated() => InputEventsEnumerated = true;

    public IEnumerable<string> RecordEnumeratedInputEvent(string name, ulong hash)
    {
        if (string.Equals(name, "AFT_OVHD_L_IRS", StringComparison.OrdinalIgnoreCase))
        { LeftIrsInputEventHash = hash; yield return $"Asobo 737 MAX left IRS readback bound to InputEvent hash {hash}."; }
        else if (string.Equals(name, "AFT_OVHD_R_IRS", StringComparison.OrdinalIgnoreCase))
        { RightIrsInputEventHash = hash; yield return $"Asobo 737 MAX right IRS readback bound to InputEvent hash {hash}."; }
        else if (string.Equals(name, "LIGHTING_POSITION_LIGHT", StringComparison.OrdinalIgnoreCase))
        { PositionLightInputEventHash = hash; yield return $"Asobo 737 MAX position light readback bound to InputEvent hash {hash}."; }
        else if (string.Equals(name, "LIGHTING_LOGO_LIGHT", StringComparison.OrdinalIgnoreCase))
        { LogoLightInputEventHash = hash; yield return $"Asobo 737 MAX logo light readback bound to InputEvent hash {hash}."; }
        else if (string.Equals(name, "PASSENGER_EXIT_LIGHTS", StringComparison.OrdinalIgnoreCase))
        { EmergencyExitInputEventHash = hash; yield return $"Asobo 737 MAX emergency exit light readback bound to InputEvent hash {hash}."; }
        else if (string.Equals(name, "COMMON_PASSENGER_EXIT_LIGHTS_COVER", StringComparison.OrdinalIgnoreCase))
        { EmergencyExitCoverInputEventHash = hash; yield return $"Asobo 737 MAX emergency exit cover readback bound to InputEvent hash {hash}."; }
        else if (string.Equals(name, "PASSENGER_FASTEN_BELTS", StringComparison.OrdinalIgnoreCase))
        { SeatbeltsInputEventHash = hash; yield return $"Asobo 737 MAX seatbelts readback bound to InputEvent hash {hash}."; }
        else if (string.Equals(name, "PASSENGER_NO_SMOKING", StringComparison.OrdinalIgnoreCase))
        { NoSmokingInputEventHash = hash; yield return $"Asobo 737 MAX no-smoking readback bound to InputEvent hash {hash}."; }
        else if (string.Equals(name, "ENGINE_APU", StringComparison.OrdinalIgnoreCase))
        { ApuInputEventHash = hash; yield return $"Asobo 737 MAX APU selector readback bound to InputEvent hash {hash}."; }
        else if (string.Equals(name, "PNEUMATICS_APU_BLEED", StringComparison.OrdinalIgnoreCase))
        { ApuBleedInputEventHash = hash; yield return $"Asobo 737 MAX APU bleed readback bound to InputEvent {name} hash {hash}."; }
        else if (IsApuGenerator(name, 0)) { _apuGeneratorInputEventHashes[0] = hash; yield return $"Asobo 737 MAX APU generator 1 readback bound to InputEvent {name} hash {hash}."; }
        else if (IsApuGenerator(name, 1)) { _apuGeneratorInputEventHashes[1] = hash; yield return $"Asobo 737 MAX APU generator 2 readback bound to InputEvent {name} hash {hash}."; }
        else
        {
            var index = Array.FindIndex(FuelPumpInputEventNames, candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) { _fuelPumpInputEventHashes[index] = hash; yield return $"Asobo 737 MAX fuel pump {index + 1} readback bound to InputEvent {name} hash {hash}."; }
        }
        if (name.IndexOf("ELECTRICAL_BATTERY", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            yield return $"Asobo 737 MAX input event discovered: {name} hash={hash}.";
            if (string.Equals(name, "COMMON_ELECTRICAL_BATTERY_COVER", StringComparison.OrdinalIgnoreCase)) BatteryCoverInputEventHash = hash;
            else if (string.Equals(name, "ELECTRICAL_BATTERY", StringComparison.OrdinalIgnoreCase)) BatteryInputEventHash = hash;
        }
    }

    public void RecordLeftIrsCommand(double value, DateTime utcNow) { _commandedLeftIrsState = value; _commandedLeftIrsUtc = utcNow; }
    public void RecordRightIrsCommand(double value, DateTime utcNow) { _commandedRightIrsState = value; _commandedRightIrsUtc = utcNow; }
    public double? ResolveIrsState(bool left, DateTime utcNow)
    {
        var input = left ? LeftIrsInputState : RightIrsInputState;
        if (input.HasValue) return input;
        var commanded = left ? _commandedLeftIrsState : _commandedRightIrsState;
        var commandedUtc = left ? _commandedLeftIrsUtc : _commandedRightIrsUtc;
        return commanded.HasValue && commandedUtc.HasValue && utcNow - commandedUtc.Value < TimeSpan.FromSeconds(15) ? commanded : null;
    }

    public Asobo737MaxRuntimeChange ApplyInputEvent(Request request, double value)
    {
        if (request == Request.Asobo737MaxBatteryInputEvent) return SetBattery(value, false);
        if (request == Request.Asobo737MaxBatteryCoverInputEvent) return SetBattery(value, true);
        if (request == Request.Asobo737MaxLeftIrsInputEvent) return Set(ref _leftIrsInputState, value, "left IRS");
        if (request == Request.Asobo737MaxRightIrsInputEvent) return Set(ref _rightIrsInputState, value, "right IRS");
        if (request == Request.Asobo737MaxPositionLightInputEvent) return Set(ref _positionLightInputState, value, "position light");
        if (request == Request.Asobo737MaxLogoLightInputEvent) return Set(ref _logoLightInputState, value, "logo light");
        if (request == Request.Asobo737MaxEmergencyExitInputEvent) return Set(ref _emergencyExitInputState, value, "emergency exit switch");
        if (request == Request.Asobo737MaxEmergencyExitCoverInputEvent) return Set(ref _emergencyExitCoverInputState, value, "emergency exit cover");
        if (request == Request.Asobo737MaxSeatbeltsInputEvent) return Set(ref _seatbeltsInputState, value, "seatbelts");
        if (request == Request.Asobo737MaxNoSmokingInputEvent) return Set(ref _noSmokingInputState, value, "no-smoking");
        if (request == Request.Asobo737MaxApuInputEvent) return Set(ref _apuInputState, value, "APU selector");
        if (request == Request.Asobo737MaxApuBleedInputEvent) return Set(ref _apuBleedInputState, value, "APU bleed");
        if (request is >= Request.Asobo737MaxApuGenerator1InputEvent and <= Request.Asobo737MaxApuGenerator2InputEvent) return SetArray(_apuGeneratorInputStates, (int)request - (int)Request.Asobo737MaxApuGenerator1InputEvent, value, "APU generator");
        if (request is >= Request.Asobo737MaxEngineBleed1InputEvent and <= Request.Asobo737MaxEngineBleed2InputEvent) return SetArray(_engineBleedInputStates, (int)request - (int)Request.Asobo737MaxEngineBleed1InputEvent, value, "engine bleed");
        if (request is >= Request.Asobo737MaxEngineGenerator1InputEvent and <= Request.Asobo737MaxEngineGenerator2InputEvent) return SetArray(_engineGeneratorInputStates, (int)request - (int)Request.Asobo737MaxEngineGenerator1InputEvent, value, "engine generator");
        if (request is >= Request.Asobo737MaxElectricHydraulicPump1InputEvent and <= Request.Asobo737MaxElectricHydraulicPump2InputEvent) return SetArray(_electricHydraulicPumpInputStates, (int)request - (int)Request.Asobo737MaxElectricHydraulicPump1InputEvent, value, "electric hydraulic pump");
        if (request is >= Request.Asobo737MaxRunwayTurnoffLeftInputEvent and <= Request.Asobo737MaxRunwayTurnoffRightInputEvent) return SetArray(_runwayTurnoffInputStates, (int)request - (int)Request.Asobo737MaxRunwayTurnoffLeftInputEvent, value, "runway turnoff light", true);
        if (request is >= Request.Asobo737MaxLandingLightLeftInputEvent and <= Request.Asobo737MaxLandingLightRightInputEvent) return SetArray(_landingLightInputStates, (int)request - (int)Request.Asobo737MaxLandingLightLeftInputEvent, value, "landing light", true);
        if (request == Request.Asobo737MaxIsolationValveInputEvent) return Set(ref _isolationValveInputState, value, "isolation valve");
        if (request == Request.Asobo737MaxLeftPackInputEvent) return Set(ref _leftPackInputState, value, "left pack");
        if (request == Request.Asobo737MaxRightPackInputEvent) return Set(ref _rightPackInputState, value, "right pack");
        if (request == Request.Asobo737MaxTaxiLightInputEvent) return Set(ref _taxiLightInputState, value, "taxi light");
        if (request == Request.Asobo737MaxAntiCollisionInputEvent) return Set(ref _antiCollisionInputState, value, "anti-collision light");
        if (request == Request.Asobo737MaxFlapsInputEvent) return Set(ref _flapsInputState, value, "flaps");
        if (request == Request.Asobo737MaxAutobrakeInputEvent) return Set(ref _autobrakeInputState, value, "autobrake");
        if (request == Request.Asobo737MaxAutothrottleInputEvent) return Set(ref _autothrottleInputState, value, "autothrottle");
        if (request == Request.Asobo737MaxTransponderOperatingModeInputEvent) return Set(ref _transponderOperatingModeInputState, value, "transponder operating mode");
        if (request == Request.Asobo737MaxTransponderModeInputEvent) return Set(ref _transponderModeInputState, value, "transponder mode selector");
        if (request is >= Request.Asobo737MaxFuelPump1InputEvent and <= Request.Asobo737MaxFuelPump6InputEvent) return SetArray(_fuelPumpInputStates, (int)request - (int)Request.Asobo737MaxFuelPump1InputEvent, value, "fuel pump");
        return default;
    }

    public void ResetConnectionState()
    {
        BatteryInputEventOn = null; BatteryCoverInputEventOn = null;
        BatteryInputEventHash = null; BatteryCoverInputEventHash = null;
        LeftIrsInputEventHash = null; RightIrsInputEventHash = null;
        PositionLightInputEventHash = null; LogoLightInputEventHash = null;
        EmergencyExitInputEventHash = null; EmergencyExitCoverInputEventHash = null;
        SeatbeltsInputEventHash = null; NoSmokingInputEventHash = null;
        ApuInputEventHash = null; ApuBleedInputEventHash = null;
        Array.Clear(_apuGeneratorInputEventHashes, 0, _apuGeneratorInputEventHashes.Length);
        Array.Clear(_fuelPumpInputEventHashes, 0, _fuelPumpInputEventHashes.Length);

        _leftIrsInputState = null; _rightIrsInputState = null;
        _positionLightInputState = null; _logoLightInputState = null;
        _emergencyExitInputState = null; _emergencyExitCoverInputState = null;
        _seatbeltsInputState = null; _noSmokingInputState = null;
        _apuInputState = null; _apuBleedInputState = null;
        _isolationValveInputState = null; _leftPackInputState = null; _rightPackInputState = null;
        _taxiLightInputState = null; _antiCollisionInputState = null;
        _flapsInputState = null; _autobrakeInputState = null;
        _autothrottleInputState = null; _transponderModeInputState = null; _transponderOperatingModeInputState = null;
        Array.Clear(_apuGeneratorInputStates, 0, _apuGeneratorInputStates.Length);
        Array.Clear(_fuelPumpInputStates, 0, _fuelPumpInputStates.Length);
        Array.Clear(_engineBleedInputStates, 0, _engineBleedInputStates.Length);
        Array.Clear(_engineGeneratorInputStates, 0, _engineGeneratorInputStates.Length);
        Array.Clear(_electricHydraulicPumpInputStates, 0, _electricHydraulicPumpInputStates.Length);
        Array.Clear(_runwayTurnoffInputStates, 0, _runwayTurnoffInputStates.Length);
        Array.Clear(_landingLightInputStates, 0, _landingLightInputStates.Length);

        _commandedLeftIrsState = null; _commandedRightIrsState = null;
        _commandedLeftIrsUtc = null; _commandedRightIrsUtc = null;
        InputEventsEnumerated = false;
    }

    public void ResetAircraftState() => ResetConnectionState();

    private Asobo737MaxRuntimeChange SetBattery(double value, bool cover)
    {
        var state = cover ? value < 0.5 : value >= 0.5;
        var previous = cover ? BatteryCoverInputEventOn : BatteryInputEventOn;
        if (cover) BatteryCoverInputEventOn = state; else BatteryInputEventOn = state;
        var name = cover ? "COMMON_ELECTRICAL_BATTERY_COVER" : "ELECTRICAL_BATTERY";
        var suffix = cover ? $" (battery {state.ToOnOff()})" : $" ({state.ToOnOff()})";
        return new Asobo737MaxRuntimeChange(true, previous != state ? $"Asobo 737 MAX {name} InputEvent={value:0.###}{suffix}." : null);
    }
    private static Asobo737MaxRuntimeChange Set(ref double? state, double value, string label)
    {
        var previous = state; state = value;
        return new Asobo737MaxRuntimeChange(true, !previous.HasValue || Math.Abs(previous.Value - value) >= 0.01 ? $"Asobo 737 MAX {label} InputEvent={value:0.###}." : null);
    }
    private static Asobo737MaxRuntimeChange SetArray(double?[] states, int index, double value, string label, bool directionalLabel = false)
    {
        var previous = states[index]; states[index] = value;
        var display = directionalLabel ? $"{label} {(index == 0 ? "left" : "right")}" : $"{label} {index + 1}";
        return new Asobo737MaxRuntimeChange(true, !previous.HasValue || Math.Abs(previous.Value - value) >= 0.01 ? $"Asobo 737 MAX {display} InputEvent={value:0.###}." : null);
    }
    private static bool IsApuGenerator(string name, int index) =>
        string.Equals(name, index == 0 ? "ELECTRICAL_APU_GENERATOR_1" : "ELECTRICAL_APU_GENERATOR_2", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, index == 0 ? "ELECTRICAL_APU_GEN_1" : "ELECTRICAL_APU_GEN_2", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, index == 0 ? "APU_GENERATOR_1" : "APU_GENERATOR_2", StringComparison.OrdinalIgnoreCase);
}
