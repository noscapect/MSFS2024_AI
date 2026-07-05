using Microsoft.FlightSimulator.SimConnect;
using Msfs2024Ai.Copilot.Checklists;
using Msfs2024Ai.Copilot.Controls;
using Msfs2024Ai.Copilot.Diagnostics;
using Msfs2024Ai.Copilot.Domain;
using Msfs2024Ai.Copilot.Procedures;
using Msfs2024Ai.Copilot.Settings;
using Msfs2024Ai.Copilot.Telemetry;
using Msfs2024Ai.Copilot.Voice;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Msfs2024Ai.Copilot;

internal sealed class CopilotService : Form
{
    private const int WmUserSimConnect = 0x0402;
    private const double MetersPerNauticalMile = 1852.0;
    private const uint PmdgNg3DataId = 0x4E473331;
    private const uint PmdgNg3DataDefinition = 0x4E473332;
    private const uint PmdgNg3ControlId = 0x4E473333;
    private const uint PmdgNg3ControlDefinition = 0x4E473334;
    private const int PmdgNg3DataSize = 916;
    private const uint ThirdPartyEventIdMin = 0x00011000;
    // Change the schema suffix whenever the ordered runtime LVar list changes.
    // MobiFlight client-data layouts persist for the simulator session.
    private const string MobiFlightRuntimeClientName = "MSFS2024_AI_Copilot_v27";
    private readonly string? _oneShotCommand;
    private readonly bool _showUi;
    private readonly CopilotSettings _settings;
    private readonly ProcedureSession _procedureSession;
    private readonly ConcurrentQueue<string> _commands = new();
    private readonly ProcedureRunner _procedureRunner;
    private VoiceCalloutQueue? _voiceCalloutQueue;
    private readonly FlightTelemetryStore _flightTelemetryStore;
    private System.Windows.Forms.Timer? _replayTimer;
    private IReadOnlyList<AircraftState> _replayStates = Array.Empty<AircraftState>();
    private int _replayIndex;
    private bool _replayActive;
    private readonly HashSet<string> _completedProcedureIds =
        new(StringComparer.OrdinalIgnoreCase);
    private SimConnect? _simConnect;
    private AircraftState? _state;
    private PendingExternalPowerProcedure? _pendingProcedure;
    private PendingBeaconProcedure? _pendingBeaconProcedure;
    private PendingNavLogoSelectorProcedure? _pendingNavLogoSelectorProcedure;
    private PendingBatteryProcedure? _pendingBatteryProcedure;
    private PendingNativeAction? _pendingNativeAction;
    private PendingFireTest? _pendingFireTest;
    private FireTestSystem? _pendingFlyByWireFireTest;
    private PendingFuelPumpSequence? _pendingFuelPumpSequence;
    private System.Windows.Forms.Timer? _fuelPumpSequenceTimer;
    private readonly List<System.Windows.Forms.Timer> _nativePulseTimers = new();
    private bool _mobiFlightReady;
    private bool _mobiFlightRuntimeReady;
    private DateTime? _mobiFlightRuntimeInitializedUtc;
    private bool _pmdgNg3DataReady;
    private PmdgNg3State? _pmdgNg3State;
    private bool NativeStateReady =>
        _nativeBattery1On.HasValue
        && _nativeBattery2On.HasValue
        && _nativeFuelPump1.HasValue
        && _nativeFuelPump2.HasValue
        && _nativeFuelPump3.HasValue
        && _nativeFuelPump4.HasValue
        && _nativeFuelPump5.HasValue
        && _nativeFuelPump6.HasValue
        && _nativeNavLogoSelectorPosition.HasValue
        && _nativeApuAvailable.HasValue
        && _nativeApuMasterSwitch.HasValue
        && _nativeApuStartButton.HasValue
        && _nativeApuBleedButton.HasValue
        && _nativeApuGeneratorOn.HasValue
        && _nativeApuFlapPercent.HasValue
        && _nativeAdirs1State.HasValue
        && _nativeAdirs2State.HasValue
        && _nativeAdirs3State.HasValue
        && _nativeAdirsOnBattery.HasValue
        && _nativeCrewOxygen.HasValue
        && _nativeStrobeSelector.HasValue
        && _nativeApuFireTest.HasValue
        && _nativeEngine1FireTest.HasValue
        && _nativeEngine2FireTest.HasValue
        && _nativeApuFireWarningLit.HasValue
        && _nativeApuFireSound.HasValue
        && _nativeEngine1FireWarningLit.HasValue
        && _nativeEngine1FireSound.HasValue
        && _nativeEngine2FireWarningLit.HasValue
        && _nativeEngine2FireSound.HasValue
        && _nativeSeatbeltSelector.HasValue
        && _nativeSeatbeltSignsOn.HasValue
        && _nativeNoSmokingSelector.HasValue
        && _nativeNoSmokingSignsOn.HasValue
        && _nativeEmergencyExitSelector.HasValue
        && _nativeSpoilersArmed.HasValue
        && _nativeAutobrakeLevel.HasValue
        && _nativeTcasAltitudeReporting.HasValue
        && _nativeGearHandlePosition.HasValue
        && _nativeWeatherRadarPwsSelector.HasValue
        && _nativeNoseLightSelector.HasValue
        && _nativeLeftLandingLightSelector.HasValue
        && _nativeRightLandingLightSelector.HasValue
        && _nativeTransponderAtcState.HasValue
        && _nativeTcasMode.HasValue
        && _nativeTransponderStandby.HasValue;
    private bool? _nativeBattery1On;
    private bool? _nativeBattery2On;
    private bool? _fbwBattery1Auto;
    private bool? _fbwBattery2Auto;
    private bool? _fbwBattery1AutoTyped;
    private bool? _fbwBattery2AutoTyped;
    private bool? _fbwCommandedBattery1Auto;
    private bool? _fbwCommandedBattery2Auto;
    private bool? _fbwExternalPowerAvailable;
    private bool? _fbwExternalPowerOn;
    private bool? _fbwExternalPowerAvailableTyped;
    private bool? _fbwExternalPowerOnTyped;
    private bool? _fbwApuMasterSwitch;
    private bool? _fbwApuStartButton;
    private bool? _fbwApuStartAvailable;
    private bool? _fbwApuBleedButton;
    private float? _fbwTransponderMode;
    private bool? _fbwParkingBrake;
    private float? _fbwEngine1State;
    private float? _fbwEngine2State;
    private float? _fbwEngine1N1;
    private float? _fbwEngine2N1;
    private bool? _fbwEngine1StarterValveOpen;
    private bool? _fbwEngine2StarterValveOpen;
    private bool? _fbwSpoilersArmed;
    private bool? _fbwCommandedSpoilersArmed;
    private DateTime? _fbwCommandedSpoilersArmedUtc;
    private float? _fbwFlapsHandleIndex;
    private float? _fbwAutobrakeLevel;
    private float? _fbwCommandedAutobrakeLevel;
    private DateTime? _fbwCommandedAutobrakeLevelUtc;
    private float? _fbwWeatherRadarPwsSelector;
    private float? _fbwCommandedWeatherRadarPwsSelector;
    private DateTime? _fbwCommandedWeatherRadarPwsSelectorUtc;
    private float? _fbwCommandedNoseLightSelector;
    private DateTime? _fbwCommandedNoseLightSelectorUtc;
    private bool? _fbwTcasAltitudeReporting;
    private float? _fbwTcasMode;
    private bool? _fbwCommandedTcasAltitudeReporting;
    private DateTime? _fbwCommandedTcasAltitudeReportingUtc;
    private float? _fbwCommandedTcasMode;
    private DateTime? _fbwCommandedTcasModeUtc;
    private float? _fbwCommandedLandingLightSelector;
    private DateTime? _fbwCommandedLandingLightSelectorUtc;
    private float? _fbwAdirs1Selector;
    private float? _fbwAdirs2Selector;
    private float? _fbwAdirs3Selector;
    private float? _fbwAdirs1SelectorTyped;
    private float? _fbwAdirs2SelectorTyped;
    private float? _fbwAdirs3SelectorTyped;
    private bool? _fbwAdirsOnBattery;
    private float? _fbwCommandedAdirs1Selector;
    private float? _fbwCommandedAdirs2Selector;
    private float? _fbwCommandedAdirs3Selector;
    private DateTime? _fbwCommandedAdirs1SelectorUtc;
    private DateTime? _fbwCommandedAdirs2SelectorUtc;
    private DateTime? _fbwCommandedAdirs3SelectorUtc;
    private bool? _fbwCrewOxygen;
    private bool? _fbwCrewOxygenTyped;
    private bool? _fbwCommandedCrewOxygen;
    private float? _fbwNavLogoSelector;
    private float? _fbwNavLogoSelectorTyped;
    private bool? _fbwStrobeAuto;
    private float? _fbwStrobeLightState;
    private float? _fbwSeatbeltSelector;
    private float? _fbwNoSmokingSelector;
    private float? _fbwEmergencyExitSelector;
    private float? _fbwBattery1Potential;
    private float? _fbwBattery2Potential;
    private double? _lastLoggedBattery1Voltage;
    private double? _lastLoggedBattery2Voltage;
    private float? _nativeFuelPump1;
    private float? _nativeFuelPump2;
    private float? _nativeFuelPump3;
    private float? _nativeFuelPump4;
    private float? _nativeFuelPump5;
    private float? _nativeFuelPump6;
    private float? _nativeNavLogoSelectorPosition;
    private float? _nativeApuAvailable;
    private float? _nativeApuMasterSwitch;
    private float? _nativeApuStartButton;
    private float? _nativeApuBleedButton;
    private float? _nativeApuGeneratorOn;
    private float? _nativeApuFlapPercent;
    private float? _nativeAdirs1State;
    private float? _nativeAdirs2State;
    private float? _nativeAdirs3State;
    private float? _nativeAdirsOnBattery;
    private float? _nativeCrewOxygen;
    private float? _nativeStrobeSelector;
    private float? _nativeApuFireTest;
    private float? _nativeEngine1FireTest;
    private float? _nativeEngine2FireTest;
    private float? _nativeApuFireWarningLit;
    private float? _nativeApuFireSound;
    private float? _nativeEngine1FireWarningLit;
    private float? _nativeEngine1FireSound;
    private float? _nativeEngine2FireWarningLit;
    private float? _nativeEngine2FireSound;
    private float? _nativeSeatbeltSelector;
    private float? _nativeSeatbeltSignsOn;
    private float? _nativeNoSmokingSelector;
    private float? _nativeNoSmokingSignsOn;
    private float? _nativeEmergencyExitSelector;
    private float? _nativeSpoilersArmed;
    private float? _nativeAutobrakeLevel;
    private float? _nativeTcasAltitudeReporting;
    private float? _nativeGearHandlePosition;
    private float? _nativeWeatherRadarPwsSelector;
    private float? _nativeNoseLightSelector;
    private float? _nativeLeftLandingLightSelector;
    private float? _nativeRightLandingLightSelector;
    private float? _nativeTransponderAtcState;
    private float? _nativeTcasMode;
    private float? _nativeTransponderStandby;
    private bool _apuFireTestCompleted;
    private bool _engine1FireTestCompleted;
    private bool _engine2FireTestCompleted;
    private System.Windows.Forms.Timer? _reconnectTimer;
    private bool _initialStateReceived;
    private bool _oneShotCommandExecuted;
    private bool _procedureSessionRestoreAttempted;
    private DateTime? _electricalPowerStableSinceUtc;
    private bool _cruiseSeatbeltMonitoring;
    private DateTime? _smoothCruiseSinceUtc;
    private DateTime _nextCruiseSeatbeltCommandUtc;
    private Label? _connectionLabel;
    private Label? _aircraftLabel;
    private Label? _phaseLabel;
    private Label? _electricalLabel;
    private Label? _recommendationLabel;
    private Label? _telemetryLabel;
    private Label? _versionLabel;
    private Label? _adapterLabel;
    private Label? _simBadgeLabel;
    private Label? _aircraftBadgeLabel;
    private Label? _adapterBadgeLabel;
    private Label? _flowBadgeLabel;
    private Label? _versionBadgeLabel;
    private Label? _procedureLabel;
    private Label? _stepLabel;
    private Label? _statusBadgeLabel;
    private Label? _waitingForLabel;
    private ProgressBar? _procedureProgress;
    private ComboBox? _automationPolicyBox;
    private NumericUpDown? _transitionAltitudeBox;
    private NumericUpDown? _takeoffV1Box;
    private NumericUpDown? _takeoffRotateBox;
    private CheckBox? _voiceCalloutsBox;
    private ComboBox? _replayFlightBox;
    private ListBox? _eventLog;
    private ListBox? _flowList;

    private enum Definition
    {
        AircraftState
    }

    private enum Request
    {
        AircraftState,
        MobiFlightResponse,
        MobiFlightRuntimeResponse = 110,
        NativeBattery1 = 111,
        NativeBattery2 = 112,
        NativeFuelPump1 = 113,
        NativeFuelPump2 = 114,
        NativeFuelPump3 = 115,
        NativeFuelPump4 = 116,
        NativeFuelPump5 = 117,
        NativeFuelPump6 = 118,
        NativeNavLogoSelector = 119,
        NativeApuAvailable = 120,
        NativeApuMasterSwitch = 121,
        NativeApuStartButton = 122,
        NativeApuBleedButton = 123,
        NativeApuGeneratorOn = 124,
        NativeApuFlapPercent = 125,
        NativeAdirs1State = 126,
        NativeAdirs2State = 127,
        NativeAdirs3State = 128,
        NativeAdirsOnBattery = 129,
        NativeCrewOxygen = 130,
        NativeStrobeSelector = 131,
        NativeApuFireTest = 132,
        NativeEngine1FireTest = 133,
        NativeEngine2FireTest = 134,
        NativeApuFireWarningLit = 135,
        NativeApuFireSound = 136,
        NativeEngine1FireWarningLit = 137,
        NativeEngine1FireSound = 138,
        NativeEngine2FireWarningLit = 139,
        NativeEngine2FireSound = 140,
        NativeSeatbeltSelector = 141,
        NativeSeatbeltSignsOn = 142,
        NativeNoSmokingSelector = 143,
        NativeNoSmokingSignsOn = 144,
        NativeEmergencyExitSelector = 145,
        NativeTransponderAtcState = 146,
        NativeTcasMode = 147,
        NativeTransponderStandby = 148,
        NativeSpoilersArmed = 149,
        NativeAutobrakeLevel = 150,
        NativeTcasAltitudeReporting = 151,
        NativeGearHandlePosition = 152,
        NativeWeatherRadarPwsSelector = 153,
        NativeNoseLightSelector = 154,
        NativeLeftLandingLightSelector = 155,
        NativeRightLandingLightSelector = 156,
        FbwBattery1Auto = 157,
        FbwBattery2Auto = 158,
        FbwBattery1Potential = 159,
        FbwBattery2Potential = 160,
        FbwBattery1AutoTyped = 161,
        FbwBattery2AutoTyped = 162,
        FbwExternalPowerAvailable = 163,
        FbwExternalPowerOn = 164,
        FbwExternalPowerAvailableTyped = 165,
        FbwExternalPowerOnTyped = 166,
        FbwAdirs1Selector = 167,
        FbwAdirs2Selector = 168,
        FbwAdirs3Selector = 169,
        FbwAdirs1SelectorTyped = 170,
        FbwAdirs2SelectorTyped = 171,
        FbwAdirs3SelectorTyped = 172,
        FbwAdirsOnBattery = 173,
        FbwCrewOxygen = 174,
        FbwCrewOxygenTyped = 175,
        FbwNavLogoSelector = 176,
        FbwNavLogoSelectorTyped = 177,
        FbwStrobeAuto = 178,
        FbwStrobeLightState = 179,
        FbwSeatbeltSelector = 180,
        FbwNoSmokingSelector = 181,
        FbwEmergencyExitSelector = 182,
        FbwApuMasterSwitch = 183,
        FbwApuStartButton = 184,
        FbwApuStartAvailable = 185,
        FbwApuBleedButton = 186,
        FbwTransponderMode = 187,
        FbwParkingBrake = 188,
        FbwEngine1State = 189,
        FbwEngine2State = 190,
        FbwEngine1N1 = 191,
        FbwEngine2N1 = 192,
        FbwEngine1StarterValveOpen = 193,
        FbwEngine2StarterValveOpen = 194,
        FbwSpoilersArmed = 195,
        FbwFlapsHandleIndex = 196,
        FbwAutobrakeLevel = 197,
        FbwWeatherRadarPwsSelector = 198,
        FbwTcasAltitudeReporting = 199,
        FbwTcasMode = 200,
        PmdgNg3Data = 300,
        PmdgNg3Control = 301
    }

    private enum ClientDataArea
    {
        MobiFlightCommand = 100,
        MobiFlightResponse = 101,
        MobiFlightRuntimeLVars = 110,
        MobiFlightRuntimeCommand = 111,
        MobiFlightRuntimeResponse = 112,
        PmdgNg3Data = unchecked((int)PmdgNg3DataId),
        PmdgNg3Control = unchecked((int)PmdgNg3ControlId)
    }

    private enum ClientDataDefinition
    {
        MobiFlightMessage = 100,
        MobiFlightRuntimeMessage = 110,
        NativeBattery1 = 111,
        NativeBattery2 = 112,
        NativeFuelPump1 = 113,
        NativeFuelPump2 = 114,
        NativeFuelPump3 = 115,
        NativeFuelPump4 = 116,
        NativeFuelPump5 = 117,
        NativeFuelPump6 = 118,
        NativeNavLogoSelector = 119,
        NativeApuAvailable = 120,
        NativeApuMasterSwitch = 121,
        NativeApuStartButton = 122,
        NativeApuBleedButton = 123,
        NativeApuGeneratorOn = 124,
        NativeApuFlapPercent = 125,
        NativeAdirs1State = 126,
        NativeAdirs2State = 127,
        NativeAdirs3State = 128,
        NativeAdirsOnBattery = 129,
        NativeCrewOxygen = 130,
        NativeStrobeSelector = 131,
        NativeApuFireTest = 132,
        NativeEngine1FireTest = 133,
        NativeEngine2FireTest = 134,
        NativeApuFireWarningLit = 135,
        NativeApuFireSound = 136,
        NativeEngine1FireWarningLit = 137,
        NativeEngine1FireSound = 138,
        NativeEngine2FireWarningLit = 139,
        NativeEngine2FireSound = 140,
        NativeSeatbeltSelector = 141,
        NativeSeatbeltSignsOn = 142,
        NativeNoSmokingSelector = 143,
        NativeNoSmokingSignsOn = 144,
        NativeEmergencyExitSelector = 145,
        NativeTransponderAtcState = 146,
        NativeTcasMode = 147,
        NativeTransponderStandby = 148,
        NativeSpoilersArmed = 149,
        NativeAutobrakeLevel = 150,
        NativeTcasAltitudeReporting = 151,
        NativeGearHandlePosition = 152,
        NativeWeatherRadarPwsSelector = 153,
        NativeNoseLightSelector = 154,
        NativeLeftLandingLightSelector = 155,
        NativeRightLandingLightSelector = 156,
        FbwBattery1Auto = 157,
        FbwBattery2Auto = 158,
        FbwBattery1Potential = 159,
        FbwBattery2Potential = 160,
        FbwBattery1AutoTyped = 161,
        FbwBattery2AutoTyped = 162,
        FbwExternalPowerAvailable = 163,
        FbwExternalPowerOn = 164,
        FbwExternalPowerAvailableTyped = 165,
        FbwExternalPowerOnTyped = 166,
        FbwAdirs1Selector = 167,
        FbwAdirs2Selector = 168,
        FbwAdirs3Selector = 169,
        FbwAdirs1SelectorTyped = 170,
        FbwAdirs2SelectorTyped = 171,
        FbwAdirs3SelectorTyped = 172,
        FbwAdirsOnBattery = 173,
        FbwCrewOxygen = 174,
        FbwCrewOxygenTyped = 175,
        FbwNavLogoSelector = 176,
        FbwNavLogoSelectorTyped = 177,
        FbwStrobeAuto = 178,
        FbwStrobeLightState = 179,
        FbwSeatbeltSelector = 180,
        FbwNoSmokingSelector = 181,
        FbwEmergencyExitSelector = 182,
        FbwApuMasterSwitch = 183,
        FbwApuStartButton = 184,
        FbwApuStartAvailable = 185,
        FbwApuBleedButton = 186,
        FbwTransponderMode = 187,
        FbwParkingBrake = 188,
        FbwEngine1State = 189,
        FbwEngine2State = 190,
        FbwEngine1N1 = 191,
        FbwEngine2N1 = 192,
        FbwEngine1StarterValveOpen = 193,
        FbwEngine2StarterValveOpen = 194,
        FbwSpoilersArmed = 195,
        FbwFlapsHandleIndex = 196,
        FbwAutobrakeLevel = 197,
        FbwWeatherRadarPwsSelector = 198,
        FbwTcasAltitudeReporting = 199,
        FbwTcasMode = 200,
        PmdgNg3Data = unchecked((int)PmdgNg3DataDefinition),
        PmdgNg3Control = unchecked((int)PmdgNg3ControlDefinition)
    }

    private enum CopilotEvent
    {
        SetExternalPower,
        SetBeacon,
        StartApu,
        SetApuBleed,
        SetApuGenerator,
        SetFuelPump,
        FuelSystemPumpOn,
        FuelSystemPumpOff,
        FuelSystemValveOpen,
        FuelSystemValveClose,
        CabinSeatbeltsToggle
    }

    private enum Priority
    {
        Highest = 1
    }

    private enum FireTestSystem
    {
        Apu,
        Engine1,
        Engine2
    }

    private enum SignSelector
    {
        Seatbelts,
        NoSmoking,
        EmergencyExit
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    private struct AircraftData
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Title;

        public double OnGround;
        public double GroundSpeed;
        public double Engine1Combustion;
        public double Engine2Combustion;
        public double Engine1Starter;
        public double Engine2Starter;
        public double Engine1N1;
        public double Engine2N1;
        public double Engine1Egt;
        public double Engine2Egt;
        public double Engine1FuelFlow;
        public double Engine2FuelFlow;
        public double Engine1IgnitionSwitch;
        public double Engine2IgnitionSwitch;
        public double Battery1;
        public double Battery2;
        public double Battery1Voltage;
        public double Battery2Voltage;
        public double ExternalPowerAvailable;
        public double ExternalPowerOn;
        public double ExternalPowerAvailableUnindexed;
        public double ExternalPowerOnUnindexed;
        public double ParkingBrake;
        public double Beacon;
        public double NavigationLights;
        public double LogoLights;
        public double TaxiLight;
        public double FbwNoseTakeoffLightCircuit;
        public double FbwLeftLandingLightCircuit;
        public double FbwRightLandingLightCircuit;
        public double FbwNoseTaxiLightCircuit;
        public double ApuRpm;
        public double ApuStarter;
        public double ApuMasterSwitch;
        public double ApuGeneratorActive;
        public double ApuGeneratorSwitch;
        public double ApuVolts;
        public double FuelPump1;
        public double FuelPump2;
        public double FuelPump3;
        public double FuelPump4;
        public double FbwFuelPump5;
        public double FbwFuelPump6;
        public double FbwFuelValve9;
        public double FbwFuelValve10;
        public double CabinSeatbeltsAlert;
        public double AltitudeAboveGround;
        public double IndicatedAltitude;
        public double IndicatedAirspeed;
        public double VerticalSpeed;
        public double GForce;
        public double RadioHeight;
        public double DecisionHeight;
        public double Engine1Reverse;
        public double Engine2Reverse;
        public double AutobrakesActive;
        public double LeftSpoilerPosition;
        public double RightSpoilerPosition;
        public double FlapsHandleIndex;
        public double GearHandle;
        public double PitchDegrees;
        public double AutopilotMaster;
        public double Exit1Open;
        public double Exit1Type;
        public double Exit1PosX;
        public double Exit1PosY;
        public double Exit1PosZ;
        public double Exit2Open;
        public double Exit2Type;
        public double Exit2PosX;
        public double Exit2PosY;
        public double Exit2PosZ;
        public double Exit3Open;
        public double Exit3Type;
        public double Exit3PosX;
        public double Exit3PosY;
        public double Exit3PosZ;
        public double Exit4Open;
        public double Exit4Type;
        public double Exit4PosX;
        public double Exit4PosY;
        public double Exit4PosZ;
        public double Exit5Open;
        public double Exit5Type;
        public double Exit5PosX;
        public double Exit5PosY;
        public double Exit5PosZ;
        public double Exit6Open;
        public double Exit6Type;
        public double Exit6PosX;
        public double Exit6PosY;
        public double Exit6PosZ;
        public double Exit7Open;
        public double Exit7Type;
        public double Exit7PosX;
        public double Exit7PosY;
        public double Exit7PosZ;
        public double Exit8Open;
        public double Exit8Type;
        public double Exit8PosX;
        public double Exit8PosY;
        public double Exit8PosZ;
        public double AtcClearedIfr;
        public double SpoilersArmed;
        public double CaptainBaroStandard;
        public double FirstOfficerBaroStandard;
        public double LeftFlapPosition;
        public double RightFlapPosition;
        public double AtcRunwaySelected;
        public double AtcRunwayStartDistanceMeters;
        public double GpsTargetDistanceMeters;
        public double GpsWaypointDistanceMeters;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct MobiFlightMessage
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
        public byte[] Data;

        public MobiFlightMessage(string value)
        {
            Data = new byte[1024];
            var bytes = System.Text.Encoding.ASCII.GetBytes(value);
            Array.Copy(bytes, Data, Math.Min(bytes.Length, Data.Length - 1));
        }

        public override string ToString()
        {
            var end = Array.IndexOf(Data, (byte)0);
            if (end < 0)
            {
                end = Data.Length;
            }
            return System.Text.Encoding.ASCII.GetString(Data, 0, end);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct MobiFlightFloat
    {
        public float Value;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct PmdgNg3RawData
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = PmdgNg3DataSize)]
        public byte[] Data;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct PmdgNg3Control
    {
        public uint Event;
        public uint Parameter;
    }

    private sealed class PmdgNg3State
    {
        public byte IrsLeftMode { get; set; }
        public byte IrsRightMode { get; set; }
        public bool Engine1StartValveOpen { get; set; }
        public bool Engine2StartValveOpen { get; set; }
        public bool LeftForwardFuelPump { get; set; }
        public bool RightForwardFuelPump { get; set; }
        public bool LeftAftFuelPump { get; set; }
        public bool RightAftFuelPump { get; set; }
        public bool LeftCenterFuelPump { get; set; }
        public bool RightCenterFuelPump { get; set; }
        public byte BatterySelector { get; set; }
        public bool GroundPowerAvailable { get; set; }
        public bool GroundPowerOn { get; set; }
        public bool ApuGen1On { get; set; }
        public bool ApuGen2On { get; set; }
        public byte EmergencyExitLights { get; set; }
        public byte NoSmokingSelector { get; set; }
        public byte FastenBeltsSelector { get; set; }
        public bool ApuBleedOn { get; set; }
        public byte LeftLandingLight { get; set; }
        public byte RightLandingLight { get; set; }
        public bool TaxiLightOn { get; set; }
        public byte ApuSelector { get; set; }
        public bool LogoLightOn { get; set; }
        public byte PositionStrobeSelector { get; set; }
        public bool AntiCollisionOn { get; set; }
        public bool SpeedbrakeArmed { get; set; }
        public byte AutobrakeSelector { get; set; }
        public byte GearLever { get; set; }
        public bool ParkingBrakeAnnunciated { get; set; }
        public byte FireDetectionTestSwitch { get; set; }
        public bool FireExtinguisherTestLeft { get; set; }
        public bool FireExtinguisherTestRight { get; set; }
        public bool FireExtinguisherTestApu { get; set; }
        public byte TransponderMode { get; set; }
        public byte TakeoffFlaps { get; set; }
        public byte V1 { get; set; }
        public byte Vr { get; set; }
        public byte LandingFlaps { get; set; }
        public bool FmcPerfInputComplete { get; set; }
        public bool GroundConnectionAvailable { get; set; }
    }

    public CopilotService(string? oneShotCommand, bool showUi)
    {
        _oneShotCommand = oneShotCommand;
        _showUi = showUi;
        _settings = SettingsStore.Load();
        _flightTelemetryStore = new FlightTelemetryStore();
        _procedureSession = ProcedureSessionStore.Load();
        foreach (var procedureId in _procedureSession.CompletedProcedureIds)
        {
            _completedProcedureIds.Add(procedureId);
        }
        _procedureRunner = new ProcedureRunner(
            command =>
            {
                if (_replayActive)
                {
                    AppendDashboardLog($"Replay action: {command}");
                    return;
                }
                _commands.Enqueue(command);
            },
            () => _settings.AutomationPolicy);
        _procedureRunner.Changed += OnProcedureChanged;
        _procedureRunner.StepCompleted += SpeakProcedureCallout;
        try
        {
            _voiceCalloutQueue = new VoiceCalloutQueue();
        }
        catch (Exception ex)
        {
            AppLog.Write($"Voice callouts unavailable: {ex.Message}");
        }
        Text = "MSFS 2024 AI Copilot";
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                   Application.ExecutablePath)
               ?? Icon;
        ShowIcon = true;
        ShowInTaskbar = showUi;
        WindowState = showUi ? FormWindowState.Normal : FormWindowState.Minimized;
        Opacity = showUi ? 1 : 0;
        if (showUi)
        {
            BuildDashboard();
            Shown += async (_, _) => await CheckForUpdatesAsync();
        }
    }

    public void Connect()
    {
        if (_simConnect != null)
        {
            return;
        }

        try
        {
            _ = Handle;
            _simConnect = new SimConnect(
                "MSFS 2024 AI Copilot",
                Handle,
                WmUserSimConnect,
                null,
                0);

            _simConnect.OnRecvOpen += OnOpen;
            _simConnect.OnRecvQuit += OnQuit;
            _simConnect.OnRecvException += OnException;
            _simConnect.OnRecvSimobjectData += OnAircraftData;
            _simConnect.OnRecvClientData += OnClientData;
        }
        catch (COMException exception)
        {
            Console.Error.WriteLine($"Could not connect to SimConnect: {exception.Message}");
            AppLog.Write($"SimConnect connection failed: {exception}");
            if (_connectionLabel != null)
            {
                _connectionLabel.Text = "Waiting for MSFS SimConnect...";
                _connectionLabel.ForeColor = System.Drawing.Color.DarkRed;
            }
            AppendDashboardLog("SimConnect unavailable; retrying in 5 seconds.");
            ScheduleReconnect();
        }
    }

    private void ScheduleReconnect()
    {
        if (_reconnectTimer != null)
        {
            return;
        }

        _reconnectTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _reconnectTimer.Tick += (_, _) =>
        {
            _reconnectTimer.Stop();
            _reconnectTimer.Dispose();
            _reconnectTimer = null;
            Connect();
        };
        _reconnectTimer.Start();
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmUserSimConnect)
        {
            _simConnect?.ReceiveMessage();
        }

        base.WndProc(ref message);
    }

    private void OnOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
    {
        Console.WriteLine(
            $"Connected â€” SimConnect {data.dwSimConnectVersionMajor}.{data.dwSimConnectVersionMinor}, " +
            $"simulator {data.dwApplicationVersionMajor}.{data.dwApplicationVersionMinor}.");
        AppendDashboardLog(
            $"Connected to MSFS â€” SimConnect {data.dwSimConnectVersionMajor}.{data.dwSimConnectVersionMinor}");
        AppLog.Write(
            $"Connected to MSFS. SimConnect {data.dwSimConnectVersionMajor}.{data.dwSimConnectVersionMinor}; " +
            $"simulator {data.dwApplicationVersionMajor}.{data.dwApplicationVersionMinor}.");
        if (_connectionLabel != null)
        {
            _connectionLabel.Text = "Connected to MSFS 2024";
            _connectionLabel.ForeColor = System.Drawing.Color.DarkGreen;
        }

        sender.AddToDataDefinition(Definition.AircraftState, "TITLE", null, SIMCONNECT_DATATYPE.STRING256, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "SIM ON GROUND", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "GROUND VELOCITY", "Knots", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "GENERAL ENG COMBUSTION:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "GENERAL ENG COMBUSTION:2", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "GENERAL ENG STARTER ACTIVE:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "GENERAL ENG STARTER ACTIVE:2", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "TURB ENG CORRECTED N1:1", "Percent", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "TURB ENG CORRECTED N1:2", "Percent", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "GENERAL ENG EXHAUST GAS TEMPERATURE:1", "Celsius", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "GENERAL ENG EXHAUST GAS TEMPERATURE:2", "Celsius", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "TURB ENG FUEL FLOW PPH:1", "Pounds per hour", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "TURB ENG FUEL FLOW PPH:2", "Pounds per hour", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "TURB ENG IGNITION SWITCH EX1:1", "Enum", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "TURB ENG IGNITION SWITCH EX1:2", "Enum", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "ELECTRICAL MASTER BATTERY:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "ELECTRICAL MASTER BATTERY:2", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "ELECTRICAL BATTERY VOLTAGE:1", "Volts", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "ELECTRICAL BATTERY VOLTAGE:2", "Volts", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "EXTERNAL POWER AVAILABLE:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "EXTERNAL POWER ON:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "EXTERNAL POWER AVAILABLE", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "EXTERNAL POWER ON", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "BRAKE PARKING POSITION", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "LIGHT BEACON", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "LIGHT NAV", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "LIGHT LOGO", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "LIGHT TAXI", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "CIRCUIT SWITCH ON:17", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "CIRCUIT SWITCH ON:18", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "CIRCUIT SWITCH ON:19", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "CIRCUIT SWITCH ON:20", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "APU PCT RPM", "Percent", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "APU PCT STARTER", "Percent", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "APU SWITCH", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "APU GENERATOR ACTIVE", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "APU GENERATOR SWITCH", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "APU VOLTS", "Volts", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "FUELSYSTEM PUMP SWITCH:1", "Enum", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "FUELSYSTEM PUMP SWITCH:2", "Enum", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "FUELSYSTEM PUMP SWITCH:3", "Enum", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "FUELSYSTEM PUMP SWITCH:4", "Enum", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "FUELSYSTEM PUMP SWITCH:5", "Enum", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "FUELSYSTEM PUMP SWITCH:6", "Enum", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "FUELSYSTEM VALVE SWITCH:9", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "FUELSYSTEM VALVE SWITCH:10", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "CABIN SEATBELTS ALERT SWITCH", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "PLANE ALT ABOVE GROUND", "Feet", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "INDICATED ALTITUDE", "Feet", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "AIRSPEED INDICATED", "Knots", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "VERTICAL SPEED", "Feet per minute", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "G FORCE", "GForce", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "RADIO HEIGHT", "Feet", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "DECISION HEIGHT", "Feet", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "GENERAL ENG REVERSE THRUST ENGAGED:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "GENERAL ENG REVERSE THRUST ENGAGED:2", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "AUTOBRAKES ACTIVE", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "SPOILERS LEFT POSITION", "Percent", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "SPOILERS RIGHT POSITION", "Percent", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "FLAPS HANDLE INDEX", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "GEAR HANDLE POSITION", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "PLANE PITCH DEGREES", "Degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "AUTOPILOT MASTER", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        for (var index = 1; index <= 8; index++)
        {
            sender.AddToDataDefinition(Definition.AircraftState, $"EXIT OPEN:{index}", "Percent", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            sender.AddToDataDefinition(Definition.AircraftState, $"EXIT TYPE:{index}", "Enum", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            sender.AddToDataDefinition(Definition.AircraftState, $"EXIT POSX:{index}", "Feet", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            sender.AddToDataDefinition(Definition.AircraftState, $"EXIT POSY:{index}", "Feet", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            sender.AddToDataDefinition(Definition.AircraftState, $"EXIT POSZ:{index}", "Feet", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        }
        sender.AddToDataDefinition(Definition.AircraftState, "ATC CLEARED IFR", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "SPOILERS ARMED", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "KOHLSMAN SETTING STD:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "KOHLSMAN SETTING STD:2", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "TRAILING EDGE FLAPS LEFT PERCENT", "Percent", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "TRAILING EDGE FLAPS RIGHT PERCENT", "Percent", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "ATC RUNWAY SELECTED", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "ATC RUNWAY START DISTANCE", "Meters", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "GPS TARGET DISTANCE", "Meters", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "GPS WP DISTANCE", "Meters", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.RegisterDataDefineStruct<AircraftData>(Definition.AircraftState);
        sender.MapClientEventToSimEvent(CopilotEvent.SetExternalPower, "SET_EXTERNAL_POWER");
        sender.MapClientEventToSimEvent(CopilotEvent.SetBeacon, "BEACON_LIGHTS_SET");
        sender.MapClientEventToSimEvent(CopilotEvent.StartApu, "APU_STARTER");
        sender.MapClientEventToSimEvent(CopilotEvent.SetApuBleed, "APU_BLEED_AIR_SOURCE_SET");
        sender.MapClientEventToSimEvent(CopilotEvent.SetApuGenerator, "APU_GENERATOR_SWITCH_SET");
        sender.MapClientEventToSimEvent(CopilotEvent.SetFuelPump, "FUELSYSTEM_PUMP_SET");
        sender.MapClientEventToSimEvent(CopilotEvent.FuelSystemPumpOn, "FUELSYSTEM_PUMP_ON");
        sender.MapClientEventToSimEvent(CopilotEvent.FuelSystemPumpOff, "FUELSYSTEM_PUMP_OFF");
        sender.MapClientEventToSimEvent(CopilotEvent.FuelSystemValveOpen, "FUELSYSTEM_VALVE_OPEN");
        sender.MapClientEventToSimEvent(CopilotEvent.FuelSystemValveClose, "FUELSYSTEM_VALVE_CLOSE");
        sender.MapClientEventToSimEvent(CopilotEvent.CabinSeatbeltsToggle, "CABIN_SEATBELTS_ALERT_SWITCH_TOGGLE");
        InitializeMobiFlight(sender);
        InitializePmdgNg3Sdk(sender);

        sender.RequestDataOnSimObject(
            Request.AircraftState,
            Definition.AircraftState,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
            0,
            0,
            0);

        var commandTimer = new System.Windows.Forms.Timer { Interval = 100 };
        commandTimer.Tick += (_, _) => DrainCommands();
        commandTimer.Start();

        if (_oneShotCommand == null)
        {
            StartConsoleReader();
        }
    }

    private void InitializeMobiFlight(SimConnect sender)
    {
        sender.MapClientDataNameToID("MobiFlight.Command", ClientDataArea.MobiFlightCommand);
        sender.CreateClientData(
            ClientDataArea.MobiFlightCommand,
            1024,
            SIMCONNECT_CREATE_CLIENT_DATA_FLAG.DEFAULT);
        sender.MapClientDataNameToID("MobiFlight.Response", ClientDataArea.MobiFlightResponse);
        sender.CreateClientData(
            ClientDataArea.MobiFlightResponse,
            1024,
            SIMCONNECT_CREATE_CLIENT_DATA_FLAG.DEFAULT);
        sender.AddToClientDataDefinition(
            ClientDataDefinition.MobiFlightMessage,
            0,
            1024,
            0,
            0);
        sender.RegisterStruct<SIMCONNECT_RECV_CLIENT_DATA, MobiFlightMessage>(
            ClientDataDefinition.MobiFlightMessage);
        sender.RequestClientData(
            ClientDataArea.MobiFlightResponse,
            Request.MobiFlightResponse,
            ClientDataDefinition.MobiFlightMessage,
            SIMCONNECT_CLIENT_DATA_PERIOD.ON_SET,
            SIMCONNECT_CLIENT_DATA_REQUEST_FLAG.CHANGED,
            0,
            0,
            0);

        SendMobiFlightCommand("MF.DummyCmd");
        SendMobiFlightCommand("MF.Ping");
        SendMobiFlightCommand("MF.DummyCmd");
        AppendDashboardLog("Connecting to installed MobiFlight aircraft adapter...");
    }

    private void SendMobiFlightCommand(string command)
    {
        _simConnect?.SetClientData(
            ClientDataArea.MobiFlightCommand,
            ClientDataDefinition.MobiFlightMessage,
            SIMCONNECT_CLIENT_DATA_SET_FLAG.DEFAULT,
            0,
            new MobiFlightMessage(command));
    }

    private void SendMobiFlightRuntimeCommand(string command)
    {
        _simConnect?.SetClientData(
            ClientDataArea.MobiFlightRuntimeCommand,
            ClientDataDefinition.MobiFlightRuntimeMessage,
            SIMCONNECT_CLIENT_DATA_SET_FLAG.DEFAULT,
            0,
            new MobiFlightMessage(command));
    }

    private void InitializePmdgNg3Sdk(SimConnect sender)
    {
        try
        {
            sender.MapClientDataNameToID("PMDG_NG3_Data", ClientDataArea.PmdgNg3Data);
            sender.AddToClientDataDefinition(
                ClientDataDefinition.PmdgNg3Data,
                0,
                PmdgNg3DataSize,
                0,
                0);
            sender.RegisterStruct<SIMCONNECT_RECV_CLIENT_DATA, PmdgNg3RawData>(
                ClientDataDefinition.PmdgNg3Data);
            sender.RequestClientData(
                ClientDataArea.PmdgNg3Data,
                Request.PmdgNg3Data,
                ClientDataDefinition.PmdgNg3Data,
                SIMCONNECT_CLIENT_DATA_PERIOD.VISUAL_FRAME,
                SIMCONNECT_CLIENT_DATA_REQUEST_FLAG.CHANGED,
                0,
                0,
                0);

            sender.MapClientDataNameToID("PMDG_NG3_Control", ClientDataArea.PmdgNg3Control);
            sender.AddToClientDataDefinition(
                ClientDataDefinition.PmdgNg3Control,
                0,
                (uint)Marshal.SizeOf<PmdgNg3Control>(),
                0,
                0);
            sender.RegisterStruct<SIMCONNECT_RECV_CLIENT_DATA, PmdgNg3Control>(
                ClientDataDefinition.PmdgNg3Control);
            sender.RequestClientData(
                ClientDataArea.PmdgNg3Control,
                Request.PmdgNg3Control,
                ClientDataDefinition.PmdgNg3Control,
                SIMCONNECT_CLIENT_DATA_PERIOD.VISUAL_FRAME,
                SIMCONNECT_CLIENT_DATA_REQUEST_FLAG.CHANGED,
                0,
                0,
                0);
            AppLog.Write("PMDG NG3 SDK client-data connection initialized.");
        }
        catch (Exception ex)
        {
            AppLog.Write($"PMDG NG3 SDK initialization failed: {ex.Message}");
        }
    }

    private void OnClientData(SimConnect sender, SIMCONNECT_RECV_CLIENT_DATA data)
    {
        if (data.dwData.Length == 0)
        {
            return;
        }

        var request = (Request)data.dwRequestID;
        if (request == Request.PmdgNg3Data)
        {
            var raw = (PmdgNg3RawData)data.dwData[0];
            _pmdgNg3State = ParsePmdgNg3State(raw.Data);
            if (!_pmdgNg3DataReady)
            {
                _pmdgNg3DataReady = true;
                AppendDashboardLog("PMDG 737 NG3 SDK data broadcast received.");
                AppLog.Write("PMDG 737 NG3 SDK data broadcast received.");
            }
            return;
        }

        if (request == Request.PmdgNg3Control)
        {
            return;
        }

        if (request is >= Request.NativeBattery1 and <= Request.FbwTcasMode)
        {
            var value = ((MobiFlightFloat)data.dwData[0]).Value;
            if (request == Request.NativeBattery1)
            {
                _nativeBattery1On = value != 0;
            }
            else if (request == Request.NativeBattery2)
            {
                _nativeBattery2On = value != 0;
            }
            else if (request == Request.FbwBattery1Auto)
            {
                var batteryOn = value != 0;
                if (!_fbwBattery1Auto.HasValue || _fbwBattery1Auto.Value != batteryOn)
                {
                    AppLog.Write($"FBW A32NX BAT 1 AUTO changed to {value:F0}.");
                }
                _fbwBattery1Auto = batteryOn;
            }
            else if (request == Request.FbwBattery2Auto)
            {
                var batteryOn = value != 0;
                if (!_fbwBattery2Auto.HasValue || _fbwBattery2Auto.Value != batteryOn)
                {
                    AppLog.Write($"FBW A32NX BAT 2 AUTO changed to {value:F0}.");
                }
                _fbwBattery2Auto = batteryOn;
            }
            else if (request == Request.FbwBattery1Potential)
            {
                if (!_fbwBattery1Potential.HasValue
                    || Math.Abs(_fbwBattery1Potential.Value - value) >= 0.1f)
                {
                    AppLog.Write($"FBW A32NX BAT 1 potential changed to {value:F1} V.");
                }
                _fbwBattery1Potential = value;
            }
            else if (request == Request.FbwBattery2Potential)
            {
                if (!_fbwBattery2Potential.HasValue
                    || Math.Abs(_fbwBattery2Potential.Value - value) >= 0.1f)
                {
                    AppLog.Write($"FBW A32NX BAT 2 potential changed to {value:F1} V.");
                }
                _fbwBattery2Potential = value;
            }
            else if (request == Request.FbwBattery1AutoTyped)
            {
                var batteryOn = value != 0;
                if (!_fbwBattery1AutoTyped.HasValue || _fbwBattery1AutoTyped.Value != batteryOn)
                {
                    AppLog.Write($"FBW A32NX BAT 1 AUTO typed changed to {value:F0}.");
                }
                _fbwBattery1AutoTyped = batteryOn;
            }
            else if (request == Request.FbwBattery2AutoTyped)
            {
                var batteryOn = value != 0;
                if (!_fbwBattery2AutoTyped.HasValue || _fbwBattery2AutoTyped.Value != batteryOn)
                {
                    AppLog.Write($"FBW A32NX BAT 2 AUTO typed changed to {value:F0}.");
                }
                _fbwBattery2AutoTyped = batteryOn;
            }
            else if (request == Request.FbwExternalPowerAvailable)
            {
                SetLoggedBool(ref _fbwExternalPowerAvailable, value, "FBW A32NX EXT PWR available");
            }
            else if (request == Request.FbwExternalPowerOn)
            {
                SetLoggedBool(ref _fbwExternalPowerOn, value, "FBW A32NX EXT PWR ON");
            }
            else if (request == Request.FbwExternalPowerAvailableTyped)
            {
                SetLoggedBool(ref _fbwExternalPowerAvailableTyped, value, "FBW A32NX EXT PWR available typed");
            }
            else if (request == Request.FbwExternalPowerOnTyped)
            {
                SetLoggedBool(ref _fbwExternalPowerOnTyped, value, "FBW A32NX EXT PWR ON typed");
            }
            else if (request == Request.FbwAdirs1Selector)
            {
                SetLoggedFloat(ref _fbwAdirs1Selector, value, "FBW A32NX ADIRS 1 selector");
            }
            else if (request == Request.FbwAdirs2Selector)
            {
                SetLoggedFloat(ref _fbwAdirs2Selector, value, "FBW A32NX ADIRS 2 selector");
            }
            else if (request == Request.FbwAdirs3Selector)
            {
                SetLoggedFloat(ref _fbwAdirs3Selector, value, "FBW A32NX ADIRS 3 selector");
            }
            else if (request == Request.FbwAdirs1SelectorTyped)
            {
                SetLoggedFloat(ref _fbwAdirs1SelectorTyped, value, "FBW A32NX ADIRS 1 selector typed");
            }
            else if (request == Request.FbwAdirs2SelectorTyped)
            {
                SetLoggedFloat(ref _fbwAdirs2SelectorTyped, value, "FBW A32NX ADIRS 2 selector typed");
            }
            else if (request == Request.FbwAdirs3SelectorTyped)
            {
                SetLoggedFloat(ref _fbwAdirs3SelectorTyped, value, "FBW A32NX ADIRS 3 selector typed");
            }
            else if (request == Request.FbwAdirsOnBattery)
            {
                SetLoggedBool(ref _fbwAdirsOnBattery, value, "FBW A32NX ADIRS ON BAT");
            }
            else if (request == Request.FbwCrewOxygen)
            {
                SetLoggedBool(ref _fbwCrewOxygen, value, "FBW A32NX crew oxygen");
            }
            else if (request == Request.FbwCrewOxygenTyped)
            {
                SetLoggedBool(ref _fbwCrewOxygenTyped, value, "FBW A32NX crew oxygen typed");
            }
            else if (request == Request.FbwNavLogoSelector)
            {
                SetLoggedFloat(ref _fbwNavLogoSelector, value, "FBW A32NX NAV/LOGO selector");
            }
            else if (request == Request.FbwNavLogoSelectorTyped)
            {
                SetLoggedFloat(ref _fbwNavLogoSelectorTyped, value, "FBW A32NX NAV/LOGO selector typed");
            }
            else if (request == Request.FbwStrobeAuto)
            {
                SetLoggedBool(ref _fbwStrobeAuto, value, "FBW A32NX strobe auto");
            }
            else if (request == Request.FbwStrobeLightState)
            {
                SetLoggedFloat(ref _fbwStrobeLightState, value, "FBW A32NX strobe light state");
            }
            else if (request == Request.FbwSeatbeltSelector)
            {
                SetLoggedFloat(ref _fbwSeatbeltSelector, value, "FBW A32NX seatbelt selector");
            }
            else if (request == Request.FbwNoSmokingSelector)
            {
                SetLoggedFloat(ref _fbwNoSmokingSelector, value, "FBW A32NX no-smoking selector");
            }
            else if (request == Request.FbwEmergencyExitSelector)
            {
                SetLoggedFloat(ref _fbwEmergencyExitSelector, value, "FBW A32NX emergency-exit selector");
            }
            else if (request == Request.FbwApuMasterSwitch)
            {
                SetLoggedBool(ref _fbwApuMasterSwitch, value, "FBW A32NX APU master");
            }
            else if (request == Request.FbwApuStartButton)
            {
                SetLoggedBool(ref _fbwApuStartButton, value, "FBW A32NX APU start");
            }
            else if (request == Request.FbwApuStartAvailable)
            {
                SetLoggedBool(ref _fbwApuStartAvailable, value, "FBW A32NX APU available");
            }
            else if (request == Request.FbwApuBleedButton)
            {
                SetLoggedBool(ref _fbwApuBleedButton, value, "FBW A32NX APU bleed");
            }
            else if (request == Request.FbwTransponderMode)
            {
                SetLoggedFloat(ref _fbwTransponderMode, value, "FBW A32NX transponder mode");
            }
            else if (request == Request.FbwParkingBrake)
            {
                SetLoggedBool(ref _fbwParkingBrake, value, "FBW A32NX parking brake");
            }
            else if (request == Request.FbwEngine1State)
            {
                SetLoggedFloat(ref _fbwEngine1State, value, "FBW A32NX engine 1 state");
            }
            else if (request == Request.FbwEngine2State)
            {
                SetLoggedFloat(ref _fbwEngine2State, value, "FBW A32NX engine 2 state");
            }
            else if (request == Request.FbwEngine1N1)
            {
                SetLoggedFloat(ref _fbwEngine1N1, value, "FBW A32NX engine 1 N1");
            }
            else if (request == Request.FbwEngine2N1)
            {
                SetLoggedFloat(ref _fbwEngine2N1, value, "FBW A32NX engine 2 N1");
            }
            else if (request == Request.FbwEngine1StarterValveOpen)
            {
                SetLoggedBool(ref _fbwEngine1StarterValveOpen, value, "FBW A32NX engine 1 starter valve");
            }
            else if (request == Request.FbwEngine2StarterValveOpen)
            {
                SetLoggedBool(ref _fbwEngine2StarterValveOpen, value, "FBW A32NX engine 2 starter valve");
            }
            else if (request == Request.FbwSpoilersArmed)
            {
                SetLoggedBool(ref _fbwSpoilersArmed, value, "FBW A32NX spoilers armed");
            }
            else if (request == Request.FbwFlapsHandleIndex)
            {
                SetLoggedFloat(ref _fbwFlapsHandleIndex, value, "FBW A32NX flaps handle");
            }
            else if (request == Request.FbwAutobrakeLevel)
            {
                SetLoggedFloat(ref _fbwAutobrakeLevel, value, "FBW A32NX autobrake mode");
            }
            else if (request == Request.FbwWeatherRadarPwsSelector)
            {
                SetLoggedFloat(ref _fbwWeatherRadarPwsSelector, value, "FBW A32NX WXR/PWS selector");
            }
            else if (request == Request.FbwTcasAltitudeReporting)
            {
                SetLoggedBool(ref _fbwTcasAltitudeReporting, value, "FBW A32NX TCAS altitude reporting");
            }
            else if (request == Request.FbwTcasMode)
            {
                SetLoggedFloat(ref _fbwTcasMode, value, "FBW A32NX TCAS mode");
            }
            else
            {
                switch (request)
                {
                    case Request.NativeFuelPump1: _nativeFuelPump1 = value; break;
                    case Request.NativeFuelPump2: _nativeFuelPump2 = value; break;
                    case Request.NativeFuelPump3: _nativeFuelPump3 = value; break;
                    case Request.NativeFuelPump4: _nativeFuelPump4 = value; break;
                    case Request.NativeFuelPump5: _nativeFuelPump5 = value; break;
                    case Request.NativeFuelPump6: _nativeFuelPump6 = value; break;
                    case Request.NativeNavLogoSelector:
                        _nativeNavLogoSelectorPosition = value;
                        break;
                    case Request.NativeApuAvailable: _nativeApuAvailable = value; break;
                    case Request.NativeApuMasterSwitch: _nativeApuMasterSwitch = value; break;
                    case Request.NativeApuStartButton: _nativeApuStartButton = value; break;
                    case Request.NativeApuBleedButton: _nativeApuBleedButton = value; break;
                    case Request.NativeApuGeneratorOn: _nativeApuGeneratorOn = value; break;
                    case Request.NativeApuFlapPercent: _nativeApuFlapPercent = value; break;
                    case Request.NativeAdirs1State: _nativeAdirs1State = value; break;
                    case Request.NativeAdirs2State: _nativeAdirs2State = value; break;
                    case Request.NativeAdirs3State: _nativeAdirs3State = value; break;
                    case Request.NativeAdirsOnBattery: _nativeAdirsOnBattery = value; break;
                    case Request.NativeCrewOxygen:
                        if (!_nativeCrewOxygen.HasValue
                            || Math.Abs(_nativeCrewOxygen.Value - value) >= 0.01)
                        {
                            AppLog.Write($"Native INI_CREW_SUPPLY changed to {value:F0}.");
                        }
                        _nativeCrewOxygen = value;
                        break;
                    case Request.NativeStrobeSelector:
                        _nativeStrobeSelector = value;
                        break;
                    case Request.NativeApuFireTest: _nativeApuFireTest = value; break;
                    case Request.NativeEngine1FireTest: _nativeEngine1FireTest = value; break;
                    case Request.NativeEngine2FireTest: _nativeEngine2FireTest = value; break;
                    case Request.NativeApuFireWarningLit: _nativeApuFireWarningLit = value; break;
                    case Request.NativeApuFireSound: _nativeApuFireSound = value; break;
                    case Request.NativeEngine1FireWarningLit: _nativeEngine1FireWarningLit = value; break;
                    case Request.NativeEngine1FireSound: _nativeEngine1FireSound = value; break;
                    case Request.NativeEngine2FireWarningLit: _nativeEngine2FireWarningLit = value; break;
                    case Request.NativeEngine2FireSound: _nativeEngine2FireSound = value; break;
                    case Request.NativeSeatbeltSelector: _nativeSeatbeltSelector = value; break;
                    case Request.NativeSeatbeltSignsOn: _nativeSeatbeltSignsOn = value; break;
                    case Request.NativeNoSmokingSelector: _nativeNoSmokingSelector = value; break;
                    case Request.NativeNoSmokingSignsOn: _nativeNoSmokingSignsOn = value; break;
                    case Request.NativeEmergencyExitSelector: _nativeEmergencyExitSelector = value; break;
                    case Request.NativeTransponderAtcState: _nativeTransponderAtcState = value; break;
                    case Request.NativeTcasMode: _nativeTcasMode = value; break;
                    case Request.NativeTransponderStandby: _nativeTransponderStandby = value; break;
                    case Request.NativeSpoilersArmed:
                        if (!_nativeSpoilersArmed.HasValue
                            || Math.Abs(_nativeSpoilersArmed.Value - value) >= 0.01)
                        {
                            AppLog.Write($"Native INI_SPOILERS_ARMED changed to {value:F0}.");
                        }
                        _nativeSpoilersArmed = value;
                        break;
                    case Request.NativeAutobrakeLevel: _nativeAutobrakeLevel = value; break;
                    case Request.NativeTcasAltitudeReporting:
                        _nativeTcasAltitudeReporting = value;
                        break;
                    case Request.NativeGearHandlePosition:
                        if (!_nativeGearHandlePosition.HasValue
                            || Math.Abs(_nativeGearHandlePosition.Value - value) >= 0.01)
                        {
                            AppLog.Write(
                                $"Native INI_GEAR_HANDLE_STATUS_ANIMATION changed to {value:F2}.");
                        }
                        _nativeGearHandlePosition = value;
                        break;
                    case Request.NativeWeatherRadarPwsSelector:
                        _nativeWeatherRadarPwsSelector = value;
                        break;
                    case Request.NativeNoseLightSelector:
                        _nativeNoseLightSelector = value;
                        break;
                    case Request.NativeLeftLandingLightSelector:
                        _nativeLeftLandingLightSelector = value;
                        break;
                    case Request.NativeRightLandingLightSelector:
                        _nativeRightLandingLightSelector = value;
                        break;
                }
            }

            ApplyNativeAircraftState();
            return;
        }

        if (request != Request.MobiFlightResponse
            && request != Request.MobiFlightRuntimeResponse)
        {
            return;
        }

        var response = ((MobiFlightMessage)data.dwData[0]).ToString();
        if (request == Request.MobiFlightResponse
            && string.Equals(response, "MF.Pong", StringComparison.OrdinalIgnoreCase))
        {
            _mobiFlightReady = true;
            AppendDashboardLog("MobiFlight aircraft adapter connected.");
            AppLog.Write("MobiFlight aircraft adapter connected (MF.Pong).");
            SendMobiFlightCommand("MF.DummyCmd");
            SendMobiFlightCommand($"MF.Clients.Add.{MobiFlightRuntimeClientName}");
            UpdateDashboard();
            TryExecuteOneShotCommand();
            return;
        }

        if (request == Request.MobiFlightResponse
            && response.IndexOf(MobiFlightRuntimeClientName, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            InitializeMobiFlightRuntime(sender);
            return;
        }

        if (!string.IsNullOrWhiteSpace(response))
        {
            AppendDashboardLog($"Adapter: {response}");
            AppLog.Write($"MobiFlight response: {response}");
        }
    }

    private void InitializeMobiFlightRuntime(SimConnect sender)
    {
        if (_mobiFlightRuntimeReady)
        {
            return;
        }

        sender.MapClientDataNameToID(
            $"{MobiFlightRuntimeClientName}.LVars",
            ClientDataArea.MobiFlightRuntimeLVars);
        sender.CreateClientData(
            ClientDataArea.MobiFlightRuntimeLVars,
            4096,
            SIMCONNECT_CREATE_CLIENT_DATA_FLAG.DEFAULT);
        sender.MapClientDataNameToID(
            $"{MobiFlightRuntimeClientName}.Command",
            ClientDataArea.MobiFlightRuntimeCommand);
        sender.CreateClientData(
            ClientDataArea.MobiFlightRuntimeCommand,
            1024,
            SIMCONNECT_CREATE_CLIENT_DATA_FLAG.DEFAULT);
        sender.MapClientDataNameToID(
            $"{MobiFlightRuntimeClientName}.Response",
            ClientDataArea.MobiFlightRuntimeResponse);
        sender.CreateClientData(
            ClientDataArea.MobiFlightRuntimeResponse,
            1024,
            SIMCONNECT_CREATE_CLIENT_DATA_FLAG.DEFAULT);

        sender.AddToClientDataDefinition(
            ClientDataDefinition.MobiFlightRuntimeMessage,
            0,
            1024,
            0,
            0);
        sender.RegisterStruct<SIMCONNECT_RECV_CLIENT_DATA, MobiFlightMessage>(
            ClientDataDefinition.MobiFlightRuntimeMessage);
        sender.RequestClientData(
            ClientDataArea.MobiFlightRuntimeResponse,
            Request.MobiFlightRuntimeResponse,
            ClientDataDefinition.MobiFlightRuntimeMessage,
            SIMCONNECT_CLIENT_DATA_PERIOD.ON_SET,
            SIMCONNECT_CLIENT_DATA_REQUEST_FLAG.CHANGED,
            0,
            0,
            0);

        RegisterMobiFlightFloat(
            sender,
            ClientDataDefinition.NativeBattery1,
            Request.NativeBattery1,
            0);
        RegisterMobiFlightFloat(
            sender,
            ClientDataDefinition.NativeBattery2,
            Request.NativeBattery2,
            sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeFuelPump1, Request.NativeFuelPump1, 2 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeFuelPump2, Request.NativeFuelPump2, 3 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeFuelPump3, Request.NativeFuelPump3, 4 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeFuelPump4, Request.NativeFuelPump4, 5 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeFuelPump5, Request.NativeFuelPump5, 6 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeFuelPump6, Request.NativeFuelPump6, 7 * sizeof(float));
        RegisterMobiFlightFloat(
            sender,
            ClientDataDefinition.NativeNavLogoSelector,
            Request.NativeNavLogoSelector,
            8 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeApuAvailable, Request.NativeApuAvailable, 9 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeApuMasterSwitch, Request.NativeApuMasterSwitch, 10 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeApuStartButton, Request.NativeApuStartButton, 11 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeApuBleedButton, Request.NativeApuBleedButton, 12 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeApuGeneratorOn, Request.NativeApuGeneratorOn, 13 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeApuFlapPercent, Request.NativeApuFlapPercent, 14 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeAdirs1State, Request.NativeAdirs1State, 15 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeAdirs2State, Request.NativeAdirs2State, 16 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeAdirs3State, Request.NativeAdirs3State, 17 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeAdirsOnBattery, Request.NativeAdirsOnBattery, 18 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeCrewOxygen, Request.NativeCrewOxygen, 19 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeStrobeSelector, Request.NativeStrobeSelector, 20 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeApuFireTest, Request.NativeApuFireTest, 21 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeEngine1FireTest, Request.NativeEngine1FireTest, 22 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeEngine2FireTest, Request.NativeEngine2FireTest, 23 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeApuFireWarningLit, Request.NativeApuFireWarningLit, 24 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeApuFireSound, Request.NativeApuFireSound, 25 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeEngine1FireWarningLit, Request.NativeEngine1FireWarningLit, 26 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeEngine1FireSound, Request.NativeEngine1FireSound, 27 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeEngine2FireWarningLit, Request.NativeEngine2FireWarningLit, 28 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeEngine2FireSound, Request.NativeEngine2FireSound, 29 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeSeatbeltSelector, Request.NativeSeatbeltSelector, 30 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeSeatbeltSignsOn, Request.NativeSeatbeltSignsOn, 31 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeNoSmokingSelector, Request.NativeNoSmokingSelector, 32 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeNoSmokingSignsOn, Request.NativeNoSmokingSignsOn, 33 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeEmergencyExitSelector, Request.NativeEmergencyExitSelector, 34 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeTransponderAtcState, Request.NativeTransponderAtcState, 35 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeTcasMode, Request.NativeTcasMode, 36 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeTransponderStandby, Request.NativeTransponderStandby, 37 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeSpoilersArmed, Request.NativeSpoilersArmed, 38 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeAutobrakeLevel, Request.NativeAutobrakeLevel, 39 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeTcasAltitudeReporting, Request.NativeTcasAltitudeReporting, 40 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeGearHandlePosition, Request.NativeGearHandlePosition, 41 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeWeatherRadarPwsSelector, Request.NativeWeatherRadarPwsSelector, 42 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeNoseLightSelector, Request.NativeNoseLightSelector, 43 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeLeftLandingLightSelector, Request.NativeLeftLandingLightSelector, 44 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeRightLandingLightSelector, Request.NativeRightLandingLightSelector, 45 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwBattery1Auto, Request.FbwBattery1Auto, 46 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwBattery2Auto, Request.FbwBattery2Auto, 47 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwBattery1Potential, Request.FbwBattery1Potential, 48 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwBattery2Potential, Request.FbwBattery2Potential, 49 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwBattery1AutoTyped, Request.FbwBattery1AutoTyped, 50 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwBattery2AutoTyped, Request.FbwBattery2AutoTyped, 51 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwExternalPowerAvailable, Request.FbwExternalPowerAvailable, 52 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwExternalPowerOn, Request.FbwExternalPowerOn, 53 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwExternalPowerAvailableTyped, Request.FbwExternalPowerAvailableTyped, 54 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwExternalPowerOnTyped, Request.FbwExternalPowerOnTyped, 55 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwAdirs1Selector, Request.FbwAdirs1Selector, 56 * sizeof(float), SIMCONNECT_CLIENT_DATA_PERIOD.SECOND);
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwAdirs2Selector, Request.FbwAdirs2Selector, 57 * sizeof(float), SIMCONNECT_CLIENT_DATA_PERIOD.SECOND);
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwAdirs3Selector, Request.FbwAdirs3Selector, 58 * sizeof(float), SIMCONNECT_CLIENT_DATA_PERIOD.SECOND);
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwAdirs1SelectorTyped, Request.FbwAdirs1SelectorTyped, 59 * sizeof(float), SIMCONNECT_CLIENT_DATA_PERIOD.SECOND);
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwAdirs2SelectorTyped, Request.FbwAdirs2SelectorTyped, 60 * sizeof(float), SIMCONNECT_CLIENT_DATA_PERIOD.SECOND);
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwAdirs3SelectorTyped, Request.FbwAdirs3SelectorTyped, 61 * sizeof(float), SIMCONNECT_CLIENT_DATA_PERIOD.SECOND);
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwAdirsOnBattery, Request.FbwAdirsOnBattery, 62 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwCrewOxygen, Request.FbwCrewOxygen, 63 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwCrewOxygenTyped, Request.FbwCrewOxygenTyped, 64 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwNavLogoSelector, Request.FbwNavLogoSelector, 65 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwNavLogoSelectorTyped, Request.FbwNavLogoSelectorTyped, 66 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwStrobeAuto, Request.FbwStrobeAuto, 67 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwStrobeLightState, Request.FbwStrobeLightState, 68 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwSeatbeltSelector, Request.FbwSeatbeltSelector, 69 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwNoSmokingSelector, Request.FbwNoSmokingSelector, 70 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwEmergencyExitSelector, Request.FbwEmergencyExitSelector, 71 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwApuMasterSwitch, Request.FbwApuMasterSwitch, 72 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwApuStartButton, Request.FbwApuStartButton, 73 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwApuStartAvailable, Request.FbwApuStartAvailable, 74 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwApuBleedButton, Request.FbwApuBleedButton, 75 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwTransponderMode, Request.FbwTransponderMode, 76 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwParkingBrake, Request.FbwParkingBrake, 77 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwEngine1State, Request.FbwEngine1State, 78 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwEngine2State, Request.FbwEngine2State, 79 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwEngine1N1, Request.FbwEngine1N1, 80 * sizeof(float), SIMCONNECT_CLIENT_DATA_PERIOD.SECOND);
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwEngine2N1, Request.FbwEngine2N1, 81 * sizeof(float), SIMCONNECT_CLIENT_DATA_PERIOD.SECOND);
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwEngine1StarterValveOpen, Request.FbwEngine1StarterValveOpen, 82 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwEngine2StarterValveOpen, Request.FbwEngine2StarterValveOpen, 83 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwSpoilersArmed, Request.FbwSpoilersArmed, 84 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwFlapsHandleIndex, Request.FbwFlapsHandleIndex, 85 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwAutobrakeLevel, Request.FbwAutobrakeLevel, 86 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwWeatherRadarPwsSelector, Request.FbwWeatherRadarPwsSelector, 87 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwTcasAltitudeReporting, Request.FbwTcasAltitudeReporting, 88 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwTcasMode, Request.FbwTcasMode, 89 * sizeof(float));

        _mobiFlightRuntimeReady = true;
        _mobiFlightRuntimeInitializedUtc = DateTime.UtcNow;
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:INI_OVHD_ELEC_BAT_1_PB_IS_AUTO_SWITCH)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:INI_OVHD_ELEC_BAT_2_PB_IS_AUTO_SWITCH)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_OUTER_TANK_LEFT_PUMP_ON)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_INNER_TANK_LEFT_PUMP_ON)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_CENTER_TANK_LEFT_PUMP_ON)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_CENTER_TANK_RIGHT_PUMP_ON)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_INNER_TANK_RIGHT_PUMP_ON)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_OUTER_TANK_RIGHT_PUMP_ON)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_LOGO_LIGHT_SWITCH)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_APU_AVAILABLE)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_APU_MASTER_SWITCH)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_APU_START_BUTTON)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_APU_BLEED_BUTTON)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_APU_GEN_ON)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_APU_FLAP_PERCENT)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_IRS1_STATE)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_IRS2_STATE)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_IRS3_STATE)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_IRS_ON_BATTERY)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_CREW_SUPPLY)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_STROBE_LIGHT_SWITCH)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_APU_FIRE_TEST)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_ENG1_FIRE_TEST)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_ENG2_FIRE_TEST)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:A320_APU_FIRE_LIT)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_APU_FIRE_SOUND)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:A320_ENG1_FIRE_LIT)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_ENG1_FIRE_SOUND)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:A320_ENG2_FIRE_LIT)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_ENG2_FIRE_SOUND)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_SEATBELTS_SWITCH)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_SEATBELTS_ON)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_NO_SMOKING_SWITCH)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_NO_SMOKING_ON)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_EMER_EXIT_SWITCH)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_TCAS_ATC_STATE)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_TCAS_MODE_PEDESTAL)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_TCAS_STBY_STATE)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_SPOILERS_ARMED)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_AUTOBRAKE_LEVEL)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_TCAS_ALT_STATE)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_GEAR_HANDLE_STATUS_ANIMATION)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_WX_SYS_SWITCH)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:INI_TAXI_LIGHT_SWITCH)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:A320_LANDING_LIGHT_SWITCH_LEFT)");
        SendMobiFlightRuntimeCommand("MF.SimVars.Add.(L:A320_LANDING_LIGHT_SWITCH_RIGHT)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_ELEC_BAT_1_PB_IS_AUTO)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_ELEC_BAT_2_PB_IS_AUTO)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_ELEC_BAT_1_POTENTIAL)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_ELEC_BAT_2_POTENTIAL)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_ELEC_BAT_1_PB_IS_AUTO, Bool)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_ELEC_BAT_2_PB_IS_AUTO, Bool)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_EXT_PWR_AVAIL:1)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_ELEC_EXT_PWR_PB_IS_ON)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_EXT_PWR_AVAIL:1, Bool)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_ELEC_EXT_PWR_PB_IS_ON, Bool)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_ADIRS_IR_1_MODE_SELECTOR_KNOB)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_ADIRS_IR_2_MODE_SELECTOR_KNOB)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_ADIRS_IR_3_MODE_SELECTOR_KNOB)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_ADIRS_IR_1_MODE_SELECTOR_KNOB, Enum)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_ADIRS_IR_2_MODE_SELECTOR_KNOB, Enum)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_ADIRS_IR_3_MODE_SELECTOR_KNOB, Enum)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_ADIRS_ON_BAT_IS_ILLUMINATED, Bool)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:PUSH_OVHD_OXYGEN_CREW)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:PUSH_OVHD_OXYGEN_CREW, Bool)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_LIGHTS_NAV_LOGO)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_LIGHTS_NAV_LOGO, Enum)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:STROBE_0_AUTO)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:LIGHTING_STROBE_0)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:XMLVAR_SWITCH_OVHD_INTLT_SEATBELT_Position)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:XMLVAR_SWITCH_OVHD_INTLT_NOSMOKING_Position)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:XMLVAR_SWITCH_OVHD_INTLT_EMEREXIT_Position)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_APU_MASTER_SW_PB_IS_ON)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_APU_START_PB_IS_ON)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_APU_START_PB_IS_AVAILABLE)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_PNEU_APU_BLEED_PB_IS_ON)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_TRANSPONDER_MODE)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_PARK_BRAKE_LEVER_POS)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_ENGINE_STATE:1)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_ENGINE_STATE:2)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_ENGINE_N1:1)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_ENGINE_N1:2)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_PNEU_ENG_1_STARTER_VALVE_OPEN)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_PNEU_ENG_2_STARTER_VALVE_OPEN)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_SPOILERS_ARMED)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_FLAPS_HANDLE_INDEX)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_AUTOBRAKES_ARMED_MODE)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_SWITCH_RADAR_PWS_POSITION)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_SWITCH_ATC_ALT)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_SWITCH_TCAS_POSITION)");
        SendMobiFlightRuntimeCommand("MF.DummyCmd");
        AppendDashboardLog("iniBuilds native state monitoring connected.");
    }

    private static void RegisterMobiFlightFloat(
        SimConnect sender,
        ClientDataDefinition definition,
        Request request,
        int offset,
        SIMCONNECT_CLIENT_DATA_PERIOD period = SIMCONNECT_CLIENT_DATA_PERIOD.ON_SET)
    {
        sender.AddToClientDataDefinition(definition, (uint)offset, sizeof(float), 0, 0);
        sender.RegisterStruct<SIMCONNECT_RECV_CLIENT_DATA, MobiFlightFloat>(definition);
        sender.RequestClientData(
            ClientDataArea.MobiFlightRuntimeLVars,
            request,
            definition,
            period,
            SIMCONNECT_CLIENT_DATA_REQUEST_FLAG.DEFAULT,
            0,
            0,
            0);
    }

    private void ApplyNativeAircraftState()
    {
        if (_replayActive)
        {
            return;
        }
        if (_state == null)
        {
            return;
        }

        if (_state.IsIniBuildsA320Family && _nativeBattery1On.HasValue)
        {
            _state.Battery1On = _nativeBattery1On.Value;
        }
        if (_state.IsIniBuildsA320Family && _nativeBattery2On.HasValue)
        {
            _state.Battery2On = _nativeBattery2On.Value;
        }
        if (_state.IsFlyByWireA320Neo
            && (_fbwBattery1AutoTyped.HasValue || _fbwBattery1Auto.HasValue))
        {
            _state.Battery1On = ResolveFbwBatteryState(
                _fbwCommandedBattery1Auto,
                _fbwBattery1AutoTyped,
                _fbwBattery1Auto,
                _state.Battery1On ? 1 : 0);
        }
        if (_state.IsFlyByWireA320Neo
            && (_fbwBattery2AutoTyped.HasValue || _fbwBattery2Auto.HasValue))
        {
            _state.Battery2On = ResolveFbwBatteryState(
                _fbwCommandedBattery2Auto,
                _fbwBattery2AutoTyped,
                _fbwBattery2Auto,
                _state.Battery2On ? 1 : 0);
        }
        if (_state.IsFlyByWireA320Neo)
        {
            if (_fbwSeatbeltSelector.HasValue)
            {
                _state.SeatbeltSelectorPosition =
                    ResolveFbwSeatbeltSelectorPosition(
                        _fbwSeatbeltSelector,
                        _state.SeatbeltSignsOn);
            }
            if (_fbwNoSmokingSelector.HasValue)
            {
                _state.NoSmokingSelectorPosition = _fbwNoSmokingSelector.Value;
                _state.NoSmokingSignsOn = Math.Abs(_fbwNoSmokingSelector.Value) < 0.1;
            }
            if (_fbwEmergencyExitSelector.HasValue)
            {
                _state.EmergencyExitSelectorPosition = _fbwEmergencyExitSelector.Value;
            }
            if (_fbwApuMasterSwitch.HasValue)
            {
                _state.ApuMasterSwitchOn = _fbwApuMasterSwitch.Value;
            }
            if (_fbwApuStartAvailable.HasValue)
            {
                _state.ApuAvailable = _fbwApuStartAvailable.Value;
            }
            if (_fbwApuStartButton.HasValue || _fbwApuStartAvailable.HasValue)
            {
                _state.ApuStartButtonOn =
                    _fbwApuStartButton == true || _fbwApuStartAvailable == true;
            }
            if (_fbwApuBleedButton.HasValue)
            {
                _state.ApuBleedOn = _fbwApuBleedButton.Value;
            }
            if (_fbwTransponderMode.HasValue)
            {
                _state.TransponderModeSelectorPosition = _fbwTransponderMode.Value;
            }
            if (_fbwTcasAltitudeReporting.HasValue
                || _fbwCommandedTcasAltitudeReporting.HasValue)
            {
                _state.TcasAltitudeReportingOn = ResolveFbwTcasAltitudeReporting(
                    _fbwCommandedTcasAltitudeReporting,
                    _fbwCommandedTcasAltitudeReportingUtc,
                    _fbwTcasAltitudeReporting);
            }
            if (_fbwTcasMode.HasValue || _fbwCommandedTcasMode.HasValue)
            {
                _state.TcasMode = ResolveFbwSelectorWithCommand(
                    _fbwCommandedTcasMode,
                    _fbwCommandedTcasModeUtc,
                    _fbwTcasMode);
            }
            if (_fbwParkingBrake.HasValue)
            {
                _state.ParkingBrakeSet = _fbwParkingBrake.Value;
            }
            if (_fbwEngine1State.HasValue || _fbwEngine1N1.HasValue)
            {
                _state.FbwEngine1State = _fbwEngine1State;
                _state.Engine1Running =
                    _fbwEngine1State == 1
                    || (_fbwEngine1N1 ?? (float)_state.Engine1N1Percent) >= 15;
            }
            if (_fbwEngine2State.HasValue || _fbwEngine2N1.HasValue)
            {
                _state.FbwEngine2State = _fbwEngine2State;
                _state.Engine2Running =
                    _fbwEngine2State == 1
                    || (_fbwEngine2N1 ?? (float)_state.Engine2N1Percent) >= 15;
            }
            if (_fbwEngine1StarterValveOpen.HasValue || _fbwEngine1State.HasValue)
            {
                _state.Engine1StarterActive =
                    _fbwEngine1StarterValveOpen == true
                    || _fbwEngine1State == 2
                    || _fbwEngine1State == 3;
            }
            if (_fbwEngine2StarterValveOpen.HasValue || _fbwEngine2State.HasValue)
            {
                _state.Engine2StarterActive =
                    _fbwEngine2StarterValveOpen == true
                    || _fbwEngine2State == 2
                    || _fbwEngine2State == 3;
            }
            if (_fbwEngine1N1.HasValue)
            {
                _state.Engine1N1Percent = _fbwEngine1N1.Value;
            }
            if (_fbwEngine2N1.HasValue)
            {
                _state.Engine2N1Percent = _fbwEngine2N1.Value;
            }
            _state.GroundSpoilersArmed = ResolveFbwSpoilersArmedState(
                _fbwCommandedSpoilersArmed,
                _fbwCommandedSpoilersArmedUtc,
                _fbwSpoilersArmed,
                _state.GroundSpoilersArmed ? 1 : 0);
            if (_fbwFlapsHandleIndex.HasValue)
            {
                _state.FlapsHandleIndex = _fbwFlapsHandleIndex.Value;
            }
            if (_fbwAutobrakeLevel.HasValue || _fbwCommandedAutobrakeLevel.HasValue)
            {
                _state.AutobrakeLevel = ResolveFbwAutobrakeLevel(
                    _fbwCommandedAutobrakeLevel,
                    _fbwCommandedAutobrakeLevelUtc,
                    _fbwAutobrakeLevel);
            }
            if (_fbwWeatherRadarPwsSelector.HasValue
                || _fbwCommandedWeatherRadarPwsSelector.HasValue)
            {
                _state.WeatherRadarPwsSelectorPosition =
                    ResolveFbwWeatherRadarPwsSelector(
                        _fbwCommandedWeatherRadarPwsSelector,
                        _fbwCommandedWeatherRadarPwsSelectorUtc,
                        _fbwWeatherRadarPwsSelector);
            }
            // From here down the fields are iniBuilds-native LVars. Do not let
            // their default/stale values masquerade as valid FBW cockpit state.
            return;
        }
        if (_nativeFuelPump1.HasValue
            && _nativeFuelPump2.HasValue
            && _nativeFuelPump3.HasValue
            && _nativeFuelPump4.HasValue
            && _nativeFuelPump5.HasValue
            && _nativeFuelPump6.HasValue)
        {
            _state.FuelPump1State = _nativeFuelPump1.Value;
            _state.FuelPump2State = _nativeFuelPump2.Value;
            _state.FuelPump3State = _nativeFuelPump3.Value;
            _state.FuelPump4State = _nativeFuelPump4.Value;
            _state.FuelPump5State = _nativeFuelPump5.Value;
            _state.FuelPump6State = _nativeFuelPump6.Value;
            _state.FuelPumpsConfigured = _nativeFuelPump1.Value != 0
                                         && _nativeFuelPump2.Value != 0
                                         && _nativeFuelPump3.Value != 0
                                         && _nativeFuelPump4.Value != 0
                                         && _nativeFuelPump5.Value != 0
                                         && _nativeFuelPump6.Value != 0;
        }
        if (_nativeNavLogoSelectorPosition.HasValue)
        {
            _state.NavLogoSelectorPosition = _nativeNavLogoSelectorPosition.Value;
        }
        if (_nativeApuAvailable.HasValue)
        {
            _state.ApuAvailable = _nativeApuAvailable.Value != 0;
        }
        if (_nativeApuMasterSwitch.HasValue)
        {
            _state.ApuMasterSwitchOn = _nativeApuMasterSwitch.Value != 0;
        }
        if (_nativeApuStartButton.HasValue)
        {
            _state.ApuStartButtonOn = _nativeApuStartButton.Value != 0;
        }
        if (_nativeApuBleedButton.HasValue)
        {
            _state.ApuBleedOn = _nativeApuBleedButton.Value != 0;
        }
        if (_nativeApuGeneratorOn.HasValue)
        {
            _state.ApuGeneratorSwitchOn = _nativeApuGeneratorOn.Value != 0;
        }
        if (_nativeApuFlapPercent.HasValue)
        {
            _state.ApuFlapPercent = _nativeApuFlapPercent.Value;
        }
        if (_nativeAdirs1State.HasValue)
        {
            _state.Adirs1SelectorState = _nativeAdirs1State.Value;
        }
        if (_nativeAdirs2State.HasValue)
        {
            _state.Adirs2SelectorState = _nativeAdirs2State.Value;
        }
        if (_nativeAdirs3State.HasValue)
        {
            _state.Adirs3SelectorState = _nativeAdirs3State.Value;
        }
        if (_nativeAdirsOnBattery.HasValue)
        {
            _state.AdirsOnBattery = _nativeAdirsOnBattery.Value != 0;
        }
        if (_nativeCrewOxygen.HasValue)
        {
            _state.CrewOxygenOn = _nativeCrewOxygen.Value != 0;
        }
        if (_nativeStrobeSelector.HasValue)
        {
            _state.StrobeSelectorPosition = _nativeStrobeSelector.Value;
        }
        _state.ApuFireTestActive = _nativeApuFireTest.HasValue && _nativeApuFireTest.Value != 0;
        _state.ApuFireWarningLit = _nativeApuFireWarningLit.HasValue && _nativeApuFireWarningLit.Value != 0;
        _state.ApuFireSoundActive = _nativeApuFireSound.HasValue && _nativeApuFireSound.Value != 0;
        _state.Engine1FireTestActive = _nativeEngine1FireTest.HasValue && _nativeEngine1FireTest.Value != 0;
        _state.Engine1FireWarningLit = _nativeEngine1FireWarningLit.HasValue && _nativeEngine1FireWarningLit.Value != 0;
        _state.Engine1FireSoundActive = _nativeEngine1FireSound.HasValue && _nativeEngine1FireSound.Value != 0;
        _state.Engine2FireTestActive = _nativeEngine2FireTest.HasValue && _nativeEngine2FireTest.Value != 0;
        _state.Engine2FireWarningLit = _nativeEngine2FireWarningLit.HasValue && _nativeEngine2FireWarningLit.Value != 0;
        _state.Engine2FireSoundActive = _nativeEngine2FireSound.HasValue && _nativeEngine2FireSound.Value != 0;
        _state.SeatbeltSelectorPosition = _nativeSeatbeltSelector;
        _state.SeatbeltSignsOn = _nativeSeatbeltSignsOn.HasValue && _nativeSeatbeltSignsOn.Value != 0;
        _state.NoSmokingSelectorPosition = _nativeNoSmokingSelector;
        _state.NoSmokingSignsOn = _nativeNoSmokingSignsOn.HasValue && _nativeNoSmokingSignsOn.Value != 0;
        _state.EmergencyExitSelectorPosition = _nativeEmergencyExitSelector;
        if (_nativeSpoilersArmed.HasValue)
        {
            _state.GroundSpoilersArmed = _nativeSpoilersArmed.Value != 0;
        }
        _state.AutobrakeLevel = _nativeAutobrakeLevel;
        _state.WeatherRadarPwsSelectorPosition = _nativeWeatherRadarPwsSelector;
        _state.NoseLightSelectorPosition = _nativeNoseLightSelector;
        _state.LeftLandingLightSelectorPosition = _nativeLeftLandingLightSelector;
        _state.RightLandingLightSelectorPosition = _nativeRightLandingLightSelector;
        _state.TcasAltitudeReportingOn =
            _nativeTcasAltitudeReporting.HasValue
                ? _nativeTcasAltitudeReporting.Value == 0
                : null;
        _state.TransponderAtcState = _nativeTransponderAtcState;
        _state.TcasMode = _nativeTcasMode;
        _state.TransponderModeSelectorPosition = _nativeTransponderStandby;
        _state.TransponderStandby = _nativeTransponderStandby.HasValue
                                    && _nativeTransponderStandby.Value != 0;
        _state.ApuFireTestCompleted = _apuFireTestCompleted;
        _state.Engine1FireTestCompleted = _engine1FireTestCompleted;
        _state.Engine2FireTestCompleted = _engine2FireTestCompleted;
        UpdateTelemetrySanity(_state);
        UpdateCockpitDisplayReadiness(_state);

        VerifyPendingBatteryProcedure();
        VerifyPendingFireTest();
        UpdateDashboard();
        TryExecuteOneShotCommand();
    }

    private void OnAircraftData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (_replayActive)
        {
            return;
        }
        if ((Request)data.dwRequestID != Request.AircraftState || data.dwData.Length == 0)
        {
            return;
        }

        var raw = (AircraftData)data.dwData[0];
        var approachDistance = ResolveApproachDistance(raw);
        var isIniBuildsA320Family =
            raw.Title.Equals("A320neo V2", StringComparison.OrdinalIgnoreCase)
            || raw.Title.Equals("A321", StringComparison.OrdinalIgnoreCase)
            || raw.Title.IndexOf("A321", StringComparison.OrdinalIgnoreCase) >= 0;
        var isFlyByWireA320Neo =
            raw.Title.IndexOf("A32NX", StringComparison.OrdinalIgnoreCase) >= 0
            || raw.Title.IndexOf("FlyByWire", StringComparison.OrdinalIgnoreCase) >= 0;
        var isPmdg737 =
            raw.Title.IndexOf("PMDG", StringComparison.OrdinalIgnoreCase) >= 0
            && raw.Title.IndexOf("737", StringComparison.OrdinalIgnoreCase) >= 0;
        var pmdg = _pmdgNg3State;
        if (isFlyByWireA320Neo)
        {
            LogChangedVoltage("FBW generic BAT 1 voltage", raw.Battery1Voltage, ref _lastLoggedBattery1Voltage);
            LogChangedVoltage("FBW generic BAT 2 voltage", raw.Battery2Voltage, ref _lastLoggedBattery2Voltage);
        }
        _state = new AircraftState
        {
            Title = raw.Title,
            OnGround = raw.OnGround != 0,
            GroundSpeedKnots = raw.GroundSpeed,
            Engine1Running = isFlyByWireA320Neo
                ? _fbwEngine1State == 1 || (_fbwEngine1N1 ?? (float)raw.Engine1N1) >= 15
                : isPmdg737
                    ? raw.Engine1Combustion != 0 || raw.Engine1N1 >= 15
                : raw.Engine1Combustion != 0,
            Engine2Running = isFlyByWireA320Neo
                ? _fbwEngine2State == 1 || (_fbwEngine2N1 ?? (float)raw.Engine2N1) >= 15
                : isPmdg737
                    ? raw.Engine2Combustion != 0 || raw.Engine2N1 >= 15
                : raw.Engine2Combustion != 0,
            Engine1StarterActive = isFlyByWireA320Neo
                ? _fbwEngine1StarterValveOpen == true
                  || _fbwEngine1State == 2
                  || _fbwEngine1State == 3
                  || raw.Engine1Starter != 0
                : isPmdg737
                    ? pmdg?.Engine1StartValveOpen == true || raw.Engine1Starter != 0
                : raw.Engine1Starter != 0,
            Engine2StarterActive = isFlyByWireA320Neo
                ? _fbwEngine2StarterValveOpen == true
                  || _fbwEngine2State == 2
                  || _fbwEngine2State == 3
                  || raw.Engine2Starter != 0
                : isPmdg737
                    ? pmdg?.Engine2StartValveOpen == true || raw.Engine2Starter != 0
                : raw.Engine2Starter != 0,
            Engine1N1Percent = isFlyByWireA320Neo
                ? _fbwEngine1N1 ?? raw.Engine1N1
                : raw.Engine1N1,
            Engine2N1Percent = isFlyByWireA320Neo
                ? _fbwEngine2N1 ?? raw.Engine2N1
                : raw.Engine2N1,
            Engine1EgtCelsius = raw.Engine1Egt,
            Engine2EgtCelsius = raw.Engine2Egt,
            Engine1FuelFlowPph = raw.Engine1FuelFlow,
            Engine2FuelFlowPph = raw.Engine2FuelFlow,
            EngineModeSelectorPosition = ResolveEngineModeSelectorPosition(
                raw.Engine1IgnitionSwitch,
                raw.Engine2IgnitionSwitch),
            FbwEngine1State = _fbwEngine1State,
            FbwEngine2State = _fbwEngine2State,
            Battery1On = isIniBuildsA320Family
                ? _nativeBattery1On ?? raw.Battery1 != 0
                : isFlyByWireA320Neo
                    ? ResolveFbwBatteryState(
                        _fbwCommandedBattery1Auto,
                        _fbwBattery1AutoTyped,
                        _fbwBattery1Auto,
                        raw.Battery1)
                : isPmdg737
                    ? pmdg?.BatterySelector == 2 || raw.Battery1 != 0
                : raw.Battery1 != 0,
            Battery2On = isIniBuildsA320Family
                ? _nativeBattery2On ?? raw.Battery2 != 0
                : isFlyByWireA320Neo
                    ? ResolveFbwBatteryState(
                        _fbwCommandedBattery2Auto,
                        _fbwBattery2AutoTyped,
                        _fbwBattery2Auto,
                        raw.Battery2)
                : isPmdg737
                    ? pmdg?.BatterySelector == 2 || raw.Battery1 != 0
                : raw.Battery2 != 0,
            Battery1Voltage = raw.Battery1Voltage,
            Battery2Voltage = raw.Battery2Voltage,
            ExternalPowerAvailable = isFlyByWireA320Neo
                ? ResolveFbwAnyTrueState(
                    _fbwExternalPowerAvailableTyped,
                    _fbwExternalPowerAvailable,
                    raw.ExternalPowerAvailableUnindexed,
                    raw.ExternalPowerAvailable)
                : isPmdg737
                    ? pmdg?.GroundPowerAvailable == true || raw.ExternalPowerAvailable != 0
                : raw.ExternalPowerAvailable != 0,
            ExternalPowerOn = isFlyByWireA320Neo
                ? ResolveFbwAnyTrueState(
                    _fbwExternalPowerOnTyped,
                    _fbwExternalPowerOn,
                    raw.ExternalPowerOnUnindexed,
                    raw.ExternalPowerOn)
                : isPmdg737
                    ? pmdg?.GroundPowerOn == true || raw.ExternalPowerOn != 0
                : raw.ExternalPowerOn != 0,
            ExternalPowerAvailableUnindexed = raw.ExternalPowerAvailableUnindexed != 0,
            ExternalPowerOnUnindexed = raw.ExternalPowerOnUnindexed != 0,
            ParkingBrakeSet = isFlyByWireA320Neo
                ? _fbwParkingBrake == true
                : isPmdg737 && pmdg != null
                    ? pmdg.ParkingBrakeAnnunciated || raw.ParkingBrake != 0
                : raw.ParkingBrake != 0,
            BeaconOn = isPmdg737 && pmdg != null
                ? pmdg.AntiCollisionOn
                : raw.Beacon != 0,
            NavigationLightsOn = raw.NavigationLights != 0,
            LogoLightsOn = isPmdg737 && pmdg != null
                ? pmdg.LogoLightOn
                : raw.LogoLights != 0,
            NavLogoSelectorPosition = isFlyByWireA320Neo
                ? ResolveFbwNavLogoSelectorPosition(_fbwNavLogoSelectorTyped, _fbwNavLogoSelector)
                : isPmdg737 && pmdg != null
                    ? pmdg.LogoLightOn ? 0 : 2
                : _nativeNavLogoSelectorPosition,
            ApuRpmPercent = raw.ApuRpm,
            ApuStarterPercent = raw.ApuStarter,
            ApuMasterSwitchOn = isFlyByWireA320Neo
                ? _fbwApuMasterSwitch == true
                : isPmdg737 && pmdg != null
                    ? pmdg.ApuSelector >= 1
                : _nativeApuMasterSwitch.HasValue
                    ? _nativeApuMasterSwitch.Value != 0
                    : raw.ApuMasterSwitch != 0,
            ApuAvailable = isFlyByWireA320Neo
                ? _fbwApuStartAvailable == true
                : isPmdg737
                    ? raw.ApuRpm >= 95 || raw.ApuGeneratorActive != 0
                : _nativeApuAvailable.HasValue && _nativeApuAvailable.Value != 0,
            ApuStartButtonOn = isFlyByWireA320Neo
                ? _fbwApuStartButton == true || _fbwApuStartAvailable == true
                : isPmdg737 && pmdg != null
                    ? pmdg.ApuSelector == 2 || raw.ApuStarter > 0
                : _nativeApuStartButton.HasValue && _nativeApuStartButton.Value != 0,
            ApuBleedOn = isFlyByWireA320Neo
                ? _fbwApuBleedButton == true
                : isPmdg737 && pmdg != null
                    ? pmdg.ApuBleedOn
                : _nativeApuBleedButton.HasValue && _nativeApuBleedButton.Value != 0,
            ApuFlapPercent = _nativeApuFlapPercent ?? 0,
            ApuGeneratorActive = raw.ApuGeneratorActive != 0,
            ApuGeneratorSwitchOn = _nativeApuGeneratorOn.HasValue
                                   && !isPmdg737
                                   ? _nativeApuGeneratorOn.Value != 0
                                   : isPmdg737 && pmdg != null
                                       ? pmdg.ApuGen1On && pmdg.ApuGen2On
                                       : raw.ApuGeneratorSwitch != 0,
            ApuVolts = raw.ApuVolts,
            FuelPumpsConfigured = isFlyByWireA320Neo
                ? raw.FuelPump2 != 0
                  && raw.FbwFuelPump5 != 0
                  && raw.FbwFuelValve9 != 0
                  && raw.FbwFuelValve10 != 0
                  && raw.FuelPump3 != 0
                  && raw.FbwFuelPump6 != 0
                : isPmdg737 && pmdg != null
                    ? pmdg.LeftAftFuelPump
                      && pmdg.LeftForwardFuelPump
                      && pmdg.RightForwardFuelPump
                      && pmdg.RightAftFuelPump
                : (_nativeFuelPump1 ?? (float)raw.FuelPump1) != 0
                  && (_nativeFuelPump2 ?? (float)raw.FuelPump2) != 0
                  && (_nativeFuelPump3 ?? (float)raw.FuelPump3) != 0
                  && (_nativeFuelPump4 ?? (float)raw.FuelPump4) != 0
                  && (_nativeFuelPump5 ?? 0) != 0
                  && (_nativeFuelPump6 ?? 0) != 0,
            FuelPump1State = isFlyByWireA320Neo ? raw.FuelPump2 : isPmdg737 && pmdg != null ? (pmdg.LeftAftFuelPump ? 1 : 0) : _nativeFuelPump1 ?? raw.FuelPump1,
            FuelPump2State = isFlyByWireA320Neo ? raw.FbwFuelPump5 : isPmdg737 && pmdg != null ? (pmdg.LeftForwardFuelPump ? 1 : 0) : _nativeFuelPump2 ?? raw.FuelPump2,
            FuelPump3State = isFlyByWireA320Neo ? raw.FbwFuelValve9 : isPmdg737 && pmdg != null ? (pmdg.RightForwardFuelPump ? 1 : 0) : _nativeFuelPump3 ?? raw.FuelPump3,
            FuelPump4State = isFlyByWireA320Neo ? raw.FbwFuelValve10 : isPmdg737 && pmdg != null ? (pmdg.RightAftFuelPump ? 1 : 0) : _nativeFuelPump4 ?? raw.FuelPump4,
            FuelPump5State = isFlyByWireA320Neo ? raw.FuelPump3 : isPmdg737 && pmdg != null ? (pmdg.LeftCenterFuelPump ? 1 : 0) : _nativeFuelPump5 ?? 0,
            FuelPump6State = isFlyByWireA320Neo ? raw.FbwFuelPump6 : isPmdg737 && pmdg != null ? (pmdg.RightCenterFuelPump ? 1 : 0) : _nativeFuelPump6 ?? 0,
            AltitudeAboveGroundFeet = raw.AltitudeAboveGround,
            IndicatedAltitudeFeet = raw.IndicatedAltitude,
            TransitionAltitudeFeet = _settings.TransitionAltitudeFeet,
            CaptainAltimeterStandard = raw.CaptainBaroStandard != 0,
            FirstOfficerAltimeterStandard = raw.FirstOfficerBaroStandard != 0,
            IndicatedAirspeedKnots = raw.IndicatedAirspeed,
            TakeoffV1SpeedKnots = _settings.TakeoffV1SpeedKnots,
            TakeoffRotateSpeedKnots = _settings.TakeoffRotateSpeedKnots,
            ApproachDistanceToTouchdownNm = approachDistance.DistanceNm,
            ApproachDistanceSource = approachDistance.Source,
            ApproachFlaps1DistanceNm = _settings.ApproachFlaps1DistanceNm,
            ApproachFlaps1AltitudeFeet = _settings.ApproachFlaps1AltitudeFeet,
            ApproachFlaps1SpeedKnots = _settings.ApproachFlaps1SpeedKnots,
            ApproachFlaps2DistanceNm = _settings.ApproachFlaps2DistanceNm,
            ApproachFlaps2AltitudeAglFeet = _settings.ApproachFlaps2AltitudeAglFeet,
            ApproachFlaps2SpeedKnots = _settings.ApproachFlaps2SpeedKnots,
            ApproachGearDistanceNm = _settings.ApproachGearDistanceNm,
            ApproachGearAltitudeAglFeet = _settings.ApproachGearAltitudeAglFeet,
            ApproachGearSpeedKnots = _settings.ApproachGearSpeedKnots,
            ApproachLandingConfigDistanceNm = _settings.ApproachLandingConfigDistanceNm,
            ApproachLandingConfigAltitudeAglFeet =
                _settings.ApproachLandingConfigAltitudeAglFeet,
            ApproachLandingConfigSpeedKnots =
                _settings.ApproachLandingConfigSpeedKnots,
            VerticalSpeedFeetPerMinute = raw.VerticalSpeed,
            GForce = raw.GForce,
            RadioHeightFeet = raw.RadioHeight,
            DecisionHeightFeet = raw.DecisionHeight,
            Engine1ReverseEngaged = raw.Engine1Reverse != 0,
            Engine2ReverseEngaged = raw.Engine2Reverse != 0,
            AutobrakesActive = raw.AutobrakesActive != 0,
            LeftSpoilerPositionPercent = raw.LeftSpoilerPosition,
            RightSpoilerPositionPercent = raw.RightSpoilerPosition,
            FlapsHandleIndex = raw.FlapsHandleIndex,
            LeftFlapPositionPercent = raw.LeftFlapPosition,
            RightFlapPositionPercent = raw.RightFlapPosition,
            GearHandleDown = isFlyByWireA320Neo
                ? raw.GearHandle != 0
                : isPmdg737 && pmdg != null
                    ? pmdg.GearLever == 2
                : _nativeGearHandlePosition.HasValue
                    ? _nativeGearHandlePosition.Value >= 0.5
                    : raw.GearHandle != 0,
            PitchDegrees = raw.PitchDegrees,
            AutopilotMasterOn = raw.AutopilotMaster != 0,
            Adirs1SelectorState = isFlyByWireA320Neo
                ? ResolveFbwSelectorState(_fbwCommandedAdirs1Selector, _fbwCommandedAdirs1SelectorUtc, _fbwAdirs1SelectorTyped, _fbwAdirs1Selector)
                : isPmdg737 && pmdg != null
                    ? pmdg.IrsLeftMode
                : _nativeAdirs1State ?? 0,
            Adirs2SelectorState = isFlyByWireA320Neo
                ? ResolveFbwSelectorState(_fbwCommandedAdirs2Selector, _fbwCommandedAdirs2SelectorUtc, _fbwAdirs2SelectorTyped, _fbwAdirs2Selector)
                : isPmdg737 && pmdg != null
                    ? pmdg.IrsRightMode
                : _nativeAdirs2State ?? 0,
            Adirs3SelectorState = isFlyByWireA320Neo
                ? ResolveFbwSelectorState(_fbwCommandedAdirs3Selector, _fbwCommandedAdirs3SelectorUtc, _fbwAdirs3SelectorTyped, _fbwAdirs3Selector)
                : isPmdg737
                    ? 2
                : _nativeAdirs3State ?? 0,
            AdirsOnBattery = isFlyByWireA320Neo
                ? _fbwAdirsOnBattery == true
                : _nativeAdirsOnBattery.HasValue && _nativeAdirsOnBattery.Value != 0,
            CrewOxygenOn = isFlyByWireA320Neo
                ? ResolveFbwInvertedBoolState(_fbwCommandedCrewOxygen, _fbwCrewOxygenTyped, _fbwCrewOxygen)
                : _nativeCrewOxygen.HasValue && _nativeCrewOxygen.Value != 0,
            StrobeSelectorPosition = isFlyByWireA320Neo
                ? ResolveFbwStrobeSelectorPosition(_fbwStrobeAuto, _fbwStrobeLightState)
                : isPmdg737 && pmdg != null
                    ? pmdg.PositionStrobeSelector == 2 ? 0 : pmdg.PositionStrobeSelector == 0 ? 1 : 2
                : _nativeStrobeSelector,
            ApuFireTestActive = isPmdg737 && pmdg != null
                ? pmdg.FireDetectionTestSwitch == 2 || pmdg.FireExtinguisherTestApu
                : _nativeApuFireTest.HasValue && _nativeApuFireTest.Value != 0,
            ApuFireWarningLit = isPmdg737 && pmdg != null
                ? pmdg.FireExtinguisherTestApu
                : _nativeApuFireWarningLit.HasValue && _nativeApuFireWarningLit.Value != 0,
            ApuFireSoundActive = _nativeApuFireSound.HasValue && _nativeApuFireSound.Value != 0,
            Engine1FireTestActive = isPmdg737 && pmdg != null
                ? pmdg.FireDetectionTestSwitch == 2 || pmdg.FireExtinguisherTestLeft
                : _nativeEngine1FireTest.HasValue && _nativeEngine1FireTest.Value != 0,
            Engine1FireWarningLit = isPmdg737 && pmdg != null
                ? pmdg.FireExtinguisherTestLeft
                : _nativeEngine1FireWarningLit.HasValue && _nativeEngine1FireWarningLit.Value != 0,
            Engine1FireSoundActive = _nativeEngine1FireSound.HasValue && _nativeEngine1FireSound.Value != 0,
            Engine2FireTestActive = isPmdg737 && pmdg != null
                ? pmdg.FireDetectionTestSwitch == 2 || pmdg.FireExtinguisherTestRight
                : _nativeEngine2FireTest.HasValue && _nativeEngine2FireTest.Value != 0,
            Engine2FireWarningLit = isPmdg737 && pmdg != null
                ? pmdg.FireExtinguisherTestRight
                : _nativeEngine2FireWarningLit.HasValue && _nativeEngine2FireWarningLit.Value != 0,
            Engine2FireSoundActive = _nativeEngine2FireSound.HasValue && _nativeEngine2FireSound.Value != 0,
            SeatbeltSelectorPosition = isFlyByWireA320Neo
                ? ResolveFbwSeatbeltSelectorPosition(
                    _fbwSeatbeltSelector,
                    raw.CabinSeatbeltsAlert != 0)
                : isPmdg737 && pmdg != null
                    ? pmdg.FastenBeltsSelector
                : _nativeSeatbeltSelector,
            SeatbeltSignsOn = isFlyByWireA320Neo
                ? raw.CabinSeatbeltsAlert != 0
                : isPmdg737 && pmdg != null
                    ? pmdg.FastenBeltsSelector == 2
                : _nativeSeatbeltSignsOn.HasValue && _nativeSeatbeltSignsOn.Value != 0,
            NoSmokingSelectorPosition = isFlyByWireA320Neo
                ? _fbwNoSmokingSelector
                : isPmdg737 && pmdg != null
                    ? pmdg.NoSmokingSelector
                : _nativeNoSmokingSelector,
            NoSmokingSignsOn = isFlyByWireA320Neo
                ? _fbwNoSmokingSelector.HasValue && Math.Abs(_fbwNoSmokingSelector.Value) < 0.1
                : isPmdg737 && pmdg != null
                    ? pmdg.NoSmokingSelector == 2
                : _nativeNoSmokingSignsOn.HasValue && _nativeNoSmokingSignsOn.Value != 0,
            EmergencyExitSelectorPosition = isFlyByWireA320Neo
                ? _fbwEmergencyExitSelector
                : isPmdg737 && pmdg != null
                    ? pmdg.EmergencyExitLights
                : _nativeEmergencyExitSelector,
            GroundSpoilersArmed = isFlyByWireA320Neo
                ? ResolveFbwSpoilersArmedState(
                    _fbwCommandedSpoilersArmed,
                    _fbwCommandedSpoilersArmedUtc,
                    _fbwSpoilersArmed,
                    raw.SpoilersArmed)
                : isPmdg737 && pmdg != null
                    ? pmdg.SpeedbrakeArmed
                : _nativeSpoilersArmed.HasValue
                    ? _nativeSpoilersArmed.Value != 0
                    : raw.SpoilersArmed != 0,
            AutobrakeLevel = isFlyByWireA320Neo
                ? ResolveFbwAutobrakeLevel(
                    _fbwCommandedAutobrakeLevel,
                    _fbwCommandedAutobrakeLevelUtc,
                    _fbwAutobrakeLevel)
                : isPmdg737 && pmdg != null
                    ? pmdg.AutobrakeSelector
                : _nativeAutobrakeLevel,
            WeatherRadarPwsSelectorPosition = isFlyByWireA320Neo
                ? ResolveFbwWeatherRadarPwsSelector(
                    _fbwCommandedWeatherRadarPwsSelector,
                    _fbwCommandedWeatherRadarPwsSelectorUtc,
                    _fbwWeatherRadarPwsSelector)
                : _nativeWeatherRadarPwsSelector,
            NoseLightSelectorPosition = isFlyByWireA320Neo
                ? ResolveFbwNoseLightSelectorPosition(
                    _fbwCommandedNoseLightSelector,
                    _fbwCommandedNoseLightSelectorUtc,
                    raw.FbwNoseTakeoffLightCircuit,
                    raw.FbwNoseTaxiLightCircuit,
                    raw.TaxiLight)
                : isPmdg737 && pmdg != null
                    ? pmdg.TaxiLightOn ? 1 : 2
                : _nativeNoseLightSelector,
            LeftLandingLightSelectorPosition = isFlyByWireA320Neo
                ? ResolveFbwLandingLightSelectorPosition(
                    _fbwCommandedLandingLightSelector,
                    _fbwCommandedLandingLightSelectorUtc,
                    raw.FbwLeftLandingLightCircuit)
                : isPmdg737 && pmdg != null
                    ? pmdg.LeftLandingLight == 2 ? 0 : 2
                : _nativeLeftLandingLightSelector,
            RightLandingLightSelectorPosition = isFlyByWireA320Neo
                ? ResolveFbwLandingLightSelectorPosition(
                    _fbwCommandedLandingLightSelector,
                    _fbwCommandedLandingLightSelectorUtc,
                    raw.FbwRightLandingLightCircuit)
                : isPmdg737 && pmdg != null
                    ? pmdg.RightLandingLight == 2 ? 0 : 2
                : _nativeRightLandingLightSelector,
            TcasAltitudeReportingOn = isFlyByWireA320Neo
                ? ResolveFbwTcasAltitudeReporting(
                    _fbwCommandedTcasAltitudeReporting,
                    _fbwCommandedTcasAltitudeReportingUtc,
                    _fbwTcasAltitudeReporting)
                : _nativeTcasAltitudeReporting.HasValue
                    ? _nativeTcasAltitudeReporting.Value == 0
                    : null,
            TransponderAtcState = _nativeTransponderAtcState,
            TcasMode = isFlyByWireA320Neo
                ? ResolveFbwSelectorWithCommand(
                    _fbwCommandedTcasMode,
                    _fbwCommandedTcasModeUtc,
                    _fbwTcasMode)
                : isPmdg737 && pmdg != null
                    ? pmdg.TransponderMode
                : _nativeTcasMode,
            TransponderModeSelectorPosition = isFlyByWireA320Neo
                ? _fbwTransponderMode
                : isPmdg737 && pmdg != null
                    ? pmdg.TransponderMode
                : _nativeTransponderStandby,
            TransponderStandby = isPmdg737 && pmdg != null
                ? pmdg.TransponderMode == 0
                : _nativeTransponderStandby.HasValue
                  && _nativeTransponderStandby.Value != 0,
            AtcClearedIfr = raw.AtcClearedIfr != 0,
            Exits = new[]
            {
                new AircraftExitState(1, raw.Exit1Type, raw.Exit1Open, raw.Exit1PosX, raw.Exit1PosY, raw.Exit1PosZ),
                new AircraftExitState(2, raw.Exit2Type, raw.Exit2Open, raw.Exit2PosX, raw.Exit2PosY, raw.Exit2PosZ),
                new AircraftExitState(3, raw.Exit3Type, raw.Exit3Open, raw.Exit3PosX, raw.Exit3PosY, raw.Exit3PosZ),
                new AircraftExitState(4, raw.Exit4Type, raw.Exit4Open, raw.Exit4PosX, raw.Exit4PosY, raw.Exit4PosZ),
                new AircraftExitState(5, raw.Exit5Type, raw.Exit5Open, raw.Exit5PosX, raw.Exit5PosY, raw.Exit5PosZ),
                new AircraftExitState(6, raw.Exit6Type, raw.Exit6Open, raw.Exit6PosX, raw.Exit6PosY, raw.Exit6PosZ),
                new AircraftExitState(7, raw.Exit7Type, raw.Exit7Open, raw.Exit7PosX, raw.Exit7PosY, raw.Exit7PosZ),
                new AircraftExitState(8, raw.Exit8Type, raw.Exit8Open, raw.Exit8PosX, raw.Exit8PosY, raw.Exit8PosZ)
            },
            ApuFireTestCompleted = _apuFireTestCompleted,
            Engine1FireTestCompleted = _engine1FireTestCompleted,
            Engine2FireTestCompleted = _engine2FireTestCompleted
        };
        UpdateTelemetrySanity(_state);
        UpdateCockpitDisplayReadiness(_state);
        _flightTelemetryStore.Record(_state, DateTime.UtcNow);

        VerifyPendingProcedure();
        VerifyPendingFireTest();
        TryRestoreProcedureSession();
        _procedureRunner.Update(_state);
        UpdateCruiseSeatbeltMonitoring();
        if (_procedureRunner.Status == ProcedureStatus.Completed
            && _procedureRunner.Definition != null)
        {
            _completedProcedureIds.Add(_procedureRunner.Definition.Id);
        }
        UpdateDashboard();
        FinishProcedureOneShotIfTerminal();

        if (_initialStateReceived)
        {
            return;
        }

        _initialStateReceived = true;
        Console.WriteLine($"Aircraft: {_state.Title}");
        AppendDashboardLog($"Aircraft detected: {_state.Title}");
        if (!_state.IsSupportedAircraft)
        {
            Console.Error.WriteLine("Warning: this build supports the iniBuilds A320neo V2, iniBuilds A321LR, FlyByWire A32NX, and PMDG 737-800.");
        }

        if (_oneShotCommand == null)
        {
            PrintHelp();
            Console.Write("> ");
        }
        TryExecuteOneShotCommand();
    }

    private static bool ResolveFbwBatteryState(
        bool? commandedPushbuttonAuto,
        bool? typedPushbuttonAuto,
        bool? untypedPushbuttonAuto,
        double genericMasterBattery)
    {
        if (commandedPushbuttonAuto.HasValue)
        {
            return commandedPushbuttonAuto.Value;
        }

        if (typedPushbuttonAuto.HasValue)
        {
            return typedPushbuttonAuto.Value;
        }

        if (untypedPushbuttonAuto.HasValue)
        {
            return untypedPushbuttonAuto.Value;
        }

        return genericMasterBattery != 0;
    }

    private static bool ResolveFbwBoolState(
        bool? typedValue,
        bool? untypedValue,
        double genericValue)
    {
        if (typedValue.HasValue)
        {
            return typedValue.Value;
        }

        if (untypedValue.HasValue)
        {
            return untypedValue.Value;
        }

        return genericValue != 0;
    }

    private static bool ResolveFbwBoolState(
        bool? commandedValue,
        bool? typedValue,
        bool? untypedValue)
    {
        if (commandedValue.HasValue)
        {
            return commandedValue.Value;
        }

        if (typedValue.HasValue)
        {
            return typedValue.Value;
        }

        return untypedValue == true;
    }

    private static bool ResolveFbwInvertedBoolState(
        bool? commandedValue,
        bool? typedValue,
        bool? untypedValue)
    {
        if (commandedValue.HasValue)
        {
            return commandedValue.Value;
        }

        if (typedValue.HasValue)
        {
            return !typedValue.Value;
        }

        if (untypedValue.HasValue)
        {
            return !untypedValue.Value;
        }

        return false;
    }

    private static bool ResolveFbwAnyTrueState(
        bool? typedValue,
        bool? untypedValue,
        double genericUnindexedValue,
        double genericIndexedValue)
    {
        return typedValue == true
               || untypedValue == true
               || genericUnindexedValue != 0
               || genericIndexedValue != 0;
    }

    private static double ResolveFbwSelectorState(
        float? commandedValue,
        DateTime? commandedUtc,
        float? typedValue,
        float? untypedValue)
    {
        if (commandedValue.HasValue
            && commandedUtc.HasValue
            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromMinutes(2))
        {
            return commandedValue.Value;
        }

        if (typedValue.HasValue)
        {
            return typedValue.Value;
        }

        if (untypedValue.HasValue)
        {
            return untypedValue.Value;
        }

        return commandedValue ?? 0;
    }

    private static double? ResolveFbwNavLogoSelectorPosition(float? typedValue, float? untypedValue)
    {
        var fbwPosition = typedValue ?? untypedValue;
        if (!fbwPosition.HasValue)
        {
            return null;
        }

        // FBW: 0=OFF, 1=SYS 1, 2=SYS 2.
        // App/iniBuilds flow semantics: 2=OFF, 1=SYS 1, 0=SYS 2.
        return (int)Math.Round(fbwPosition.Value) switch
        {
            0 => 2,
            1 => 1,
            2 => 0,
            _ => fbwPosition.Value
        };
    }

    private static bool ResolveFbwSpoilersArmedState(
        bool? commandedValue,
        DateTime? commandedUtc,
        bool? fbwLVarValue,
        double genericSpoilersArmed)
    {
        if (commandedValue.HasValue
            && commandedUtc.HasValue
            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromMinutes(2))
        {
            return commandedValue.Value;
        }

        if (genericSpoilersArmed != 0)
        {
            return true;
        }

        return fbwLVarValue == true;
    }

    private static double? ResolveFbwAutobrakeLevel(
        float? commandedValue,
        DateTime? commandedUtc,
        float? fbwLVarValue)
    {
        if (commandedValue.HasValue
            && commandedUtc.HasValue
            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromMinutes(2))
        {
            return commandedValue.Value;
        }

        return fbwLVarValue;
    }

    private static double? ResolveFbwWeatherRadarPwsSelector(
        float? commandedValue,
        DateTime? commandedUtc,
        float? fbwLVarValue)
    {
        if (commandedValue.HasValue
            && commandedUtc.HasValue
            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromMinutes(2))
        {
            return commandedValue.Value;
        }

        return fbwLVarValue;
    }

    private static double ResolveFbwNoseLightSelectorPosition(
        float? commandedValue,
        DateTime? commandedUtc,
        double takeoffCircuitOn,
        double taxiCircuitOn,
        double taxiLightOn)
    {
        if (commandedValue.HasValue
            && commandedUtc.HasValue
            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromMinutes(2))
        {
            return commandedValue.Value;
        }

        if (takeoffCircuitOn != 0)
        {
            return 0;
        }

        if (taxiCircuitOn != 0 || taxiLightOn != 0)
        {
            return 1;
        }

        return 2;
    }

    private static bool? ResolveFbwTcasAltitudeReporting(
        bool? commandedValue,
        DateTime? commandedUtc,
        bool? fbwLVarValue)
    {
        if (commandedValue.HasValue
            && commandedUtc.HasValue
            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromMinutes(2))
        {
            return commandedValue.Value;
        }

        return fbwLVarValue;
    }

    private static double? ResolveFbwSelectorWithCommand(
        float? commandedValue,
        DateTime? commandedUtc,
        float? fbwLVarValue)
    {
        if (commandedValue.HasValue
            && commandedUtc.HasValue
            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromMinutes(2))
        {
            return commandedValue.Value;
        }

        return fbwLVarValue;
    }

    private static double? ResolveFbwSeatbeltSelectorPosition(
        float? fbwLVarValue,
        bool seatbeltSignsOn)
    {
        if (!fbwLVarValue.HasValue)
        {
            return null;
        }

        // FBW exposes the seatbelt switch as a two-position LVar:
        // 1 = AUTO, 0 = manual. In manual, the active cabin alert state tells
        // us whether that manual state is ON or OFF in our three-state flow
        // model.
        return Math.Abs(fbwLVarValue.Value - 1) < 0.1
            ? 1
            : seatbeltSignsOn
                ? 0
                : 2;
    }

    private static double ResolveFbwLandingLightSelectorPosition(
        float? commandedValue,
        DateTime? commandedUtc,
        double circuitOn)
    {
        if (commandedValue.HasValue
            && commandedUtc.HasValue
            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromMinutes(2))
        {
            return commandedValue.Value;
        }

        return circuitOn != 0 ? 0 : 2;
    }

    private static double? ResolveFbwStrobeSelectorPosition(bool? autoValue, float? lightState)
    {
        if (autoValue == true)
        {
            return 1;
        }

        if (!lightState.HasValue)
        {
            return null;
        }

        // The FBW/Asobo strobe switch exposes the same visible order:
        // 0=ON, 1=AUTO, 2=OFF. Prefer the explicit AUTO flag when present.
        return (int)Math.Round(lightState.Value);
    }

    private static double? ResolveEngineModeSelectorPosition(
        double engine1IgnitionSwitch,
        double engine2IgnitionSwitch)
    {
        if (Math.Abs(engine1IgnitionSwitch - engine2IgnitionSwitch) > 0.1)
        {
            return null;
        }

        var position = (int)Math.Round(engine1IgnitionSwitch);
        return position >= 0 && position <= 2
            ? position
            : null;
    }

    private static void LogChangedVoltage(string label, double value, ref double? previousValue)
    {
        if (!previousValue.HasValue || Math.Abs(previousValue.Value - value) >= 0.1)
        {
            AppLog.Write($"{label} changed to {value:F1} V.");
            previousValue = value;
        }
    }

    private static void SetLoggedBool(ref bool? target, float value, string label)
    {
        var boolValue = value != 0;
        if (!target.HasValue || target.Value != boolValue)
        {
            AppLog.Write($"{label} changed to {value:F0}.");
        }
        target = boolValue;
    }

    private static void SetLoggedFloat(ref float? target, float value, string label)
    {
        if (!target.HasValue || Math.Abs(target.Value - value) >= 0.01f)
        {
            AppLog.Write($"{label} changed to {value:F0}.");
        }
        target = value;
    }

    private static PmdgNg3State ParsePmdgNg3State(byte[] data)
    {
        byte ByteAt(int offset) =>
            data.Length > offset ? data[offset] : (byte)0;

        bool BoolAt(int offset) => ByteAt(offset) != 0;

        return new PmdgNg3State
        {
            IrsLeftMode = ByteAt(11),
            IrsRightMode = ByteAt(12),
            Engine1StartValveOpen = BoolAt(44),
            Engine2StartValveOpen = BoolAt(45),
            LeftForwardFuelPump = BoolAt(89),
            RightForwardFuelPump = BoolAt(90),
            LeftAftFuelPump = BoolAt(91),
            RightAftFuelPump = BoolAt(92),
            LeftCenterFuelPump = BoolAt(93),
            RightCenterFuelPump = BoolAt(94),
            BatterySelector = ByteAt(133),
            GroundPowerAvailable = BoolAt(142),
            GroundPowerOn = BoolAt(143),
            ApuGen1On = BoolAt(147),
            ApuGen2On = BoolAt(148),
            EmergencyExitLights = ByteAt(217),
            NoSmokingSelector = ByteAt(218),
            FastenBeltsSelector = ByteAt(219),
            ApuBleedOn = BoolAt(284),
            LeftLandingLight = ByteAt(372),
            RightLandingLight = ByteAt(373),
            TaxiLightOn = BoolAt(378),
            ApuSelector = ByteAt(379),
            LogoLightOn = BoolAt(383),
            PositionStrobeSelector = ByteAt(384),
            AntiCollisionOn = BoolAt(385),
            SpeedbrakeArmed = BoolAt(477),
            AutobrakeSelector = ByteAt(487),
            GearLever = ByteAt(506),
            ParkingBrakeAnnunciated = BoolAt(574),
            FireDetectionTestSwitch = ByteAt(579),
            FireExtinguisherTestLeft = BoolAt(593),
            FireExtinguisherTestRight = BoolAt(594),
            FireExtinguisherTestApu = BoolAt(595),
            TransponderMode = ByteAt(612),
            TakeoffFlaps = ByteAt(620),
            V1 = ByteAt(621),
            Vr = ByteAt(622),
            LandingFlaps = ByteAt(624),
            FmcPerfInputComplete = BoolAt(634),
            GroundConnectionAvailable = BoolAt(658)
        };
    }

    private void SendPmdgNg3Control(uint sdkEventOffset, uint parameter)
    {
        if (_simConnect == null)
        {
            return;
        }

        _simConnect.SetClientData(
            ClientDataArea.PmdgNg3Control,
            ClientDataDefinition.PmdgNg3Control,
            SIMCONNECT_CLIENT_DATA_SET_FLAG.DEFAULT,
            0,
            new PmdgNg3Control
            {
                Event = ThirdPartyEventIdMin + sdkEventOffset,
                Parameter = parameter
            });
    }

    private static (double? DistanceNm, string Source) ResolveApproachDistance(
        AircraftData raw)
    {
        if (raw.AtcRunwaySelected != 0)
        {
            var runwayDistance = MetersToNauticalMiles(
                raw.AtcRunwayStartDistanceMeters);
            if (runwayDistance.HasValue)
            {
                return (runwayDistance, "ATC runway");
            }
        }

        var targetDistance = MetersToNauticalMiles(raw.GpsTargetDistanceMeters);
        if (targetDistance.HasValue)
        {
            return (targetDistance, "GPS target");
        }

        var waypointDistance = MetersToNauticalMiles(raw.GpsWaypointDistanceMeters);
        return waypointDistance.HasValue
            ? (waypointDistance, "GPS waypoint")
            : (null, "");
    }

    private static double? MetersToNauticalMiles(double meters)
    {
        if (double.IsNaN(meters)
            || double.IsInfinity(meters)
            || meters <= 0
            || meters > 100 * MetersPerNauticalMile)
        {
            return null;
        }

        return meters / MetersPerNauticalMile;
    }

    private void TryExecuteOneShotCommand()
    {
        if (_oneShotCommand == null
            || _oneShotCommandExecuted
            || _state == null)
        {
            return;
        }

        var requiresAircraftAdapter =
            _oneShotCommand.StartsWith("battery-", StringComparison.OrdinalIgnoreCase)
            || _oneShotCommand.StartsWith("nav-logo ", StringComparison.OrdinalIgnoreCase)
            || _oneShotCommand.StartsWith("adirs-", StringComparison.OrdinalIgnoreCase)
            || _oneShotCommand.StartsWith("crew-oxygen ", StringComparison.OrdinalIgnoreCase)
            || _oneShotCommand.StartsWith("strobe ", StringComparison.OrdinalIgnoreCase)
            || _oneShotCommand.StartsWith("fire-test ", StringComparison.OrdinalIgnoreCase)
            || _oneShotCommand.StartsWith("apu-", StringComparison.OrdinalIgnoreCase)
            || _oneShotCommand.StartsWith("seatbelts ", StringComparison.OrdinalIgnoreCase)
            || _oneShotCommand.StartsWith("no-smoking ", StringComparison.OrdinalIgnoreCase)
            || _oneShotCommand.StartsWith("emergency-exit ", StringComparison.OrdinalIgnoreCase)
            || _oneShotCommand.StartsWith("transponder ", StringComparison.OrdinalIgnoreCase)
            || _oneShotCommand.StartsWith("atc-system ", StringComparison.OrdinalIgnoreCase)
            || _oneShotCommand.StartsWith("tcas altitude-reporting ", StringComparison.OrdinalIgnoreCase)
            || _oneShotCommand.StartsWith("tcas traffic ", StringComparison.OrdinalIgnoreCase)
            || _oneShotCommand.StartsWith("wxr-pws ", StringComparison.OrdinalIgnoreCase)
            || _oneShotCommand.StartsWith("nose-light ", StringComparison.OrdinalIgnoreCase)
            || _oneShotCommand.StartsWith("landing-lights ", StringComparison.OrdinalIgnoreCase)
            || _oneShotCommand.StartsWith("tcas-mode ", StringComparison.OrdinalIgnoreCase);
        if (requiresAircraftAdapter && !_mobiFlightReady)
        {
            return;
        }
        var nativeStateReady = _oneShotCommand.ToLowerInvariant() switch
        {
            "status" => _mobiFlightRuntimeInitializedUtc.HasValue
                        && DateTime.UtcNow - _mobiFlightRuntimeInitializedUtc.Value
                        >= TimeSpan.FromSeconds(2),
            var command when command.StartsWith("battery-1 ") => _mobiFlightRuntimeReady,
            var command when command.StartsWith("battery-2 ") => _mobiFlightRuntimeReady,
            var command when command.StartsWith("nav-logo ") => _mobiFlightRuntimeReady,
            var command when command.StartsWith("apu-") =>
                _state?.IsFlyByWireA320Neo == true
                    ? _mobiFlightRuntimeReady
                    : _nativeApuAvailable.HasValue
                      && _nativeApuMasterSwitch.HasValue
                      && _nativeApuStartButton.HasValue
                      && _nativeApuBleedButton.HasValue
                      && _nativeApuGeneratorOn.HasValue
                      && _nativeApuFlapPercent.HasValue,
            var command when command.StartsWith("fuel-pumps ") =>
                _state?.IsFlyByWireA320Neo == true
                    || _nativeFuelPump1.HasValue
                && _nativeFuelPump2.HasValue
                && _nativeFuelPump3.HasValue
                && _nativeFuelPump4.HasValue
                && _nativeFuelPump5.HasValue
                && _nativeFuelPump6.HasValue,
            var command when command.StartsWith("adirs-1 ") => _nativeAdirs1State.HasValue,
            var command when command.StartsWith("adirs-2 ") => _nativeAdirs2State.HasValue,
            var command when command.StartsWith("adirs-3 ") => _nativeAdirs3State.HasValue,
            var command when command.StartsWith("crew-oxygen ") => true,
            var command when command.StartsWith("strobe ") =>
                _state?.IsFlyByWireA320Neo == true
                    ? _mobiFlightRuntimeReady
                    : _nativeStrobeSelector.HasValue,
            var command when command == "fire-test apu" =>
                _state?.IsFlyByWireA320Neo == true || _nativeApuFireTest.HasValue,
            var command when command == "fire-test engine-1" =>
                _state?.IsFlyByWireA320Neo == true || _nativeEngine1FireTest.HasValue,
            var command when command == "fire-test engine-2" =>
                _state?.IsFlyByWireA320Neo == true || _nativeEngine2FireTest.HasValue,
            var command when command.StartsWith("seatbelts ") =>
                _state?.IsFlyByWireA320Neo == true
                    ? _mobiFlightRuntimeReady
                    : _nativeSeatbeltSelector.HasValue,
            var command when command.StartsWith("no-smoking ") =>
                _state?.IsFlyByWireA320Neo == true
                    ? _mobiFlightRuntimeReady
                    : _nativeNoSmokingSelector.HasValue,
            var command when command.StartsWith("emergency-exit ") =>
                _state?.IsFlyByWireA320Neo == true
                    ? _mobiFlightRuntimeReady
                    : _nativeEmergencyExitSelector.HasValue,
            var command when command.StartsWith("transponder ") =>
                _state?.IsFlyByWireA320Neo == true
                    ? _mobiFlightRuntimeReady
                    : _nativeTransponderStandby.HasValue,
            var command when command.StartsWith("atc-system ") => _nativeTransponderAtcState.HasValue,
            var command when command.StartsWith("tcas altitude-reporting ") =>
                _state?.IsFlyByWireA320Neo == true
                    ? _mobiFlightRuntimeReady
                    : _nativeTcasAltitudeReporting.HasValue,
            var command when command.StartsWith("tcas traffic ") =>
                _state?.IsFlyByWireA320Neo == true
                    ? _mobiFlightRuntimeReady
                    : _nativeTcasMode.HasValue,
            var command when command.StartsWith("wxr-pws ") =>
                _state?.IsFlyByWireA320Neo == true
                    ? _mobiFlightRuntimeReady
                    : _nativeWeatherRadarPwsSelector.HasValue,
            var command when command.StartsWith("nose-light ") =>
                _state?.IsFlyByWireA320Neo == true
                    || _nativeNoseLightSelector.HasValue,
            var command when command.StartsWith("landing-lights ") =>
                _state?.IsFlyByWireA320Neo == true
                    || _nativeLeftLandingLightSelector.HasValue
                    && _nativeRightLandingLightSelector.HasValue,
            var command when command.StartsWith("tcas-mode ") => _nativeTransponderStandby.HasValue,
            _ => true
        };
        if (!nativeStateReady)
        {
            return;
        }
        _oneShotCommandExecuted = true;
        ExecuteCommand(_oneShotCommand);
    }

    private void StartConsoleReader()
    {
        var thread = new Thread(() =>
        {
            while (!IsDisposed)
            {
                var line = Console.ReadLine();
                if (line == null)
                {
                    return;
                }

                _commands.Enqueue(line);
            }
        })
        {
            IsBackground = true,
            Name = "Copilot console input"
        };
        thread.Start();
    }

    private void DrainCommands()
    {
        TryExecuteOneShotCommand();
        while (_commands.TryDequeue(out var command))
        {
            ExecuteCommand(command);
            if (_oneShotCommand == null && !IsDisposed)
            {
                Console.Write("> ");
            }
        }
    }

    private void ExecuteCommand(string command)
    {
        var normalized = command.Trim().ToLowerInvariant();
        if (_replayActive
            && !normalized.StartsWith("procedure ", StringComparison.Ordinal)
            && normalized is not "status"
                and not "checklist"
                and not "phase"
                and not "capabilities"
                and not "help")
        {
            AppendDashboardLog(
                $"Replay blocked cockpit command: {normalized}");
            FinishOneShot();
            return;
        }
        switch (normalized)
        {
            case "status":
                PrintStatus();
                FinishOneShot();
                break;
            case "fbw-bridge-status":
                PrintFbwBridgeStatus();
                FinishOneShot();
                break;
            case "checklist":
                PrintChecklist();
                FinishOneShot();
                break;
            case "phase":
                PrintPhase();
                FinishOneShot();
                break;
            case "capabilities":
                PrintCapabilities();
                FinishOneShot();
                break;
            case var value when value.StartsWith("procedure start ", StringComparison.Ordinal):
                StartProcedureById(normalized.Substring("procedure start ".Length));
                break;
            case "procedure status":
                PrintProcedureStatus();
                FinishOneShot();
                break;
            case "procedure confirm":
                ConfirmProcedureStep();
                break;
            case "procedure pause":
                _procedureRunner.Pause();
                FinishOneShot();
                break;
            case "procedure resume":
                ResumeProcedure();
                break;
            case "procedure cancel":
                CancelFuelPumpSequence();
                _procedureRunner.Cancel();
                FinishOneShot();
                break;
            case "procedure reset":
                ResetFlightProgress();
                break;
            case var value when value.StartsWith("debug jump ", StringComparison.Ordinal):
                DebugJumpToFlowById(normalized.Substring("debug jump ".Length));
                break;
            case "external-power on":
                SetExternalPower(true);
                break;
            case "external-power off":
                SetExternalPower(false);
                break;
            case "beacon on":
                SetBeacon(true);
                break;
            case "beacon off":
                SetBeacon(false);
                break;
            case "nav-logo off":
                SetNavLogoSelector(2);
                break;
            case "nav-logo 2":
                SetNavLogoSelector(0);
                break;
            case "battery-1 on":
                SetBattery(1, true);
                break;
            case "battery-1 off":
                SetBattery(1, false);
                break;
            case "battery-2 on":
                SetBattery(2, true);
                break;
            case "battery-2 off":
                SetBattery(2, false);
                break;
            case "apu-master on":
                SetApuMaster(true);
                break;
            case "apu-master off":
                SetApuMaster(false);
                break;
            case "apu-start on":
                SetApuStart(true);
                break;
            case "apu-start off":
                SetApuStart(false);
                break;
            case "apu-bleed on":
                SetApuBleed(true);
                break;
            case "apu-bleed off":
                SetApuBleed(false);
                break;
            case "apu-generator on":
                SetApuGenerator(true);
                break;
            case "apu-generator off":
                SetApuGenerator(false);
                break;
            case "fuel-pumps on":
                SetFuelPumps(true);
                break;
            case "fuel-pumps off":
                SetFuelPumps(false);
                break;
            case "adirs-1 nav":
                SetAdirsSelector(1, 1);
                break;
            case "adirs-2 nav":
                SetAdirsSelector(2, 1);
                break;
            case "adirs-3 nav":
                SetAdirsSelector(3, 1);
                break;
            case "adirs-1 off":
                SetAdirsSelector(1, 0);
                break;
            case "adirs-2 off":
                SetAdirsSelector(2, 0);
                break;
            case "adirs-3 off":
                SetAdirsSelector(3, 0);
                break;
            case "crew-oxygen on":
                SetCrewOxygen(true);
                break;
            case "crew-oxygen off":
                SetCrewOxygen(false);
                break;
            case "strobe on":
                SetStrobeSelector(0);
                break;
            case "strobe auto":
                SetStrobeSelector(1);
                break;
            case "strobe off":
                SetStrobeSelector(2);
                break;
            case "fire-test apu":
                StartFireTest(FireTestSystem.Apu);
                break;
            case "fire-test engine-1":
                StartFireTest(FireTestSystem.Engine1);
                break;
            case "fire-test engine-2":
                StartFireTest(FireTestSystem.Engine2);
                break;
            case "seatbelts on":
                SetSignSelector(SignSelector.Seatbelts, 0);
                break;
            case "seatbelts auto":
                SetSignSelector(SignSelector.Seatbelts, 1);
                break;
            case "seatbelts off":
                SetSignSelector(SignSelector.Seatbelts, 2);
                break;
            case "no-smoking on":
                SetSignSelector(SignSelector.NoSmoking, 0);
                break;
            case "no-smoking auto":
                SetSignSelector(SignSelector.NoSmoking, 1);
                break;
            case "no-smoking off":
                SetSignSelector(SignSelector.NoSmoking, 2);
                break;
            case "emergency-exit on":
                SetSignSelector(SignSelector.EmergencyExit, 0);
                break;
            case "emergency-exit arm":
                SetSignSelector(SignSelector.EmergencyExit, 1);
                break;
            case "emergency-exit off":
                SetSignSelector(SignSelector.EmergencyExit, 2);
                break;
            case "transponder stby":
            case "tcas-mode 0":
                SetTransponderModeSelector(0);
                break;
            case "transponder auto":
            case "tcas-mode 1":
                SetTransponderModeSelector(1);
                break;
            case "transponder on":
            case "tcas-mode 2":
                SetTransponderModeSelector(2);
                break;
            case "atc-system 1":
                SetAtcSystem(0, 1);
                break;
            case "atc-system 2":
                SetAtcSystem(1, 2);
                break;
            case "tcas traffic tara":
                SetTcasTrafficMode(2);
                break;
            case "tcas altitude-reporting on":
                SetTcasAltitudeReporting(true);
                break;
            case "gear up":
                SetGearUp();
                break;
            case "gear down":
                SetGearDown();
                break;
            case "ground-spoilers disarm":
                SetGroundSpoilersDisarmed();
                break;
            case "altimeters standard":
                SetAltimetersStandard();
                break;
            case "wxr-pws 1":
                SetWeatherRadarPwsSelector(1);
                break;
            case "nose-light off":
                SetNoseLightSelector(2);
                break;
            case "nose-light taxi":
                SetNoseLightSelector(1);
                break;
            case "nose-light takeoff":
                SetNoseLightSelector(0);
                break;
            case "landing-lights retract":
                SetLandingLightSelectors(2);
                break;
            case "landing-lights off":
                SetLandingLightSelectors(1);
                break;
            case "landing-lights on":
                SetLandingLightSelectors(0);
                break;
            case "ground-spoilers arm":
                SetGroundSpoilersArmed();
                break;
            case "flaps config-1":
                SetFlapsExtended(1);
                break;
            case "flaps config-2":
                SetFlapsExtended(2);
                break;
            case "flaps config-3":
                SetFlapsExtended(3);
                break;
            case "flaps full":
                SetFlapsExtended(4);
                break;
            case "flaps clean":
                SetFlapsClean();
                break;
            case "autobrake max":
                SetAutobrake(3, "MAX");
                break;
            case "autobrake low":
                SetAutobrake(1, "LOW");
                break;
            case "autobrake off":
                SetAutobrake(0, "OFF");
                break;
            case "pmdg irs left nav":
                SetPmdgIrsSelector(left: true, 2);
                break;
            case "pmdg irs right nav":
                SetPmdgIrsSelector(left: false, 2);
                break;
            case "pmdg logo on":
                SetPmdgLogoLight(true);
                break;
            case "pmdg position steady":
                SetPmdgPositionStrobe(0);
                break;
            case "pmdg strobes on":
                SetPmdgPositionStrobe(2);
                break;
            case "pmdg strobes off":
                SetPmdgPositionStrobe(0);
                break;
            case "pmdg emergency-exit arm":
                SetPmdgEmergencyExitLights(1);
                break;
            case "pmdg seatbelts on":
                SetPmdgSeatbelts(2);
                break;
            case "pmdg seatbelts auto":
                SetPmdgSeatbelts(1);
                break;
            case "pmdg no-smoking auto":
                SetPmdgNoSmoking(1);
                break;
            case "pmdg fuel-pumps on":
                SetPmdgFuelPumps(true);
                break;
            case "pmdg fuel-pumps off":
                SetPmdgFuelPumps(false);
                break;
            case "pmdg apu on":
                SetPmdgApuSelector(1);
                break;
            case "pmdg apu start":
                SetPmdgApuSelector(2);
                break;
            case "pmdg apu off":
                SetPmdgApuSelector(0);
                break;
            case "pmdg apu-bleed on":
                SetPmdgApuBleed(true);
                break;
            case "pmdg apu-bleed off":
                SetPmdgApuBleed(false);
                break;
            case "pmdg apu-generators on":
                SetPmdgApuGenerators(true);
                break;
            case "pmdg spoilers arm":
                SetPmdgSpeedbrakeArm();
                break;
            case "pmdg spoilers down":
                SetPmdgSpeedbrakeDown();
                break;
            case "pmdg gear up":
                SetPmdgGear(0);
                break;
            case "pmdg gear down":
                SetPmdgGear(2);
                break;
            case "pmdg flaps takeoff":
            case "pmdg flaps 1":
                SetPmdgFlapsDetent(1);
                break;
            case "pmdg flaps 5":
                SetPmdgFlapsDetent(5);
                break;
            case "pmdg flaps landing":
                SetPmdgFlapsDetent(30);
                break;
            case "pmdg flaps clean":
                SetPmdgFlapsDetent(0);
                break;
            case "pmdg autobrake rto":
                SetPmdgAutobrake(0);
                break;
            case "pmdg autobrake landing":
                SetPmdgAutobrake(2);
                break;
            case "pmdg autobrake off":
                SetPmdgAutobrake(1);
                break;
            case "pmdg taxi-light on":
                SetPmdgTaxiLight(true);
                break;
            case "pmdg landing-lights on":
                SetPmdgLandingLights(true);
                break;
            case "pmdg landing-lights off":
                SetPmdgLandingLights(false);
                break;
            case "pmdg transponder tara":
                SetPmdgTransponderMode(4);
                break;
            case "pmdg transponder stby":
                SetPmdgTransponderMode(0);
                break;
            case "pmdg beacon off":
                SetPmdgAntiCollision(false);
                break;
            case "help":
                PrintHelp();
                FinishOneShot();
                break;
            case "quit":
            case "exit":
                Application.ExitThread();
                break;
            default:
                Console.Error.WriteLine($"Unknown command: {command}");
                PrintHelp();
                FinishOneShot(2);
                break;
        }
    }

    private void StartProcedure(ProcedureDefinition definition)
    {
        if (_state == null)
        {
            Console.Error.WriteLine("Cannot start procedure: aircraft state is unavailable.");
            FinishOneShot(3);
            return;
        }

        _cruiseSeatbeltMonitoring =
            string.Equals(definition.Id, "cruise", StringComparison.OrdinalIgnoreCase);
        _smoothCruiseSinceUtc = null;
        _nextCruiseSeatbeltCommandUtc = DateTime.MinValue;
        _procedureRunner.Start(definition, _state);
        FinishProcedureOneShotIfTerminal();
    }

    private void SetPmdgIrsSelector(bool left, uint position)
    {
        SendPmdgNg3Control(left ? 255u : 256u, position);
        FinishOneShot();
    }

    private void SetPmdgLogoLight(bool on)
    {
        if (_pmdgNg3State?.LogoLightOn == on)
        {
            FinishOneShot();
            return;
        }

        SendPmdgNg3Control(122, on ? 1u : 0u);
        FinishOneShot();
    }

    private void SetPmdgPositionStrobe(uint position)
    {
        SendPmdgNg3Control(123, position);
        FinishOneShot();
    }

    private void SetPmdgEmergencyExitLights(uint position)
    {
        SendPmdgNg3Control(100, position);
        FinishOneShot();
    }

    private void SetPmdgSeatbelts(uint position)
    {
        SendPmdgNg3Control(104, position);
        FinishOneShot();
    }

    private void SetPmdgNoSmoking(uint position)
    {
        SendPmdgNg3Control(103, position);
        FinishOneShot();
    }

    private void SetPmdgFuelPumps(bool on)
    {
        var parameter = on ? 1u : 0u;
        foreach (var offset in new[] { 37u, 38u, 39u, 40u })
        {
            SendPmdgNg3Control(offset, parameter);
        }
        FinishOneShot();
    }

    private void SetPmdgApuSelector(uint position)
    {
        SendPmdgNg3Control(118, position);
        FinishOneShot();
    }

    private void SetPmdgApuBleed(bool on)
    {
        SendPmdgNg3Control(211, on ? 1u : 0u);
        FinishOneShot();
    }

    private void SetPmdgApuGenerators(bool on)
    {
        var parameter = on ? 1u : 0u;
        SendPmdgNg3Control(28, parameter);
        SendPmdgNg3Control(29, parameter);
        FinishOneShot();
    }

    private void SetPmdgAutobrake(uint position)
    {
        SendPmdgNg3Control(460, position);
        FinishOneShot();
    }

    private void SetPmdgSpeedbrakeArm()
    {
        SendPmdgNg3Control(6792, 0);
        FinishOneShot();
    }

    private void SetPmdgSpeedbrakeDown()
    {
        SendPmdgNg3Control(6791, 0);
        FinishOneShot();
    }

    private void SetPmdgGear(uint position)
    {
        SendPmdgNg3Control(455, position);
        FinishOneShot();
    }

    private void SetPmdgTaxiLight(bool on)
    {
        SendPmdgNg3Control(117, on ? 1u : 0u);
        FinishOneShot();
    }

    private void SetPmdgLandingLights(bool on)
    {
        var position = on ? 2u : 0u;
        SendPmdgNg3Control(111, position);
        SendPmdgNg3Control(112, position);
        SendPmdgNg3Control(113, on ? 1u : 0u);
        SendPmdgNg3Control(114, on ? 1u : 0u);
        FinishOneShot();
    }

    private void SetPmdgTransponderMode(uint mode)
    {
        SendPmdgNg3Control(798, mode);
        FinishOneShot();
    }

    private void SetPmdgAntiCollision(bool on)
    {
        SendPmdgNg3Control(124, on ? 1u : 0u);
        FinishOneShot();
    }

    private void SetPmdgFlapsDetent(int detent)
    {
        var offset = detent switch
        {
            0 => 7141u,
            1 => 7142u,
            2 => 7143u,
            5 => 7144u,
            10 => 7145u,
            15 => 7146u,
            25 => 7147u,
            30 => 7148u,
            40 => 7149u,
            _ => 7141u
        };
        SendPmdgNg3Control(offset, 0);
        FinishOneShot();
    }

    private void TryRestoreProcedureSession()
    {
        if (_procedureSessionRestoreAttempted
            || _state == null
            || !_state.IsSupportedAircraft
            || (_state.IsIniBuildsA320Family && !NativeStateReady)
            || (_state.IsSupportedBoeing737 && !_pmdgNg3DataReady))
        {
            return;
        }

        _procedureSessionRestoreAttempted = true;
        var activeProcedureId = _procedureSession.ActiveProcedureId;
        if (string.IsNullOrWhiteSpace(activeProcedureId))
        {
            return;
        }

        var definition =
            ProcedureCatalog.Find(_state, activeProcedureId!);
        if (definition == null)
        {
            _procedureSession.ActiveProcedureId = null;
            SaveProcedureSession();
            return;
        }

        _cruiseSeatbeltMonitoring =
            string.Equals(definition.Id, "cruise", StringComparison.OrdinalIgnoreCase);
        _procedureRunner.Restore(
            definition,
            _procedureSession.ActiveStepIndex,
            _state);
        AppendDashboardLog(
            $"Restored saved procedure session: {definition.Name}.");
    }

    private void StartProcedureById(string id)
    {
        var definition = ProcedureCatalog.Find(_state, id);
        if (definition == null)
        {
            Console.Error.WriteLine($"Unknown procedure: {id}");
            FinishOneShot(2);
            return;
        }

        StartProcedure(definition);
    }

    private void ResetFlightProgress()
    {
        CancelFuelPumpSequence();
        _procedureRunner.Cancel();
        _completedProcedureIds.Clear();
        _procedureSession.ResetProgress(DateTime.UtcNow);
        ProcedureSessionStore.Save(_procedureSession);
        _cruiseSeatbeltMonitoring = false;
        _smoothCruiseSinceUtc = null;
        _nextCruiseSeatbeltCommandUtc = DateTime.MinValue;
        if (_flowList != null && _flowList.Items.Count > 0)
        {
            _flowList.SelectedIndex = 0;
        }
        AppendDashboardLog(
            "New flight started: all saved flow progress was reset.");
        UpdateDashboard();
        FinishOneShot();
    }

    private void DebugJumpToFlowById(string id)
    {
        var flows = ProcedureCatalog.ForAircraft(_state);
        var indexedFlow = flows
            .Select((definition, index) => new { definition, index })
            .FirstOrDefault(item =>
                string.Equals(item.definition.Id, id, StringComparison.OrdinalIgnoreCase));
        if (indexedFlow == null)
        {
            Console.Error.WriteLine($"Unknown debug flow: {id}");
            FinishOneShot(2);
            return;
        }
        if (_state == null)
        {
            Console.Error.WriteLine("Cannot debug-jump procedure: aircraft state is unavailable.");
            FinishOneShot(3);
            return;
        }

        CancelFuelPumpSequence();
        _procedureRunner.Cancel();
        _completedProcedureIds.Clear();
        for (var i = 0; i < indexedFlow.index; i++)
        {
            _completedProcedureIds.Add(flows[i].Id);
        }

        if (_flowList != null
            && indexedFlow.index >= 0
            && indexedFlow.index < _flowList.Items.Count)
        {
            _flowList.SelectedIndex = indexedFlow.index;
        }

        AppendDashboardLog(
            $"Debug jump: marked flows 1-{indexedFlow.index} complete and starting {indexedFlow.definition.Name}. Aircraft state was not changed.");
        StartProcedure(indexedFlow.definition);
        SaveProcedureSession();
        UpdateDashboard();
    }

    private ProcedureDefinition? GetAutomaticNextFlow(string completedId)
    {
        var flows = ProcedureCatalog.ForAircraft(_state);
        var index = flows
            .Select((definition, flowIndex) => new { definition, flowIndex })
            .FirstOrDefault(item =>
                string.Equals(
                    item.definition.Id,
                    completedId,
                    StringComparison.OrdinalIgnoreCase))
            ?.flowIndex ?? -1;
        if (index < 0 || index >= flows.Count - 1)
        {
            return null;
        }

        var enabled = completedId switch
        {
            "before-takeoff" => _settings.AutoChainFlow6To7,
            "approach-landing" => _settings.AutoChainFlow10To11,
            "after-landing-taxi" => _settings.AutoChainFlow11To12,
            _ => _settings.AutoChainEarlierFlows
        };
        return enabled ? flows[index + 1] : null;
    }

    private void ConfirmProcedureStep()
    {
        if (_state == null)
        {
            Console.Error.WriteLine("Cannot confirm procedure step: aircraft state is unavailable.");
            FinishOneShot(3);
            return;
        }

        _procedureRunner.ConfirmManualStep(_state);
        FinishProcedureOneShotIfTerminal();
    }

    private void ResumeProcedure()
    {
        if (_state == null)
        {
            Console.Error.WriteLine("Cannot resume procedure: aircraft state is unavailable.");
            FinishOneShot(3);
            return;
        }

        _procedureRunner.Resume(_state);
        FinishProcedureOneShotIfTerminal();
    }

    private void OnProcedureChanged()
    {
        var completedDefinition = _procedureRunner.Status == ProcedureStatus.Completed
            ? _procedureRunner.Definition
            : null;
        if (_procedureRunner.Status == ProcedureStatus.Completed
            && completedDefinition != null)
        {
            _completedProcedureIds.Add(completedDefinition.Id);
        }
        SaveProcedureSession();
        PrintProcedureUpdate();

        var nextFlow = completedDefinition == null
            ? null
            : GetAutomaticNextFlow(completedDefinition.Id);
        if (nextFlow != null)
        {
            AppendDashboardLog(
                $"{completedDefinition!.Name} complete; {nextFlow.Name} will start automatically.");
            _commands.Enqueue($"procedure start {nextFlow.Id}");
        }
    }

    private void SaveProcedureSession()
    {
        var active =
            _procedureRunner.Definition != null
            && _procedureRunner.Status is ProcedureStatus.Running
                or ProcedureStatus.WaitingForManualAction
                or ProcedureStatus.WaitingForVerification
                or ProcedureStatus.Paused;
        _procedureSession.ActiveProcedureId =
            active ? _procedureRunner.Definition!.Id : null;
        _procedureSession.ActiveStepIndex =
            active ? _procedureRunner.CurrentStepIndex : 0;
        _procedureSession.CompletedProcedureIds =
            _completedProcedureIds.OrderBy(id => id).ToList();
        _procedureSession.SavedUtc = DateTime.UtcNow;
        ProcedureSessionStore.Save(_procedureSession);
    }

    private void PrintProcedureUpdate()
    {
        var step = _procedureRunner.CurrentStep;
        Console.WriteLine(
            $"Procedure: {_procedureRunner.Definition?.Name ?? "none"} | " +
            $"{_procedureRunner.Status} | " +
            $"{_procedureRunner.CompletedStepCount}/{_procedureRunner.Definition?.Steps.Count ?? 0}");
        if (step != null)
        {
            Console.WriteLine($"Current step: {step.Label}");
        }
        var procedureMessage = _procedureRunner.Message;
        if (!string.IsNullOrWhiteSpace(procedureMessage))
        {
            Console.WriteLine(procedureMessage);
            AppendDashboardLog(procedureMessage!);
        }
        if (_state != null
            && _procedureRunner.Status == ProcedureStatus.WaitingForVerification
            && step != null
            && TryDescribeApproachGateStatus(step.Id, _state, out var gateStatus))
        {
            AppendDashboardLog(gateStatus);
        }
        UpdateDashboard();
    }

    private void SpeakProcedureCallout(ProcedureStep step)
    {
        if (!_settings.EnableStandardCallouts || _voiceCalloutQueue == null)
        {
            return;
        }
        if (step.Id == "fo-reverse-callout"
            && _state?.ReverseThrustEngaged != true)
        {
            return;
        }

        var phrase = step.Id switch
        {
            "captain-engine-two" => "Engine two on",
            "fo-engine-two-starter" => "Engine two starter valve open",
            "fo-engine-two-fuel" => "Engine two fuel flow",
            "fo-engine-two-stable" => "Engine two stabilized",
            "captain-engine-one" => "Engine one on",
            "fo-engine-one-starter" => "Engine one starter valve open",
            "fo-engine-one-fuel" => "Engine one fuel flow",
            "fo-engine-one-stable" => "Engine one stabilized",
            "fo-cabin-call" => "Cabin crew, prepare for takeoff",
            "fo-cabin-landing-call" => "Cabin crew, prepare for landing",
            "captain-takeoff" => "Thrust set",
            "fo-100-knots" => "One hundred knots",
            "fo-v1" => "V one",
            "fo-rotate" => "Rotate",
            "positive-climb" => "Positive climb",
            "fo-gear-up" => "Landing gear up",
            "fo-gear-down" => "Landing gear down",
            "fo-approaching-minimums" => "Approaching minimums",
            "fo-minimums" => "Minimums",
            "fo-spoilers-callout" => "Spoilers",
            "fo-reverse-callout" => "Reverse green",
            "fo-decel-callout" => "Decel",
            _ => null
        };
        if (phrase == null)
        {
            return;
        }

        try
        {
            _voiceCalloutQueue.Enqueue(phrase, GetCalloutPriority(step.Id));
            AppendDashboardLog($"Voice callout: {phrase}");
        }
        catch (InvalidOperationException ex)
        {
            AppLog.Write($"Voice callout failed: {ex.Message}");
        }
    }

    private static int GetCalloutPriority(string stepId) =>
        stepId switch
        {
            "fo-100-knots" or "fo-v1" or "fo-rotate"
                or "positive-climb" or "fo-gear-up"
                or "fo-gear-down" or "fo-approaching-minimums"
                or "fo-minimums" or "fo-spoilers-callout"
                or "fo-reverse-callout" or "fo-decel-callout" => 100,
            "captain-takeoff" or "fo-cabin-call"
                or "fo-cabin-landing-call" => 70,
            _ => 30
        };

    private void PrintProcedureStatus()
    {
        PrintProcedureUpdate();
    }

    private void FinishProcedureOneShotIfTerminal()
    {
        if (_procedureRunner.Definition == null)
        {
            return;
        }

        if (_procedureRunner.Status is ProcedureStatus.Completed
            or ProcedureStatus.Failed
            or ProcedureStatus.Idle
            or ProcedureStatus.Paused)
        {
            FinishOneShot(_procedureRunner.Status == ProcedureStatus.Failed ? 4 : 0);
        }
    }

    private void SetBeacon(bool desiredOn)
    {
        if (_simConnect == null || _state == null)
        {
            Console.Error.WriteLine("Beacon procedure blocked: aircraft state is unavailable.");
            FinishOneShot(3);
            return;
        }

        if (!_state.IsSupportedA320)
        {
            Console.Error.WriteLine("Beacon procedure blocked: the loaded aircraft is not a supported A320.");
            FinishOneShot(3);
            return;
        }

        if (_state.BeaconOn == desiredOn)
        {
            Console.WriteLine($"Beacon is already {(desiredOn ? "ON" : "OFF")}.");
            FinishOneShot();
            return;
        }

        _simConnect.TransmitClientEvent(
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            CopilotEvent.SetBeacon,
            desiredOn ? 1u : 0u,
            Priority.Highest,
            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);

        _pendingBeaconProcedure = new PendingBeaconProcedure(
            desiredOn,
            DateTime.UtcNow.AddSeconds(5));
        Console.WriteLine($"Beacon command sent: {(desiredOn ? "ON" : "OFF")}; awaiting readback.");
    }

    private void SetNavLogoSelector(int nativePosition)
    {
        if (_state == null)
        {
            Console.Error.WriteLine("NAV & LOGO procedure blocked: aircraft state is unavailable.");
            FinishOneShot(3);
            return;
        }

        if (_state.IsFlyByWireA320Neo)
        {
            SetFlyByWireNavLogoSelector(nativePosition);
            return;
        }

        if (!_state.IsIniBuildsA320Family || !_mobiFlightReady)
        {
            Console.Error.WriteLine("NAV & LOGO procedure blocked: iniBuilds adapter is unavailable.");
            FinishOneShot(4);
            return;
        }

        if (_state.NavLogoSelectorPosition.HasValue
            && Math.Abs(_state.NavLogoSelectorPosition.Value - nativePosition) < 0.1)
        {
            Console.WriteLine($"NAV & LOGO selector is already at {FormatNavLogoPosition(nativePosition)}.");
            FinishOneShot();
            return;
        }

        var stateEvent = nativePosition switch
        {
            0 => "AIRLINER_LT_NAVLOGO_STATE1",
            1 => "AIRLINER_LT_NAVLOGO_STATE2",
            2 => "AIRLINER_LT_NAVLOGO_STATE3",
            _ => throw new ArgumentOutOfRangeException(
                nameof(nativePosition),
                nativePosition,
                "NAV & LOGO selector position must be 0, 1, or 2.")
        };
        SendMobiFlightCommand($"MF.SimVars.Set.(>B:{stateEvent})");
        SendMobiFlightCommand("MF.DummyCmd");

        _pendingNavLogoSelectorProcedure = new PendingNavLogoSelectorProcedure(
            nativePosition,
            DateTime.UtcNow.AddSeconds(5));
        AppendDashboardLog(
            $"NAV & LOGO command sent: {FormatNavLogoPosition(nativePosition)}; awaiting native readback.");
    }

    private void SetFlyByWireNavLogoSelector(int nativePosition)
    {
        if (_state == null)
        {
            Console.Error.WriteLine("NAV & LOGO procedure blocked: aircraft state is unavailable.");
            FinishOneShot(3);
            return;
        }

        if (!_mobiFlightRuntimeReady)
        {
            Console.Error.WriteLine("NAV & LOGO procedure blocked: FBW runtime adapter is unavailable.");
            FinishOneShot(4);
            return;
        }

        if (_state.NavLogoSelectorPosition.HasValue
            && Math.Abs(_state.NavLogoSelectorPosition.Value - nativePosition) < 0.1)
        {
            Console.WriteLine($"NAV & LOGO selector is already at {FormatNavLogoPosition(nativePosition)}.");
            FinishOneShot();
            return;
        }

        var fbwPosition = nativePosition switch
        {
            0 => 2,
            1 => 1,
            2 => 0,
            _ => throw new ArgumentOutOfRangeException(
                nameof(nativePosition),
                nativePosition,
                "NAV & LOGO selector position must be 0, 1, or 2.")
        };

        SendMobiFlightCommand($"MF.SimVars.Set.{fbwPosition} (>B:A32NX_OVH_LIGHTS_NAV_LOGO_SW_Set)");
        SendMobiFlightCommand("MF.DummyCmd");

        _pendingNavLogoSelectorProcedure = new PendingNavLogoSelectorProcedure(
            nativePosition,
            DateTime.UtcNow.AddSeconds(5));
        AppendDashboardLog(
            $"FBW NAV & LOGO command sent: {FormatNavLogoPosition(nativePosition)}; awaiting readback.");
    }

    private static string FormatNavLogoPosition(int nativePosition) =>
        nativePosition switch
        {
            2 => "OFF",
            1 => "1",
            0 => "2",
            _ => nativePosition.ToString()
        };

    private void SetBattery(int batteryNumber, bool desiredOn)
    {
        if (_simConnect == null || _state == null)
        {
            Console.Error.WriteLine("Battery procedure blocked: aircraft state is unavailable.");
            FinishOneShot(3);
            return;
        }

        if (!_state.IsSupportedA320 || !_state.OnGround || !_state.EnginesOff)
        {
            Console.Error.WriteLine(
                "Battery procedure blocked: requires a supported A320 on the ground with engines off.");
            FinishOneShot(3);
            return;
        }

        var currentState = batteryNumber == 1
            ? _state.Battery1On
            : _state.Battery2On;
        if (currentState == desiredOn)
        {
            Console.WriteLine($"BAT {batteryNumber} is already {desiredOn.ToOnOff()}.");
            FinishOneShot();
            return;
        }

        if (!_mobiFlightReady)
        {
            Console.Error.WriteLine(
                "Battery procedure blocked: MobiFlight aircraft adapter is not connected.");
            FinishOneShot(4);
            return;
        }

        if (_state.IsFlyByWireA320Neo)
        {
            ExecuteFlyByWireBatteryCommand(batteryNumber, desiredOn);
        }
        else
        {
            var preset = $"Battery_{batteryNumber}_{(desiredOn ? "On" : "Off")}";
            if (!ExecuteDocumentedPreset(preset))
            {
                FinishOneShot(4);
                return;
            }
        }
        SendMobiFlightCommand("MF.DummyCmd");
        _pendingBatteryProcedure = new PendingBatteryProcedure(
            batteryNumber,
            desiredOn,
            DateTime.UtcNow.AddSeconds(5));
        Console.WriteLine(
            $"BAT {batteryNumber} command sent: {desiredOn.ToOnOff()}; awaiting readback.");
        AppendDashboardLog($"BAT {batteryNumber} command sent: {desiredOn.ToOnOff()}");
    }

    private void ExecuteFlyByWireBatteryCommand(int batteryNumber, bool desiredOn)
    {
        var value = desiredOn ? 1 : 0;
        var calculatorCode =
            $"{value} (>L:A32NX_OVHD_ELEC_BAT_{batteryNumber}_PB_IS_AUTO, Bool)";
        SendMobiFlightCommand($"MF.SimVars.Set.{calculatorCode}");
        if (batteryNumber == 1)
        {
            _fbwCommandedBattery1Auto = desiredOn;
        }
        else
        {
            _fbwCommandedBattery2Auto = desiredOn;
        }
        AppLog.Write(
            $"Executed FBW battery command: {calculatorCode}");
    }

    private bool ExecuteDocumentedPreset(string preset)
    {
        if (!IniBuildsA320ControlCatalog.TryGet(preset, out var control))
        {
            AppendDashboardLog(
                $"Control blocked: documented iniBuilds preset '{preset}' is not in the catalog.");
            return false;
        }

        SendMobiFlightCommand($"MF.SimVars.Set.{control.CalculatorCode}");
        AppLog.Write(
            $"Executed documented preset {control.Preset} from {control.Source}.");
        return true;
    }

    private void SetApuMaster(bool desiredOn)
    {
        if (_state?.IsFlyByWireA320Neo == true)
        {
            SetFlyByWireBoolLVarAction(
                "APU master",
                "A32NX_OVHD_APU_MASTER_SW_PB_IS_ON",
                desiredOn,
                state => state.ApuMasterSwitchOn == desiredOn);
            return;
        }

        PulseApuGroundCommand(
            "APU master",
            "INI_APU_MASTER_SWITCH_CMD",
            desiredOn,
            state => state.ApuMasterSwitchOn == desiredOn);
    }

    private void SetApuStart(bool desiredOn)
    {
        if (_state?.IsFlyByWireA320Neo == true)
        {
            if (!desiredOn)
            {
                AppendDashboardLog("APU start OFF is not a supported FBW action.");
                FinishOneShot(4);
                return;
            }

            SetFlyByWireBoolLVarAction(
                "APU start",
                "A32NX_OVHD_APU_START_PB_IS_ON",
                true,
                state => state.ApuStartButtonOn || state.ApuAvailable);
            return;
        }

        PulseApuGroundCommand(
            "APU start",
            "INI_APU_START_BUTTON_CMD",
            desiredOn,
            state => state.ApuStartButtonOn == desiredOn);
    }

    private void SetFlyByWireBoolLVarAction(
        string name,
        string lvarName,
        bool desiredOn,
        Func<AircraftState, bool> verify)
    {
        if (_state == null || !_mobiFlightRuntimeReady)
        {
            AppendDashboardLog($"{name} blocked: FBW runtime adapter is unavailable.");
            FinishOneShot(4);
            return;
        }

        if (verify(_state))
        {
            AppendDashboardLog($"{name} already {desiredOn.ToOnOff()}.");
            FinishOneShot();
            return;
        }

        var value = desiredOn ? 1 : 0;
        SendMobiFlightCommand($"MF.SimVars.Set.{value} (>L:{lvarName})");
        SendMobiFlightCommand($"MF.SimVars.Set.{value} (>L:{lvarName}, Bool)");
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(name, verify, desiredOn, TimeSpan.FromSeconds(10));
    }

    private void PulseApuGroundCommand(
        string name,
        string commandLVar,
        bool desiredOn,
        Func<AircraftState, bool> verify,
        TimeSpan? timeout = null)
    {
        if (!ValidateNativeInputAction(name, requireStationary: false))
        {
            return;
        }
        if (!_state!.OnGround)
        {
            AppendDashboardLog($"{name} blocked: aircraft must be on the ground.");
            FinishOneShot(3);
            return;
        }
        if (verify(_state))
        {
            AppendDashboardLog($"{name} already {desiredOn.ToOnOff()}.");
            FinishOneShot();
            return;
        }

        SendNativePulse(commandLVar);
        BeginNativeAction(name, verify, desiredOn, timeout);
    }

    private void SetApuBleed(bool desiredOn)
    {
        if (_state?.IsFlyByWireA320Neo == true)
        {
            SetFlyByWireBoolLVarAction(
                "APU bleed",
                "A32NX_OVHD_PNEU_APU_BLEED_PB_IS_ON",
                desiredOn,
                state => state.ApuBleedOn == desiredOn);
            return;
        }

        ToggleNativeMouserect(
            "APU bleed",
            "INI_APU_BLEED_BUTTON",
            "__APU_BLEEDIsPressed",
            desiredOn,
            state => state.ApuBleedOn == desiredOn,
            requireStationary: false);
    }

    private void SetApuGenerator(bool desiredOn)
        => PulseInputEvent(
            "APU generator",
            3205083420795941787UL,
            desiredOn,
            state => state.ApuGeneratorSwitchOn == desiredOn);

    private void SetFuelPumps(bool desiredOn)
    {
        if (_state?.IsFlyByWireA320Neo == true)
        {
            SetFlyByWireFuelPumps(desiredOn);
            return;
        }

        if (!ValidateNativeInputAction("Fuel pumps"))
        {
            return;
        }
        var alreadyDesired = desiredOn
            ? _state!.FuelPumpsConfigured
            : AreAllFuelPumpsOff(_state!);
        if (alreadyDesired)
        {
            AppendDashboardLog($"Fuel pumps already {(desiredOn ? "ON" : "OFF")}.");
            FinishOneShot();
            return;
        }

        var state = _state!;
        var pumpStates = new[]
        {
            state.FuelPump1State,
            state.FuelPump2State,
            state.FuelPump3State,
            state.FuelPump4State,
            state.FuelPump5State,
            state.FuelPump6State
        };
        var selectors = new[]
        {
            "INI_OUTER_TANK_LEFT",
            "INI_INNER_TANK_LEFT",
            "INI_CENTER_TANK_LEFT",
            "INI_CENTER_TANK_RIGHT",
            "INI_INNER_TANK_RIGHT",
            "INI_OUTER_TANK_RIGHT"
        };
        var pressStates = new[]
        {
            "__FUEL_ENG1_L1IsPressed",
            "__FUEL_ENG1_L2IsPressed",
            "__FUEL_CTR_1IsPressed",
            "__FUEL_CTR_2IsPressed",
            "__FUEL_ENG2_R1IsPressed",
            "__FUEL_ENG2_R2IsPressed"
        };

        var toggles = new Queue<FuelPumpToggle>();
        for (var index = 0; index < pumpStates.Length; index++)
        {
            var isOn = Math.Abs(pumpStates[index]) >= 0.1;
            if (isOn == desiredOn)
            {
                continue;
            }

            toggles.Enqueue(
                new FuelPumpToggle(
                    index + 1,
                    $"(L:{selectors[index]}) ! (>L:{selectors[index]}) " +
                    $"(L:{pressStates[index]}) ! (>L:{pressStates[index]})"));
        }

        _pendingFuelPumpSequence = new PendingFuelPumpSequence(toggles, desiredOn);
        _fuelPumpSequenceTimer?.Dispose();
        _fuelPumpSequenceTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _fuelPumpSequenceTimer.Tick += (_, _) => ExecuteNextFuelPumpToggle();
        ExecuteNextFuelPumpToggle();
    }

    private void SetFlyByWireFuelPumps(bool desiredOn)
    {
        if (_simConnect == null || _state == null)
        {
            AppendDashboardLog("Fuel pumps blocked: aircraft state is unavailable.");
            FinishOneShot(3);
            return;
        }

        var alreadyDesired = desiredOn
            ? _state.FuelPumpsConfigured
            : AreAllFuelPumpsOff(_state);
        if (alreadyDesired)
        {
            AppendDashboardLog($"Fuel pumps already {(desiredOn ? "ON" : "OFF")}.");
            FinishOneShot();
            return;
        }

        var states = new[]
        {
            _state.FuelPump1State,
            _state.FuelPump2State,
            _state.FuelPump3State,
            _state.FuelPump4State,
            _state.FuelPump5State,
            _state.FuelPump6State
        };
        var commands = desiredOn
            ? new[] { "2 (>K:FUELSYSTEM_PUMP_ON)", "5 (>K:FUELSYSTEM_PUMP_ON)", "9 (>K:FUELSYSTEM_VALVE_OPEN)", "10 (>K:FUELSYSTEM_VALVE_OPEN)", "3 (>K:FUELSYSTEM_PUMP_ON)", "6 (>K:FUELSYSTEM_PUMP_ON)" }
            : new[] { "2 (>K:FUELSYSTEM_PUMP_OFF)", "5 (>K:FUELSYSTEM_PUMP_OFF)", "9 (>K:FUELSYSTEM_VALVE_CLOSE)", "10 (>K:FUELSYSTEM_VALVE_CLOSE)", "3 (>K:FUELSYSTEM_PUMP_OFF)", "6 (>K:FUELSYSTEM_PUMP_OFF)" };

        var toggles = new Queue<FuelPumpToggle>();
        for (var index = 0; index < states.Length; index++)
        {
            var isOn = Math.Abs(states[index]) >= 0.1;
            if (isOn == desiredOn)
            {
                continue;
            }

            toggles.Enqueue(new FuelPumpToggle(index + 1, commands[index]));
        }

        _pendingFuelPumpSequence = new PendingFuelPumpSequence(toggles, desiredOn);
        _fuelPumpSequenceTimer?.Dispose();
        _fuelPumpSequenceTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _fuelPumpSequenceTimer.Tick += (_, _) => ExecuteNextFuelPumpToggle();
        ExecuteNextFuelPumpToggle();
    }

    private void ExecuteNextFuelPumpToggle()
    {
        if (_pendingFuelPumpSequence == null)
        {
            StopFuelPumpSequenceTimer();
            return;
        }

        if (_pendingFuelPumpSequence.Toggles.Count == 0)
        {
            var desiredOn = _pendingFuelPumpSequence.DesiredOn;
            _pendingFuelPumpSequence = null;
            StopFuelPumpSequenceTimer();
            BeginNativeAction(
                "Fuel pumps",
                current => desiredOn
                    ? current.FuelPumpsConfigured
                    : AreAllFuelPumpsOff(current),
                desiredOn,
                TimeSpan.FromSeconds(10));
            return;
        }

        var toggle = _pendingFuelPumpSequence.Toggles.Dequeue();
        // Buttons are spaced one second apart for a believable F/O cadence.
        SendMobiFlightCommand($"MF.SimVars.Set.{toggle.CalculatorCode}");
        SendMobiFlightCommand("MF.DummyCmd");
        AppendDashboardLog(
            $"Fuel pump {toggle.Number}/6 pressed " +
            $"{_pendingFuelPumpSequence.DesiredOn.ToOnOff()}.");
        _fuelPumpSequenceTimer?.Start();
    }

    private void StopFuelPumpSequenceTimer()
    {
        _fuelPumpSequenceTimer?.Stop();
        _fuelPumpSequenceTimer?.Dispose();
        _fuelPumpSequenceTimer = null;
    }

    private void UpdateCockpitDisplayReadiness(AircraftState state)
    {
        var electricalPowerEstablished =
            state.Battery1On
            && state.Battery2On
            && state.ExternalPowerOn;
        if (!electricalPowerEstablished)
        {
            _electricalPowerStableSinceUtc = null;
            state.CockpitDisplaysReady = false;
            return;
        }

        _electricalPowerStableSinceUtc ??= DateTime.UtcNow;
        state.CockpitDisplaysReady =
            DateTime.UtcNow - _electricalPowerStableSinceUtc.Value
            >= TimeSpan.FromSeconds(45);
    }

    private static void UpdateTelemetrySanity(AircraftState state)
    {
        state.TelemetryIssues = AircraftStateSanity.Evaluate(state);
        state.FlapReadbackSane =
            !state.TelemetryIssues.Any(
                issue => issue.IndexOf(
                    "flap",
                    StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private void CancelFuelPumpSequence()
    {
        if (_pendingFuelPumpSequence == null)
        {
            return;
        }

        _pendingFuelPumpSequence = null;
        StopFuelPumpSequenceTimer();
        AppendDashboardLog("Fuel-pump press sequence cancelled.");
    }

    private static bool AreAllFuelPumpsOff(AircraftState state) =>
        Math.Abs(state.FuelPump1State) < 0.1
        && Math.Abs(state.FuelPump2State) < 0.1
        && Math.Abs(state.FuelPump3State) < 0.1
        && Math.Abs(state.FuelPump4State) < 0.1
        && Math.Abs(state.FuelPump5State) < 0.1
        && Math.Abs(state.FuelPump6State) < 0.1;

    private void ToggleNativeMouserect(
        string name,
        string selectorLVar,
        string pressLVar,
        bool desiredOn,
        Func<AircraftState, bool> verify,
        bool requireStationary = true)
    {
        if (!ValidateNativeInputAction(name, requireStationary: requireStationary))
        {
            return;
        }
        if (verify(_state!))
        {
            AppendDashboardLog($"{name} already {desiredOn.ToOnOff()}.");
            FinishOneShot();
            return;
        }

        SendMobiFlightCommand(
            $"MF.SimVars.Set.(L:{selectorLVar}) ! (>L:{selectorLVar}) " +
            $"(L:{pressLVar}) ! (>L:{pressLVar})");
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(name, verify, desiredOn);
    }

    private void PulseInputEvent(
        string name,
        ulong inputEventHash,
        bool desiredOn,
        Func<AircraftState, bool> verify,
        TimeSpan? timeout = null)
    {
        if (!ValidateNativeInputAction(name))
        {
            return;
        }
        if (verify(_state!))
        {
            AppendDashboardLog($"{name} already {desiredOn.ToOnOff()}.");
            FinishOneShot();
            return;
        }

        SendInputEventPulse(inputEventHash);
        BeginNativeAction(name, verify, desiredOn, timeout);
    }

    private void SendInputEventPulse(ulong inputEventHash)
    {
        _simConnect!.SetInputEvent(inputEventHash, 1.0);
        var releaseTimer = new System.Windows.Forms.Timer { Interval = 500 };
        releaseTimer.Tick += (_, _) =>
        {
            releaseTimer.Stop();
            _simConnect?.SetInputEvent(inputEventHash, 0.0);
            _nativePulseTimers.Remove(releaseTimer);
            releaseTimer.Dispose();
        };
        _nativePulseTimers.Add(releaseTimer);
        releaseTimer.Start();
    }

    private void SetAdirsSelector(int selector, int position)
    {
        if (_state?.IsFlyByWireA320Neo == true)
        {
            SetFlyByWireAdirsSelector(selector, position);
            return;
        }

        if (!ValidateNativeInputAction($"ADIRS {selector}"))
        {
            return;
        }

        var inputEventHash = selector switch
        {
            1 => 5157929863266406690UL,
            2 => 9260957592121887383UL,
            3 => 14012218200692620292UL,
            _ => throw new ArgumentOutOfRangeException(nameof(selector))
        };
        Func<AircraftState, double> readState = selector switch
        {
            1 => state => state.Adirs1SelectorState,
            2 => state => state.Adirs2SelectorState,
            3 => state => state.Adirs3SelectorState,
            _ => throw new ArgumentOutOfRangeException(nameof(selector))
        };
        bool Verify(AircraftState state) =>
            Math.Abs(readState(state) - position) < 0.1;

        if (Verify(_state!))
        {
            AppendDashboardLog($"ADIRS {selector} already at position {position}.");
            FinishOneShot();
            return;
        }

        // AIRLINER_ADIRS_n is a FLOAT64 rotary-selector Input Event.
        // Passive monitoring established OFF=0 and NAV=1. The independent
        // postcondition is the corresponding INI_IRSn_STATE native LVar.
        _simConnect!.SetInputEvent(inputEventHash, (double)position);
        BeginNativeAction(
            $"ADIRS {selector} selector",
            Verify,
            position != 0,
            TimeSpan.FromSeconds(10));
    }

    private void SetFlyByWireAdirsSelector(int selector, int position)
    {
        if (_simConnect == null || _state == null)
        {
            AppendDashboardLog($"ADIRS {selector} blocked: aircraft state is unavailable.");
            FinishOneShot(3);
            return;
        }

        Func<AircraftState, double> readState = selector switch
        {
            1 => state => state.Adirs1SelectorState,
            2 => state => state.Adirs2SelectorState,
            3 => state => state.Adirs3SelectorState,
            _ => throw new ArgumentOutOfRangeException(nameof(selector))
        };
        bool Verify(AircraftState state) =>
            Math.Abs(readState(state) - position) < 0.1;

        if (Verify(_state))
        {
            AppendDashboardLog($"ADIRS {selector} already at position {position}.");
            FinishOneShot();
            return;
        }

        var inputEventHash = selector switch
        {
            1 => 5157929863266406690UL,
            2 => 9260957592121887383UL,
            3 => 14012218200692620292UL,
            _ => throw new ArgumentOutOfRangeException(nameof(selector))
        };
        var lvarName = $"A32NX_OVHD_ADIRS_IR_{selector}_MODE_SELECTOR_KNOB";
        var calculatorCode = $"{position} (>L:{lvarName})";
        _simConnect.SetInputEvent(inputEventHash, (double)position);
        SendMobiFlightCommand($"MF.SimVars.Set.{calculatorCode}");
        SendMobiFlightCommand($"MF.SimVars.Set.{position} (>L:{lvarName}, Enum)");
        SendMobiFlightCommand($"MF.SimVars.Set.{position} (>L:{lvarName}, Number)");
        SendMobiFlightCommand("MF.DummyCmd");
        var commandedUtc = DateTime.UtcNow;
        switch (selector)
        {
            case 1:
                _fbwCommandedAdirs1Selector = position;
                _fbwCommandedAdirs1SelectorUtc = commandedUtc;
                break;
            case 2:
                _fbwCommandedAdirs2Selector = position;
                _fbwCommandedAdirs2SelectorUtc = commandedUtc;
                break;
            case 3:
                _fbwCommandedAdirs3Selector = position;
                _fbwCommandedAdirs3SelectorUtc = commandedUtc;
                break;
        }
        AppLog.Write(
            $"Executed FBW ADIRS command: input {inputEventHash}={position}; {calculatorCode}");
        _state.Adirs1SelectorState = selector == 1 ? position : _state.Adirs1SelectorState;
        _state.Adirs2SelectorState = selector == 2 ? position : _state.Adirs2SelectorState;
        _state.Adirs3SelectorState = selector == 3 ? position : _state.Adirs3SelectorState;
        AppendDashboardLog(
            $"ADIRS {selector} command sent: NAV; FBW cockpit command accepted.");
        FinishOneShot();
    }

    private void SetCrewOxygen(bool desiredOn)
    {
        if (_state?.IsFlyByWireA320Neo == true)
        {
            SetFlyByWireCrewOxygen(desiredOn);
            return;
        }

        if (!ValidateNativeInputAction("Crew oxygen", requireCompleteNativeState: false))
        {
            return;
        }
        if (_state!.CrewOxygenOn == desiredOn)
        {
            AppendDashboardLog($"Crew oxygen already {desiredOn.ToOnOff()}.");
            FinishOneShot();
            return;
        }

        // Exact Behavior Viewer Mouserect code. The Input Event Set binding
        // only changes _ButtonAnimVar and does not operate the oxygen supply.
        SendMobiFlightCommand(
            "MF.SimVars.Set.1 (>O:_ButtonAnimVar) " +
            "(L:INI_CREW_SUPPLY) ! (>L:INI_CREW_SUPPLY) " +
            "(L:__OXY_CREWIsPressed) ! (>L:__OXY_CREWIsPressed)");
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            "Crew oxygen",
            state => state.CrewOxygenOn == desiredOn,
            desiredOn,
            TimeSpan.FromSeconds(10));
    }

    private void SetFlyByWireCrewOxygen(bool desiredOn)
    {
        if (_simConnect == null || _state == null)
        {
            AppendDashboardLog("Crew oxygen blocked: aircraft state is unavailable.");
            FinishOneShot(3);
            return;
        }

        if (_state.CrewOxygenOn == desiredOn)
        {
            AppendDashboardLog($"Crew oxygen already {desiredOn.ToOnOff()}.");
            FinishOneShot();
            return;
        }

        // FBW uses the raw pushbutton LVar as an inverted state for this switch:
        // 0 = crew supply ON, 1 = crew supply OFF.
        var desiredRawState = desiredOn ? 0 : 1;
        if (_fbwCrewOxygenTyped.HasValue && ((int)Math.Round(_fbwCrewOxygenTyped.Value ? 1.0 : 0.0)) == desiredRawState)
        {
            AppendDashboardLog($"Crew oxygen already {desiredOn.ToOnOff()}.");
            FinishOneShot();
            return;
        }

        if (_fbwCrewOxygen.HasValue && ((int)Math.Round(_fbwCrewOxygen.Value ? 1.0 : 0.0)) == desiredRawState)
        {
            AppendDashboardLog($"Crew oxygen already {desiredOn.ToOnOff()}.");
            FinishOneShot();
            return;
        }

        SendMobiFlightCommand($"MF.SimVars.Set.{desiredRawState} (>L:PUSH_OVHD_OXYGEN_CREW)");
        SendMobiFlightCommand($"MF.SimVars.Set.{desiredRawState} (>L:PUSH_OVHD_OXYGEN_CREW, Bool)");
        SendMobiFlightCommand("MF.DummyCmd");
        _fbwCommandedCrewOxygen = desiredOn;
        _state.CrewOxygenOn = desiredOn;
        AppLog.Write($"Executed FBW crew oxygen command: raw L:PUSH_OVHD_OXYGEN_CREW={desiredRawState}");
        AppendDashboardLog(
            $"Crew oxygen command sent: {desiredOn.ToOnOff()}.");
        FinishOneShot();
    }

    private void SetStrobeSelector(int desiredPosition)
    {
        if (_state?.IsFlyByWireA320Neo == true)
        {
            SetFlyByWireStrobeSelector(desiredPosition);
            return;
        }

        if (!ValidateNativeInputAction(
                "Strobe selector",
                requireCompleteNativeState: true,
                requireStationary: false))
        {
            return;
        }
        if (_state!.StrobeSelectorPosition.HasValue
            && Math.Abs(_state.StrobeSelectorPosition.Value - desiredPosition) < 0.1)
        {
            AppendDashboardLog(
                $"Strobe selector already {FormatStrobePosition(desiredPosition)}.");
            FinishOneShot();
            return;
        }

        // AIRLINER_LT_STROBE is a FLOAT64 three-position selector:
        // ON=0, AUTO=1, OFF=2. Verify against INI_STROBE_LIGHT_SWITCH.
        _simConnect!.SetInputEvent(8986586253276960537UL, (double)desiredPosition);
        BeginNativeAction(
            "Strobe selector",
            state => state.StrobeSelectorPosition.HasValue
                     && Math.Abs(state.StrobeSelectorPosition.Value - desiredPosition) < 0.1,
            desiredPosition != 2,
            TimeSpan.FromSeconds(10));
    }

    private void SetFlyByWireStrobeSelector(int desiredPosition)
    {
        if (_state == null)
        {
            AppendDashboardLog("Strobe selector blocked: aircraft state is unavailable.");
            FinishOneShot(3);
            return;
        }

        if (!_mobiFlightRuntimeReady)
        {
            AppendDashboardLog("Strobe selector blocked: FBW runtime adapter is unavailable.");
            FinishOneShot(4);
            return;
        }

        if (_state.StrobeSelectorPosition.HasValue
            && Math.Abs(_state.StrobeSelectorPosition.Value - desiredPosition) < 0.1)
        {
            AppendDashboardLog(
                $"Strobe selector already {FormatStrobePosition(desiredPosition)}.");
            FinishOneShot();
            return;
        }

        var calculatorCode = desiredPosition switch
        {
            0 => "0 (>L:STROBE_0_AUTO) 0 (>K:STROBES_ON)",
            1 => "1 (>L:STROBE_0_AUTO) 0 (>K:STROBES_ON)",
            2 => "0 (>L:STROBE_0_AUTO) 0 (>K:STROBES_OFF)",
            _ => throw new ArgumentOutOfRangeException(
                nameof(desiredPosition),
                desiredPosition,
                "Strobe selector position must be 0, 1, or 2.")
        };

        SendMobiFlightCommand($"MF.SimVars.Set.{calculatorCode}");
        SendMobiFlightCommand("MF.DummyCmd");
        AppLog.Write($"Executed FBW strobe command: {calculatorCode}");
        BeginNativeAction(
            "Strobe selector",
            state => state.StrobeSelectorPosition.HasValue
                     && Math.Abs(state.StrobeSelectorPosition.Value - desiredPosition) < 0.1,
            desiredPosition != 2,
            TimeSpan.FromSeconds(10));
    }

    private static string FormatStrobePosition(int position) =>
        position switch
        {
            0 => "ON",
            1 => "AUTO",
            2 => "OFF",
            _ => position.ToString()
        };

    private void StartFireTest(FireTestSystem system)
    {
        if (_state?.IsFlyByWireA320Neo == true)
        {
            StartFlyByWireFireTest(system);
            return;
        }

        if (!ValidateNativeInputAction(FormatFireTestName(system)))
        {
            return;
        }
        if (_pendingFireTest != null)
        {
            AppendDashboardLog("A fire test is already in progress.");
            FinishOneShot(4);
            return;
        }

        var inputEventHash = system switch
        {
            FireTestSystem.Apu => 4216857869517805758UL,
            FireTestSystem.Engine1 => 11463015441207054266UL,
            FireTestSystem.Engine2 => 13978300836120052149UL,
            _ => throw new ArgumentOutOfRangeException(nameof(system))
        };
        var name = FormatFireTestName(system);
        SetFireTestPressed(system, inputEventHash, true);
        _pendingFireTest = new PendingFireTest(
            system,
            inputEventHash,
            DateTime.UtcNow.AddSeconds(10));
        AppendDashboardLog($"{name} button held; awaiting active test readback.");
    }

    private void StartFlyByWireFireTest(FireTestSystem system)
    {
        if (_simConnect == null || _state == null)
        {
            AppendDashboardLog($"{FormatFireTestName(system)} blocked: aircraft state is unavailable.");
            FinishOneShot(3);
            return;
        }

        if (_pendingFlyByWireFireTest.HasValue || _pendingFireTest != null)
        {
            AppendDashboardLog("A fire test is already in progress.");
            FinishOneShot(4);
            return;
        }

        var name = FormatFireTestName(system);
        _pendingFlyByWireFireTest = system;
        SetFlyByWireFireTestPressed(system, true);
        AppendDashboardLog($"{name} button held for FBW fire test.");

        var releaseTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        releaseTimer.Tick += (_, _) =>
        {
            releaseTimer.Stop();
            SetFlyByWireFireTestPressed(system, false);
            switch (system)
            {
                case FireTestSystem.Apu: _apuFireTestCompleted = true; break;
                case FireTestSystem.Engine1: _engine1FireTestCompleted = true; break;
                case FireTestSystem.Engine2: _engine2FireTestCompleted = true; break;
            }

            if (_state != null)
            {
                _state.ApuFireTestCompleted = _apuFireTestCompleted;
                _state.Engine1FireTestCompleted = _engine1FireTestCompleted;
                _state.Engine2FireTestCompleted = _engine2FireTestCompleted;
            }

            _pendingFlyByWireFireTest = null;
            _nativePulseTimers.Remove(releaseTimer);
            releaseTimer.Dispose();
            AppendDashboardLog($"{name} completed and released safely.");
            FinishOneShot();
        };
        _nativePulseTimers.Add(releaseTimer);
        releaseTimer.Start();
    }

    private void VerifyPendingFireTest()
    {
        if (_pendingFireTest == null || _state == null || _simConnect == null)
        {
            return;
        }

        var test = _pendingFireTest;
        var active = test.System switch
        {
            FireTestSystem.Apu =>
                _state.ApuFireWarningLit || _state.ApuFireSoundActive,
            FireTestSystem.Engine1 =>
                _state.Engine1FireWarningLit || _state.Engine1FireSoundActive,
            FireTestSystem.Engine2 =>
                _state.Engine2FireWarningLit || _state.Engine2FireSoundActive,
            _ => false
        };
        var name = FormatFireTestName(test.System);

        if (!test.ActivationObserved)
        {
            if (active)
            {
                test.ActivationObserved = true;
                test.ReleaseUtc = DateTime.UtcNow.AddSeconds(5);
                AppendDashboardLog($"{name} active indication verified; holding for 5 seconds.");
                return;
            }

            if (DateTime.UtcNow >= test.DeadlineUtc)
            {
                SetFireTestPressed(test.System, test.InputEventHash, false);
                _pendingFireTest = null;
                var message = $"{name} failed to activate.";
                RecordDiagnosticFailure(
                    message,
                    new[]
                    {
                        $"Fire test system: {test.System}",
                        $"InputEvent hash: {test.InputEventHash}",
                        $"Active readback observed: {active}"
                    });
                AppendDashboardLog(message);
                _procedureRunner.Fail(message);
                FinishOneShot(4);
            }
            return;
        }

        if (!test.ReleaseSent && DateTime.UtcNow >= test.ReleaseUtc)
        {
            SetFireTestPressed(test.System, test.InputEventHash, false);
            test.ReleaseSent = true;
            test.DeadlineUtc = DateTime.UtcNow.AddSeconds(5);
            AppendDashboardLog($"{name} button released; awaiting cleared readback.");
            return;
        }

        if (test.ReleaseSent && !active)
        {
            switch (test.System)
            {
                case FireTestSystem.Apu: _apuFireTestCompleted = true; break;
                case FireTestSystem.Engine1: _engine1FireTestCompleted = true; break;
                case FireTestSystem.Engine2: _engine2FireTestCompleted = true; break;
            }
            _state.ApuFireTestCompleted = _apuFireTestCompleted;
            _state.Engine1FireTestCompleted = _engine1FireTestCompleted;
            _state.Engine2FireTestCompleted = _engine2FireTestCompleted;
            _pendingFireTest = null;
            AppendDashboardLog($"{name} completed and released safely.");
            FinishOneShot();
            return;
        }

        if (test.ReleaseSent && DateTime.UtcNow >= test.DeadlineUtc)
        {
            _pendingFireTest = null;
            var message = $"{name} did not clear after release.";
            RecordDiagnosticFailure(
                message,
                new[]
                {
                    $"Fire test system: {test.System}",
                    $"InputEvent hash: {test.InputEventHash}",
                    $"Active readback observed: {active}"
                });
            AppendDashboardLog(message);
            _procedureRunner.Fail(message);
            FinishOneShot(4);
        }
    }

    private static string FormatFireTestName(FireTestSystem system) =>
        system switch
        {
            FireTestSystem.Apu => "APU fire test",
            FireTestSystem.Engine1 => "Engine 1 fire test",
            FireTestSystem.Engine2 => "Engine 2 fire test",
            _ => system.ToString()
        };

    private void SetSignSelector(SignSelector selector, int desiredPosition)
    {
        if (_state?.IsFlyByWireA320Neo == true)
        {
            SetFlyByWireSignSelector(selector, desiredPosition);
            return;
        }

        if (!ValidateNativeInputAction(
                FormatSignSelectorName(selector),
                requireStationary: false))
        {
            return;
        }

        var inputEventHash = selector switch
        {
            SignSelector.Seatbelts => 12887035727064807174UL,
            SignSelector.NoSmoking => 12889273306186432835UL,
            SignSelector.EmergencyExit => 15249578372676866282UL,
            _ => throw new ArgumentOutOfRangeException(nameof(selector))
        };
        double? ReadPosition(AircraftState state) =>
            selector switch
            {
                SignSelector.Seatbelts => state.SeatbeltSelectorPosition,
                SignSelector.NoSmoking => state.NoSmokingSelectorPosition,
                SignSelector.EmergencyExit => state.EmergencyExitSelectorPosition,
                _ => null
            };
        bool Verify(AircraftState state)
        {
            var position = ReadPosition(state);
            if (!position.HasValue || Math.Abs(position.Value - desiredPosition) >= 0.1)
            {
                return false;
            }

            return selector switch
            {
                SignSelector.Seatbelts when desiredPosition == 0 => state.SeatbeltSignsOn,
                SignSelector.NoSmoking when desiredPosition == 0 => state.NoSmokingSignsOn,
                _ => true
            };
        }

        if (Verify(_state!))
        {
            AppendDashboardLog(
                $"{FormatSignSelectorName(selector)} already " +
                $"{FormatSignSelectorPosition(selector, desiredPosition)}.");
            FinishOneShot();
            return;
        }

        _simConnect!.SetInputEvent(inputEventHash, (double)desiredPosition);
        BeginNativeAction(
            FormatSignSelectorName(selector),
            Verify,
            desiredPosition != 2,
            TimeSpan.FromSeconds(10));
    }

    private void SetFlyByWireSignSelector(SignSelector selector, int desiredPosition)
    {
        if (_simConnect == null || _state == null)
        {
            AppendDashboardLog($"{FormatSignSelectorName(selector)} blocked: aircraft state is unavailable.");
            FinishOneShot(3);
            return;
        }

        double? ReadPosition(AircraftState state) =>
            selector switch
            {
                SignSelector.Seatbelts => state.SeatbeltSelectorPosition,
                SignSelector.NoSmoking => state.NoSmokingSelectorPosition,
                SignSelector.EmergencyExit => state.EmergencyExitSelectorPosition,
                _ => null
            };

        bool Verify(AircraftState state)
        {
            var position = ReadPosition(state);
            if (!position.HasValue || Math.Abs(position.Value - desiredPosition) >= 0.1)
            {
                return false;
            }

            return selector switch
            {
                SignSelector.Seatbelts when desiredPosition == 1 => state.SeatbeltSignsOn,
                SignSelector.NoSmoking when desiredPosition == 0 => state.NoSmokingSignsOn,
                _ => true
            };
        }

        if (Verify(_state))
        {
            AppendDashboardLog(
                $"{FormatSignSelectorName(selector)} already " +
                $"{FormatSignSelectorPosition(selector, desiredPosition)}.");
            FinishOneShot();
            return;
        }

        switch (selector)
        {
            case SignSelector.Seatbelts:
            {
                var fbwSeatbeltPosition = desiredPosition == 1 ? 1 : 0;
                SendMobiFlightCommand(
                    $"MF.SimVars.Set.{fbwSeatbeltPosition} (>L:XMLVAR_SWITCH_OVHD_INTLT_SEATBELT_Position)");
                if (desiredPosition != 1
                    && (desiredPosition == 0) != _state.SeatbeltSignsOn)
                {
                    SendMobiFlightCommand("MF.SimVars.Set.1 (>K:CABIN_SEATBELTS_ALERT_SWITCH_TOGGLE)");
                }
                break;
            }
            case SignSelector.NoSmoking:
                SendMobiFlightCommand(
                    $"MF.SimVars.Set.{desiredPosition} (>L:XMLVAR_SWITCH_OVHD_INTLT_NOSMOKING_Position)");
                break;
            case SignSelector.EmergencyExit:
                SendMobiFlightCommand(
                    $"MF.SimVars.Set.{desiredPosition} (>L:XMLVAR_SWITCH_OVHD_INTLT_EMEREXIT_Position)");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(selector));
        }

        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            FormatSignSelectorName(selector),
            Verify,
            desiredPosition != 2,
            TimeSpan.FromSeconds(10));
    }

    private static string FormatSignSelectorName(SignSelector selector) =>
        selector switch
        {
            SignSelector.Seatbelts => "Seatbelt selector",
            SignSelector.NoSmoking => "No-smoking selector",
            SignSelector.EmergencyExit => "Emergency-exit selector",
            _ => selector.ToString()
        };

    private static string FormatSignSelectorPosition(
        SignSelector selector,
        int position) =>
        position switch
        {
            0 => "ON",
            1 => selector == SignSelector.EmergencyExit ? "ARM" : "AUTO",
            2 => "OFF",
            _ => position.ToString()
        };

    private static string FormatOptionalSignPosition(
        SignSelector selector,
        double? position) =>
        position.HasValue
            ? FormatSignSelectorPosition(selector, (int)Math.Round(position.Value))
            : "UNKNOWN";

    private void SetTransponderModeSelector(int desiredPosition)
    {
        if (_state?.IsFlyByWireA320Neo == true)
        {
            SetFlyByWireTransponderModeSelector(desiredPosition);
            return;
        }

        if (!ValidateNativeInputAction("Transponder mode selector", requireStationary: false))
        {
            return;
        }
        if (_state!.TransponderModeSelectorPosition.HasValue
            && Math.Abs(_state.TransponderModeSelectorPosition.Value - desiredPosition) < 0.1)
        {
            AppendDashboardLog(
                $"Transponder mode selector already " +
                $"{FormatTransponderModePosition(desiredPosition)}.");
            FinishOneShot();
            return;
        }

        var stateEvent = desiredPosition switch
        {
            0 => "AIRLINER_TCAS_MODE_State1",
            1 => "AIRLINER_TCAS_MODE_State2",
            2 => "AIRLINER_TCAS_MODE_State3",
            _ => throw new ArgumentOutOfRangeException(nameof(desiredPosition))
        };
        SendMobiFlightCommand($"MF.SimVars.Set.(>B:{stateEvent})");
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            "Transponder mode selector",
            state => state.TransponderModeSelectorPosition.HasValue
                     && Math.Abs(state.TransponderModeSelectorPosition.Value - desiredPosition) < 0.1,
            desiredPosition != 0,
            TimeSpan.FromSeconds(10),
            FormatTransponderModePosition(desiredPosition));
    }

    private void SetFlyByWireTransponderModeSelector(int desiredPosition)
    {
        if (_state == null || !_mobiFlightRuntimeReady)
        {
            AppendDashboardLog("Transponder mode selector blocked: FBW runtime adapter is unavailable.");
            FinishOneShot(4);
            return;
        }

        if (_state.TransponderModeSelectorPosition.HasValue
            && Math.Abs(_state.TransponderModeSelectorPosition.Value - desiredPosition) < 0.1)
        {
            AppendDashboardLog(
                $"Transponder mode selector already " +
                $"{FormatTransponderModePosition(desiredPosition)}.");
            FinishOneShot();
            return;
        }

        SendMobiFlightCommand(
            $"MF.SimVars.Set.{desiredPosition} (>L:A32NX_TRANSPONDER_MODE, number)");
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            "Transponder mode selector",
            state => state.TransponderModeSelectorPosition.HasValue
                     && Math.Abs(state.TransponderModeSelectorPosition.Value - desiredPosition) < 0.1,
            desiredPosition != 0,
            TimeSpan.FromSeconds(10),
            FormatTransponderModePosition(desiredPosition));
    }

    private static string FormatTransponderModePosition(int position) =>
        position switch
        {
            0 => "STBY",
            1 => "AUTO",
            2 => "ON",
            _ => position.ToString()
        };

    private void SetAtcSystem(int desiredState, int displaySystem)
    {
        if (!ValidateNativeInputAction("ATC system selector"))
        {
            return;
        }
        if (_state!.TransponderAtcState.HasValue
            && Math.Abs(_state.TransponderAtcState.Value - desiredState) < 0.1)
        {
            AppendDashboardLog($"ATC system {displaySystem} already selected.");
            FinishOneShot();
            return;
        }

        // Exact Behavior Viewer Mouserect: the selector toggles system 1/2.
        SendMobiFlightCommand(
            "MF.SimVars.Set.(L:INI_TCAS_ATC_STATE) ! (>L:INI_TCAS_ATC_STATE)");
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            "ATC system selector",
            state => state.TransponderAtcState.HasValue
                     && Math.Abs(state.TransponderAtcState.Value - desiredState) < 0.1,
            desiredState != 0,
            TimeSpan.FromSeconds(10));
    }

    private void SetTcasTrafficMode(int desiredPosition)
    {
        if (_state?.IsFlyByWireA320Neo == true)
        {
            if (_state == null || !_mobiFlightRuntimeReady)
            {
                AppendDashboardLog("TCAS traffic mode blocked: FBW runtime adapter is unavailable.");
                FinishOneShot(4);
                return;
            }
            if (_state.TcasMode.HasValue
                && Math.Abs(_state.TcasMode.Value - desiredPosition) < 0.1)
            {
                AppendDashboardLog("TCAS traffic mode already TA/RA.");
                FinishOneShot();
                return;
            }

            SendMobiFlightCommand(
                $"MF.SimVars.Set.{desiredPosition} (>L:A32NX_SWITCH_TCAS_POSITION)");
            _fbwCommandedTcasMode = desiredPosition;
            _fbwCommandedTcasModeUtc = DateTime.UtcNow;
            SendMobiFlightCommand("MF.DummyCmd");
            BeginNativeAction(
                "TCAS traffic mode",
                state => state.TcasMode.HasValue
                         && Math.Abs(state.TcasMode.Value - desiredPosition) < 0.1,
                true,
                TimeSpan.FromSeconds(10));
            return;
        }

        if (!ValidateNativeInputAction("TCAS traffic mode", requireStationary: false))
        {
            return;
        }
        if (_state!.TcasMode.HasValue
            && Math.Abs(_state.TcasMode.Value - desiredPosition) < 0.1)
        {
            AppendDashboardLog("TCAS traffic mode already TA/RA.");
            FinishOneShot();
            return;
        }

        var stateEvent = desiredPosition switch
        {
            0 => "AIRLINER_TCAS_STBY_STBY",
            1 => "AIRLINER_TCAS_STBY_TA",
            2 => "AIRLINER_TCAS_STBY_TARA",
            _ => throw new ArgumentOutOfRangeException(nameof(desiredPosition))
        };
        SendMobiFlightCommand($"MF.SimVars.Set.(>B:{stateEvent})");
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            "TCAS traffic mode",
            state => state.TcasMode.HasValue
                     && Math.Abs(state.TcasMode.Value - desiredPosition) < 0.1,
            true);
    }

    private void SetTcasAltitudeReporting(bool desiredOn)
    {
        if (_state?.IsFlyByWireA320Neo == true)
        {
            if (_state == null || !_mobiFlightRuntimeReady)
            {
                AppendDashboardLog("TCAS altitude reporting blocked: FBW runtime adapter is unavailable.");
                FinishOneShot(4);
                return;
            }
            if (_state.TcasAltitudeReportingOn.HasValue
                && _state.TcasAltitudeReportingOn.Value == desiredOn)
            {
                AppendDashboardLog(
                    $"TCAS altitude reporting already {desiredOn.ToOnOff()}.");
                FinishOneShot();
                return;
            }

            var value = desiredOn ? 1 : 0;
            SendMobiFlightCommand(
                $"MF.SimVars.Set.{value} (>L:A32NX_SWITCH_ATC_ALT)");
            _fbwCommandedTcasAltitudeReporting = desiredOn;
            _fbwCommandedTcasAltitudeReportingUtc = DateTime.UtcNow;
            SendMobiFlightCommand("MF.DummyCmd");
            BeginNativeAction(
                "TCAS altitude reporting",
                state => state.TcasAltitudeReportingOn.HasValue
                         && state.TcasAltitudeReportingOn.Value == desiredOn,
                desiredOn,
                TimeSpan.FromSeconds(10));
            return;
        }

        if (!ValidateNativeInputAction("TCAS altitude reporting", requireStationary: false))
        {
            return;
        }
        if (_state!.TcasAltitudeReportingOn.HasValue
            && _state.TcasAltitudeReportingOn.Value == desiredOn)
        {
            AppendDashboardLog(
                $"TCAS altitude reporting already {desiredOn.ToOnOff()}.");
            FinishOneShot();
            return;
        }

        SendMobiFlightCommand(
            "MF.SimVars.Set.(L:INI_TCAS_ALT_STATE) ! (>L:INI_TCAS_ALT_STATE)");
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            "TCAS altitude reporting",
            state => state.TcasAltitudeReportingOn.HasValue
                     && state.TcasAltitudeReportingOn.Value == desiredOn,
            desiredOn);
    }

    private void SetGearUp()
    {
        if (_simConnect == null || _state == null)
        {
            AppendDashboardLog("Landing gear UP blocked: simulator state is unavailable.");
            FinishOneShot(3);
            return;
        }
        if (_state.OnGround || _state.VerticalSpeedFeetPerMinute <= 100)
        {
            AppendDashboardLog("Landing gear UP blocked: positive airborne climb is required.");
            FinishOneShot(3);
            return;
        }
        if (!_state.GearHandleDown)
        {
            AppendDashboardLog("Landing gear already UP.");
            FinishOneShot();
            return;
        }

        SendMobiFlightCommand(_state.IsFlyByWireA320Neo
            ? "MF.SimVars.Set.(>K:GEAR_UP)"
            : "MF.SimVars.Set.(>B:LANDING_GEAR_Gear_Inc) " +
              "'INI.GEAR_UP' (>F:KeyEvent)");
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            "Landing gear",
            state => !state.GearHandleDown,
            true,
            TimeSpan.FromSeconds(12));
    }

    private void SetGearDown()
    {
        if (_simConnect == null || _state == null)
        {
            AppendDashboardLog("Landing gear DOWN blocked: simulator state is unavailable.");
            FinishOneShot(3);
            return;
        }
        if (_state.GearHandleDown)
        {
            AppendDashboardLog("Landing gear already DOWN.");
            FinishOneShot();
            return;
        }

        SendMobiFlightCommand(_state.IsFlyByWireA320Neo
            ? "MF.SimVars.Set.(>K:GEAR_DOWN)"
            : "MF.SimVars.Set.(>B:LANDING_GEAR_Gear_Dec) " +
              "'INI.GEAR_DOWN' (>F:KeyEvent)");
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            "Landing gear",
            state => state.GearHandleDown,
            true,
            TimeSpan.FromSeconds(15));
    }

    private void SetGroundSpoilersDisarmed()
    {
        if (_simConnect == null || _state == null || !_state.IsSupportedA320)
        {
            AppendDashboardLog("Ground spoilers DISARM blocked: simulator state is unavailable.");
            FinishOneShot(3);
            return;
        }
        if (!_state.GroundSpoilersArmed)
        {
            AppendDashboardLog("Ground spoilers already DISARMED.");
            FinishOneShot();
            return;
        }
        if (_state.IsFlyByWireA320Neo)
        {
            SendMobiFlightCommand("MF.SimVars.Set.0 (>K:SPOILERS_ARM_SET)");
            _fbwCommandedSpoilersArmed = false;
            _fbwCommandedSpoilersArmedUtc = DateTime.UtcNow;
        }
        else
        {
            SendMobiFlightCommand(
                "MF.SimVars.Set.0 'INI.SPOILERS_SET' (>F:KeyEvent) " +
                "'INI.SPOILERS_ARM_OFF' (>F:KeyEvent) " +
                "(>B:AIRLINER_SPEEDBRAKE_Set)");
        }
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            "Ground spoilers",
            state => !state.GroundSpoilersArmed,
            false);
    }

    private void SetAltimetersStandard()
    {
        if (_simConnect == null || _state == null)
        {
            AppendDashboardLog("Altimeters STD blocked: simulator state is unavailable.");
            FinishOneShot(3);
            return;
        }
        if (_state.IndicatedAltitudeFeet < _settings.TransitionAltitudeFeet)
        {
            AppendDashboardLog(
                $"Altimeters STD blocked: transition altitude is {_settings.TransitionAltitudeFeet} feet.");
            FinishOneShot(3);
            return;
        }
        if (_state.CaptainAltimeterStandard && _state.FirstOfficerAltimeterStandard)
        {
            AppendDashboardLog("Captain and First Officer altimeters already STD.");
            FinishOneShot();
            return;
        }

        if (!_state.CaptainAltimeterStandard)
        {
            SendInputEventPulse(10580266766214260807UL);
        }
        if (!_state.FirstOfficerAltimeterStandard)
        {
            SendInputEventPulse(3529555828385965624UL);
        }
        SendMobiFlightCommand(
            "MF.SimVars.Set.1 (>K:BAROMETRIC_STD_PRESSURE) " +
            "2 (>K:BAROMETRIC_STD_PRESSURE)");
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            "Captain and First Officer altimeters STD",
            state => state.CaptainAltimeterStandard
                     && state.FirstOfficerAltimeterStandard,
            true,
            TimeSpan.FromSeconds(10));
    }

    private void SetWeatherRadarPwsSelector(int desiredPosition)
    {
        if (_state?.IsFlyByWireA320Neo == true)
        {
            if (_simConnect == null
                || !_mobiFlightReady
                || !_mobiFlightRuntimeReady
                || !_state.WeatherRadarPwsSelectorPosition.HasValue)
            {
                AppendDashboardLog("WXR/PWS selector blocked: FBW runtime readback is unavailable.");
                FinishOneShot(4);
                return;
            }

            if (Math.Abs(_state.WeatherRadarPwsSelectorPosition.Value - desiredPosition) < 0.1)
            {
                AppendDashboardLog($"WXR/PWS selector already at position {desiredPosition}.");
                FinishOneShot();
                return;
            }

            SendMobiFlightCommand(
                $"MF.SimVars.Set.{desiredPosition} (>L:A32NX_SWITCH_RADAR_PWS_POSITION)");
            _fbwCommandedWeatherRadarPwsSelector = desiredPosition;
            _fbwCommandedWeatherRadarPwsSelectorUtc = DateTime.UtcNow;
            SendMobiFlightCommand("MF.DummyCmd");
            BeginNativeAction(
                "WXR/PWS selector",
                state => state.WeatherRadarPwsSelectorPosition.HasValue
                         && Math.Abs(
                             state.WeatherRadarPwsSelectorPosition.Value
                             - desiredPosition) < 0.1,
                desiredPosition != 0,
                TimeSpan.FromSeconds(10));
            return;
        }

        if (!ValidateNativeInputAction(
                "WXR/PWS selector",
                requireCompleteNativeState: true,
                requireStationary: false))
        {
            return;
        }

        var nativePosition = desiredPosition switch
        {
            0 => 1, // physical OFF
            1 => 0, // physical mode 1
            2 => 2, // physical mode 2
            _ => throw new ArgumentOutOfRangeException(
                nameof(desiredPosition),
                desiredPosition,
                "WXR/PWS selector position must be OFF, 1, or 2.")
        };

        _simConnect!.SetInputEvent(14794713865952973521UL, (double)nativePosition);
        BeginNativeAction(
            "WXR/PWS selector",
            state => state.WeatherRadarPwsSelectorPosition.HasValue
                     && Math.Abs(
                         state.WeatherRadarPwsSelectorPosition.Value
                         - nativePosition) < 0.1,
            desiredPosition != 0,
            TimeSpan.FromSeconds(10));
    }

    private void SetNoseLightSelector(int desiredPosition)
    {
        if (_state?.IsFlyByWireA320Neo == true)
        {
            if (_simConnect == null || !_mobiFlightReady)
            {
                AppendDashboardLog("Nose light selector blocked: simulator state is unavailable.");
                FinishOneShot(3);
                return;
            }
            if (_state.NoseLightSelectorPosition.HasValue
                && Math.Abs(_state.NoseLightSelectorPosition.Value - desiredPosition) < 0.1)
            {
                AppendDashboardLog($"Nose light already at position {desiredPosition}.");
                FinishOneShot();
                return;
            }

            var calculatorCode = desiredPosition switch
            {
                0 => "(A:CIRCUIT SWITCH ON:20, Bool) ! if{ 20 (>K:ELECTRICAL_CIRCUIT_TOGGLE) } (A:CIRCUIT SWITCH ON:17, Bool) ! if{ 17 (>K:ELECTRICAL_CIRCUIT_TOGGLE) }",
                1 => "0 (>L:LIGHTING_LANDING_1) (A:CIRCUIT SWITCH ON:17, Bool) if{ 17 (>K:ELECTRICAL_CIRCUIT_TOGGLE) } (A:CIRCUIT SWITCH ON:20, Bool) ! if{ 20 (>K:ELECTRICAL_CIRCUIT_TOGGLE) }",
                2 => "(A:CIRCUIT SWITCH ON:17, Bool) if{ 17 (>K:ELECTRICAL_CIRCUIT_TOGGLE) } (A:CIRCUIT SWITCH ON:20, Bool) if{ 20 (>K:ELECTRICAL_CIRCUIT_TOGGLE) }",
                _ => throw new ArgumentOutOfRangeException(nameof(desiredPosition))
            };

            SendMobiFlightCommand($"MF.SimVars.Set.{calculatorCode}");
            _fbwCommandedNoseLightSelector = desiredPosition;
            _fbwCommandedNoseLightSelectorUtc = DateTime.UtcNow;
            SendMobiFlightCommand("MF.DummyCmd");
            BeginNativeAction(
                "Nose light selector",
                state => state.NoseLightSelectorPosition.HasValue
                         && Math.Abs(
                             state.NoseLightSelectorPosition.Value - desiredPosition) < 0.1,
                desiredPosition != 2);
            return;
        }

        if (!ValidateNativeInputAction("Nose light selector", false, false))
        {
            return;
        }
        if (_state!.NoseLightSelectorPosition.HasValue
            && Math.Abs(_state.NoseLightSelectorPosition.Value - desiredPosition) < 0.1)
        {
            AppendDashboardLog($"Nose light already at position {desiredPosition}.");
            FinishOneShot();
            return;
        }

        var stateEvent = desiredPosition switch
        {
            0 => "AIRLINER_LT_TAXI_State1",
            1 => "AIRLINER_LT_TAXI_State2",
            2 => "AIRLINER_LT_TAXI_State3",
            _ => throw new ArgumentOutOfRangeException(nameof(desiredPosition))
        };
        SendMobiFlightCommand($"MF.SimVars.Set.(>B:{stateEvent})");
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            "Nose light selector",
            state => state.NoseLightSelectorPosition.HasValue
                     && Math.Abs(
                         state.NoseLightSelectorPosition.Value - desiredPosition) < 0.1,
            desiredPosition != 0);
    }

    private void SetLandingLightSelectors(int desiredPosition)
    {
        if (_state?.IsFlyByWireA320Neo == true)
        {
            if (_simConnect == null || !_mobiFlightReady)
            {
                AppendDashboardLog("Landing lights blocked: simulator state is unavailable.");
                FinishOneShot(3);
                return;
            }

            bool VerifyFbw(AircraftState state) =>
                state.LeftLandingLightSelectorPosition.HasValue
                && state.RightLandingLightSelectorPosition.HasValue
                && Math.Abs(state.LeftLandingLightSelectorPosition.Value - desiredPosition) < 0.1
                && Math.Abs(state.RightLandingLightSelectorPosition.Value - desiredPosition) < 0.1;

            if (VerifyFbw(_state))
            {
                AppendDashboardLog(
                    $"Landing light selectors already at position {desiredPosition}.");
                FinishOneShot();
                return;
            }

            var calculatorCode = desiredPosition switch
            {
                0 => "0 (>L:LIGHTING_LANDING_2) 0 (>L:LANDING_2_RETRACTED) (A:CIRCUIT SWITCH ON:18, Bool) ! if{ 18 (>K:ELECTRICAL_CIRCUIT_TOGGLE) } 0 (>L:LIGHTING_LANDING_3) 0 (>L:LANDING_3_RETRACTED) (A:CIRCUIT SWITCH ON:19, Bool) ! if{ 19 (>K:ELECTRICAL_CIRCUIT_TOGGLE) }",
                1 => "(A:CIRCUIT SWITCH ON:18, Bool) if{ 18 (>K:ELECTRICAL_CIRCUIT_TOGGLE) } (A:CIRCUIT SWITCH ON:19, Bool) if{ 19 (>K:ELECTRICAL_CIRCUIT_TOGGLE) }",
                2 => "2 (>L:LIGHTING_LANDING_2) 1 (>L:LANDING_2_RETRACTED) (A:CIRCUIT SWITCH ON:18, Bool) if{ 18 (>K:ELECTRICAL_CIRCUIT_TOGGLE) } 2 (>L:LIGHTING_LANDING_3) 1 (>L:LANDING_3_RETRACTED) (A:CIRCUIT SWITCH ON:19, Bool) if{ 19 (>K:ELECTRICAL_CIRCUIT_TOGGLE) }",
                _ => throw new ArgumentOutOfRangeException(nameof(desiredPosition))
            };

            SendMobiFlightCommand($"MF.SimVars.Set.{calculatorCode}");
            _fbwCommandedLandingLightSelector = desiredPosition;
            _fbwCommandedLandingLightSelectorUtc = DateTime.UtcNow;
            SendMobiFlightCommand("MF.DummyCmd");
            BeginNativeAction(
                "Landing lights",
                VerifyFbw,
                desiredPosition == 0,
                TimeSpan.FromSeconds(10),
                FormatLandingLightPosition(desiredPosition));
            return;
        }

        if (!ValidateNativeInputAction("Landing lights", false, false))
        {
            return;
        }
        bool Verify(AircraftState state) =>
            state.LeftLandingLightSelectorPosition.HasValue
            && state.RightLandingLightSelectorPosition.HasValue
            && Math.Abs(
                state.LeftLandingLightSelectorPosition.Value - desiredPosition) < 0.1
            && Math.Abs(
                state.RightLandingLightSelectorPosition.Value - desiredPosition) < 0.1;
        if (Verify(_state!))
        {
            AppendDashboardLog(
                $"Landing light selectors already at position {desiredPosition}.");
            FinishOneShot();
            return;
        }

        var leftEvent = $"AIRLINER_LT_LDG_L_State{desiredPosition + 1}";
        var rightEvent = $"AIRLINER_LT_LDG_R_State{desiredPosition + 1}";
        SendMobiFlightCommand(
            $"MF.SimVars.Set.(>B:{leftEvent}) (>B:{rightEvent})");
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            "Landing lights",
            Verify,
            desiredPosition == 2,
            desiredLabel: FormatLandingLightPosition(desiredPosition));
    }

    private static string FormatLandingLightPosition(int position) =>
        position switch
        {
            0 => "ON",
            1 => "OFF",
            2 => "RETRACTED",
            _ => position.ToString()
        };

    private void UpdateCruiseSeatbeltMonitoring()
    {
        if (!_cruiseSeatbeltMonitoring
            || _state == null
            || _simConnect == null
            || !_state.IsSupportedA320
            || _state.OnGround)
        {
            return;
        }

        var turbulenceDetected =
            _state.GForce < 0.85
            || _state.GForce > 1.15;
        if (turbulenceDetected)
        {
            _smoothCruiseSinceUtc = null;
            if (DateTime.UtcNow >= _nextCruiseSeatbeltCommandUtc
                && _state.SeatbeltSelectorPosition.HasValue
                && Math.Abs(_state.SeatbeltSelectorPosition.Value) >= 0.1
                && _pendingNativeAction == null)
            {
                _nextCruiseSeatbeltCommandUtc = DateTime.UtcNow.AddSeconds(15);
                AppendDashboardLog(
                    $"Cruise turbulence detected ({_state.GForce:F2} G); seatbelts ON.");
                SetSignSelector(SignSelector.Seatbelts, 0);
            }
            return;
        }

        if (Math.Abs(_state.VerticalSpeedFeetPerMinute) > 500)
        {
            _smoothCruiseSinceUtc = null;
            return;
        }

        _smoothCruiseSinceUtc ??= DateTime.UtcNow;
        if (DateTime.UtcNow - _smoothCruiseSinceUtc.Value < TimeSpan.FromMinutes(5)
            || DateTime.UtcNow < _nextCruiseSeatbeltCommandUtc
            || !_state.SeatbeltSelectorPosition.HasValue
            || Math.Abs(_state.SeatbeltSelectorPosition.Value - 2) < 0.1
            || _pendingNativeAction != null)
        {
            return;
        }

        _nextCruiseSeatbeltCommandUtc = DateTime.UtcNow.AddSeconds(15);
        AppendDashboardLog("Cruise smooth for five minutes; seatbelts OFF.");
        SetSignSelector(SignSelector.Seatbelts, 2);
    }

    private void SetFireTestPressed(
        FireTestSystem system,
        ulong inputEventHash,
        bool pressed)
    {
        if (system == FireTestSystem.Apu)
        {
            // Exact Behavior Viewer Mouserect Lock/Unlock behavior:
            // Lock writes 1; Unlock writes 0. One write sustains the held state.
            SendMobiFlightCommand(
                $"MF.SimVars.Set.{(pressed ? 1 : 0)} (>L:INI_APU_FIRE_TEST)");
            SendMobiFlightCommand("MF.DummyCmd");
            return;
        }

        var testLVar = system switch
        {
            FireTestSystem.Engine1 => "INI_ENG1_FIRE_TEST",
            FireTestSystem.Engine2 => "INI_ENG2_FIRE_TEST",
            _ => throw new ArgumentOutOfRangeException(nameof(system))
        };
        SendMobiFlightCommand(
            $"MF.SimVars.Set.{(pressed ? 1 : 0)} (>L:{testLVar})");
        SendMobiFlightCommand("MF.DummyCmd");
    }

    private void SetFlyByWireFireTestPressed(FireTestSystem system, bool pressed)
    {
        var value = pressed ? 1 : 0;
        switch (system)
        {
            case FireTestSystem.Apu:
                SendMobiFlightCommand($"MF.SimVars.Set.{value} (>L:A32NX_FIRE_TEST_APU)");
                SendMobiFlightCommand($"MF.SimVars.Set.{value} (>L:A32NX_FIRE_TEST_APU, Bool)");
                SendMobiFlightCommand($"MF.SimVars.Set.{value} (>L:FIRE_TEST_APU)");
                SendMobiFlightCommand($"MF.SimVars.Set.{value} (>L:FIRE_TEST_APU, Bool)");
                break;
            case FireTestSystem.Engine1:
                SendMobiFlightCommand($"MF.SimVars.Set.{value} (>L:A32NX_FIRE_TEST_ENG1)");
                SendMobiFlightCommand($"MF.SimVars.Set.{value} (>L:A32NX_FIRE_TEST_ENG1, Bool)");
                break;
            case FireTestSystem.Engine2:
                SendMobiFlightCommand($"MF.SimVars.Set.{value} (>L:A32NX_FIRE_TEST_ENG2)");
                SendMobiFlightCommand($"MF.SimVars.Set.{value} (>L:A32NX_FIRE_TEST_ENG2, Bool)");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(system));
        }

        SendMobiFlightCommand("MF.DummyCmd");
        AppLog.Write($"Executed FBW {FormatFireTestName(system)} {(pressed ? "press" : "release")} command.");
    }

    private void PulseNativeCommand(
        string name,
        string commandLVar,
        bool desiredOn,
        Func<AircraftState, bool> verify,
        TimeSpan? timeout = null)
    {
        if (!ValidateNativeInputAction(name))
        {
            return;
        }
        if (verify(_state!))
        {
            AppendDashboardLog($"{name} already {desiredOn.ToOnOff()}.");
            FinishOneShot();
            return;
        }

        SendNativePulse(commandLVar);
        BeginNativeAction(name, verify, desiredOn, timeout);
    }

    private void SetApuStarter(bool desiredOn)
    {
        if (!ValidateNativeInputAction("APU start"))
        {
            return;
        }
        if (desiredOn && _state!.ApuAvailable)
        {
            AppendDashboardLog("APU already available.");
            FinishOneShot();
            return;
        }
        if (!desiredOn)
        {
            AppendDashboardLog("APU START OFF is not a supported system event.");
            FinishOneShot(4);
            return;
        }

        TransmitSystemEvent(CopilotEvent.StartApu, 0, 0);
        BeginNativeAction(
            "APU start",
            state => state.ApuAvailable,
            true,
            TimeSpan.FromSeconds(60));
    }

    private void SetIndexedSystemEvent(
        string name,
        CopilotEvent eventId,
        uint index,
        bool desiredOn,
        Func<AircraftState, bool> verify)
    {
        if (!ValidateNativeInputAction(name))
        {
            return;
        }
        if (verify(_state!))
        {
            AppendDashboardLog($"{name} already {desiredOn.ToOnOff()}.");
            FinishOneShot();
            return;
        }

        TransmitSystemEvent(eventId, index, desiredOn ? 1u : 0u);
        BeginNativeAction(name, verify, desiredOn);
    }

    private void TransmitSystemEvent(
        CopilotEvent eventId,
        uint data0,
        uint data1)
    {
        _simConnect!.TransmitClientEvent_EX1(
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            eventId,
            Priority.Highest,
            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY,
            data0,
            data1,
            0,
            0,
            0);
    }

    private bool ValidateAfterStartAction(string name)
    {
        if (_simConnect == null
            || _state == null
            || !_state.IsSupportedA320
            || !_state.OnGround
            || !_mobiFlightReady
            || !_mobiFlightRuntimeReady)
        {
            AppendDashboardLog($"{name} blocked: aircraft or native readback is unavailable.");
            FinishOneShot(4);
            return false;
        }
        return true;
    }

    private void SetGroundSpoilersArmed()
    {
        if (_state?.IsFlyByWireA320Neo == true)
        {
            if (_simConnect == null || !_mobiFlightRuntimeReady)
            {
                AppendDashboardLog("Ground spoilers blocked: FBW runtime adapter is unavailable.");
                FinishOneShot(4);
                return;
            }
        }
        else if (!ValidateNativeInputAction(
                     "Ground spoilers",
                     requireCompleteNativeState: false,
                     requireStationary: false))
        {
            return;
        }
        if (_state!.GroundSpoilersArmed)
        {
            AppendDashboardLog("Ground spoilers already ARMED.");
            FinishOneShot();
            return;
        }

        if (_state.IsFlyByWireA320Neo)
        {
            SendMobiFlightCommand("MF.SimVars.Set.1 (>K:SPOILERS_ARM_SET)");
            _fbwCommandedSpoilersArmed = true;
            _fbwCommandedSpoilersArmedUtc = DateTime.UtcNow;
        }
        else
        {
            SendMobiFlightCommand(
                "MF.SimVars.Set.0 'INI.SPOILERS_SET' (>F:KeyEvent) " +
                "'INI.SPOILERS_ARM_ON' (>F:KeyEvent) " +
                "(>B:AIRLINER_SPEEDBRAKE_Set)");
        }
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction("Ground spoilers", state => state.GroundSpoilersArmed, true);
    }

    private void SetTakeoffFlaps(uint handleIndex)
    {
        if (!ValidateAfterStartAction("Takeoff flaps"))
        {
            return;
        }
        if (_state!.FlapsAtDetent((int)handleIndex))
        {
            AppendDashboardLog($"Takeoff flaps already CONFIG {handleIndex}.");
            FinishOneShot();
            return;
        }

        SendMobiFlightCommand(
            "MF.SimVars.Set.16384 (A:FLAPS NUM HANDLE POSITIONS, Number) / " +
            "(>B:HANDLING_Flaps_Inc)");
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            "Takeoff flaps",
            state => state.FlapsAtDetent((int)handleIndex),
            true);
    }

    private void SetFlapsExtended(uint desiredPosition)
    {
        if (_simConnect == null || _state == null || !_state.IsSupportedA320)
        {
            AppendDashboardLog("Flap extension blocked: simulator state is unavailable.");
            FinishOneShot(3);
            return;
        }
        if (_state.FlapsAtDetent((int)desiredPosition))
        {
            AppendDashboardLog(
                $"Flaps already CONFIG {desiredPosition} " +
                $"(handle index {_state.FlapsHandleIndex:F2}).");
            FinishOneShot();
            return;
        }
        if (!_state.IsIniBuildsA321Lr
            && _state.FlapsHandleIndex > desiredPosition)
        {
            AppendDashboardLog(
                $"Flap extension blocked: current position {_state.FlapsHandleIndex:F0} exceeds target {desiredPosition}.");
            FinishOneShot(3);
            return;
        }
        var maximumCommandSpeed = GetFlapCommandMaximumSpeed(desiredPosition);
        if (_state.IsIniBuildsA321Lr
            && _state.IndicatedAirspeedKnots > maximumCommandSpeed)
        {
            AppendDashboardLog(
                $"Flaps CONFIG {desiredPosition} waiting: IAS {_state.IndicatedAirspeedKnots:F0} kt exceeds safe command speed {maximumCommandSpeed} kt.");
            FinishOneShot(3);
            return;
        }

        if (_state.IsFlyByWireA320Neo)
        {
            SendMobiFlightCommand(
                $"MF.SimVars.Set.{desiredPosition} (>L:A32NX_FLAPS_HANDLE_INDEX)");
        }
        else
        {
            SendMobiFlightCommand(
                "MF.SimVars.Set.16384 (A:FLAPS NUM HANDLE POSITIONS, Number) / " +
                "(>B:HANDLING_Flaps_Inc)");
        }
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            $"Flaps CONFIG {desiredPosition}",
            state => state.FlapsAtDetent((int)desiredPosition),
            true,
            TimeSpan.FromSeconds(15));
    }

    private int GetFlapCommandMaximumSpeed(uint desiredPosition) =>
        desiredPosition switch
        {
            1 => _state?.EffectiveApproachFlaps1SpeedKnots
                 ?? _settings.ApproachFlaps1SpeedKnots,
            2 => _state?.EffectiveApproachFlaps2SpeedKnots
                 ?? _settings.ApproachFlaps2SpeedKnots,
            3 => _state?.EffectiveApproachFlaps3SpeedKnots
                 ?? _settings.ApproachLandingConfigSpeedKnots,
            4 => _state?.EffectiveApproachFlapsFullSpeedKnots
                 ?? _settings.ApproachLandingConfigSpeedKnots,
            _ => 250
        };

    private void SetFlapsClean()
    {
        if (_simConnect == null || _state == null || !_state.IsSupportedA320)
        {
            AppendDashboardLog("Flaps retraction blocked: simulator state is unavailable.");
            FinishOneShot(3);
            return;
        }
        if (_state.FlapsAtDetent(0))
        {
            AppendDashboardLog("Flaps already CLEAN.");
            FinishOneShot();
            return;
        }
        if (!_state.OnGround
            && _state.AltitudeAboveGroundFeet < 400)
        {
            AppendDashboardLog(
                "Flaps retraction blocked: requires at least 400 feet AGL.");
            FinishOneShot(3);
            return;
        }

        if (_state.IsFlyByWireA320Neo)
        {
            SendMobiFlightCommand(
                "MF.SimVars.Set.0 (>L:A32NX_FLAPS_HANDLE_INDEX)");
        }
        else
        {
            SendMobiFlightCommand(_state.OnGround
                ? "MF.SimVars.Set.0 (>B:HANDLING_Flaps_Set)"
                : "MF.SimVars.Set.16384 (A:FLAPS NUM HANDLE POSITIONS, Number) / " +
                  "(>B:HANDLING_Flaps_Dec)");
        }
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            "Flaps CLEAN",
            state => state.FlapsAtDetent(0),
            true,
            TimeSpan.FromSeconds(15));
    }

    private void SetAutobrake(int desiredLevel, string label)
    {
        if (_simConnect == null
            || _state == null
            || !_state.IsSupportedA320
            || !_mobiFlightReady
            || !_mobiFlightRuntimeReady
            || !_state.AutobrakeLevel.HasValue)
        {
            AppendDashboardLog(
                $"Autobrake {label} blocked: aircraft or native readback is unavailable.");
            FinishOneShot(4);
            return;
        }
        if (Math.Abs(_state.AutobrakeLevel.Value - desiredLevel) < 0.1)
        {
            AppendDashboardLog($"Autobrake already {label}.");
            FinishOneShot();
            return;
        }

        if (_state.IsFlyByWireA320Neo)
        {
            SendMobiFlightCommand(
                $"MF.SimVars.Set.{desiredLevel} (>L:A32NX_AUTOBRAKES_ARMED_MODE_SET)");
            _fbwCommandedAutobrakeLevel = desiredLevel;
            _fbwCommandedAutobrakeLevelUtc = DateTime.UtcNow;
        }
        else
        {
            SendMobiFlightCommand(
                $"MF.SimVars.Set.{desiredLevel} (>L:INI_AUTOBRAKE_LEVEL)");
        }
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            $"Autobrake {label}",
            state => state.AutobrakeLevel.HasValue
                     && Math.Abs(
                         state.AutobrakeLevel.Value - desiredLevel) < 0.1,
            true);
    }

    private void SendNativePulse(string commandLVar)
    {
        SendMobiFlightCommand($"MF.SimVars.Set.1 (>L:{commandLVar})");
        SendMobiFlightCommand("MF.DummyCmd");
        var releaseTimer = new System.Windows.Forms.Timer { Interval = 500 };
        releaseTimer.Tick += (_, _) =>
        {
            releaseTimer.Stop();
            SendMobiFlightCommand($"MF.SimVars.Set.0 (>L:{commandLVar})");
            SendMobiFlightCommand("MF.DummyCmd");
            _nativePulseTimers.Remove(releaseTimer);
            releaseTimer.Dispose();
        };
        _nativePulseTimers.Add(releaseTimer);
        releaseTimer.Start();
    }

    private bool ValidateNativeInputAction(
        string name,
        bool requireCompleteNativeState = true,
        bool requireStationary = true)
    {
        if (_simConnect == null
            || _state == null
            || !_mobiFlightReady
            || !_mobiFlightRuntimeReady
            || (requireCompleteNativeState && !NativeStateReady))
        {
            AppendDashboardLog($"{name} blocked: native aircraft readback is unavailable.");
            FinishOneShot(4);
            return false;
        }
        if (!_state.IsIniBuildsA320Family)
        {
            AppendDashboardLog($"{name} blocked: the loaded aircraft is not a supported iniBuilds A320-family aircraft.");
            FinishOneShot(3);
            return false;
        }
        if (requireStationary
            && (!_state.OnGround || _state.GroundSpeedKnots > 0.5))
        {
            AppendDashboardLog($"{name} blocked: aircraft must be stationary on the ground.");
            FinishOneShot(3);
            return false;
        }
        return true;
    }

    private void BeginNativeAction(
        string name,
        Func<AircraftState, bool> verify,
        bool desiredOn,
        TimeSpan? timeout = null,
        string? desiredLabel = null)
    {
        _pendingNativeAction = new PendingNativeAction(
            name,
            verify,
            desiredOn,
            desiredLabel ?? desiredOn.ToOnOff(),
            DateTime.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(8)));
        AppendDashboardLog(
            $"{name} command sent: {_pendingNativeAction.DesiredLabel}; awaiting native readback.");
    }

    private void SetExternalPower(bool desiredOn)
    {
        if (_simConnect == null || _state == null)
        {
            Console.Error.WriteLine("External-power procedure blocked: aircraft state is unavailable.");
            FinishOneShot(3);
            return;
        }

        if (_state.IsFlyByWireA320Neo)
        {
            if (desiredOn && !_state.ExternalPowerAvailable)
            {
                AppendDashboardLog("External power blocked: external power is not available.");
                FinishOneShot(3);
                return;
            }

            SetFlyByWireBoolLVarAction(
                "External power",
                "A32NX_OVHD_ELEC_EXT_PWR_PB_IS_ON",
                desiredOn,
                state => state.ExternalPowerOn == desiredOn);
            return;
        }

        var blockedReason = ValidateExternalPowerProcedure(_state, desiredOn);
        if (blockedReason != null)
        {
            Console.Error.WriteLine($"External-power procedure blocked: {blockedReason}");
            FinishOneShot(3);
            return;
        }

        if (_state.ExternalPowerOn == desiredOn)
        {
            Console.WriteLine($"External power is already {(desiredOn ? "ON" : "OFF")}.");
            FinishOneShot();
            return;
        }

        _simConnect.TransmitClientEvent_EX1(
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            CopilotEvent.SetExternalPower,
            Priority.Highest,
            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY,
            1,
            desiredOn ? 1u : 0u,
            0,
            0,
            0);

        _pendingProcedure = new PendingExternalPowerProcedure(
            desiredOn,
            DateTime.UtcNow.AddSeconds(5));
        Console.WriteLine($"External power command sent: {(desiredOn ? "ON" : "OFF")}; awaiting readback.");
    }

    private static string? ValidateExternalPowerProcedure(AircraftState state, bool desiredOn)
    {
        if (!state.IsIniBuildsA320Family)
        {
            return "the loaded aircraft is not a supported iniBuilds A320-family aircraft.";
        }

        if (!state.OnGround || state.GroundSpeedKnots > 0.5)
        {
            return "aircraft must be stationary on the ground.";
        }

        if (!state.EnginesOff)
        {
            return "engines must be off for this initial procedure.";
        }

        if (desiredOn && !state.ExternalPowerAvailable)
        {
            return "external power is not available.";
        }

        if (!desiredOn
            && state.EnginesOff
            && !(state.ApuAvailable && state.ApuGeneratorSwitchOn))
        {
            return "native APU availability and generator-on state are required before disconnect.";
        }

        return null;
    }

    private void VerifyPendingProcedure()
    {
        if (_pendingProcedure == null || _state == null)
        {
            VerifyPendingBeaconProcedure();
            VerifyPendingNavLogoSelectorProcedure();
            VerifyPendingBatteryProcedure();
            VerifyPendingNativeAction();
            return;
        }

        if (_state.ExternalPowerOn == _pendingProcedure.DesiredOn)
        {
            Console.WriteLine($"External power verified {_pendingProcedure.DesiredOn.ToOnOff()}.");
            _pendingProcedure = null;
            FinishOneShot();
            return;
        }

        if (DateTime.UtcNow >= _pendingProcedure.DeadlineUtc)
        {
            var message =
                $"External power verification failed; aircraft still reports {_state.ExternalPowerOn.ToOnOff()}.";
            Console.Error.WriteLine(message);
            RecordDiagnosticFailure(
                "External power verification failed",
                new[]
                {
                    $"Expected: {_pendingProcedure.DesiredOn.ToOnOff()}",
                    $"Actual: {_state.ExternalPowerOn.ToOnOff()}"
                });
            _pendingProcedure = null;
            FinishOneShot(4);
        }

        VerifyPendingBeaconProcedure();
        VerifyPendingNavLogoSelectorProcedure();
        VerifyPendingBatteryProcedure();
        VerifyPendingNativeAction();
    }

    private void VerifyPendingBeaconProcedure()
    {
        if (_pendingBeaconProcedure == null || _state == null)
        {
            return;
        }

        if (_state.BeaconOn == _pendingBeaconProcedure.DesiredOn)
        {
            Console.WriteLine($"Beacon verified {_pendingBeaconProcedure.DesiredOn.ToOnOff()}.");
            _pendingBeaconProcedure = null;
            FinishOneShot();
            return;
        }

        if (DateTime.UtcNow >= _pendingBeaconProcedure.DeadlineUtc)
        {
            var message =
                $"Beacon verification failed; aircraft still reports {_state.BeaconOn.ToOnOff()}.";
            Console.Error.WriteLine(message);
            RecordDiagnosticFailure(
                "Beacon verification failed",
                new[]
                {
                    $"Expected: {_pendingBeaconProcedure.DesiredOn.ToOnOff()}",
                    $"Actual: {_state.BeaconOn.ToOnOff()}"
                });
            _pendingBeaconProcedure = null;
            FinishOneShot(4);
        }
    }

    private void VerifyPendingNavLogoSelectorProcedure()
    {
        if (_pendingNavLogoSelectorProcedure == null || _state == null)
        {
            return;
        }

        if (_state.IsFlyByWireA320Neo)
        {
            var desiredOff = _pendingNavLogoSelectorProcedure.DesiredPosition == 2;
            var lightsMatch = desiredOff
                ? !_state.NavigationLightsOn && !_state.LogoLightsOn
                : _state.NavigationLightsOn && _state.LogoLightsOn;
            if (lightsMatch)
            {
                AppendDashboardLog(
                    $"NAV & LOGO lights verified " +
                    $"{(desiredOff ? "OFF" : "ON")}.");
                _pendingNavLogoSelectorProcedure = null;
                FinishOneShot();
                return;
            }
        }

        if (_state.NavLogoSelectorPosition.HasValue
            && Math.Abs(
                _state.NavLogoSelectorPosition.Value
                - _pendingNavLogoSelectorProcedure.DesiredPosition) < 0.1)
        {
            AppendDashboardLog(
                $"NAV & LOGO selector verified " +
                $"{FormatNavLogoPosition(_pendingNavLogoSelectorProcedure.DesiredPosition)}.");
            _pendingNavLogoSelectorProcedure = null;
            FinishOneShot();
            return;
        }

        if (DateTime.UtcNow >= _pendingNavLogoSelectorProcedure.DeadlineUtc)
        {
            RecordDiagnosticFailure(
                "NAV & LOGO verification failed",
                new[]
                {
                    $"Expected selector: {_pendingNavLogoSelectorProcedure.DesiredPosition}",
                    $"Actual selector: {(_state.NavLogoSelectorPosition.HasValue ? _state.NavLogoSelectorPosition.Value.ToString("F0") : "unknown")}"
                });
            AppendDashboardLog(
                "NAV & LOGO verification failed; native selector reports " +
                $"{(_state.NavLogoSelectorPosition.HasValue ? _state.NavLogoSelectorPosition.Value.ToString("F0") : "unknown")}.");
            _pendingNavLogoSelectorProcedure = null;
            FinishOneShot(4);
        }
    }

    private void VerifyPendingBatteryProcedure()
    {
        if (_pendingBatteryProcedure == null || _state == null)
        {
            return;
        }

        var actual = _pendingBatteryProcedure.BatteryNumber == 1
            ? _state.Battery1On
            : _state.Battery2On;
        if (actual == _pendingBatteryProcedure.DesiredOn)
        {
            Console.WriteLine(
                $"BAT {_pendingBatteryProcedure.BatteryNumber} verified " +
                $"{_pendingBatteryProcedure.DesiredOn.ToOnOff()}.");
            AppendDashboardLog(
                $"BAT {_pendingBatteryProcedure.BatteryNumber} verified " +
                _pendingBatteryProcedure.DesiredOn.ToOnOff());
            _pendingBatteryProcedure = null;
            FinishOneShot();
            return;
        }

        if (DateTime.UtcNow >= _pendingBatteryProcedure.DeadlineUtc)
        {
            Console.Error.WriteLine(
                $"BAT {_pendingBatteryProcedure.BatteryNumber} verification failed; " +
                $"aircraft still reports {actual.ToOnOff()}.");
            RecordDiagnosticFailure(
                $"BAT {_pendingBatteryProcedure.BatteryNumber} verification failed",
                new[]
                {
                    $"Expected: {_pendingBatteryProcedure.DesiredOn.ToOnOff()}",
                    $"Actual: {actual.ToOnOff()}"
                });
            AppendDashboardLog(
                $"BAT {_pendingBatteryProcedure.BatteryNumber} verification failed");
            _pendingBatteryProcedure = null;
            FinishOneShot(4);
        }
    }

    private void VerifyPendingNativeAction()
    {
        if (_pendingNativeAction == null || _state == null)
        {
            return;
        }
        if (_pendingNativeAction.Verify(_state))
        {
            AppendDashboardLog(
                $"{_pendingNativeAction.Name} verified {_pendingNativeAction.DesiredLabel}.");
            _pendingNativeAction = null;
            FinishOneShot();
            return;
        }
        if (DateTime.UtcNow >= _pendingNativeAction.DeadlineUtc)
        {
            var message = $"{_pendingNativeAction.Name} native verification failed.";
            RecordDiagnosticFailure(
                message,
                new[]
                {
                    $"Pending action: {_pendingNativeAction.Name}",
                    $"Expected: {_pendingNativeAction.DesiredLabel}",
                    $"Deadline UTC: {_pendingNativeAction.DeadlineUtc:O}"
                });
            AppendDashboardLog(message);
            _pendingNativeAction = null;
            if (_procedureRunner.Status == ProcedureStatus.WaitingForVerification)
            {
                _procedureRunner.Fail(message);
            }
            FinishOneShot(4);
        }
    }

    private void RecordDiagnosticFailure(string summary, IEnumerable<string>? details = null)
    {
        var step = _procedureRunner.CurrentStep;
        DiagnosticLog.RecordFailure(
            summary,
            _state,
            _procedureRunner.Definition?.Name,
            step?.Id,
            step?.Label,
            details);
    }

    private void PrintStatus()
    {
        if (_state == null)
        {
            Console.WriteLine("Aircraft state unavailable.");
            return;
        }

        Console.WriteLine($"Aircraft: {_state.Title}");
        Console.WriteLine($"Ground: {_state.OnGround}; speed: {_state.GroundSpeedKnots:F1} kt; parking brake: {_state.ParkingBrakeSet.ToSetReleased()}");
        Console.WriteLine($"Engines 1/2: {_state.Engine1Running.ToOnOff()}/{_state.Engine2Running.ToOnOff()}");
        Console.WriteLine(
            $"Engine start 1 â€” starter/N1/EGT/fuel: " +
            $"{_state.Engine1StarterActive.ToOnOff()}/{_state.Engine1N1Percent:F1}%/" +
            $"{_state.Engine1EgtCelsius:F0}C/{_state.Engine1FuelFlowPph:F0} pph");
        Console.WriteLine(
            $"Engine start 2 â€” starter/N1/EGT/fuel: " +
            $"{_state.Engine2StarterActive.ToOnOff()}/{_state.Engine2N1Percent:F1}%/" +
            $"{_state.Engine2EgtCelsius:F0}C/{_state.Engine2FuelFlowPph:F0} pph");
        Console.WriteLine($"Batteries 1/2: {_state.Battery1On.ToOnOff()}/{_state.Battery2On.ToOnOff()}");
        Console.WriteLine($"External power available/on: {_state.ExternalPowerAvailable.ToYesNo()}/{_state.ExternalPowerOn.ToOnOff()}");
        Console.WriteLine($"Beacon: {_state.BeaconOn.ToOnOff()}");
        Console.WriteLine(
            $"Generic NAV/logo light flags (not selector position): " +
            $"{_state.NavigationLightsOn.ToOnOff()}/{_state.LogoLightsOn.ToOnOff()}");
        Console.WriteLine(
            $"NAV & LOGO selector: " +
            $"{(_state.NavLogoSelectorPosition.HasValue ? FormatNavLogoPosition((int)Math.Round(_state.NavLogoSelectorPosition.Value)) : "UNKNOWN")}");
        Console.WriteLine(
            $"ADIRS selectors 1/2/3: {_state.Adirs1SelectorState:F0}/" +
            $"{_state.Adirs2SelectorState:F0}/{_state.Adirs3SelectorState:F0}; " +
            $"ON BAT: {_state.AdirsOnBattery.ToOnOff()}");
        Console.WriteLine($"Crew oxygen supply: {_state.CrewOxygenOn.ToOnOff()}");
        Console.WriteLine(
            $"Strobe selector: " +
            $"{(_state.StrobeSelectorPosition.HasValue ? FormatStrobePosition((int)Math.Round(_state.StrobeSelectorPosition.Value)) : "UNKNOWN")}");
        Console.WriteLine(
            $"Fire tests APU/ENG1/ENG2 active: " +
            $"{_state.ApuFireTestActive.ToYesNo()}/" +
            $"{_state.Engine1FireTestActive.ToYesNo()}/" +
            $"{_state.Engine2FireTestActive.ToYesNo()}");
        Console.WriteLine(
            $"Signs seatbelts/no-smoking/emergency-exit: " +
            $"{FormatOptionalSignPosition(SignSelector.Seatbelts, _state.SeatbeltSelectorPosition)}/" +
            $"{FormatOptionalSignPosition(SignSelector.NoSmoking, _state.NoSmokingSelectorPosition)}/" +
            $"{FormatOptionalSignPosition(SignSelector.EmergencyExit, _state.EmergencyExitSelectorPosition)}");
        Console.WriteLine(
            $"After-start configuration â€” spoilers/flaps/autobrake: " +
            $"{(_state.GroundSpoilersArmed ? "ARMED" : "DISARMED")}/" +
            $"{_state.FlapsHandleIndex:F0}/" +
            $"{(_state.AutobrakeLevel?.ToString("F0") ?? "UNKNOWN")}");
        Console.WriteLine(
            $"Transponder ATC/TCAS/mode: " +
            $"{(_state.TransponderAtcState?.ToString("F0") ?? "UNKNOWN")}/" +
            $"{(_state.TcasMode?.ToString("F0") ?? "UNKNOWN")}/" +
            $"{(_state.TransponderModeSelectorPosition.HasValue ? FormatTransponderModePosition((int)Math.Round(_state.TransponderModeSelectorPosition.Value)) : "UNKNOWN")}");
        Console.WriteLine(
            $"TCAS altitude reporting: " +
            $"{(_state.TcasAltitudeReportingOn.HasValue ? _state.TcasAltitudeReportingOn.Value.ToOnOff() : "UNKNOWN")}");
        Console.WriteLine($"ATC IFR clearance granted: {_state.AtcClearedIfr.ToYesNo()}");
        var configuredExits = _state.Exits.Where(exit => exit.IsConfigured).ToArray();
        Console.WriteLine(
            $"Configured exits: {configuredExits.Length}; required cabin/cargo doors closed: " +
            $"{_state.RequiredDoorsClosed.ToYesNo()}");
        foreach (var exit in configuredExits)
        {
            Console.WriteLine(
                $"  Exit {exit.Index}: type {exit.Type:F0}, open {exit.OpenPercent:F0}%");
        }
        Console.WriteLine(
            $"APU master/starter/RPM: {_state.ApuMasterSwitchOn.ToOnOff()}/" +
            $"{_state.ApuStarterPercent:F1}%/{_state.ApuRpmPercent:F1}%");
        Console.WriteLine(
            $"APU native available/start/bleed: {_state.ApuAvailable.ToYesNo()}/" +
            $"{_state.ApuStartButtonOn.ToOnOff()}/{_state.ApuBleedOn.ToOnOff()}");
        Console.WriteLine($"APU native intake flap: {_state.ApuFlapPercent:F0}%");
        Console.WriteLine(
            $"APU generator switch/active/volts: {_state.ApuGeneratorSwitchOn.ToOnOff()}/" +
            $"{_state.ApuGeneratorActive.ToOnOff()}/{_state.ApuVolts:F1} V");
        Console.WriteLine($"Fuel pumps configured: {_state.FuelPumpsConfigured.ToYesNo()}");
        Console.WriteLine(
            $"Fuel pump switches L1/L2/C1/C2/R1/R2: {_state.FuelPump1State:F0}/" +
            $"{_state.FuelPump2State:F0}/{_state.FuelPump3State:F0}/" +
            $"{_state.FuelPump4State:F0}/{_state.FuelPump5State:F0}/" +
            $"{_state.FuelPump6State:F0}");
        Console.WriteLine(
            $"Flight: {_state.AltitudeAboveGroundFeet:F0} ft AGL, " +
            $"{_state.IndicatedAltitudeFeet:F0} ft indicated, " +
            $"{_state.IndicatedAirspeedKnots:F0} kt, VS {_state.VerticalSpeedFeetPerMinute:F0} fpm");
        Console.WriteLine(
            $"Transition altitude: {_state.TransitionAltitudeFeet} ft; " +
            $"baro STD CPT/FO: {_state.CaptainAltimeterStandard.ToYesNo()}/" +
            $"{_state.FirstOfficerAltimeterStandard.ToYesNo()}");
        Console.WriteLine(
            $"Configured takeoff V1/VR speeds: " +
            $"{_state.TakeoffV1SpeedKnots}/{_state.TakeoffRotateSpeedKnots} kt");
        Console.WriteLine(
            $"Configuration: flaps {_state.FlapsHandleIndex:F0}, gear {(_state.GearHandleDown ? "DOWN" : "UP")}, " +
            $"AP {_state.AutopilotMasterOn.ToOnOff()}");
    }

    private void PrintChecklist()
    {
        if (_state == null)
        {
            Console.WriteLine("Aircraft state unavailable.");
            return;
        }

        Console.WriteLine("Cockpit preparation â€” electrical power");
        foreach (var step in CockpitPreparationProcedure.Evaluate(_state))
        {
            PrintChecklistItem(step.Label, step.Complete, step.ActionHint);
        }
    }

    private static void PrintChecklistItem(string label, bool complete, string? note = null)
    {
        Console.WriteLine($"[{(complete ? "x" : " ")}] {label}{(note == null || complete ? "" : $" â€” {note}")}");
    }

    private void PrintFbwBridgeStatus()
    {
        if (_state == null)
        {
            Console.WriteLine("Aircraft state unavailable.");
            AppendDashboardLog("FBW bridge status unavailable: aircraft state missing.");
            return;
        }

        var lines = new[]
        {
            "FBW bridge status snapshot:",
            $"  Aircraft: {_state.Title}",
            $"  Detected FBW: {_state.IsFlyByWireA320Neo.ToYesNo()}",
            $"  App BAT 1/2: {_state.Battery1On.ToOnOff()}/{_state.Battery2On.ToOnOff()}",
            $"  FBW BAT 1 AUTO untyped/typed/commanded: {FormatOptionalBool(_fbwBattery1Auto)}/{FormatOptionalBool(_fbwBattery1AutoTyped)}/{FormatOptionalBool(_fbwCommandedBattery1Auto)}",
            $"  FBW BAT 2 AUTO untyped/typed/commanded: {FormatOptionalBool(_fbwBattery2Auto)}/{FormatOptionalBool(_fbwBattery2AutoTyped)}/{FormatOptionalBool(_fbwCommandedBattery2Auto)}",
            $"  FBW BAT potential 1/2: {FormatOptionalFloat(_fbwBattery1Potential, "F1")}/{FormatOptionalFloat(_fbwBattery2Potential, "F1")} V",
            $"  App EXT PWR available/on: {_state.ExternalPowerAvailable.ToYesNo()}/{_state.ExternalPowerOn.ToOnOff()}",
            $"  FBW EXT PWR available untyped/typed: {FormatOptionalBool(_fbwExternalPowerAvailable)}/{FormatOptionalBool(_fbwExternalPowerAvailableTyped)}",
            $"  FBW EXT PWR ON untyped/typed: {FormatOptionalBool(_fbwExternalPowerOn)}/{FormatOptionalBool(_fbwExternalPowerOnTyped)}",
            $"  Generic EXT PWR unindexed available/on: {_state.ExternalPowerAvailableUnindexed.ToYesNo()}/{_state.ExternalPowerOnUnindexed.ToOnOff()}",
            $"  App ADIRS 1/2/3 selector: {_state.Adirs1SelectorState:F0}/{_state.Adirs2SelectorState:F0}/{_state.Adirs3SelectorState:F0}",
            $"  FBW ADIRS 1 untyped/typed/commanded: {FormatOptionalFloat(_fbwAdirs1Selector, "F0")}/{FormatOptionalFloat(_fbwAdirs1SelectorTyped, "F0")}/{FormatOptionalFloat(_fbwCommandedAdirs1Selector, "F0")}",
            $"  FBW ADIRS 2 untyped/typed/commanded: {FormatOptionalFloat(_fbwAdirs2Selector, "F0")}/{FormatOptionalFloat(_fbwAdirs2SelectorTyped, "F0")}/{FormatOptionalFloat(_fbwCommandedAdirs2Selector, "F0")}",
            $"  FBW ADIRS 3 untyped/typed/commanded: {FormatOptionalFloat(_fbwAdirs3Selector, "F0")}/{FormatOptionalFloat(_fbwAdirs3SelectorTyped, "F0")}/{FormatOptionalFloat(_fbwCommandedAdirs3Selector, "F0")}",
            $"  FBW ADIRS ON BAT: {FormatOptionalBool(_fbwAdirsOnBattery)}",
            $"  FBW crew oxygen untyped/typed/commanded: {FormatOptionalBool(_fbwCrewOxygen)}/{FormatOptionalBool(_fbwCrewOxygenTyped)}/{FormatOptionalBool(_fbwCommandedCrewOxygen)}",
            $"  Generic battery volts 1/2: {_state.Battery1Voltage:F1}/{_state.Battery2Voltage:F1} V"
        };

        foreach (var line in lines)
        {
            Console.WriteLine(line);
            AppLog.Write(line);
        }

        AppendDashboardLog("FBW bridge status snapshot written to log.");
    }

    private static string FormatOptionalBool(bool? value) =>
        value.HasValue ? value.Value.ToOnOff() : "UNKNOWN";

    private static string FormatOptionalFloat(float? value, string format) =>
        value.HasValue ? value.Value.ToString(format) : "UNKNOWN";

    private void PrintPhase()
    {
        Console.WriteLine(
            _state == null
                ? "Operational phase: Unknown"
                : $"Operational phase: {OperationalPhaseDetector.Detect(_state)}");
    }

    private static void PrintCapabilities()
    {
        foreach (var capability in A320Capabilities.All)
        {
            Console.WriteLine(
                $"{capability.Id,-18} {capability.Support,-14} {capability.Name} ({capability.InterfaceName})");
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Commands: status | fbw-bridge-status | phase | checklist | capabilities");
        Console.WriteLine("          external-power on | external-power off");
        Console.WriteLine("          beacon on | beacon off");
        Console.WriteLine("          nav-logo off | nav-logo 2");
        Console.WriteLine("          battery-1 on/off | battery-2 on/off");
        Console.WriteLine("          apu-master on/off | apu-start on/off | apu-bleed on/off");
        Console.WriteLine("          apu-generator on/off | fuel-pumps on/off");
        Console.WriteLine("          ground-spoilers arm | flaps config-1/2/3/full | autobrake max/low");
        Console.WriteLine("          gear up | gear down");
        Console.WriteLine("          ground-spoilers disarm | altimeters standard");
        Console.WriteLine("          tcas altitude-reporting on | tcas traffic tara");
        Console.WriteLine("          procedure start <flow-id> | procedure status");
        Console.WriteLine("          flow ids: power-up-initial-setup through parking-shutdown");
        Console.WriteLine("          procedure confirm | procedure pause | procedure resume | procedure cancel | procedure reset");
        Console.WriteLine("          help | quit");
    }

    private void BuildDashboard()
    {
        Width = 920;
        Height = 760;
        MinimumSize = new System.Drawing.Size(820, 680);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = System.Drawing.Color.FromArgb(242, 245, 248);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 8
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 135));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var title = new Label
        {
            Text = "MSFS 2024 A320 Virtual First Officer",
            AutoSize = true,
            Font = new System.Drawing.Font(Font.FontFamily, 16, System.Drawing.FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10)
        };
        root.Controls.Add(title);

        var topStatusBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 12)
        };
        _simBadgeLabel = NewStatusBadge("MSFS CONNECTING", System.Drawing.Color.DimGray);
        _aircraftBadgeLabel = NewStatusBadge("AIRCRAFT WAITING", System.Drawing.Color.DimGray);
        _adapterBadgeLabel = NewStatusBadge("ADAPTER WAITING", System.Drawing.Color.DimGray);
        _flowBadgeLabel = NewStatusBadge("FLOW IDLE", System.Drawing.Color.DimGray);
        _versionBadgeLabel = NewStatusBadge($"v{GetApplicationVersion()}", System.Drawing.Color.FromArgb(40, 68, 106));
        topStatusBar.Controls.Add(_simBadgeLabel);
        topStatusBar.Controls.Add(_aircraftBadgeLabel);
        topStatusBar.Controls.Add(_adapterBadgeLabel);
        topStatusBar.Controls.Add(_flowBadgeLabel);
        topStatusBar.Controls.Add(_versionBadgeLabel);
        root.Controls.Add(topStatusBar);

        var statusPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 14)
        };
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(statusPanel);

        _connectionLabel = AddDashboardRow(statusPanel, "Simulator", "Connecting...");
        _aircraftLabel = AddDashboardRow(statusPanel, "Aircraft", "Waiting for state...");
        _phaseLabel = AddDashboardRow(statusPanel, "Operational phase", "Unknown");
        _electricalLabel = AddDashboardRow(statusPanel, "Electrical", "Waiting for state...");
        _adapterLabel = AddDashboardRow(statusPanel, "Aircraft adapter", "Connecting...");
        _recommendationLabel = AddDashboardRow(statusPanel, "Recommended flow", "Waiting for state...");
        _telemetryLabel = AddDashboardRow(statusPanel, "Current-step telemetry", "Waiting for state...");
        _versionLabel = AddDashboardRow(
            statusPanel,
            "Version",
            $"{GetApplicationVersion()} â€” checking GitHub releases...");

        var settingsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 14)
        };
        settingsPanel.Controls.Add(new Label
        {
            Text = "Automation:",
            AutoSize = true,
            Margin = new Padding(0, 7, 4, 0)
        });
        _automationPolicyBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 190
        };
        _automationPolicyBox.Items.AddRange(
            Enum.GetValues(typeof(AutomationPolicy)).Cast<object>().ToArray());
        _automationPolicyBox.SelectedItem = _settings.AutomationPolicy;
        _automationPolicyBox.SelectedIndexChanged += (_, _) =>
        {
            _settings.AutomationPolicy = (AutomationPolicy)_automationPolicyBox.SelectedItem;
            SettingsStore.Save(_settings);
        };
        settingsPanel.Controls.Add(_automationPolicyBox);

        _voiceCalloutsBox = new CheckBox
        {
            Text = "Voice callouts",
            AutoSize = true,
            Checked = _settings.EnableStandardCallouts,
            Enabled = _voiceCalloutQueue != null,
            Margin = new Padding(18, 5, 0, 0)
        };
        _voiceCalloutsBox.CheckedChanged += (_, _) =>
        {
            _settings.EnableStandardCallouts = _voiceCalloutsBox.Checked;
            SettingsStore.Save(_settings);
            if (!_voiceCalloutsBox.Checked)
            {
                _voiceCalloutQueue?.Clear();
            }
        };
        settingsPanel.Controls.Add(_voiceCalloutsBox);

        var featureSettingsButton = new Button
        {
            Text = "Approach & chaining settings",
            AutoSize = true,
            Margin = new Padding(18, 2, 0, 0)
        };
        featureSettingsButton.Click += (_, _) => ShowFeatureSettingsDialog();
        settingsPanel.Controls.Add(featureSettingsButton);

        var debugJumpButton = new Button
        {
            Text = "Debug jump to flow",
            AutoSize = true,
            Margin = new Padding(10, 2, 0, 0)
        };
        debugJumpButton.Click += (_, _) => ShowDebugJumpDialog();
        settingsPanel.Controls.Add(debugJumpButton);

        settingsPanel.Controls.Add(new Label
        {
            Text = "Transition altitude:",
            AutoSize = true,
            Margin = new Padding(18, 7, 4, 0)
        });
        _transitionAltitudeBox = new NumericUpDown
        {
            Minimum = 1000,
            Maximum = 20000,
            Increment = 100,
            Value = Math.Max(1000, Math.Min(20000, _settings.TransitionAltitudeFeet)),
            Width = 80,
            ThousandsSeparator = true
        };
        _transitionAltitudeBox.ValueChanged += (_, _) =>
        {
            _settings.TransitionAltitudeFeet = (int)_transitionAltitudeBox.Value;
            SettingsStore.Save(_settings);
            if (_state != null)
            {
                _state.TransitionAltitudeFeet = _settings.TransitionAltitudeFeet;
            }
        };
        settingsPanel.Controls.Add(_transitionAltitudeBox);
        settingsPanel.Controls.Add(new Label
        {
            Text = "ft",
            AutoSize = true,
            Margin = new Padding(2, 7, 0, 0)
        });

        settingsPanel.Controls.Add(new Label
        {
            Text = "V1:",
            AutoSize = true,
            Margin = new Padding(18, 7, 4, 0)
        });
        _takeoffV1Box = new NumericUpDown
        {
            Minimum = 80,
            Maximum = 219,
            Increment = 1,
            Value = Math.Max(80, Math.Min(219, _settings.TakeoffV1SpeedKnots)),
            Width = 64
        };
        _takeoffV1Box.ValueChanged += (_, _) =>
        {
            _settings.TakeoffV1SpeedKnots = (int)_takeoffV1Box.Value;
            if (_takeoffRotateBox != null
                && _takeoffRotateBox.Value <= _takeoffV1Box.Value)
            {
                _takeoffRotateBox.Value = Math.Min(
                    _takeoffRotateBox.Maximum,
                    _takeoffV1Box.Value + 1);
            }
            SettingsStore.Save(_settings);
            if (_state != null)
            {
                _state.TakeoffV1SpeedKnots = _settings.TakeoffV1SpeedKnots;
            }
        };
        settingsPanel.Controls.Add(_takeoffV1Box);
        settingsPanel.Controls.Add(new Label
        {
            Text = "kt",
            AutoSize = true,
            Margin = new Padding(2, 7, 0, 0)
        });

        settingsPanel.Controls.Add(new Label
        {
            Text = "VR:",
            AutoSize = true,
            Margin = new Padding(10, 7, 4, 0)
        });
        _takeoffRotateBox = new NumericUpDown
        {
            Minimum = 80,
            Maximum = 220,
            Increment = 1,
            Value = Math.Max(
                _settings.TakeoffV1SpeedKnots + 1,
                Math.Min(220, _settings.TakeoffRotateSpeedKnots)),
            Width = 64
        };
        _takeoffRotateBox.ValueChanged += (_, _) =>
        {
            if (_takeoffV1Box != null
                && _takeoffRotateBox.Value <= _takeoffV1Box.Value)
            {
                _takeoffRotateBox.Value = Math.Min(
                    _takeoffRotateBox.Maximum,
                    _takeoffV1Box.Value + 1);
                return;
            }
            _settings.TakeoffRotateSpeedKnots = (int)_takeoffRotateBox.Value;
            SettingsStore.Save(_settings);
            if (_state != null)
            {
                _state.TakeoffRotateSpeedKnots =
                    _settings.TakeoffRotateSpeedKnots;
            }
        };
        settingsPanel.Controls.Add(_takeoffRotateBox);
        settingsPanel.Controls.Add(new Label
        {
            Text = "kt",
            AutoSize = true,
            Margin = new Padding(2, 7, 0, 0)
        });
        root.Controls.Add(settingsPanel);

        var timelineGroup = new GroupBox
        {
            Text = "Checklist and assistance flow â€” gate to gate",
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };
        var timelineLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        timelineLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        timelineLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _flowList = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            DisplayMember = nameof(ProcedureListItem.DisplayName)
        };
        foreach (var procedure in ProcedureCatalog.ForAircraft(_state))
        {
            _flowList.Items.Add(new ProcedureListItem(procedure));
        }
        _flowList.SelectedIndex = 0;
        timelineLayout.Controls.Add(_flowList, 0, 0);
        var startSelectedFlow = new Button
        {
            Text = "Start selected flow",
            AutoSize = true,
            Anchor = AnchorStyles.Top,
            Margin = new Padding(10, 4, 0, 0)
        };
        startSelectedFlow.Click += (_, _) =>
        {
            if (_flowList.SelectedItem is ProcedureListItem item)
            {
                _commands.Enqueue($"procedure start {item.Definition.Id}");
            }
        };
        timelineLayout.Controls.Add(startSelectedFlow, 1, 0);
        timelineGroup.Controls.Add(timelineLayout);
        root.Controls.Add(timelineGroup);

        var procedureGroup = new GroupBox
        {
            Text = "Active procedure",
            Dock = DockStyle.Fill,
            Padding = new Padding(12)
        };
        root.Controls.Add(procedureGroup);

        var procedureLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5
        };
        procedureLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        procedureLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        procedureLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        procedureLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        procedureLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        procedureGroup.Controls.Add(procedureLayout);

        _procedureLabel = NewDashboardLabel("None");
        _stepLabel = NewDashboardLabel("No active step");
        _statusBadgeLabel = NewDashboardLabel("Idle");
        _statusBadgeLabel.Font = new System.Drawing.Font(
            SystemFonts.DefaultFont,
            System.Drawing.FontStyle.Bold);
        _waitingForLabel = NewDashboardLabel("Waiting for: none");
        _waitingForLabel.MaximumSize = new System.Drawing.Size(680, 0);
        _procedureProgress = new ProgressBar { Dock = DockStyle.Top, Height = 22 };
        procedureLayout.Controls.Add(_statusBadgeLabel);
        procedureLayout.Controls.Add(_procedureLabel);
        procedureLayout.Controls.Add(_stepLabel);
        procedureLayout.Controls.Add(_waitingForLabel);
        procedureLayout.Controls.Add(_procedureProgress);

        var procedureButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight
        };
        procedureButtons.Controls.Add(NewCommandButton("Start first flow", "procedure start power-up-initial-setup"));
        procedureButtons.Controls.Add(NewCommandButton("Confirm completed", "procedure confirm"));
        procedureButtons.Controls.Add(NewCommandButton("Pause", "procedure pause"));
        procedureButtons.Controls.Add(NewCommandButton("Resume", "procedure resume"));
        procedureButtons.Controls.Add(NewCommandButton("Cancel", "procedure cancel"));
        var resetProgressButton = new Button
        {
            Text = "New flight / Reset progress",
            AutoSize = true
        };
        resetProgressButton.Click += (_, _) =>
        {
            var result = MessageBox.Show(
                this,
                "Reset the active flow and all completed-flow progress for a new flight?\n\nSettings and saved flight replays will be kept.",
                "Start a new flight",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (result == DialogResult.Yes)
            {
                _commands.Enqueue("procedure reset");
            }
        };
        procedureButtons.Controls.Add(resetProgressButton);
        procedureGroup.Controls.Add(procedureButtons);

        var logGroup = new GroupBox
        {
            Text = "Activity log",
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };
        _eventLog = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            Font = new System.Drawing.Font("Consolas", 9)
        };
        logGroup.Controls.Add(_eventLog);
        root.Controls.Add(logGroup);

        var toolsShell = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 14, 0, 0)
        };
        var toggleToolsButton = new Button
        {
            Text = "Show tools & diagnostics",
            AutoSize = true
        };
        toolsShell.Controls.Add(toggleToolsButton);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 6, 0, 0),
            Visible = false
        };
        toggleToolsButton.Click += (_, _) =>
        {
            actions.Visible = !actions.Visible;
            toggleToolsButton.Text = actions.Visible
                ? "Hide tools & diagnostics"
                : "Show tools & diagnostics";
        };
        actions.Controls.Add(NewCommandButton("FBW bridge status", "fbw-bridge-status"));
        actions.Controls.Add(NewCommandButton("External power ON", "external-power on"));
        actions.Controls.Add(NewCommandButton("External power OFF", "external-power off"));
        actions.Controls.Add(NewCommandButton("Beacon ON", "beacon on"));
        actions.Controls.Add(NewCommandButton("Beacon OFF", "beacon off"));
        actions.Controls.Add(NewCommandButton("NAV&LOGO 2", "nav-logo 2"));
        actions.Controls.Add(NewCommandButton("NAV&LOGO OFF", "nav-logo off"));
        actions.Controls.Add(NewCommandButton("BAT 1 ON", "battery-1 on"));
        actions.Controls.Add(NewCommandButton("BAT 2 ON", "battery-2 on"));
        _replayFlightBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 190,
            Margin = new Padding(18, 3, 0, 0)
        };
        RefreshReplayFlightList();
        actions.Controls.Add(_replayFlightBox);
        var replayButton = new Button
        {
            Text = "Replay flight (10x)",
            AutoSize = true
        };
        replayButton.Click += (_, _) => StartSelectedReplay();
        actions.Controls.Add(replayButton);
        var stopReplayButton = new Button
        {
            Text = "Stop replay",
            AutoSize = true
        };
        stopReplayButton.Click += (_, _) => StopReplay();
        actions.Controls.Add(stopReplayButton);
        var exportDiagnosticsButton = new Button
        {
            Text = "Export diagnostics",
            AutoSize = true,
            Margin = new Padding(18, 3, 0, 0)
        };
        exportDiagnosticsButton.Click += (_, _) => ExportDiagnostics();
        actions.Controls.Add(exportDiagnosticsButton);
        var copyDiagnosticsButton = new Button
        {
            Text = "Copy last diagnostic",
            AutoSize = true
        };
        copyDiagnosticsButton.Click += (_, _) => CopyLastDiagnostic();
        actions.Controls.Add(copyDiagnosticsButton);
        toolsShell.Controls.Add(actions);
        root.Controls.Add(toolsShell);
    }

    private void ShowDebugJumpDialog()
    {
        using var dialog = new Form
        {
            Text = "Debug jump to flow",
            Width = 480,
            Height = 360,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 1,
            RowCount = 4
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        dialog.Controls.Add(layout);

        var warning = new Label
        {
            Text =
                "Developer/test shortcut: this marks earlier app flows complete and starts the selected flow. " +
                "It does not change aircraft systems, position, speed, fuel, or flight-plan state.",
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(430, 0),
            Margin = new Padding(0, 0, 0, 10)
        };
        layout.Controls.Add(warning, 0, 0);

        var list = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            DisplayMember = nameof(ProcedureListItem.DisplayName)
        };
        foreach (var procedure in ProcedureCatalog.ForAircraft(_state))
        {
            list.Items.Add(new ProcedureListItem(procedure));
        }
        var defaultIndex = ProcedureCatalog.ForAircraft(_state)
            .Select((definition, index) => new { definition, index })
            .FirstOrDefault(item =>
                string.Equals(
                    item.definition.Id,
                    "approach-landing",
                    StringComparison.OrdinalIgnoreCase))
            ?.index ?? 0;
        list.SelectedIndex = Math.Max(0, Math.Min(defaultIndex, list.Items.Count - 1));
        layout.Controls.Add(list, 0, 1);

        var detail = new Label
        {
            Text = "Example: select Flow 10 to test approach/landing from an airborne saved position.",
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 10)
        };
        layout.Controls.Add(detail, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        var jump = new Button
        {
            Text = "Jump to selected flow",
            AutoSize = true,
            DialogResult = DialogResult.OK
        };
        var cancel = new Button
        {
            Text = "Cancel",
            AutoSize = true,
            DialogResult = DialogResult.Cancel
        };
        buttons.Controls.Add(jump);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 3);
        dialog.AcceptButton = jump;
        dialog.CancelButton = cancel;

        if (dialog.ShowDialog(this) == DialogResult.OK
            && list.SelectedItem is ProcedureListItem item)
        {
            _commands.Enqueue($"debug jump {item.Definition.Id}");
        }
    }

    private void ShowFeatureSettingsDialog()
    {
        using var dialog = new Form
        {
            Text = "Approach schedule and flow chaining",
            Width = 540,
            Height = 650,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 3,
            AutoScroll = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
        dialog.Controls.Add(layout);

        NumericUpDown AddNumber(string label, int value, int minimum, int maximum, string unit)
        {
            var row = layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Margin = new Padding(0, 8, 4, 0)
            }, 0, row);
            var box = new NumericUpDown
            {
                Minimum = minimum,
                Maximum = maximum,
                Value = Math.Max(minimum, Math.Min(maximum, value)),
                Width = 80,
                ThousandsSeparator = true
            };
            layout.Controls.Add(box, 1, row);
            layout.Controls.Add(new Label
            {
                Text = unit,
                AutoSize = true,
                Margin = new Padding(2, 8, 0, 0)
            }, 2, row);
            return box;
        }

        var flapDistance = AddNumber(
            "Flaps 1 target distance",
            _settings.ApproachFlaps1DistanceNm, 3, 30, "NM");
        var flapAltitude = AddNumber(
            "Flaps 1 maximum indicated altitude",
            _settings.ApproachFlaps1AltitudeFeet, 1000, 20000, "ft");
        var flapSpeed = AddNumber(
            "Flaps 1 max command speed",
            _settings.ApproachFlaps1SpeedKnots, 100, 250, "kt");
        var flap2Distance = AddNumber(
            "Flaps 2 target distance",
            _settings.ApproachFlaps2DistanceNm, 2, 25, "NM");
        var flap2Altitude = AddNumber(
            "Flaps 2 maximum radio altitude",
            _settings.ApproachFlaps2AltitudeAglFeet, 1000, 8000, "ft");
        var flap2Speed = AddNumber(
            "Flaps 2 max command speed",
            _settings.ApproachFlaps2SpeedKnots, 100, 230, "kt");
        var gearDistance = AddNumber(
            "Gear-down target distance",
            _settings.ApproachGearDistanceNm, 2, 20, "NM");
        var gearAltitude = AddNumber(
            "Gear-down maximum radio altitude",
            _settings.ApproachGearAltitudeAglFeet, 500, 5000, "ft");
        var gearSpeed = AddNumber(
            "Gear-down target speed",
            _settings.ApproachGearSpeedKnots, 100, 250, "kt");
        var landingDistance = AddNumber(
            "Landing configuration target distance",
            _settings.ApproachLandingConfigDistanceNm, 1, 15, "NM");
        var landingAltitude = AddNumber(
            "Landing configuration maximum radio altitude",
            _settings.ApproachLandingConfigAltitudeAglFeet, 300, 3000, "ft");
        var landingSpeed = AddNumber(
            "Landing config max command speed",
            _settings.ApproachLandingConfigSpeedKnots, 100, 220, "kt");

        CheckBox AddCheck(string text, bool value)
        {
            var row = layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var box = new CheckBox
            {
                Text = text,
                Checked = value,
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 0)
            };
            layout.Controls.Add(box, 0, row);
            layout.SetColumnSpan(box, 3);
            return box;
        }

        var earlierChains = AddCheck(
            "Automatically chain other early flows",
            _settings.AutoChainEarlierFlows);
        var flow6Chain = AddCheck(
            "Automatically start Flow 7 after Flow 6",
            _settings.AutoChainFlow6To7);
        var flow10Chain = AddCheck(
            "Automatically start Flow 11 after Flow 10",
            _settings.AutoChainFlow10To11);
        var flow11Chain = AddCheck(
            "Automatically start Flow 12 after Flow 11",
            _settings.AutoChainFlow11To12);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        var save = new Button { Text = "Save", DialogResult = DialogResult.OK };
        var defaults = new Button { Text = "Restore standard" };
        defaults.Click += (_, _) =>
        {
            flapDistance.Value = 15;
            flapAltitude.Value = 10000;
            flapSpeed.Value = 230;
            flap2Distance.Value = 10;
            flap2Altitude.Value = 4000;
            flap2Speed.Value = 200;
            gearDistance.Value = 7;
            gearAltitude.Value = 2500;
            gearSpeed.Value = 210;
            landingDistance.Value = 5;
            landingAltitude.Value = 1800;
            landingSpeed.Value = 185;
            earlierChains.Checked = false;
            flow6Chain.Checked = true;
            flow10Chain.Checked = true;
            flow11Chain.Checked = true;
        };
        buttons.Controls.Add(save);
        buttons.Controls.Add(defaults);
        var buttonRow = layout.RowCount++;
        layout.Controls.Add(buttons, 0, buttonRow);
        layout.SetColumnSpan(buttons, 3);
        dialog.AcceptButton = save;

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _settings.ApproachFlaps1DistanceNm = (int)flapDistance.Value;
        _settings.ApproachFlaps1AltitudeFeet = (int)flapAltitude.Value;
        _settings.ApproachFlaps1SpeedKnots = (int)flapSpeed.Value;
        _settings.ApproachFlaps2DistanceNm = (int)flap2Distance.Value;
        _settings.ApproachFlaps2AltitudeAglFeet = (int)flap2Altitude.Value;
        _settings.ApproachFlaps2SpeedKnots = (int)flap2Speed.Value;
        _settings.ApproachGearDistanceNm = (int)gearDistance.Value;
        _settings.ApproachGearAltitudeAglFeet = (int)gearAltitude.Value;
        _settings.ApproachGearSpeedKnots = (int)gearSpeed.Value;
        _settings.ApproachLandingConfigDistanceNm = (int)landingDistance.Value;
        _settings.ApproachLandingConfigAltitudeAglFeet = (int)landingAltitude.Value;
        _settings.ApproachLandingConfigSpeedKnots = (int)landingSpeed.Value;
        _settings.AutoChainEarlierFlows = earlierChains.Checked;
        _settings.AutoChainFlow6To7 = flow6Chain.Checked;
        _settings.AutoChainFlow10To11 = flow10Chain.Checked;
        _settings.AutoChainFlow11To12 = flow11Chain.Checked;
        SettingsStore.Save(_settings);
        ApplyApproachSettingsToState();
        AppendDashboardLog("Approach schedule and flow chaining settings saved.");
    }

    private void ApplyApproachSettingsToState()
    {
        if (_state == null)
        {
            return;
        }
        _state.ApproachFlaps1DistanceNm = _settings.ApproachFlaps1DistanceNm;
        _state.ApproachFlaps1AltitudeFeet = _settings.ApproachFlaps1AltitudeFeet;
        _state.ApproachFlaps1SpeedKnots = _settings.ApproachFlaps1SpeedKnots;
        _state.ApproachFlaps2DistanceNm = _settings.ApproachFlaps2DistanceNm;
        _state.ApproachFlaps2AltitudeAglFeet = _settings.ApproachFlaps2AltitudeAglFeet;
        _state.ApproachFlaps2SpeedKnots = _settings.ApproachFlaps2SpeedKnots;
        _state.ApproachGearDistanceNm = _settings.ApproachGearDistanceNm;
        _state.ApproachGearAltitudeAglFeet = _settings.ApproachGearAltitudeAglFeet;
        _state.ApproachGearSpeedKnots = _settings.ApproachGearSpeedKnots;
        _state.ApproachLandingConfigDistanceNm =
            _settings.ApproachLandingConfigDistanceNm;
        _state.ApproachLandingConfigAltitudeAglFeet =
            _settings.ApproachLandingConfigAltitudeAglFeet;
        _state.ApproachLandingConfigSpeedKnots =
            _settings.ApproachLandingConfigSpeedKnots;
    }

    private void RefreshReplayFlightList()
    {
        if (_replayFlightBox == null)
        {
            return;
        }
        _replayFlightBox.Items.Clear();
        foreach (var recording in _flightTelemetryStore.Recordings)
        {
            _replayFlightBox.Items.Add(new ReplayFlightItem(recording));
        }
        if (_replayFlightBox.Items.Count > 0)
        {
            _replayFlightBox.SelectedIndex = 0;
        }
    }

    private void StartSelectedReplay()
    {
        if (_replayFlightBox?.SelectedItem is not ReplayFlightItem item)
        {
            AppendDashboardLog("No completed flight recording is available.");
            return;
        }

        StopReplay();
        _replayStates = _flightTelemetryStore.Load(item.Path);
        if (_replayStates.Count == 0)
        {
            AppendDashboardLog("The selected flight recording is empty.");
            return;
        }

        _replayActive = true;
        _replayIndex = 0;
        _replayTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _replayTimer.Tick += (_, _) => AdvanceReplay();
        _replayTimer.Start();
        AppendDashboardLog(
            $"Replay started: {Path.GetFileName(item.Path)} at 10x speed. Cockpit commands are suppressed.");
    }

    private void AdvanceReplay()
    {
        if (!_replayActive || _replayIndex >= _replayStates.Count)
        {
            StopReplay();
            return;
        }

        _state = _replayStates[_replayIndex++];
        UpdateTelemetrySanity(_state);
        _procedureRunner.Update(_state);
        UpdateDashboard();
    }

    private void StopReplay()
    {
        var wasActive = _replayActive;
        _replayTimer?.Stop();
        _replayTimer?.Dispose();
        _replayTimer = null;
        _replayStates = Array.Empty<AircraftState>();
        _replayIndex = 0;
        _replayActive = false;
        if (wasActive)
        {
            AppendDashboardLog("Replay stopped; live simulator telemetry resumed.");
        }
        RefreshReplayFlightList();
    }

    private void ExportDiagnostics()
    {
        try
        {
            var path = DiagnosticLog.ExportLatest(_flightTelemetryStore);
            AppendDashboardLog($"Diagnostic package exported: {path}");
            MessageBox.Show(
                this,
                $"Diagnostic package exported:\n\n{path}",
                "Diagnostics exported",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            AppendDashboardLog("Diagnostic export failed.");
            DiagnosticLog.RecordFailure(
                "Diagnostic export failed",
                _state,
                details: new[] { ex.ToString() });
            MessageBox.Show(
                this,
                $"Diagnostic export failed:\n\n{ex.Message}",
                "Diagnostics export failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void CopyLastDiagnostic()
    {
        try
        {
            Clipboard.SetText(DiagnosticLog.GetLastEntry());
            AppendDashboardLog("Last diagnostic entry copied to clipboard.");
        }
        catch (Exception ex)
        {
            AppendDashboardLog("Copy diagnostic failed.");
            DiagnosticLog.RecordFailure(
                "Copy diagnostic failed",
                _state,
                details: new[] { ex.ToString() });
        }
    }

    private void AppendDashboardLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        AppLog.Write(message);
        if (!_showUi || _eventLog == null)
        {
            return;
        }

        var entry = $"{DateTime.Now:HH:mm:ss}  {message}";
        _eventLog.Items.Add(entry);
        while (_eventLog.Items.Count > 200)
        {
            _eventLog.Items.RemoveAt(0);
        }
        _eventLog.TopIndex = _eventLog.Items.Count - 1;
    }

    private static Label AddDashboardRow(TableLayoutPanel panel, string name, string value)
    {
        var row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(
            new Label
            {
                Text = name,
                AutoSize = true,
                Font = new System.Drawing.Font(SystemFonts.DefaultFont, System.Drawing.FontStyle.Bold),
                Margin = new Padding(0, 3, 12, 3)
            },
            0,
            row);
        var valueLabel = NewDashboardLabel(value);
        panel.Controls.Add(valueLabel, 1, row);
        return valueLabel;
    }

    private static Label NewDashboardLabel(string text) =>
        new()
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(0, 3, 0, 3)
        };

    private static Label NewStatusBadge(string text, System.Drawing.Color backColor) =>
        new()
        {
            Text = text,
            AutoSize = true,
            BackColor = backColor,
            ForeColor = System.Drawing.Color.White,
            Font = new System.Drawing.Font(
                SystemFonts.DefaultFont,
                System.Drawing.FontStyle.Bold),
            Padding = new Padding(8, 4, 8, 4),
            Margin = new Padding(0, 0, 8, 4)
        };

    private static void SetStatusBadge(
        Label? label,
        string text,
        System.Drawing.Color backColor)
    {
        if (label == null)
        {
            return;
        }

        label.Text = text;
        label.BackColor = backColor;
        label.ForeColor = System.Drawing.Color.White;
    }

    private Button NewCommandButton(string label, string command)
    {
        var button = new Button
        {
            Text = label,
            AutoSize = true,
            Margin = new Padding(4)
        };
        button.Click += (_, _) => _commands.Enqueue(command);
        return button;
    }

    private void UpdateDashboard()
    {
        if (!_showUi || _state == null)
        {
            return;
        }

        _aircraftLabel!.Text = _state.Title;
        _phaseLabel!.Text = OperationalPhaseDetector.Detect(_state).ToString();
        SetStatusBadge(
            _simBadgeLabel,
            _simConnect != null ? "MSFS CONNECTED" : "MSFS DISCONNECTED",
            _simConnect != null
                ? System.Drawing.Color.FromArgb(39, 130, 87)
                : System.Drawing.Color.FromArgb(150, 48, 48));
        SetStatusBadge(
            _aircraftBadgeLabel,
            _state.IsA320NeoV2
                ? "iniBuilds A320neo V2"
                : _state.IsIniBuildsA321Lr
                    ? "iniBuilds A321LR"
                    : _state.IsFlyByWireA320Neo
                    ? "FBW A32NX EXPERIMENTAL"
                    : _state.IsPmdg737800
                    ? "PMDG 737-800"
                    : "AIRCRAFT UNSUPPORTED",
            _state.IsSupportedAircraft
                ? System.Drawing.Color.FromArgb(39, 130, 87)
                : System.Drawing.Color.FromArgb(172, 113, 37));
        SetStatusBadge(
            _adapterBadgeLabel,
            _state.IsSupportedBoeing737
                ? _pmdgNg3DataReady
                    ? "PMDG SDK OK"
                    : "PMDG SDK WAITING"
                : _mobiFlightReady ? "MOBIFLIGHT OK" : "ADAPTER OFFLINE",
            _state.IsSupportedBoeing737
                ? _pmdgNg3DataReady
                    ? System.Drawing.Color.FromArgb(39, 130, 87)
                    : System.Drawing.Color.FromArgb(172, 113, 37)
                : _mobiFlightReady
                ? System.Drawing.Color.FromArgb(39, 130, 87)
                : System.Drawing.Color.FromArgb(150, 48, 48));
        _electricalLabel!.Text =
            $"BAT 1 {_state.Battery1On.ToOnOff()} | BAT 2 {_state.Battery2On.ToOnOff()} | " +
            $"EXT PWR {_state.ExternalPowerOn.ToOnOff()} ({_state.ExternalPowerAvailable.ToYesNo()} available) | " +
            $"Beacon {_state.BeaconOn.ToOnOff()} | NAV&LOGO " +
            $"{(_state.NavLogoSelectorPosition.HasValue ? FormatNavLogoPosition((int)Math.Round(_state.NavLogoSelectorPosition.Value)) : "UNKNOWN")} | " +
            $"APU {_state.ApuMasterSwitchOn.ToOnOff()}/{_state.ApuRpmPercent:F0}%";
        _adapterLabel!.Text = _state.IsSupportedBoeing737
            ? _pmdgNg3DataReady
                ? "PMDG NG3 SDK data connected"
                : "PMDG NG3 SDK waiting - enable [SDK] EnableDataBroadcast=1 in 737_Options.ini"
            : _mobiFlightReady
                ? "MobiFlight connected"
                : "MobiFlight not connected â€” aircraft controls unavailable";
        _adapterLabel.ForeColor = _state.IsSupportedBoeing737
            ? _pmdgNg3DataReady
                ? System.Drawing.Color.DarkGreen
                : System.Drawing.Color.DarkOrange
            : _mobiFlightReady
            ? System.Drawing.Color.DarkGreen
            : System.Drawing.Color.DarkRed;
        _telemetryLabel!.Text = FormatCurrentStepTelemetry(_state);
        _telemetryLabel.ForeColor = _state.TelemetryIssues.Count == 0
            ? System.Drawing.Color.DarkSlateBlue
            : System.Drawing.Color.DarkRed;

        var definition = _procedureRunner.Definition;
        var currentStep = _procedureRunner.CurrentStep;
        SetStatusBadge(
            _flowBadgeLabel,
            definition == null
                ? "FLOW IDLE"
                : $"{FormatProcedureStatus(_procedureRunner.Status).ToUpperInvariant()} - {definition.Name.Split('.')[0]}",
            _procedureRunner.Status switch
            {
                ProcedureStatus.Running => System.Drawing.Color.FromArgb(39, 130, 87),
                ProcedureStatus.WaitingForVerification => System.Drawing.Color.FromArgb(40, 95, 150),
                ProcedureStatus.WaitingForManualAction => System.Drawing.Color.FromArgb(190, 126, 37),
                ProcedureStatus.Paused => System.Drawing.Color.FromArgb(151, 110, 35),
                ProcedureStatus.Completed => System.Drawing.Color.FromArgb(39, 130, 87),
                ProcedureStatus.Failed => System.Drawing.Color.FromArgb(150, 48, 48),
                _ => System.Drawing.Color.DimGray
            });
        SetStatusBadge(
            _versionBadgeLabel,
            $"v{GetApplicationVersion()}",
            System.Drawing.Color.FromArgb(40, 68, 106));
        UpdateProcedureStatusBadge();
        _procedureLabel!.Text =
            definition == null
                ? "None"
                : $"{definition.Name} â€” {_procedureRunner.Status} â€” {definition.AutomationSummary}";
        _stepLabel!.Text =
            currentStep == null
                ? "No active step"
                : $"Current step: {currentStep.Label} " +
                  $"({FormatCrewRole(currentStep.AssignedRole)})";
        _waitingForLabel!.Text = FormatWaitingReason(
            currentStep,
            _state,
            _procedureRunner.Status);
        _waitingForLabel.ForeColor = _procedureRunner.Status == ProcedureStatus.Failed
            ? System.Drawing.Color.DarkRed
            : System.Drawing.Color.DimGray;
        _procedureProgress!.Maximum = Math.Max(1, definition?.Steps.Count ?? 1);
        _procedureProgress.Value = Math.Min(
            _procedureProgress.Maximum,
            _procedureRunner.CompletedStepCount);

        var recommendation = FlowRecommendationEngine.Recommend(
            _state,
            _completedProcedureIds);
        _recommendationLabel!.Text =
            $"{recommendation.Procedure.Name} â€” {recommendation.Reason}";
        _recommendationLabel.ForeColor = recommendation.Overdue
            ? System.Drawing.Color.DarkRed
            : System.Drawing.Color.DarkBlue;
        RefreshFlowList(recommendation.Procedure.Id, definition?.Id);
    }

    private void UpdateProcedureStatusBadge()
    {
        if (_statusBadgeLabel == null)
        {
            return;
        }

        var status = _procedureRunner.Status;
        _statusBadgeLabel.Text = $"Status: {FormatProcedureStatus(status)}";
        _statusBadgeLabel.ForeColor = status switch
        {
            ProcedureStatus.Running => System.Drawing.Color.DarkGreen,
            ProcedureStatus.WaitingForVerification => System.Drawing.Color.DarkBlue,
            ProcedureStatus.WaitingForManualAction => System.Drawing.Color.DarkOrange,
            ProcedureStatus.Paused => System.Drawing.Color.DarkGoldenrod,
            ProcedureStatus.Completed => System.Drawing.Color.DarkGreen,
            ProcedureStatus.Failed => System.Drawing.Color.DarkRed,
            _ => System.Drawing.Color.DimGray
        };
    }

    private static string FormatProcedureStatus(ProcedureStatus status) =>
        status switch
        {
            ProcedureStatus.Idle => "Idle",
            ProcedureStatus.Running => "Running",
            ProcedureStatus.WaitingForManualAction => "Waiting for pilot",
            ProcedureStatus.WaitingForVerification => "Monitoring",
            ProcedureStatus.Paused => "Paused",
            ProcedureStatus.Completed => "Complete",
            ProcedureStatus.Failed => "Failed",
            _ => status.ToString()
        };

    private static string FormatCrewRole(CrewRole role) =>
        role switch
        {
            CrewRole.Captain => "Captain",
            CrewRole.FirstOfficer => "First Officer",
            CrewRole.Either => "Either pilot",
            _ => role.ToString()
        };

    private static string FormatWaitingReason(
        ProcedureStep? step,
        AircraftState state,
        ProcedureStatus status)
    {
        if (step == null)
        {
            return "Waiting for: none";
        }
        if (status == ProcedureStatus.WaitingForManualAction)
        {
            return step.ManualInstruction != null
                ? $"Waiting for: {step.ManualInstruction}"
                : $"Waiting for: pilot confirmation of {step.Label}.";
        }
        if (status == ProcedureStatus.WaitingForVerification
            || status == ProcedureStatus.Running)
        {
            return $"Waiting for: {DescribeStepCondition(step, state)}";
        }
        if (status == ProcedureStatus.Failed)
        {
            return "Waiting for: resolve the failed item, then resume or restart the flow.";
        }

        return "Waiting for: none";
    }

    private static string DescribeStepCondition(ProcedureStep step, AircraftState state)
    {
        if (step.Kind == ProcedureStepKind.AutomaticAction)
        {
            return step.Command == null
                ? $"{step.Label} readback."
                : $"command '{step.Command}' to verify as {step.Label}.";
        }

        return step.Id switch
        {
            "captain-park" =>
                $"gate parking: stopped, parking brake ON, engines OFF. Current GS {state.GroundSpeedKnots:F1} kt, parking brake {state.ParkingBrakeSet.ToOnOff()}, engines {(state.EnginesOff ? "OFF" : "running")}.",
            "captain-taxi" =>
                $"taxi movement. Current GS {state.GroundSpeedKnots:F1} kt, parking brake {state.ParkingBrakeSet.ToOnOff()}.",
            "apu-available" or "shutdown-power" =>
                $"APU AVAIL or external power. APU {state.ApuRpmPercent:F0}%, external power {state.ExternalPowerOn.ToOnOff()}.",
            "captain-engine-shutdown" =>
                $"engine masters OFF. Engines {(state.EnginesOff ? "OFF" : "running")}.",
            "approach-config-start" =>
                $"Flaps 1 gate: distance <= {state.ApproachFlaps1DistanceNm} NM or altitude <= {state.ApproachFlaps1AltitudeFeet:N0} ft.",
            "flaps-one-speed" =>
                $"Flaps CONFIG 1 speed safe: IAS {state.IndicatedAirspeedKnots:F0} kt <= {state.EffectiveApproachFlaps1SpeedKnots} kt.",
            "flaps-two-point" =>
                $"Flaps 2 gate: distance <= {state.ApproachFlaps2DistanceNm} NM or radio altitude <= {state.ApproachFlaps2AltitudeAglFeet:N0} ft.",
            "flaps-two-speed" =>
                $"Flaps CONFIG 2 speed safe: IAS {state.IndicatedAirspeedKnots:F0} kt <= {state.EffectiveApproachFlaps2SpeedKnots} kt.",
            "gear-down-point" =>
                $"gear gate: distance <= {state.ApproachGearDistanceNm} NM or radio altitude <= {state.ApproachGearAltitudeAglFeet:N0} ft.",
            "landing-config-point" =>
                $"landing-config gate: distance <= {state.ApproachLandingConfigDistanceNm} NM or radio altitude <= {state.ApproachLandingConfigAltitudeAglFeet:N0} ft.",
            "landing-config-speed" =>
                $"Landing configuration speed safe: IAS {state.IndicatedAirspeedKnots:F0} kt <= {state.EffectiveApproachFlaps3SpeedKnots} kt.",
            "flaps-full-speed" =>
                $"Flaps FULL speed safe: IAS {state.IndicatedAirspeedKnots:F0} kt <= {state.EffectiveApproachFlapsFullSpeedKnots} kt.",
            "fo-approaching-minimums" =>
                $"radio altitude at DH + 100 ft. RA {state.RadioHeightFeet:F0} ft, DH {state.DecisionHeightFeet:F0} ft.",
            "fo-minimums" =>
                $"radio altitude at DH. RA {state.RadioHeightFeet:F0} ft, DH {state.DecisionHeightFeet:F0} ft.",
            "touchdown" =>
                $"touchdown. On ground {state.OnGround.ToYesNo()}, radio height {state.RadioHeightFeet:F0} ft.",
            "captain-runway-exit" =>
                $"taxi speed after landing. Current GS {state.GroundSpeedKnots:F1} kt.",
            _ => step.Label
        };
    }

    private static bool TryDescribeApproachGateStatus(
        string stepId,
        AircraftState state,
        out string description)
    {
        int distanceGate;
        int speedGate;
        string fallbackLabel;
        bool fallbackReached;

        switch (stepId)
        {
            case "approach-config-start":
                distanceGate = state.ApproachFlaps1DistanceNm;
                speedGate = state.EffectiveApproachFlaps1SpeedKnots;
                fallbackLabel = $"ALT <= {state.ApproachFlaps1AltitudeFeet:N0} ft";
                fallbackReached =
                    state.IndicatedAltitudeFeet <= state.ApproachFlaps1AltitudeFeet;
                break;
            case "flaps-two-point":
                distanceGate = state.ApproachFlaps2DistanceNm;
                speedGate = state.EffectiveApproachFlaps2SpeedKnots;
                fallbackLabel = $"AGL <= {state.ApproachFlaps2AltitudeAglFeet:N0} ft";
                fallbackReached =
                    state.AltitudeAboveGroundFeet <= state.ApproachFlaps2AltitudeAglFeet;
                break;
            case "gear-down-point":
                distanceGate = state.ApproachGearDistanceNm;
                speedGate = state.ApproachGearSpeedKnots;
                fallbackLabel = $"AGL <= {state.ApproachGearAltitudeAglFeet:N0} ft";
                fallbackReached =
                    state.AltitudeAboveGroundFeet <= state.ApproachGearAltitudeAglFeet;
                break;
            case "landing-config-point":
                distanceGate = state.ApproachLandingConfigDistanceNm;
                speedGate = state.EffectiveApproachFlaps3SpeedKnots;
                fallbackLabel = $"AGL <= {state.ApproachLandingConfigAltitudeAglFeet:N0} ft";
                fallbackReached =
                    state.AltitudeAboveGroundFeet <= state.ApproachLandingConfigAltitudeAglFeet;
                break;
            default:
                description = string.Empty;
                return false;
        }

        var distanceReached =
            state.ApproachDistanceToTouchdownNm.HasValue
            && state.ApproachDistanceToTouchdownNm.Value > 0
            && state.ApproachDistanceToTouchdownNm.Value <= distanceGate;
        var distanceText = state.ApproachDistanceToTouchdownNm.HasValue
            ? $"{state.ApproachDistanceToTouchdownNm.Value:F1} NM {state.ApproachDistanceSource}"
            : "n/a";
        var blockers = new List<string>();
        if (!distanceReached && !fallbackReached)
        {
            blockers.Add($"distance/fallback not reached ({distanceText}, {fallbackLabel})");
        }

        description =
            "Approach gate status: " +
            $"IAS {state.IndicatedAirspeedKnots:F0} kt (speed reference {speedGate} kt), " +
            $"ALT {state.IndicatedAltitudeFeet:F0} ft, " +
            $"AGL {state.AltitudeAboveGroundFeet:F0} ft, " +
            $"DIST {distanceText} <= {distanceGate} NM, " +
            $"fallback {fallbackLabel} {(fallbackReached ? "met" : "not met")}; " +
            (blockers.Count == 0
                ? "gate ready."
                : "waiting for " + string.Join(" and ", blockers) + ".");
        return true;
    }

    private string FormatCurrentStepTelemetry(AircraftState state)
    {
        if (state.TelemetryIssues.Count > 0)
        {
            return "READBACK INCONSISTENT â€” " +
                   string.Join(" ", state.TelemetryIssues);
        }

        var stepId = _procedureRunner.CurrentStep?.Id;
        var flight =
            $"AGL {state.AltitudeAboveGroundFeet:F0} ft | " +
            $"ALT {state.IndicatedAltitudeFeet:F0} ft | " +
            $"IAS {state.IndicatedAirspeedKnots:F0} kt | " +
            $"VS {state.VerticalSpeedFeetPerMinute:F0} fpm";
        var distance = state.ApproachDistanceToTouchdownNm.HasValue
            ? $" | DIST {state.ApproachDistanceToTouchdownNm.Value:F1} NM {state.ApproachDistanceSource}"
            : " | DIST n/a";
        return stepId switch
        {
            "fo-v1" => $"{flight} | target V1 {state.TakeoffV1SpeedKnots} kt",
            "fo-rotate" => $"{flight} | target VR {state.TakeoffRotateSpeedKnots} kt",
            "approach-config-start" =>
                $"{flight} | trigger â‰¤{state.ApproachFlaps1AltitudeFeet:N0} ft indicated or distance gate",
            "flaps-one-speed" =>
                $"{flight} | wait IAS â‰¤{state.EffectiveApproachFlaps1SpeedKnots} kt for CONFIG 1",
            "flaps-two-speed" =>
                $"{flight} | wait IAS â‰¤{state.EffectiveApproachFlaps2SpeedKnots} kt for CONFIG 2",
            "gear-down-point" =>
                $"{flight} | trigger â‰¤{state.ApproachGearAltitudeAglFeet:N0} ft AGL or distance gate",
            "landing-config-point" =>
                $"{flight} | trigger â‰¤{state.ApproachLandingConfigAltitudeAglFeet:N0} ft AGL or distance gate",
            "landing-config-speed" =>
                $"{flight} | wait IAS â‰¤{state.EffectiveApproachFlaps3SpeedKnots} kt for CONFIG 3",
            "flaps-full-speed" =>
                $"{flight} | wait IAS â‰¤{state.EffectiveApproachFlapsFullSpeedKnots} kt for FULL",
            "fo-approaching-minimums" or "fo-minimums" =>
                $"{flight} | RA {state.RadioHeightFeet:F0} ft | DH {state.DecisionHeightFeet:F0} ft",
            "fo-flaps-one" or "fo-flaps-two" or "fo-flaps-three" or "fo-flaps-full"
                or "fo-flaps" or "fo-flaps-zero" =>
                $"{flight} | flap handle {state.FlapsHandleIndex:F0} | " +
                $"surfaces L/R {state.LeftFlapPositionPercent:F1}/{state.RightFlapPositionPercent:F1}%",
            "fo-gear-up" or "fo-gear-down" =>
                $"{flight} | gear {(state.GearHandleDown ? "DOWN" : "UP")}",
            "fo-display-initialization" =>
                $"BAT 1/2 {state.Battery1On.ToOnOff()}/{state.Battery2On.ToOnOff()} | " +
                $"EXT PWR {state.ExternalPowerOn.ToOnOff()} | waiting for 45 s stable power",
            _ => flight
        };
    }

    private static string GetApplicationVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "development";

    private async Task CheckForUpdatesAsync()
    {
        if (_versionLabel == null)
        {
            return;
        }

        try
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(8)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "MSFS2024-AI-First-Officer");
            using var response = await client.GetAsync(
                "https://api.github.com/repos/noscapect/MSFS2024_AI/releases/latest");
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _versionLabel.Text =
                    $"{GetApplicationVersion()} â€” no GitHub release published";
                return;
            }
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var match = System.Text.RegularExpressions.Regex.Match(
                json,
                "\"tag_name\"\\s*:\\s*\"v?(?<version>[^\"]+)\"");
            if (!match.Success
                || !Version.TryParse(match.Groups["version"].Value, out var latest))
            {
                _versionLabel.Text =
                    $"{GetApplicationVersion()} â€” release status unavailable";
                return;
            }

            var current =
                Assembly.GetExecutingAssembly().GetName().Version
                ?? new Version();
            _versionLabel.Text = latest > current
                ? $"{GetApplicationVersion()} â€” update available: {latest}"
                : $"{GetApplicationVersion()} â€” up to date";
            _versionLabel.ForeColor = latest > current
                ? System.Drawing.Color.DarkOrange
                : System.Drawing.Color.DarkGreen;
        }
        catch (Exception ex)
        {
            _versionLabel.Text =
                $"{GetApplicationVersion()} â€” update check unavailable";
            AppLog.Write($"GitHub update check failed: {ex.Message}");
        }
    }

    private void RefreshFlowList(string recommendedId, string? activeId)
    {
        if (_flowList == null)
        {
            return;
        }

        var selectedIndex = _flowList.SelectedIndex;
        var topIndex = _flowList.Items.Count > 0 ? _flowList.TopIndex : 0;
        _flowList.BeginUpdate();
        for (var index = 0; index < ProcedureCatalog.ForAircraft(_state).Count; index++)
        {
            var procedure = ProcedureCatalog.ForAircraft(_state)[index];
            var item = new ProcedureListItem(
                procedure,
                _completedProcedureIds.Contains(procedure.Id),
                procedure.Id == recommendedId,
                procedure.Id == activeId);
            if (index < _flowList.Items.Count)
            {
                _flowList.Items[index] = item;
            }
            else
            {
                _flowList.Items.Add(item);
            }
        }
        while (_flowList.Items.Count > ProcedureCatalog.ForAircraft(_state).Count)
        {
            _flowList.Items.RemoveAt(_flowList.Items.Count - 1);
        }
        _flowList.EndUpdate();

        if (selectedIndex >= 0 && selectedIndex < _flowList.Items.Count)
        {
            _flowList.SelectedIndex = selectedIndex;
        }
        if (_flowList.Items.Count > 0)
        {
            _flowList.TopIndex = Math.Max(
                0,
                Math.Min(topIndex, _flowList.Items.Count - 1));
        }
    }

    private void FinishOneShot(int exitCode = 0)
    {
        if (_oneShotCommand == null
            || _pendingProcedure != null
            || _pendingBeaconProcedure != null
            || _pendingNavLogoSelectorProcedure != null
            || _pendingBatteryProcedure != null
            || _pendingNativeAction != null
            || _pendingFireTest != null
            || _pendingFlyByWireFireTest.HasValue
            || _pendingFuelPumpSequence != null)
        {
            return;
        }

        if (_oneShotCommand.StartsWith("procedure start ", StringComparison.OrdinalIgnoreCase)
            && _procedureRunner.Status is ProcedureStatus.Running
                or ProcedureStatus.WaitingForManualAction
                or ProcedureStatus.WaitingForVerification)
        {
            return;
        }

        Environment.ExitCode = exitCode;
        Application.ExitThread();
    }

    private static void OnException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
    {
        var exception = (SIMCONNECT_EXCEPTION)data.dwException;
        if (exception == SIMCONNECT_EXCEPTION.ALREADY_CREATED)
        {
            return;
        }

        Console.Error.WriteLine($"SimConnect exception: {exception}");
        AppLog.Write($"SimConnect exception: {exception}");
    }

    private void OnQuit(SimConnect sender, SIMCONNECT_RECV data)
    {
        Console.WriteLine("MSFS closed the SimConnect session.");
        AppLog.Write("MSFS closed the SimConnect session.");
        _mobiFlightReady = false;
        _simConnect?.Dispose();
        _simConnect = null;
        if (_connectionLabel != null)
        {
            _connectionLabel.Text = "Disconnected; waiting for MSFS...";
            _connectionLabel.ForeColor = System.Drawing.Color.DarkRed;
        }
        ScheduleReconnect();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pendingFuelPumpSequence = null;
            StopFuelPumpSequenceTimer();
            if (_pendingFireTest != null)
            {
                if (_simConnect != null)
                {
                    SetFireTestPressed(
                        _pendingFireTest.System,
                        _pendingFireTest.InputEventHash,
                        false);
                }
                _pendingFireTest = null;
            }
            if (_pendingFlyByWireFireTest.HasValue)
            {
                SetFlyByWireFireTestPressed(_pendingFlyByWireFireTest.Value, false);
                _pendingFlyByWireFireTest = null;
            }
            _reconnectTimer?.Dispose();
            foreach (var pulseTimer in _nativePulseTimers.ToArray())
            {
                pulseTimer.Dispose();
            }
            _nativePulseTimers.Clear();
            StopReplay();
            _flightTelemetryStore.Dispose();
            _voiceCalloutQueue?.Dispose();
            _voiceCalloutQueue = null;
            _simConnect?.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class PendingExternalPowerProcedure
    {
        public PendingExternalPowerProcedure(bool desiredOn, DateTime deadlineUtc)
        {
            DesiredOn = desiredOn;
            DeadlineUtc = deadlineUtc;
        }

        public bool DesiredOn { get; }
        public DateTime DeadlineUtc { get; }
    }

    private sealed class ReplayFlightItem
    {
        public ReplayFlightItem(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public override string ToString() =>
            System.IO.Path.GetFileNameWithoutExtension(Path)
                .Replace("flight-", "Flight ");
    }

    private sealed class PendingBeaconProcedure
    {
        public PendingBeaconProcedure(bool desiredOn, DateTime deadlineUtc)
        {
            DesiredOn = desiredOn;
            DeadlineUtc = deadlineUtc;
        }

        public bool DesiredOn { get; }
        public DateTime DeadlineUtc { get; }
    }

    private sealed class PendingNavLogoSelectorProcedure
    {
        public PendingNavLogoSelectorProcedure(int desiredPosition, DateTime deadlineUtc)
        {
            DesiredPosition = desiredPosition;
            DeadlineUtc = deadlineUtc;
        }

        public int DesiredPosition { get; }
        public DateTime DeadlineUtc { get; }
    }

    private sealed class PendingBatteryProcedure
    {
        public PendingBatteryProcedure(
            int batteryNumber,
            bool desiredOn,
            DateTime deadlineUtc)
        {
            BatteryNumber = batteryNumber;
            DesiredOn = desiredOn;
            DeadlineUtc = deadlineUtc;
        }

        public int BatteryNumber { get; }
        public bool DesiredOn { get; }
        public DateTime DeadlineUtc { get; }
    }

    private sealed class PendingNativeAction
    {
        public PendingNativeAction(
            string name,
            Func<AircraftState, bool> verify,
            bool desiredOn,
            string desiredLabel,
            DateTime deadlineUtc)
        {
            Name = name;
            Verify = verify;
            DesiredOn = desiredOn;
            DesiredLabel = desiredLabel;
            DeadlineUtc = deadlineUtc;
        }

        public string Name { get; }
        public Func<AircraftState, bool> Verify { get; }
        public bool DesiredOn { get; }
        public string DesiredLabel { get; }
        public DateTime DeadlineUtc { get; }
    }

    private sealed class PendingFireTest
    {
        public PendingFireTest(
            FireTestSystem system,
            ulong inputEventHash,
            DateTime deadlineUtc)
        {
            System = system;
            InputEventHash = inputEventHash;
            DeadlineUtc = deadlineUtc;
        }

        public FireTestSystem System { get; }
        public ulong InputEventHash { get; }
        public bool ActivationObserved { get; set; }
        public bool ReleaseSent { get; set; }
        public DateTime ReleaseUtc { get; set; }
        public DateTime DeadlineUtc { get; set; }
    }

    private sealed class PendingFuelPumpSequence
    {
        public PendingFuelPumpSequence(
            Queue<FuelPumpToggle> toggles,
            bool desiredOn)
        {
            Toggles = toggles;
            DesiredOn = desiredOn;
        }

        public Queue<FuelPumpToggle> Toggles { get; }
        public bool DesiredOn { get; }
    }

    private sealed class FuelPumpToggle
    {
        public FuelPumpToggle(
            int number,
            string calculatorCode)
        {
            Number = number;
            CalculatorCode = calculatorCode;
        }

        public int Number { get; }
        public string CalculatorCode { get; }
    }

    private sealed class ProcedureListItem
    {
        public ProcedureListItem(
            ProcedureDefinition definition,
            bool completed = false,
            bool recommended = false,
            bool active = false)
        {
            Definition = definition;
            Completed = completed;
            Recommended = recommended;
            Active = active;
        }

        public ProcedureDefinition Definition { get; }
        public bool Completed { get; }
        public bool Recommended { get; }
        public bool Active { get; }
        public string DisplayName
        {
            get
            {
                var status = Completed
                    ? "[DONE]"
                    : Active
                        ? "[ACTIVE]"
                        : Recommended
                            ? "[NEXT]"
                            : "[    ]";
                return $"{status} {Definition.Name} - {Definition.AutomationSummary}";
            }
        }

        public override string ToString() =>
            $"{(Completed ? "âœ“" : Active ? "â–¶" : Recommended ? "â†’" : " ")} " +
            $"{Definition.Name} â€” {Definition.AutomationSummary}";
    }
}

internal static class DisplayExtensions
{
    public static string ToOnOff(this bool value) => value ? "ON" : "OFF";
    public static string ToYesNo(this bool value) => value ? "YES" : "NO";
    public static string ToSetReleased(this bool value) => value ? "SET" : "RELEASED";
}
