namespace Msfs2024Ai.Copilot.Telemetry;

internal readonly struct PmdgNg3RuntimeUpdate
{
    public PmdgNg3RuntimeUpdate(
        PmdgNg3State state,
        bool becameReady,
        IReadOnlyList<string> diagnostics)
    {
        State = state;
        BecameReady = becameReady;
        Diagnostics = diagnostics;
    }

    public PmdgNg3State State { get; }
    public bool BecameReady { get; }
    public IReadOnlyList<string> Diagnostics { get; }
}

internal readonly struct PmdgNg3ApuRuntimeSnapshot
{
    public PmdgNg3ApuRuntimeSnapshot(
        bool powerEstablished,
        bool available,
        bool bleedWarmupComplete)
    {
        PowerEstablished = powerEstablished;
        Available = available;
        BleedWarmupComplete = bleedWarmupComplete;
    }

    public bool PowerEstablished { get; }
    public bool Available { get; }
    public bool BleedWarmupComplete { get; }
}

/// <summary>
/// Owns PMDG 737 NG3 SDK readback interpretation and its transient runtime
/// reconciliation state. Cockpit command transport and procedure orchestration
/// deliberately remain in <see cref="CopilotService"/>.
/// </summary>
internal sealed class PmdgNg3RuntimeState
{
    private static readonly TimeSpan CommandedReadbackWindow = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ApuBleedWarmup = TimeSpan.FromSeconds(60);

    private byte? _loggedBatterySelector;
    private bool? _loggedGroundPowerAvailable;
    private bool? _loggedGroundPowerOn;
    private string? _loggedElectricalSignature;
    private string? _loggedAirStartSignature;
    private bool _apuGenOffBusSeen;
    private DateTime? _apuAvailableSinceUtc;
    private float? _commandedLeftIrsMode;
    private float? _commandedRightIrsMode;
    private DateTime? _commandedLeftIrsModeUtc;
    private DateTime? _commandedRightIrsModeUtc;
    private bool? _commandedLogoLightOn;
    private DateTime? _commandedLogoLightUtc;
    private float? _commandedPositionStrobeSelector;
    private DateTime? _commandedPositionStrobeUtc;
    private float? _commandedLandingLightSelector;
    private DateTime? _commandedLandingLightUtc;
    private float? _commandedEmergencyExitSelector;
    private DateTime? _commandedEmergencyExitUtc;
    private bool _fireFaultInopTestCompleted;
    private bool _fireOverheatTestCompleted;
    private bool _extinguisherTest1Completed;
    private bool _extinguisherTest2Completed;
    private bool _fireFaultInopActiveObserved;
    private bool _fireOverheatActiveObserved;
    private bool _fireWarnCancellationObserved;
    private bool _extinguisherTest1ActiveObserved;
    private bool _extinguisherTest2ActiveObserved;

    public PmdgNg3State? State { get; private set; }
    public bool IsReady { get; private set; }
    public DateTime? ApuAvailableSinceUtc => _apuAvailableSinceUtc;
    public bool FireFaultInopTestCompleted => _fireFaultInopTestCompleted;
    public bool FireOverheatTestCompleted => _fireOverheatTestCompleted;
    public bool ExtinguisherTest1Completed => _extinguisherTest1Completed;
    public bool ExtinguisherTest2Completed => _extinguisherTest2Completed;

    public PmdgNg3RuntimeUpdate ApplyData(byte[] data)
    {
        var state = ParseState(data);
        State = state;
        ObserveFireTests(state);

        var diagnostics = new List<string>();
        AddElectricalDiagnostics(state, diagnostics);
        AddAirStartDiagnostic(state, diagnostics);

        var becameReady = !IsReady;
        IsReady = true;
        return new PmdgNg3RuntimeUpdate(state, becameReady, diagnostics);
    }

    public PmdgNg3ApuRuntimeSnapshot ObserveAircraftFrame(bool isPmdg737, DateTime utcNow)
    {
        var state = State;
        if (isPmdg737 && state != null)
        {
            if (state.ApuGenOffBus)
            {
                _apuGenOffBusSeen = true;
            }
            else if (state.ApuEgtNeedle <= 0)
            {
                _apuGenOffBusSeen = false;
            }
        }
        else
        {
            _apuGenOffBusSeen = false;
        }

        var powerEstablished =
            isPmdg737
            && state != null
            && state.ApuEgtNeedle > 0
            && _apuGenOffBusSeen
            && state.ApuGen1On
            && state.ApuGen2On
            && !state.ApuGenOffBus
            && !state.TransferBus1Off
            && !state.TransferBus2Off
            && state.AcTransferBus1Powered
            && state.AcTransferBus2Powered;
        var available =
            isPmdg737
            && state != null
            && (state.ApuGenOffBus || powerEstablished);
        if (available)
        {
            if (!_apuAvailableSinceUtc.HasValue)
            {
                _apuAvailableSinceUtc = utcNow;
            }
        }
        else
        {
            _apuAvailableSinceUtc = null;
        }

        var bleedWarmupComplete =
            !isPmdg737
            || (available
                && _apuAvailableSinceUtc.HasValue
                && utcNow - _apuAvailableSinceUtc.Value >= ApuBleedWarmup);
        return new PmdgNg3ApuRuntimeSnapshot(powerEstablished, available, bleedWarmupComplete);
    }

    public void RecordLeftIrsCommand(float position, DateTime utcNow)
    {
        _commandedLeftIrsMode = position;
        _commandedLeftIrsModeUtc = utcNow;
    }

    public void RecordRightIrsCommand(float position, DateTime utcNow)
    {
        _commandedRightIrsMode = position;
        _commandedRightIrsModeUtc = utcNow;
    }

    public void RecordLogoLightCommand(bool on, DateTime utcNow)
    {
        _commandedLogoLightOn = on;
        _commandedLogoLightUtc = utcNow;
    }

    public void RecordPositionStrobeCommand(float position, DateTime utcNow)
    {
        _commandedPositionStrobeSelector = position;
        _commandedPositionStrobeUtc = utcNow;
    }

    public void RecordLandingLightCommand(float position, DateTime utcNow)
    {
        _commandedLandingLightSelector = position;
        _commandedLandingLightUtc = utcNow;
    }

    public void RecordEmergencyExitCommand(float position, DateTime utcNow)
    {
        _commandedEmergencyExitSelector = position;
        _commandedEmergencyExitUtc = utcNow;
    }

    public void ClearLogoLightCommand() =>
        (_commandedLogoLightOn, _commandedLogoLightUtc) = (null, null);

    public void ClearPositionStrobeCommand() =>
        (_commandedPositionStrobeSelector, _commandedPositionStrobeUtc) = (null, null);

    public double ResolveLeftIrsMode(DateTime utcNow) =>
        ResolveCommandedSelectorState(_commandedLeftIrsMode, _commandedLeftIrsModeUtc, State!.IrsLeftMode, utcNow);

    public double ResolveRightIrsMode(DateTime utcNow) =>
        ResolveCommandedSelectorState(_commandedRightIrsMode, _commandedRightIrsModeUtc, State!.IrsRightMode, utcNow);

    public bool ResolveNavigationLightsOn(DateTime utcNow) =>
        ResolveCommandedPositionLightState(
            _commandedPositionStrobeSelector,
            _commandedPositionStrobeUtc,
            State!.PositionStrobeSelector,
            utcNow);

    public bool ResolveLogoLightsOn(DateTime utcNow) =>
        ResolveCommandedBoolState(_commandedLogoLightOn, _commandedLogoLightUtc, State!.LogoLightOn, utcNow);

    public double ResolveNavLogoSelectorPosition(DateTime utcNow) =>
        ResolveLogoLightsOn(utcNow) ? 0 : 2;

    public double ResolvePositionStrobeSelector(DateTime utcNow)
    {
        var value = State!.PositionStrobeSelector;
        if (IsCurrent(_commandedPositionStrobeUtc, utcNow)
            && _commandedPositionStrobeSelector.HasValue)
        {
            value = (byte)Math.Round(_commandedPositionStrobeSelector.Value);
        }

        // App flow semantics: 0=ON/strobe, 1=AUTO/steady, 2=OFF.
        return value == 2 ? 0 : value == 0 ? 1 : 2;
    }

    public double ResolveEmergencyExitSelector(DateTime utcNow) =>
        ResolveCommandedSelectorState(
            _commandedEmergencyExitSelector,
            _commandedEmergencyExitUtc,
            State!.EmergencyExitLights,
            utcNow);

    public double ResolveLandingLightSelector(bool left, DateTime utcNow) =>
        ResolveCommandedSelectorState(
            _commandedLandingLightSelector,
            _commandedLandingLightUtc,
            left ? State!.LeftLandingLight : State!.RightLandingLight,
            utcNow);

    public void ClearCommandedState()
    {
        _commandedLeftIrsMode = null;
        _commandedRightIrsMode = null;
        _commandedLeftIrsModeUtc = null;
        _commandedRightIrsModeUtc = null;
        _commandedLogoLightOn = null;
        _commandedLogoLightUtc = null;
        _commandedPositionStrobeSelector = null;
        _commandedPositionStrobeUtc = null;
        _commandedLandingLightSelector = null;
        _commandedLandingLightUtc = null;
        _commandedEmergencyExitSelector = null;
        _commandedEmergencyExitUtc = null;
        ResetFireTestObservations();
    }

    public void ResetConnectionState()
    {
        State = null;
        IsReady = false;
        _apuGenOffBusSeen = false;
        _apuAvailableSinceUtc = null;
        _loggedBatterySelector = null;
        _loggedGroundPowerAvailable = null;
        _loggedGroundPowerOn = null;
        _loggedElectricalSignature = null;
        _loggedAirStartSignature = null;
        ClearCommandedState();
    }

    private void AddElectricalDiagnostics(PmdgNg3State state, ICollection<string> diagnostics)
    {
        if (!_loggedBatterySelector.HasValue || _loggedBatterySelector.Value != state.BatterySelector)
        {
            diagnostics.Add($"PMDG battery selector changed to {state.BatterySelector}.");
            _loggedBatterySelector = state.BatterySelector;
        }

        if (!_loggedGroundPowerAvailable.HasValue || _loggedGroundPowerAvailable.Value != state.GroundPowerAvailable)
        {
            diagnostics.Add($"PMDG ground power available changed to {(state.GroundPowerAvailable ? 1 : 0)}.");
            _loggedGroundPowerAvailable = state.GroundPowerAvailable;
        }

        if (!_loggedGroundPowerOn.HasValue || _loggedGroundPowerOn.Value != state.GroundPowerOn)
        {
            diagnostics.Add($"PMDG ground power switch changed to {(state.GroundPowerOn ? 1 : 0)}.");
            _loggedGroundPowerOn = state.GroundPowerOn;
        }

        var powerSignature =
            $"gndSw={(state.GroundPowerOn ? 1 : 0)} " +
            $"engGenSwL={(state.EngineGen1On ? 1 : 0)} engGenSwR={(state.EngineGen2On ? 1 : 0)} " +
            $"apuGenSwL={(state.ApuGen1On ? 1 : 0)} apuGenSwR={(state.ApuGen2On ? 1 : 0)} " +
            $"apuOffBus={(state.ApuGenOffBus ? 1 : 0)} " +
            $"xferOffL={(state.TransferBus1Off ? 1 : 0)} xferOffR={(state.TransferBus2Off ? 1 : 0)} " +
            $"sourceOffL={(state.Source1Off ? 1 : 0)} sourceOffR={(state.Source2Off ? 1 : 0)} " +
            $"genBusOffL={(state.GenBus1Off ? 1 : 0)} genBusOffR={(state.GenBus2Off ? 1 : 0)} " +
            $"acXferL={(state.AcTransferBus1Powered ? 1 : 0)} acXferR={(state.AcTransferBus2Powered ? 1 : 0)}";
        if (!string.Equals(_loggedElectricalSignature, powerSignature, StringComparison.Ordinal))
        {
            diagnostics.Add($"PMDG power source: {powerSignature} apuEgt={state.ApuEgtNeedle:F0}.");
            _loggedElectricalSignature = powerSignature;
        }
    }

    private void AddAirStartDiagnostic(PmdgNg3State state, ICollection<string> diagnostics)
    {
        var signature =
            $"packL={state.LeftPackSwitch} packR={state.RightPackSwitch} " +
            $"apuBleed={(state.ApuBleedOn ? 1 : 0)} iso={state.IsolationValveSwitch} " +
            $"ductL={Math.Round(state.LeftDuctPressurePsi / 5f) * 5f:F0} " +
            $"ductR={Math.Round(state.RightDuctPressurePsi / 5f) * 5f:F0} " +
            $"engStartL={state.Engine1StartSelector} engStartR={state.Engine2StartSelector} " +
            $"startValveL={(state.Engine1StartValveOpen ? 1 : 0)} startValveR={(state.Engine2StartValveOpen ? 1 : 0)}";
        if (string.Equals(_loggedAirStartSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        _loggedAirStartSignature = signature;
        diagnostics.Add($"PMDG air/start: {signature}.");
    }

    private void ObserveFireTests(PmdgNg3State state)
    {
        var fullOverheatFirePattern = state.FireHandle1Illuminated
            && state.FireHandleApuIlluminated
            && state.FireHandle2Illuminated
            && state.FireEngine1OverheatIlluminated
            && state.FireEngine2OverheatIlluminated
            && state.FireWheelWellIlluminated;
        var allExtinguisherLightsLit = state.FireExtinguisherTestLeft
            && state.FireExtinguisherTestRight
            && state.FireExtinguisherTestApu;

        if (state.FireDetectionTestSwitch == 0
            && state.FireFaultIlluminated
            && state.FireApuDetectorInoperativeIlluminated)
            _fireFaultInopActiveObserved = true;
        if (state.FireDetectionTestSwitch == 2
            && fullOverheatFirePattern
            && state.FireWarnLeftIlluminated
            && state.FireWarnRightIlluminated)
            _fireOverheatActiveObserved = true;
        if (state.FireDetectionTestSwitch == 2
            && _fireOverheatActiveObserved
            && !state.FireWarnLeftIlluminated
            && !state.FireWarnRightIlluminated
            && fullOverheatFirePattern)
            _fireWarnCancellationObserved = true;
        if (state.FireExtinguisherTestSwitch == 0 && allExtinguisherLightsLit)
            _extinguisherTest1ActiveObserved = true;
        if (state.FireExtinguisherTestSwitch == 2 && allExtinguisherLightsLit)
            _extinguisherTest2ActiveObserved = true;

        if (state.FireDetectionTestSwitch == 1 && _fireFaultInopActiveObserved)
            _fireFaultInopTestCompleted = true;
        if (state.FireDetectionTestSwitch == 1
            && _fireOverheatActiveObserved
            && _fireWarnCancellationObserved)
            _fireOverheatTestCompleted = true;
        if (state.FireExtinguisherTestSwitch == 1 && _extinguisherTest1ActiveObserved)
            _extinguisherTest1Completed = true;
        if (state.FireExtinguisherTestSwitch == 1 && _extinguisherTest2ActiveObserved)
            _extinguisherTest2Completed = true;
    }

    private void ResetFireTestObservations()
    {
        _fireFaultInopTestCompleted = false;
        _fireOverheatTestCompleted = false;
        _extinguisherTest1Completed = false;
        _extinguisherTest2Completed = false;
        _fireFaultInopActiveObserved = false;
        _fireOverheatActiveObserved = false;
        _fireWarnCancellationObserved = false;
        _extinguisherTest1ActiveObserved = false;
        _extinguisherTest2ActiveObserved = false;
    }

    private static bool IsCurrent(DateTime? commandedUtc, DateTime utcNow) =>
        commandedUtc.HasValue && utcNow - commandedUtc.Value < CommandedReadbackWindow;

    private static double ResolveCommandedSelectorState(
        float? commandedValue,
        DateTime? commandedUtc,
        byte sdkValue,
        DateTime utcNow) =>
        commandedValue.HasValue && IsCurrent(commandedUtc, utcNow)
            ? commandedValue.Value
            : sdkValue;

    private static bool ResolveCommandedBoolState(
        bool? commandedValue,
        DateTime? commandedUtc,
        bool sdkValue,
        DateTime utcNow) =>
        commandedValue.HasValue && IsCurrent(commandedUtc, utcNow)
            ? commandedValue.Value
            : sdkValue;

    private static bool ResolveCommandedPositionLightState(
        float? commandedValue,
        DateTime? commandedUtc,
        byte sdkValue,
        DateTime utcNow)
    {
        if (commandedValue.HasValue && IsCurrent(commandedUtc, utcNow))
        {
            // PMDG position/strobe selector: 0=steady, 1=off, 2=strobe & steady.
            return Math.Abs(commandedValue.Value - 1) >= 0.1f;
        }

        return sdkValue != 1;
    }

    private static PmdgNg3State ParseState(byte[] data)
    {
        byte ByteAt(int offset) => data.Length > offset ? data[offset] : (byte)0;
        bool BoolAt(int offset) => ByteAt(offset) != 0;
        float FloatAt(int offset) =>
            data.Length >= offset + sizeof(float) ? BitConverter.ToSingle(data, offset) : 0;

        return new PmdgNg3State
        {
            IrsLeftMode = ByteAt(11), IrsRightMode = ByteAt(12), IrsLeftAlignLight = BoolAt(3), IrsRightAlignLight = BoolAt(4),
            IrsLeftOnDcLight = BoolAt(5), IrsRightOnDcLight = BoolAt(6), IrsLeftFault = BoolAt(7), IrsRightFault = BoolAt(8),
            Engine1StartValveOpen = BoolAt(44), Engine2StartValveOpen = BoolAt(45), Engine1ReverserAnnunciated = BoolAt(38), Engine2ReverserAnnunciated = BoolAt(39),
            LeftForwardFuelPump = BoolAt(89), RightForwardFuelPump = BoolAt(90), LeftAftFuelPump = BoolAt(91), RightAftFuelPump = BoolAt(92),
            LeftCenterFuelPump = BoolAt(93), RightCenterFuelPump = BoolAt(94), CenterFuelQuantityPounds = FloatAt(116), BatterySelector = ByteAt(133),
            GroundPowerAvailable = BoolAt(142), GroundPowerOn = BoolAt(143), DcBus1Powered = BoolAt(186), DcBus2Powered = BoolAt(187),
            AcTransferBus1Powered = BoolAt(189), AcTransferBus2Powered = BoolAt(190), EngineGen1On = BoolAt(145), EngineGen2On = BoolAt(146),
            ApuGen1On = BoolAt(147), ApuGen2On = BoolAt(148), TransferBus1Off = BoolAt(149), TransferBus2Off = BoolAt(150),
            Source1Off = BoolAt(151), Source2Off = BoolAt(152), GenBus1Off = BoolAt(153), GenBus2Off = BoolAt(154), ApuGenOffBus = BoolAt(155),
            ApuEgtNeedle = FloatAt(200), ElectricHydraulicPump1LowPressure = BoolAt(262), ElectricHydraulicPump2LowPressure = BoolAt(263),
            ElectricHydraulicPump1On = BoolAt(268), ElectricHydraulicPump2On = BoolAt(269), EmergencyExitLights = ByteAt(217),
            NoSmokingSelector = ByteAt(218), FastenBeltsSelector = ByteAt(219), LeftPackSwitch = ByteAt(280), RightPackSwitch = ByteAt(281),
            ApuBleedOn = BoolAt(284), IsolationValveSwitch = ByteAt(285), LeftDuctPressurePsi = FloatAt(296), RightDuctPressurePsi = FloatAt(300),
            LeftLandingLight = ByteAt(372), RightLandingLight = ByteAt(373), LeftRunwayTurnoffLight = BoolAt(376), RightRunwayTurnoffLight = BoolAt(377),
            TaxiLightOn = BoolAt(378), ApuSelector = ByteAt(379), Engine1StartSelector = ByteAt(380), Engine2StartSelector = ByteAt(381),
            LogoLightOn = BoolAt(383), PositionStrobeSelector = ByteAt(384), AntiCollisionOn = BoolAt(385), SpeedbrakeArmed = BoolAt(477),
            SpeedbrakeExtended = BoolAt(479), AutobrakeSelector = ByteAt(487), AutobrakeDisarmed = BoolAt(489), BrakePressureNeedle = FloatAt(508),
            GearLever = ByteAt(506), ParkingBrakeAnnunciated = BoolAt(574), FireWarnLeftIlluminated = BoolAt(388), FireWarnRightIlluminated = BoolAt(389),
            FireDetectionTestSwitch = ByteAt(579), FireEngine1OverheatIlluminated = BoolAt(577), FireEngine2OverheatIlluminated = BoolAt(578),
            FireHandle1Illuminated = BoolAt(583), FireHandleApuIlluminated = BoolAt(584), FireHandle2Illuminated = BoolAt(585),
            FireWheelWellIlluminated = BoolAt(586), FireFaultIlluminated = BoolAt(587), FireApuDetectorInoperativeIlluminated = BoolAt(588),
            FireExtinguisherTestSwitch = ByteAt(592), FireExtinguisherTestLeft = BoolAt(593), FireExtinguisherTestRight = BoolAt(594),
            FireExtinguisherTestApu = BoolAt(595), TransponderMode = ByteAt(612), TakeoffFlaps = ByteAt(620), V1 = ByteAt(621),
            Vr = ByteAt(622), LandingFlaps = ByteAt(624), LandingVref = ByteAt(625), FmcPerfInputComplete = BoolAt(634),
            IrsAligned = BoolAt(654), GroundConnectionAvailable = BoolAt(658)
        };
    }
}

internal sealed class PmdgNg3State
{
    public byte IrsLeftMode { get; internal set; } public byte IrsRightMode { get; internal set; }
    public bool IrsLeftAlignLight { get; internal set; } public bool IrsRightAlignLight { get; internal set; }
    public bool IrsLeftOnDcLight { get; internal set; } public bool IrsRightOnDcLight { get; internal set; }
    public bool IrsLeftFault { get; internal set; } public bool IrsRightFault { get; internal set; }
    public bool IrsAligned { get; internal set; } public bool Engine1StartValveOpen { get; internal set; } public bool Engine2StartValveOpen { get; internal set; }
    public bool Engine1ReverserAnnunciated { get; internal set; } public bool Engine2ReverserAnnunciated { get; internal set; }
    public bool LeftForwardFuelPump { get; internal set; } public bool RightForwardFuelPump { get; internal set; } public bool LeftAftFuelPump { get; internal set; } public bool RightAftFuelPump { get; internal set; }
    public bool LeftCenterFuelPump { get; internal set; } public bool RightCenterFuelPump { get; internal set; } public float CenterFuelQuantityPounds { get; internal set; }
    public byte BatterySelector { get; internal set; } public bool GroundPowerAvailable { get; internal set; } public bool GroundPowerOn { get; internal set; }
    public bool DcBus1Powered { get; internal set; } public bool DcBus2Powered { get; internal set; } public bool AcTransferBus1Powered { get; internal set; } public bool AcTransferBus2Powered { get; internal set; }
    public bool EngineGen1On { get; internal set; } public bool EngineGen2On { get; internal set; } public bool ApuGen1On { get; internal set; } public bool ApuGen2On { get; internal set; }
    public bool TransferBus1Off { get; internal set; } public bool TransferBus2Off { get; internal set; } public bool Source1Off { get; internal set; } public bool Source2Off { get; internal set; }
    public bool GenBus1Off { get; internal set; } public bool GenBus2Off { get; internal set; } public bool ApuGenOffBus { get; internal set; } public float ApuEgtNeedle { get; internal set; }
    public bool ApuAvailableForTransfer => ApuGenOffBus;
    public bool ElectricHydraulicPump1On { get; internal set; } public bool ElectricHydraulicPump2On { get; internal set; }
    public bool ElectricHydraulicPump1LowPressure { get; internal set; } public bool ElectricHydraulicPump2LowPressure { get; internal set; }
    public byte EmergencyExitLights { get; internal set; } public byte NoSmokingSelector { get; internal set; } public byte FastenBeltsSelector { get; internal set; }
    public byte LeftPackSwitch { get; internal set; } public byte RightPackSwitch { get; internal set; } public bool ApuBleedOn { get; internal set; } public byte IsolationValveSwitch { get; internal set; }
    public float LeftDuctPressurePsi { get; internal set; } public float RightDuctPressurePsi { get; internal set; }
    public byte LeftLandingLight { get; internal set; } public byte RightLandingLight { get; internal set; }
    public bool LeftRunwayTurnoffLight { get; internal set; } public bool RightRunwayTurnoffLight { get; internal set; } public bool TaxiLightOn { get; internal set; }
    public byte ApuSelector { get; internal set; } public byte Engine1StartSelector { get; internal set; } public byte Engine2StartSelector { get; internal set; }
    public bool LogoLightOn { get; internal set; } public byte PositionStrobeSelector { get; internal set; } public bool AntiCollisionOn { get; internal set; }
    public bool SpeedbrakeArmed { get; internal set; } public bool SpeedbrakeExtended { get; internal set; } public byte AutobrakeSelector { get; internal set; }
    public bool AutobrakeDisarmed { get; internal set; } public float BrakePressureNeedle { get; internal set; } public byte GearLever { get; internal set; }
    public bool ParkingBrakeAnnunciated { get; internal set; } public bool FireWarnLeftIlluminated { get; internal set; } public bool FireWarnRightIlluminated { get; internal set; }
    public byte FireDetectionTestSwitch { get; internal set; } public bool FireEngine1OverheatIlluminated { get; internal set; } public bool FireEngine2OverheatIlluminated { get; internal set; }
    public bool FireHandle1Illuminated { get; internal set; } public bool FireHandleApuIlluminated { get; internal set; } public bool FireHandle2Illuminated { get; internal set; }
    public bool FireWheelWellIlluminated { get; internal set; } public bool FireFaultIlluminated { get; internal set; } public bool FireApuDetectorInoperativeIlluminated { get; internal set; }
    public byte FireExtinguisherTestSwitch { get; internal set; } public bool FireExtinguisherTestLeft { get; internal set; } public bool FireExtinguisherTestRight { get; internal set; }
    public bool FireExtinguisherTestApu { get; internal set; } public byte TransponderMode { get; internal set; } public byte TakeoffFlaps { get; internal set; }
    public byte V1 { get; internal set; } public byte Vr { get; internal set; } public byte LandingFlaps { get; internal set; } public byte LandingVref { get; internal set; }
    public bool FmcPerfInputComplete { get; internal set; } public bool GroundConnectionAvailable { get; internal set; }
}
