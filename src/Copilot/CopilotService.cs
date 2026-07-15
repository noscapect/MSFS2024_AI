using Microsoft.FlightSimulator.SimConnect;
using Msfs2024Ai.Copilot.AircraftAdapters.FbwA320;
using Msfs2024Ai.Copilot.AircraftAdapters.IniBuildsA320;
using Msfs2024Ai.Copilot.AircraftAdapters.IniBuildsA321;
using Msfs2024Ai.Copilot.AircraftIdentity;
using Msfs2024Ai.Copilot.Checklists;
using Msfs2024Ai.Copilot.Controls;
using Msfs2024Ai.Copilot.Diagnostics;
using Msfs2024Ai.Copilot.Domain;
using Msfs2024Ai.Copilot.Procedures;
using Msfs2024Ai.Copilot.Settings;
using Msfs2024Ai.Copilot.SimBrief;
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
    private const bool EnableExperimentalFlyByWireA380X = false;

    private const int WmUserSimConnect = 0x0402;
    private const double MetersPerNauticalMile = 1852.0;
    private const uint PmdgNg3DataId = 0x4E473331;
    private const uint PmdgNg3DataDefinition = 0x4E473332;
    private const uint PmdgNg3ControlId = 0x4E473333;
    private const uint PmdgNg3ControlDefinition = 0x4E473334;
    private const int PmdgNg3DataSize = 914;
    private const uint ThirdPartyEventIdMin = 0x00011000;
    private const uint PmdgMouseRightSingle = 0x80000000;
    private const uint PmdgMouseLeftSingle = 0x20000000;
    private const double PmdgCenterFuelPumpRequiredThresholdPounds = 500;
    private static readonly TimeSpan PmdgApuBleedWarmup = TimeSpan.FromSeconds(60);
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
    private readonly AircraftIdentityResolver _aircraftIdentityResolver = new();
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
    private System.Windows.Forms.Timer? _a330InputEventPollingTimer;
    private readonly List<System.Windows.Forms.Timer> _nativePulseTimers = new();
    private bool _mobiFlightReady;
    private bool _mobiFlightRuntimeReady;
    private DateTime? _mobiFlightRuntimeInitializedUtc;
    private bool _pmdgNg3DataReady;
    private PmdgNg3State? _pmdgNg3State;
    private byte? _loggedPmdgBatterySelector;
    private bool? _loggedPmdgGroundPowerAvailable;
    private bool? _loggedPmdgGroundPowerOn;
    private bool _pmdgApuGenOffBusSeen;
    private string? _loggedPmdgElectricalBytes;
    private string? _loggedPmdgAirStartSignature;
    private DateTime? _pmdgApuAvailableSinceUtc;
    private float? _pmdgCommandedLeftIrsMode;
    private float? _pmdgCommandedRightIrsMode;
    private DateTime? _pmdgCommandedLeftIrsModeUtc;
    private DateTime? _pmdgCommandedRightIrsModeUtc;
    private bool? _pmdgCommandedLogoLightOn;
    private DateTime? _pmdgCommandedLogoLightUtc;
    private float? _pmdgCommandedPositionStrobeSelector;
    private DateTime? _pmdgCommandedPositionStrobeUtc;
    private float? _pmdgCommandedLandingLightSelector;
    private DateTime? _pmdgCommandedLandingLightUtc;
    private float? _pmdgCommandedEmergencyExitSelector;
    private DateTime? _pmdgCommandedEmergencyExitUtc;
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
    private bool? _fbwA380ExternalPower1AvailableTyped;
    private bool? _fbwA380ExternalPower1OnTyped;
    private bool? _fbwA380ExternalPower2AvailableTyped;
    private bool? _fbwA380ExternalPower2OnTyped;
    private bool? _fbwA380ExternalPower3AvailableTyped;
    private bool? _fbwA380ExternalPower3OnTyped;
    private bool? _fbwA380ExternalPower4AvailableTyped;
    private bool? _fbwA380ExternalPower4OnTyped;
    private double? _lastLoggedA380ExternalPowerDirectSignature;
    private double? _lastLoggedA380AcPowerSignature;
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
    private DateTime? _fbwCommandedCrewOxygenUtc;
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
    private readonly double?[] _a330FuelPumpInputStates = new double?[6];
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
    private bool? _a330ApuBatteryInputEventOn;
    private static readonly ulong[] A330FuelPumpInputEventHashes =
    {
        17160241956476466648UL, // AIRLINER_FUEL_ENG1_L1
        2969085048935345773UL,  // AIRLINER_FUEL_ENG1_L2
        4057842237641144121UL,  // AIRLINER_FUEL_ENG1_LSTBY
        14122780585044930898UL, // AIRLINER_FUEL_ENG2_RSTBY
        3693509800080360825UL,  // AIRLINER_FUEL_ENG2_R1
        17604810245581348556UL  // AIRLINER_FUEL_ENG2_R2
    };
    private static readonly ulong[] A330SignInputEventHashes =
    {
        9259149979614333466UL,  // AIRLINER_SEATBELTS_TOGGLE
        17089552564781619528UL, // AIRLINER_NOSMOKING_TOGGLE
        10225559723282857283UL  // AIRLINER_EMER_EXIT_TOGGLE
    };
    private readonly double?[] _a330SignInputStates = new double?[3];
    private static readonly ulong[] A330AdirsInputEventHashes =
    {
        13492439889652946135UL, // AIRLINER_ADIRS1_MODE
        16561688374715259608UL, // AIRLINER_ADIRS2_MODE
        1287651589091488428UL   // AIRLINER_ADIRS3_MODE
    };
    private readonly double?[] _a330AdirsInputStates = new double?[3];
    private const ulong A330StrobeInputEventHash = 10028340691099543317UL;
    private double? _a330StrobeInputState;
    private const ulong A330NavLogoInputEventHash = 10348631634011558414UL;
    private double? _a330NavLogoInputState;
    private static readonly ulong[] A330ApuInputEventHashes =
    {
        4080745756015573070UL, // AIRLINER_APU_MASTER
        9344724743939237602UL, // AIRLINER_APU_START
        8638866639146676618UL  // AIRLINER_AIR_APU_BLEED
    };
    private readonly double?[] _a330ApuInputStates = new double?[3];
    private const ulong A330TransponderModeInputEventHash = 14182293921746398447UL;
    private double? _a330TransponderModeInputState;
    private const ulong A330CrewOxygenInputEventHash = 8814143036634973369UL;
    private double? _a330CrewOxygenInputState;
    private const ulong A330SpoilerLeverInputEventHash = 1712305263919831311UL;
    private double? _a330SpoilerLeverInputState;
    private bool? _a330CommandedSpoilersArmed;
    private const ulong A330FlapsInputEventHash = 10630178068256299397UL;
    private double? _a330FlapsInputState;
    private static readonly ulong[] A330AutobrakeInputEventHashes =
    {
        7289021414699629450UL,  // AIRLINER_AUTOBRK_LO
        3008453113287741137UL,  // AIRLINER_AUTOBRK_MED
        10376295413381294961UL  // AIRLINER_AUTOBRK_HI
    };
    private readonly double?[] _a330AutobrakeInputStates = new double?[3];
    private const ulong A330WeatherRadarPwsInputEventHash = 16710120045550625168UL;
    private double? _a330WeatherRadarPwsInputState;
    private const ulong A330NoseLightInputEventHash = 7704909914815877606UL;
    private double? _a330NoseLightInputState;
    private const ulong A330TcasTrafficInputEventHash = 11751227568307765711UL;
    private double? _a330TcasTrafficInputState;
    private const ulong A330TcasAltitudeInputEventHash = 8240611082898456697UL;
    private double? _a330TcasAltitudeInputState;
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
    private PictureBox? _aircraftThumbnailBox;
    private Label? _aircraftCardTitleLabel;
    private Label? _aircraftCardVariationLabel;
    private Label? _aircraftCardSourceLabel;
    private string? _aircraftCardTitle;
    private string? _aircraftCardResolvedTitle;
    private IReadOnlyList<string> _aircraftCardImagePaths = Array.Empty<string>();
    private int _aircraftCardImageIndex;
    private CancellationTokenSource? _aircraftIdentityLookupCancellation;
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
    private Button? _startSelectedFlowButton;
    private Button? _confirmCompletedButton;
    private Button? _simBriefImportButton;
    private ImportedFlightPlan? _simBriefFlightPlan;
    private bool _simBriefImportInProgress;

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
        FbwA380ExternalPower1AvailableTyped = 201,
        FbwA380ExternalPower1OnTyped = 202,
        FbwA380ExternalPower2AvailableTyped = 203,
        FbwA380ExternalPower2OnTyped = 204,
        FbwA380ExternalPower3AvailableTyped = 205,
        FbwA380ExternalPower3OnTyped = 206,
        FbwA380ExternalPower4AvailableTyped = 207,
        FbwA380ExternalPower4OnTyped = 208,
        A330ApuBatteryInputEvent = 210,
        A330FuelPump1InputEvent = 211,
        A330FuelPump2InputEvent = 212,
        A330FuelPump3InputEvent = 213,
        A330FuelPump4InputEvent = 214,
        A330FuelPump5InputEvent = 215,
        A330FuelPump6InputEvent = 216,
        A330SeatbeltsInputEvent = 217,
        A330NoSmokingInputEvent = 218,
        A330EmergencyExitInputEvent = 219,
        A330Adirs1InputEvent = 220,
        A330Adirs2InputEvent = 221,
        A330Adirs3InputEvent = 222,
        A330StrobeInputEvent = 223,
        A330NavLogoInputEvent = 224,
        A330ApuMasterInputEvent = 225,
        A330ApuStartInputEvent = 226,
        A330ApuBleedInputEvent = 227,
        A330TransponderModeInputEvent = 228,
        A330CrewOxygenInputEvent = 229,
        A330SpoilerLeverInputEvent = 230,
        A330FlapsInputEvent = 231,
        A330AutobrakeLowInputEvent = 232,
        A330AutobrakeMediumInputEvent = 233,
        A330AutobrakeHighInputEvent = 234,
        A330WeatherRadarPwsInputEvent = 235,
        A330NoseLightInputEvent = 236,
        A330TcasTrafficInputEvent = 237,
        A330TcasAltitudeInputEvent = 238,
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
        FbwA380ExternalPower1AvailableTyped = 201,
        FbwA380ExternalPower1OnTyped = 202,
        FbwA380ExternalPower2AvailableTyped = 203,
        FbwA380ExternalPower2OnTyped = 204,
        FbwA380ExternalPower3AvailableTyped = 205,
        FbwA380ExternalPower3OnTyped = 206,
        FbwA380ExternalPower4AvailableTyped = 207,
        FbwA380ExternalPower4OnTyped = 208,
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
        CabinSeatbeltsToggle,
        GearUp,
        GearDown,
        RotorBrake
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
        public double Battery3;
        public double Battery1Voltage;
        public double Battery2Voltage;
        public double Battery3Voltage;
        public double ExternalPowerAvailable;
        public double ExternalPowerOn;
        public double ExternalPower2Available;
        public double ExternalPower2On;
        public double ExternalPowerAvailableUnindexed;
        public double ExternalPowerOnUnindexed;
        public double FbwA380ExternalPower1Available;
        public double FbwA380ExternalPower1On;
        public double FbwA380ExternalPower2Available;
        public double FbwA380ExternalPower2On;
        public double FbwA380ExternalPower3Available;
        public double FbwA380ExternalPower3On;
        public double FbwA380ExternalPower4Available;
        public double FbwA380ExternalPower4On;
        public double FbwA380AcBus1Powered;
        public double FbwA380AcBus2Powered;
        public double FbwA380AcBus3Powered;
        public double FbwA380AcBus4Powered;
        public double ParkingBrake;
        public double Beacon;
        public double NavigationLights;
        public double LogoLights;
        public double TaxiLight;
        public double FbwNoseLightSelectorPosition;
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
        public double AutopilotApproachHold;
        public double AutopilotGlideslopeHold;
        public double Nav1HasLocalizer;
        public double Nav1HasGlideslope;
        public double Nav2HasLocalizer;
        public double Nav2HasGlideslope;
        public double Nav1ActiveFrequency;
        public double Nav2ActiveFrequency;
        public double Nav1Course;
        public double Nav2Course;
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
        public bool IrsLeftAlignLight { get; set; }
        public bool IrsRightAlignLight { get; set; }
        public bool IrsLeftOnDcLight { get; set; }
        public bool IrsRightOnDcLight { get; set; }
        public bool IrsLeftFault { get; set; }
        public bool IrsRightFault { get; set; }
        public bool IrsAligned { get; set; }
        public bool Engine1StartValveOpen { get; set; }
        public bool Engine2StartValveOpen { get; set; }
        public bool Engine1ReverserAnnunciated { get; set; }
        public bool Engine2ReverserAnnunciated { get; set; }
        public bool LeftForwardFuelPump { get; set; }
        public bool RightForwardFuelPump { get; set; }
        public bool LeftAftFuelPump { get; set; }
        public bool RightAftFuelPump { get; set; }
        public bool LeftCenterFuelPump { get; set; }
        public bool RightCenterFuelPump { get; set; }
        public float CenterFuelQuantityPounds { get; set; }
        public byte BatterySelector { get; set; }
        public bool GroundPowerAvailable { get; set; }
        public bool GroundPowerOn { get; set; }
        public bool DcBus1Powered { get; set; }
        public bool DcBus2Powered { get; set; }
        public bool AcTransferBus1Powered { get; set; }
        public bool AcTransferBus2Powered { get; set; }
        public bool EngineGen1On { get; set; }
        public bool EngineGen2On { get; set; }
        public bool ApuGen1On { get; set; }
        public bool ApuGen2On { get; set; }
        public bool TransferBus1Off { get; set; }
        public bool TransferBus2Off { get; set; }
        public bool Source1Off { get; set; }
        public bool Source2Off { get; set; }
        public bool GenBus1Off { get; set; }
        public bool GenBus2Off { get; set; }
        public bool ApuGenOffBus { get; set; }
        public float ApuEgtNeedle { get; set; }
        public bool ApuAvailableForTransfer => ApuGenOffBus;
        public bool ElectricHydraulicPump1On { get; set; }
        public bool ElectricHydraulicPump2On { get; set; }
        public bool ElectricHydraulicPump1LowPressure { get; set; }
        public bool ElectricHydraulicPump2LowPressure { get; set; }
        public byte EmergencyExitLights { get; set; }
        public byte NoSmokingSelector { get; set; }
        public byte FastenBeltsSelector { get; set; }
        public byte LeftPackSwitch { get; set; }
        public byte RightPackSwitch { get; set; }
        public bool ApuBleedOn { get; set; }
        public byte IsolationValveSwitch { get; set; }
        public float LeftDuctPressurePsi { get; set; }
        public float RightDuctPressurePsi { get; set; }
        public byte LeftLandingLight { get; set; }
        public byte RightLandingLight { get; set; }
        public bool LeftRunwayTurnoffLight { get; set; }
        public bool RightRunwayTurnoffLight { get; set; }
        public bool TaxiLightOn { get; set; }
        public byte ApuSelector { get; set; }
        public byte Engine1StartSelector { get; set; }
        public byte Engine2StartSelector { get; set; }
        public bool LogoLightOn { get; set; }
        public byte PositionStrobeSelector { get; set; }
        public bool AntiCollisionOn { get; set; }
        public bool SpeedbrakeArmed { get; set; }
        public bool SpeedbrakeExtended { get; set; }
        public byte AutobrakeSelector { get; set; }
        public bool AutobrakeDisarmed { get; set; }
        public float BrakePressureNeedle { get; set; }
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
        public byte LandingVref { get; set; }
        public bool FmcPerfInputComplete { get; set; }
        public bool GroundConnectionAvailable { get; set; }
    }

    public CopilotService(string? oneShotCommand, bool showUi)
    {
        _oneShotCommand = oneShotCommand;
        _showUi = showUi;
        _settings = SettingsStore.Load();
        _simBriefFlightPlan = SimBriefCacheStore.Load();
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
        Text = "MSFS 2024 Virtual First Officer";
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
                "MSFS 2024 Virtual First Officer",
                Handle,
                WmUserSimConnect,
                null,
                0);

            _simConnect.OnRecvOpen += OnOpen;
            _simConnect.OnRecvQuit += OnQuit;
            _simConnect.OnRecvException += OnException;
            _simConnect.OnRecvSimobjectData += OnAircraftData;
            _simConnect.OnRecvClientData += OnClientData;
            _simConnect.OnRecvGetInputEvent += OnGetInputEvent;
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
            $"Connected - SimConnect {data.dwSimConnectVersionMajor}.{data.dwSimConnectVersionMinor}, " +
            $"simulator {data.dwApplicationVersionMajor}.{data.dwApplicationVersionMinor}.");
        AppendDashboardLog(
            $"Connected to MSFS - SimConnect {data.dwSimConnectVersionMajor}.{data.dwSimConnectVersionMinor}");
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
        sender.AddToDataDefinition(Definition.AircraftState, "ELECTRICAL MASTER BATTERY:3", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "ELECTRICAL BATTERY VOLTAGE:1", "Volts", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "ELECTRICAL BATTERY VOLTAGE:2", "Volts", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "ELECTRICAL BATTERY VOLTAGE:3", "Volts", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "EXTERNAL POWER AVAILABLE:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "EXTERNAL POWER ON:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "EXTERNAL POWER AVAILABLE:2", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "EXTERNAL POWER ON:2", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "EXTERNAL POWER AVAILABLE", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "EXTERNAL POWER ON", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:A32NX_EXT_PWR_AVAIL:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:A32NX_OVHD_ELEC_EXT_PWR_1_PB_IS_ON", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:A32NX_EXT_PWR_AVAIL:2", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:A32NX_OVHD_ELEC_EXT_PWR_2_PB_IS_ON", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:A32NX_EXT_PWR_AVAIL:3", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:A32NX_OVHD_ELEC_EXT_PWR_3_PB_IS_ON", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:A32NX_EXT_PWR_AVAIL:4", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:A32NX_OVHD_ELEC_EXT_PWR_4_PB_IS_ON", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:A32NX_ELEC_AC_1_BUS_IS_POWERED", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:A32NX_ELEC_AC_2_BUS_IS_POWERED", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:A32NX_ELEC_AC_3_BUS_IS_POWERED", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:A32NX_ELEC_AC_4_BUS_IS_POWERED", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "BRAKE PARKING POSITION", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "LIGHT BEACON", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "LIGHT NAV", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "LIGHT LOGO", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "LIGHT TAXI", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:LIGHTING_LANDING_1", "Enum", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
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
        sender.AddToDataDefinition(Definition.AircraftState, "AUTOPILOT APPROACH HOLD", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "AUTOPILOT GLIDESLOPE HOLD", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "NAV HAS LOCALIZER:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "NAV HAS GLIDE SLOPE:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "NAV HAS LOCALIZER:2", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "NAV HAS GLIDE SLOPE:2", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "NAV ACTIVE FREQUENCY:1", "MHz", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "NAV ACTIVE FREQUENCY:2", "MHz", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "NAV OBS:1", "Degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "NAV OBS:2", "Degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
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
        sender.MapClientEventToSimEvent(CopilotEvent.GearUp, "GEAR_UP");
        sender.MapClientEventToSimEvent(CopilotEvent.GearDown, "GEAR_DOWN");
        sender.MapClientEventToSimEvent(CopilotEvent.RotorBrake, "ROTOR_BRAKE");
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

        _a330InputEventPollingTimer?.Stop();
        _a330InputEventPollingTimer?.Dispose();
        _a330InputEventPollingTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _a330InputEventPollingTimer.Tick += (_, _) =>
        {
            if (_state?.IsIniBuildsA330 != true)
            {
                return;
            }

            try
            {
                sender.GetInputEvent(Request.A330ApuBatteryInputEvent, 14438692519264741429UL);
                for (var index = 0; index < A330FuelPumpInputEventHashes.Length; index++)
                {
                    sender.GetInputEvent((Request)((int)Request.A330FuelPump1InputEvent + index), A330FuelPumpInputEventHashes[index]);
                }
                for (var index = 0; index < A330SignInputEventHashes.Length; index++)
                {
                    sender.GetInputEvent((Request)((int)Request.A330SeatbeltsInputEvent + index), A330SignInputEventHashes[index]);
                }
                for (var index = 0; index < A330AdirsInputEventHashes.Length; index++)
                {
                    sender.GetInputEvent((Request)((int)Request.A330Adirs1InputEvent + index), A330AdirsInputEventHashes[index]);
                }
                sender.GetInputEvent(Request.A330StrobeInputEvent, A330StrobeInputEventHash);
                sender.GetInputEvent(Request.A330NavLogoInputEvent, A330NavLogoInputEventHash);
                for (var index = 0; index < A330ApuInputEventHashes.Length; index++)
                {
                    sender.GetInputEvent((Request)((int)Request.A330ApuMasterInputEvent + index), A330ApuInputEventHashes[index]);
                }
                sender.GetInputEvent(Request.A330TransponderModeInputEvent, A330TransponderModeInputEventHash);
                sender.GetInputEvent(Request.A330CrewOxygenInputEvent, A330CrewOxygenInputEventHash);
                sender.GetInputEvent(Request.A330SpoilerLeverInputEvent, A330SpoilerLeverInputEventHash);
                sender.GetInputEvent(Request.A330FlapsInputEvent, A330FlapsInputEventHash);
                for (var index = 0; index < A330AutobrakeInputEventHashes.Length; index++)
                {
                    sender.GetInputEvent((Request)((int)Request.A330AutobrakeLowInputEvent + index), A330AutobrakeInputEventHashes[index]);
                }
                sender.GetInputEvent(Request.A330WeatherRadarPwsInputEvent, A330WeatherRadarPwsInputEventHash);
                sender.GetInputEvent(Request.A330NoseLightInputEvent, A330NoseLightInputEventHash);
                sender.GetInputEvent(Request.A330TcasTrafficInputEvent, A330TcasTrafficInputEventHash);
                sender.GetInputEvent(Request.A330TcasAltitudeInputEvent, A330TcasAltitudeInputEventHash);
            }
            catch (COMException exception)
            {
                AppLog.Write($"A330 InputEvent poll failed: {exception.Message}");
            }
        };
        _a330InputEventPollingTimer.Start();

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

    private void OnGetInputEvent(SimConnect sender, SIMCONNECT_RECV_GET_INPUT_EVENT data)
    {
        var request = (Request)data.dwRequestID;
        var numericValue = TryReadInputEventNumber(data.Value);
        if (!numericValue.HasValue)
        {
            return;
        }

        if (request is >= Request.A330FuelPump1InputEvent and <= Request.A330FuelPump6InputEvent)
        {
            var pumpIndex = (int)request - (int)Request.A330FuelPump1InputEvent;
            var wasOn = _a330FuelPumpInputStates[pumpIndex].HasValue
                        && _a330FuelPumpInputStates[pumpIndex]!.Value >= 0.5;
            _a330FuelPumpInputStates[pumpIndex] = numericValue.Value;
            var pumpIsOn = numericValue.Value >= 0.5;
            if (wasOn != pumpIsOn)
            {
                AppLog.Write($"A330 fuel pump InputEvent {pumpIndex + 1}={numericValue.Value:0.###} ({pumpIsOn.ToOnOff()}).");
            }

            ApplyNativeAircraftState();
            return;
        }

        if (request is >= Request.A330SeatbeltsInputEvent and <= Request.A330EmergencyExitInputEvent)
        {
            var signIndex = (int)request - (int)Request.A330SeatbeltsInputEvent;
            var previous = _a330SignInputStates[signIndex];
            _a330SignInputStates[signIndex] = numericValue.Value;
            if (!previous.HasValue || Math.Abs(previous.Value - numericValue.Value) >= 0.1)
            {
                AppLog.Write($"A330 sign InputEvent {signIndex + 1}={numericValue.Value:0.###}.");
            }

            ApplyNativeAircraftState();
            return;
        }

        if (request is >= Request.A330Adirs1InputEvent and <= Request.A330Adirs3InputEvent)
        {
            var adirsIndex = (int)request - (int)Request.A330Adirs1InputEvent;
            var previous = _a330AdirsInputStates[adirsIndex];
            _a330AdirsInputStates[adirsIndex] = numericValue.Value;
            if (!previous.HasValue || Math.Abs(previous.Value - numericValue.Value) >= 0.1)
            {
                AppLog.Write($"A330 ADIRS {adirsIndex + 1} InputEvent={numericValue.Value:0.###}.");
            }

            ApplyNativeAircraftState();
            return;
        }

        if (request == Request.A330StrobeInputEvent)
        {
            var previous = _a330StrobeInputState;
            _a330StrobeInputState = numericValue.Value;
            if (!previous.HasValue || Math.Abs(previous.Value - numericValue.Value) >= 0.1)
            {
                AppLog.Write($"A330 strobe InputEvent={numericValue.Value:0.###}.");
            }

            ApplyNativeAircraftState();
            return;
        }

        if (request == Request.A330NavLogoInputEvent)
        {
            var previous = _a330NavLogoInputState;
            _a330NavLogoInputState = numericValue.Value;
            if (!previous.HasValue || Math.Abs(previous.Value - numericValue.Value) >= 0.1)
            {
                AppLog.Write($"A330 NAV/LOGO InputEvent={numericValue.Value:0.###}.");
            }

            ApplyNativeAircraftState();
            return;
        }

        if (request is >= Request.A330ApuMasterInputEvent and <= Request.A330ApuBleedInputEvent)
        {
            var apuIndex = (int)request - (int)Request.A330ApuMasterInputEvent;
            var previous = _a330ApuInputStates[apuIndex];
            _a330ApuInputStates[apuIndex] = numericValue.Value;
            if (!previous.HasValue || Math.Abs(previous.Value - numericValue.Value) >= 0.1)
            {
                AppLog.Write($"A330 APU InputEvent {apuIndex + 1}={numericValue.Value:0.###}.");
            }

            ApplyNativeAircraftState();
            return;
        }

        if (request == Request.A330TransponderModeInputEvent)
        {
            var previous = _a330TransponderModeInputState;
            _a330TransponderModeInputState = numericValue.Value;
            if (!previous.HasValue || Math.Abs(previous.Value - numericValue.Value) >= 0.1)
            {
                AppLog.Write($"A330 transponder mode InputEvent={numericValue.Value:0.###}.");
            }

            ApplyNativeAircraftState();
            return;
        }

        if (request == Request.A330CrewOxygenInputEvent)
        {
            var previous = _a330CrewOxygenInputState;
            _a330CrewOxygenInputState = numericValue.Value;
            if (!previous.HasValue || Math.Abs(previous.Value - numericValue.Value) >= 0.1)
            {
                AppLog.Write($"A330 crew oxygen InputEvent={numericValue.Value:0.###}.");
            }

            ApplyNativeAircraftState();
            return;
        }

        if (request == Request.A330SpoilerLeverInputEvent)
        {
            var previous = _a330SpoilerLeverInputState;
            _a330SpoilerLeverInputState = numericValue.Value;
            if (!previous.HasValue || Math.Abs(previous.Value - numericValue.Value) >= 0.01)
            {
                AppLog.Write($"A330 spoiler lever InputEvent={numericValue.Value:0.###}.");
            }

            return;
        }

        if (request == Request.A330FlapsInputEvent)
        {
            var previous = _a330FlapsInputState;
            _a330FlapsInputState = numericValue.Value;
            if (!previous.HasValue || Math.Abs(previous.Value - numericValue.Value) >= 0.01)
            {
                AppLog.Write($"A330 flaps InputEvent={numericValue.Value:0.###}.");
            }

            ApplyNativeAircraftState();
            return;
        }

        if (request is >= Request.A330AutobrakeLowInputEvent and <= Request.A330AutobrakeHighInputEvent)
        {
            var autobrakeIndex = (int)request - (int)Request.A330AutobrakeLowInputEvent;
            var previous = _a330AutobrakeInputStates[autobrakeIndex];
            _a330AutobrakeInputStates[autobrakeIndex] = numericValue.Value;
            if (!previous.HasValue || Math.Abs(previous.Value - numericValue.Value) >= 0.1)
            {
                AppLog.Write($"A330 autobrake InputEvent {autobrakeIndex + 1}={numericValue.Value:0.###}.");
            }

            ApplyNativeAircraftState();
            return;
        }

        if (request == Request.A330WeatherRadarPwsInputEvent)
        {
            var previous = _a330WeatherRadarPwsInputState;
            _a330WeatherRadarPwsInputState = numericValue.Value;
            if (!previous.HasValue || Math.Abs(previous.Value - numericValue.Value) >= 0.1)
            {
                AppLog.Write($"A330 WXR/PWS InputEvent={numericValue.Value:0.###}.");
            }

            ApplyNativeAircraftState();
            return;
        }

        if (request == Request.A330NoseLightInputEvent)
        {
            var previous = _a330NoseLightInputState;
            _a330NoseLightInputState = numericValue.Value;
            if (!previous.HasValue || Math.Abs(previous.Value - numericValue.Value) >= 0.1)
            {
                AppLog.Write($"A330 nose light InputEvent={numericValue.Value:0.###}.");
            }

            ApplyNativeAircraftState();
            return;
        }

        if (request == Request.A330TcasTrafficInputEvent)
        {
            var previous = _a330TcasTrafficInputState;
            _a330TcasTrafficInputState = numericValue.Value;
            if (!previous.HasValue || Math.Abs(previous.Value - numericValue.Value) >= 0.1)
            {
                AppLog.Write($"A330 TCAS traffic InputEvent={numericValue.Value:0.###}.");
            }

            ApplyNativeAircraftState();
            return;
        }

        if (request == Request.A330TcasAltitudeInputEvent)
        {
            var previous = _a330TcasAltitudeInputState;
            _a330TcasAltitudeInputState = numericValue.Value;
            if (!previous.HasValue || Math.Abs(previous.Value - numericValue.Value) >= 0.1)
            {
                AppLog.Write($"A330 TCAS altitude InputEvent={numericValue.Value:0.###}.");
            }

            ApplyNativeAircraftState();
            return;
        }

        if (request != Request.A330ApuBatteryInputEvent)
        {
            return;
        }

        var isOn = numericValue.Value >= 0.5;
        if (_a330ApuBatteryInputEventOn != isOn)
        {
            _a330ApuBatteryInputEventOn = isOn;
            AppLog.Write($"A330 AIRLINER_ELEC_APU_BAT InputEvent={numericValue.Value:0.###} ({isOn.ToOnOff()}).");
        }
        else
        {
            _a330ApuBatteryInputEventOn = isOn;
        }

        ApplyNativeAircraftState();
    }

    private static double? TryReadInputEventNumber(object? value)
    {
        if (value is Array array)
        {
            foreach (var item in array)
            {
                var nestedValue = TryReadInputEventNumber(item);
                if (nestedValue.HasValue)
                {
                    return nestedValue;
                }
            }

            return null;
        }

        try
        {
            return value == null ? null : Convert.ToDouble(value);
        }
        catch (InvalidCastException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
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
            LogPmdgElectricalBytes(raw.Data);
            _pmdgNg3State = ParsePmdgNg3State(raw.Data);
            LogPmdgElectricalChanges(_pmdgNg3State);
            LogPmdgAirStartChanges(_pmdgNg3State);
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

        if (request is >= Request.NativeBattery1 and <= Request.FbwA380ExternalPower4OnTyped)
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
            else if (request == Request.FbwA380ExternalPower1AvailableTyped)
            {
                SetLoggedBool(ref _fbwA380ExternalPower1AvailableTyped, value, "FBW A380X EXT PWR 1 available typed");
            }
            else if (request == Request.FbwA380ExternalPower1OnTyped)
            {
                SetLoggedBool(ref _fbwA380ExternalPower1OnTyped, value, "FBW A380X EXT PWR 1 ON typed");
            }
            else if (request == Request.FbwA380ExternalPower2AvailableTyped)
            {
                SetLoggedBool(ref _fbwA380ExternalPower2AvailableTyped, value, "FBW A380X EXT PWR 2 available typed");
            }
            else if (request == Request.FbwA380ExternalPower2OnTyped)
            {
                SetLoggedBool(ref _fbwA380ExternalPower2OnTyped, value, "FBW A380X EXT PWR 2 ON typed");
            }
            else if (request == Request.FbwA380ExternalPower3AvailableTyped)
            {
                SetLoggedBool(ref _fbwA380ExternalPower3AvailableTyped, value, "FBW A380X EXT PWR 3 available typed");
            }
            else if (request == Request.FbwA380ExternalPower3OnTyped)
            {
                SetLoggedBool(ref _fbwA380ExternalPower3OnTyped, value, "FBW A380X EXT PWR 3 ON typed");
            }
            else if (request == Request.FbwA380ExternalPower4AvailableTyped)
            {
                SetLoggedBool(ref _fbwA380ExternalPower4AvailableTyped, value, "FBW A380X EXT PWR 4 available typed");
            }
            else if (request == Request.FbwA380ExternalPower4OnTyped)
            {
                SetLoggedBool(ref _fbwA380ExternalPower4OnTyped, value, "FBW A380X EXT PWR 4 ON typed");
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
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwA380ExternalPower1AvailableTyped, Request.FbwA380ExternalPower1AvailableTyped, 90 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwA380ExternalPower1OnTyped, Request.FbwA380ExternalPower1OnTyped, 91 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwA380ExternalPower2AvailableTyped, Request.FbwA380ExternalPower2AvailableTyped, 92 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwA380ExternalPower2OnTyped, Request.FbwA380ExternalPower2OnTyped, 93 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwA380ExternalPower3AvailableTyped, Request.FbwA380ExternalPower3AvailableTyped, 94 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwA380ExternalPower3OnTyped, Request.FbwA380ExternalPower3OnTyped, 95 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwA380ExternalPower4AvailableTyped, Request.FbwA380ExternalPower4AvailableTyped, 96 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.FbwA380ExternalPower4OnTyped, Request.FbwA380ExternalPower4OnTyped, 97 * sizeof(float));
        _mobiFlightRuntimeReady = true;
        _mobiFlightRuntimeInitializedUtc = DateTime.UtcNow;
        SendMobiFlightRuntimeCommand("MF.SimVars.Clear");
        AppLog.Write("MobiFlight runtime SimVar table clear requested before registering app variables.");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:INI_OVHD_ELEC_BAT_1_PB_IS_AUTO_SWITCH)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:INI_OVHD_ELEC_BAT_2_PB_IS_AUTO_SWITCH)");
        foreach (var pump in A320FuelPumpProfile.Pumps)
        {
            SendMobiFlightRuntimeCommand(
                $"MF.SimVars.Add.(L:{pump.ReadbackLVar})");
        }
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
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_EXT_PWR_AVAIL:1, Bool)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_ELEC_EXT_PWR_1_PB_IS_ON, Bool)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_EXT_PWR_AVAIL:2, Bool)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_ELEC_EXT_PWR_2_PB_IS_ON, Bool)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_EXT_PWR_AVAIL:3, Bool)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_ELEC_EXT_PWR_3_PB_IS_ON, Bool)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_EXT_PWR_AVAIL:4, Bool)");
        SendMobiFlightRuntimeCommand(
            "MF.SimVars.Add.(L:A32NX_OVHD_ELEC_EXT_PWR_4_PB_IS_ON, Bool)");
        SendMobiFlightRuntimeCommand("MF.DummyCmd");
        AppLog.Write("FBW runtime offsets registered: ADIRS 1/2/3=56/57/58, typed=59/60/61, crew oxygen=63/64, NAV/LOGO=65/66, strobe=67/68.");
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

        if (_state.IsIniBuildsAirbusFamily && _nativeBattery1On.HasValue)
        {
            _state.Battery1On = _nativeBattery1On.Value;
        }
        if (_state.IsIniBuildsAirbusFamily && _nativeBattery2On.HasValue)
        {
            _state.Battery2On = _nativeBattery2On.Value;
        }
        if (_state.IsFlyByWireAirbus
            && (_fbwBattery1AutoTyped.HasValue || _fbwBattery1Auto.HasValue))
        {
            _state.Battery1On = ResolveFbwBatteryState(
                _fbwCommandedBattery1Auto,
                _fbwBattery1AutoTyped,
                _fbwBattery1Auto,
                _state.Battery1On ? 1 : 0);
        }
        if (_state.IsFlyByWireAirbus
            && (_fbwBattery2AutoTyped.HasValue || _fbwBattery2Auto.HasValue))
        {
            _state.Battery2On = ResolveFbwBatteryState(
                _fbwCommandedBattery2Auto,
                _fbwBattery2AutoTyped,
                _fbwBattery2Auto,
                _state.Battery2On ? 1 : 0);
        }
        if (_state.IsFlyByWireAirbus)
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
        if (_state.IsIniBuildsA330 && A330FuelPumpInputEventsReady())
        {
            _state.FuelPump1State = _a330FuelPumpInputStates[0]!.Value;
            _state.FuelPump2State = _a330FuelPumpInputStates[1]!.Value;
            _state.FuelPump3State = _a330FuelPumpInputStates[2]!.Value;
            _state.FuelPump4State = _a330FuelPumpInputStates[3]!.Value;
            _state.FuelPump5State = _a330FuelPumpInputStates[4]!.Value;
            _state.FuelPump6State = _a330FuelPumpInputStates[5]!.Value;
            _state.FuelPumpsConfigured = A330FuelPumpsConfigured();
        }
        else if (_nativeFuelPump1.HasValue
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
        if (_state.IsIniBuildsA330 && _a330FlapsInputState.HasValue)
        {
            _state.FlapsHandleIndex = _a330FlapsInputState.Value;
        }
        if (_nativeNavLogoSelectorPosition.HasValue)
        {
            _state.NavLogoSelectorPosition = _nativeNavLogoSelectorPosition.Value;
        }
        if (_state.IsIniBuildsA330 && _a330NavLogoInputState.HasValue)
        {
            _state.NavLogoSelectorPosition = _a330NavLogoInputState.Value;
        }
        if (_nativeApuAvailable.HasValue)
        {
            _state.ApuAvailable = _nativeApuAvailable.Value != 0;
        }
        if (_state.IsIniBuildsA330)
        {
            _state.ApuAvailable = _state.ApuRpmPercent >= 95;
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
        if (_state.IsIniBuildsA330)
        {
            if (_a330ApuInputStates[0].HasValue)
            {
                _state.ApuMasterSwitchOn = _a330ApuInputStates[0]!.Value >= 0.5;
            }
            if (_a330ApuInputStates[1].HasValue)
            {
                _state.ApuStartButtonOn = _a330ApuInputStates[1]!.Value >= 0.5;
            }
            if (_a330ApuInputStates[2].HasValue)
            {
                _state.ApuBleedOn = _a330ApuInputStates[2]!.Value >= 0.5;
            }
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
        if (_state.IsIniBuildsA330)
        {
            if (_a330AdirsInputStates[0].HasValue)
            {
                _state.Adirs1SelectorState = _a330AdirsInputStates[0]!.Value;
            }
            if (_a330AdirsInputStates[1].HasValue)
            {
                _state.Adirs2SelectorState = _a330AdirsInputStates[1]!.Value;
            }
            if (_a330AdirsInputStates[2].HasValue)
            {
                _state.Adirs3SelectorState = _a330AdirsInputStates[2]!.Value;
            }
        }
        if (_nativeAdirsOnBattery.HasValue)
        {
            _state.AdirsOnBattery = _nativeAdirsOnBattery.Value != 0;
        }
        if (_nativeCrewOxygen.HasValue)
        {
            _state.CrewOxygenOn = _nativeCrewOxygen.Value != 0;
        }
        if (_state.IsIniBuildsA330 && _a330CrewOxygenInputState.HasValue)
        {
            _state.CrewOxygenOn = _a330CrewOxygenInputState.Value >= 0.5;
        }
        if (_nativeStrobeSelector.HasValue)
        {
            _state.StrobeSelectorPosition = _nativeStrobeSelector.Value;
        }
        if (_state.IsIniBuildsA330 && _a330StrobeInputState.HasValue)
        {
            _state.StrobeSelectorPosition = _a330StrobeInputState.Value;
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
        if (_state.IsIniBuildsA330 && A330SignInputEventsReady())
        {
            _state.SeatbeltSelectorPosition = _a330SignInputStates[0];
            _state.SeatbeltSignsOn = _a330SignInputStates[0] >= 0.5;
            _state.NoSmokingSelectorPosition = _a330SignInputStates[1];
            _state.NoSmokingSignsOn = _a330SignInputStates[1] >= 0.5;
            _state.EmergencyExitSelectorPosition = _a330SignInputStates[2];
        }
        if (_state.IsIniBuildsA330)
        {
            if (_a330CommandedSpoilersArmed.HasValue)
            {
                _state.GroundSpoilersArmed = _a330CommandedSpoilersArmed.Value;
            }
        }
        else if (_nativeSpoilersArmed.HasValue)
        {
            _state.GroundSpoilersArmed = _nativeSpoilersArmed.Value != 0;
        }
        _state.AutobrakeLevel = _nativeAutobrakeLevel;
        if (_state.IsIniBuildsA330)
        {
            _state.AutobrakeLevel = ResolveA330AutobrakeLevel();
        }
        _state.WeatherRadarPwsSelectorPosition = _nativeWeatherRadarPwsSelector;
        if (_state.IsIniBuildsA330 && _a330WeatherRadarPwsInputState.HasValue)
        {
            // A330 Boolean is inverted: 1=OFF, 0=AUTO.
            _state.WeatherRadarPwsSelectorPosition =
                _a330WeatherRadarPwsInputState.Value >= 0.5 ? 0 : 1;
        }
        _state.NoseLightSelectorPosition = _nativeNoseLightSelector;
        if (_state.IsIniBuildsA330 && _a330NoseLightInputState.HasValue)
        {
            _state.NoseLightSelectorPosition = _a330NoseLightInputState.Value;
        }
        _state.LeftLandingLightSelectorPosition = _nativeLeftLandingLightSelector;
        _state.RightLandingLightSelectorPosition = _nativeRightLandingLightSelector;
        _state.TcasAltitudeReportingOn =
            _nativeTcasAltitudeReporting.HasValue
                ? _state.IsIniBuildsA330
                    ? _a330TcasAltitudeInputState.HasValue
                        ? _a330TcasAltitudeInputState.Value >= 0.5
                        : null
                    : _nativeTcasAltitudeReporting.Value == 0
                : null;
        if (_state.IsIniBuildsA330 && _a330TcasAltitudeInputState.HasValue)
        {
            _state.TcasAltitudeReportingOn = _a330TcasAltitudeInputState.Value >= 0.5;
        }
        _state.TransponderAtcState = _nativeTransponderAtcState;
        _state.TcasMode = _nativeTcasMode;
        if (_state.IsIniBuildsA330 && _a330TcasTrafficInputState.HasValue)
        {
            _state.TcasMode = _a330TcasTrafficInputState.Value;
        }
        _state.TransponderModeSelectorPosition = _nativeTransponderStandby;
        _state.TransponderStandby = _nativeTransponderStandby.HasValue
                                    && _nativeTransponderStandby.Value != 0;
        if (_state.IsIniBuildsA330 && _a330TransponderModeInputState.HasValue)
        {
            _state.TransponderModeSelectorPosition = _a330TransponderModeInputState.Value;
            _state.TransponderStandby = _a330TransponderModeInputState.Value < 0.5;
        }
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
        var isIniBuildsAirbusFamily =
            raw.Title.Equals("A320neo V2", StringComparison.OrdinalIgnoreCase)
            || raw.Title.Equals("A321", StringComparison.OrdinalIgnoreCase)
            || raw.Title.IndexOf("A321", StringComparison.OrdinalIgnoreCase) >= 0
            || raw.Title.Equals("A330", StringComparison.OrdinalIgnoreCase)
            || raw.Title.IndexOf("A330", StringComparison.OrdinalIgnoreCase) >= 0;
        var isIniBuildsA330 =
            raw.Title.Equals("A330", StringComparison.OrdinalIgnoreCase)
            || raw.Title.IndexOf("A330", StringComparison.OrdinalIgnoreCase) >= 0;
        var hasFlyByWireA380XSignature =
            raw.Title.IndexOf("A380X", StringComparison.OrdinalIgnoreCase) >= 0
            || raw.Title.IndexOf("A380-842", StringComparison.OrdinalIgnoreCase) >= 0
            || raw.Title.IndexOf("A380", StringComparison.OrdinalIgnoreCase) >= 0
            && raw.Title.IndexOf("FlyByWire", StringComparison.OrdinalIgnoreCase) >= 0;
        var isFlyByWireA380X =
            EnableExperimentalFlyByWireA380X && hasFlyByWireA380XSignature;
        var isFlyByWireA320Neo =
            !hasFlyByWireA380XSignature
            && (raw.Title.IndexOf("A32NX", StringComparison.OrdinalIgnoreCase) >= 0
                || raw.Title.IndexOf("A320", StringComparison.OrdinalIgnoreCase) >= 0
                && raw.Title.IndexOf("FlyByWire", StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(raw.Title, "FlyByWire A32NX", StringComparison.OrdinalIgnoreCase));
        var isFlyByWireAirbus = isFlyByWireA320Neo || isFlyByWireA380X;
        var isPmdg737 =
            raw.Title.IndexOf("PMDG", StringComparison.OrdinalIgnoreCase) >= 0
            && raw.Title.IndexOf("737", StringComparison.OrdinalIgnoreCase) >= 0
            || raw.Title.IndexOf("737-800", StringComparison.OrdinalIgnoreCase) >= 0
            || raw.Title.IndexOf("738", StringComparison.OrdinalIgnoreCase) >= 0;
        var pmdg = _pmdgNg3State;
        if (isFlyByWireAirbus)
        {
            LogChangedVoltage("FBW generic BAT 1 voltage", raw.Battery1Voltage, ref _lastLoggedBattery1Voltage);
            LogChangedVoltage("FBW generic BAT 2 voltage", raw.Battery2Voltage, ref _lastLoggedBattery2Voltage);
            if (isFlyByWireA380X)
            {
                var externalPowerDirectSignature =
                    raw.FbwA380ExternalPower1Available * 1
                    + raw.FbwA380ExternalPower1On * 10
                    + raw.FbwA380ExternalPower2Available * 100
                    + raw.FbwA380ExternalPower2On * 1000
                    + raw.FbwA380ExternalPower3Available * 10000
                    + raw.FbwA380ExternalPower3On * 100000
                    + raw.FbwA380ExternalPower4Available * 1000000
                    + raw.FbwA380ExternalPower4On * 10000000;
                LogChangedFloat(
                    "FBW A380 direct EXT PWR avail/on signature",
                    externalPowerDirectSignature,
                    ref _lastLoggedA380ExternalPowerDirectSignature);

                var acPowerSignature =
                    raw.FbwA380AcBus1Powered * 1
                    + raw.FbwA380AcBus2Powered * 10
                    + raw.FbwA380AcBus3Powered * 100
                    + raw.FbwA380AcBus4Powered * 1000;
                LogChangedFloat(
                    "FBW A380 AC bus powered signature",
                    acPowerSignature,
                    ref _lastLoggedA380AcPowerSignature);
            }
        }
        if (isPmdg737 && pmdg != null)
        {
            if (pmdg.ApuGenOffBus)
            {
                _pmdgApuGenOffBusSeen = true;
            }
            else if (pmdg.ApuEgtNeedle <= 0)
            {
                _pmdgApuGenOffBusSeen = false;
            }
        }
        else
        {
            _pmdgApuGenOffBusSeen = false;
        }
        var pmdgApuPowerEstablished =
            isPmdg737
            && pmdg != null
            && pmdg.ApuEgtNeedle > 0
            && _pmdgApuGenOffBusSeen
            && pmdg.ApuGen1On
            && pmdg.ApuGen2On
            && !pmdg.ApuGenOffBus
            && !pmdg.TransferBus1Off
            && !pmdg.TransferBus2Off
            && pmdg.AcTransferBus1Powered
            && pmdg.AcTransferBus2Powered;
        var pmdgApuAvailable =
            isPmdg737
            && pmdg != null
            && (pmdg.ApuGenOffBus || pmdgApuPowerEstablished);
        var nowUtc = DateTime.UtcNow;
        if (pmdgApuAvailable)
        {
            if (!_pmdgApuAvailableSinceUtc.HasValue)
            {
                _pmdgApuAvailableSinceUtc = nowUtc;
            }
        }
        else
        {
            _pmdgApuAvailableSinceUtc = null;
        }

        var pmdgApuBleedWarmupComplete =
            !isPmdg737
            || (pmdgApuAvailable
                && _pmdgApuAvailableSinceUtc.HasValue
                && nowUtc - _pmdgApuAvailableSinceUtc.Value >= PmdgApuBleedWarmup);

        _state = new AircraftState
        {
            Title = raw.Title,
            OnGround = raw.OnGround != 0,
            GroundSpeedKnots = raw.GroundSpeed,
            Engine1Running = isFlyByWireAirbus
                ? _fbwEngine1State == 1 || (_fbwEngine1N1 ?? (float)raw.Engine1N1) >= 15
                : isPmdg737
                    ? raw.Engine1Combustion != 0 || raw.Engine1N1 >= 15
                : raw.Engine1Combustion != 0,
            Engine2Running = isFlyByWireAirbus
                ? _fbwEngine2State == 1 || (_fbwEngine2N1 ?? (float)raw.Engine2N1) >= 15
                : isPmdg737
                    ? raw.Engine2Combustion != 0 || raw.Engine2N1 >= 15
                : raw.Engine2Combustion != 0,
            Engine1StarterActive = isFlyByWireAirbus
                ? _fbwEngine1StarterValveOpen == true
                  || _fbwEngine1State == 2
                  || _fbwEngine1State == 3
                  || raw.Engine1Starter != 0
                : isPmdg737
                    ? pmdg?.Engine1StartValveOpen == true || raw.Engine1Starter != 0
                : raw.Engine1Starter != 0,
            Engine2StarterActive = isFlyByWireAirbus
                ? _fbwEngine2StarterValveOpen == true
                  || _fbwEngine2State == 2
                  || _fbwEngine2State == 3
                  || raw.Engine2Starter != 0
                : isPmdg737
                    ? pmdg?.Engine2StartValveOpen == true || raw.Engine2Starter != 0
                : raw.Engine2Starter != 0,
            Engine1StartSwitchPosition = isPmdg737 && pmdg != null
                ? pmdg.Engine1StartSelector
                : null,
            Engine2StartSwitchPosition = isPmdg737 && pmdg != null
                ? pmdg.Engine2StartSelector
                : null,
            Engine1N1Percent = isFlyByWireAirbus
                ? _fbwEngine1N1 ?? raw.Engine1N1
                : raw.Engine1N1,
            Engine2N1Percent = isFlyByWireAirbus
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
            Battery1On = isIniBuildsAirbusFamily
                ? _nativeBattery1On ?? raw.Battery1 != 0
                : isFlyByWireAirbus
                    ? ResolveFbwBatteryState(
                        _fbwCommandedBattery1Auto,
                        _fbwBattery1AutoTyped,
                        _fbwBattery1Auto,
                        raw.Battery1)
                : isPmdg737
                    ? pmdg != null && pmdg.BatterySelector != 0
                : raw.Battery1 != 0,
            Battery2On = isIniBuildsAirbusFamily
                ? _nativeBattery2On ?? raw.Battery2 != 0
                : isFlyByWireAirbus
                    ? ResolveFbwBatteryState(
                        _fbwCommandedBattery2Auto,
                        _fbwBattery2AutoTyped,
                        _fbwBattery2Auto,
                        raw.Battery2)
                : isPmdg737
                    ? pmdg != null && pmdg.BatterySelector != 0
                : raw.Battery2 != 0,
            Battery1Voltage = raw.Battery1Voltage,
            Battery2Voltage = raw.Battery2Voltage,
            ApuBatteryOn = !isIniBuildsA330
                || _a330ApuBatteryInputEventOn == true,
            ExternalPowerAvailable = isFlyByWireAirbus
                ? ResolveFbwAnyTrueState(
                    _fbwExternalPowerAvailableTyped,
                    _fbwExternalPowerAvailable,
                    raw.ExternalPowerAvailableUnindexed,
                    raw.ExternalPowerAvailable,
                    _fbwA380ExternalPower1AvailableTyped,
                    _fbwA380ExternalPower2AvailableTyped,
                    _fbwA380ExternalPower3AvailableTyped,
                    _fbwA380ExternalPower4AvailableTyped,
                    raw.FbwA380ExternalPower1Available != 0,
                    raw.FbwA380ExternalPower2Available != 0,
                    raw.FbwA380ExternalPower3Available != 0,
                    raw.FbwA380ExternalPower4Available != 0)
                : isPmdg737
                    ? pmdg?.GroundPowerAvailable == true
                : isIniBuildsA330
                    ? raw.ExternalPowerAvailable != 0 || raw.ExternalPower2Available != 0
                : raw.ExternalPowerAvailable != 0,
            ExternalPowerOn = isFlyByWireAirbus
                ? ResolveFbwAnyTrueState(
                    _fbwExternalPowerOnTyped,
                    _fbwExternalPowerOn,
                    raw.ExternalPowerOnUnindexed,
                    raw.ExternalPowerOn,
                    _fbwA380ExternalPower1OnTyped,
                    _fbwA380ExternalPower2OnTyped,
                    _fbwA380ExternalPower3OnTyped,
                    _fbwA380ExternalPower4OnTyped,
                    raw.FbwA380ExternalPower1On != 0,
                    raw.FbwA380ExternalPower2On != 0,
                    raw.FbwA380ExternalPower3On != 0,
                    raw.FbwA380ExternalPower4On != 0,
                    isFlyByWireA380X
                    && raw.ApuRpm < 5
                    && raw.Engine1Combustion == 0
                    && raw.Engine2Combustion == 0
                    && (
                        raw.FbwA380AcBus1Powered != 0
                        || raw.FbwA380AcBus2Powered != 0
                        || raw.FbwA380AcBus3Powered != 0
                        || raw.FbwA380AcBus4Powered != 0))
                : isPmdg737
                    ? pmdg?.GroundPowerOn == true
                      && pmdg.AcTransferBus1Powered
                      && pmdg.AcTransferBus2Powered
                      && !pmdgApuPowerEstablished
                : isIniBuildsA330
                    ? raw.ExternalPowerOn != 0
                      && (raw.ExternalPower2Available == 0 || raw.ExternalPower2On != 0)
                : raw.ExternalPowerOn != 0,
            ExternalPower1Available = raw.ExternalPowerAvailable != 0,
            ExternalPower1On = raw.ExternalPowerOn != 0,
            ExternalPower2Available = raw.ExternalPower2Available != 0,
            ExternalPower2On = raw.ExternalPower2On != 0,
            ExternalPowerAvailableUnindexed = raw.ExternalPowerAvailableUnindexed != 0,
            ExternalPowerOnUnindexed = raw.ExternalPowerOnUnindexed != 0,
            FbwA380ExternalPower1Available = raw.FbwA380ExternalPower1Available != 0,
            FbwA380ExternalPower1On = raw.FbwA380ExternalPower1On != 0,
            FbwA380ExternalPower2Available = raw.FbwA380ExternalPower2Available != 0,
            FbwA380ExternalPower2On = raw.FbwA380ExternalPower2On != 0,
            FbwA380ExternalPower3Available = raw.FbwA380ExternalPower3Available != 0,
            FbwA380ExternalPower3On = raw.FbwA380ExternalPower3On != 0,
            FbwA380ExternalPower4Available = raw.FbwA380ExternalPower4Available != 0,
            FbwA380ExternalPower4On = raw.FbwA380ExternalPower4On != 0,
            FbwA380AcBus1Powered = raw.FbwA380AcBus1Powered != 0,
            FbwA380AcBus2Powered = raw.FbwA380AcBus2Powered != 0,
            FbwA380AcBus3Powered = raw.FbwA380AcBus3Powered != 0,
            FbwA380AcBus4Powered = raw.FbwA380AcBus4Powered != 0,
            ParkingBrakeSet = isFlyByWireAirbus
                ? _fbwParkingBrake == true
                : isPmdg737 && pmdg != null
                    ? pmdg.ParkingBrakeAnnunciated || raw.ParkingBrake != 0
                : raw.ParkingBrake != 0,
            BeaconOn = isPmdg737 && pmdg != null
                ? pmdg.AntiCollisionOn
                : raw.Beacon != 0,
            NavigationLightsOn = isPmdg737 && pmdg != null
                ? ResolvePmdgCommandedPositionLightState(
                    _pmdgCommandedPositionStrobeSelector,
                    _pmdgCommandedPositionStrobeUtc,
                    pmdg.PositionStrobeSelector)
                : raw.NavigationLights != 0,
            LogoLightsOn = isPmdg737 && pmdg != null
                ? ResolvePmdgCommandedBoolState(
                    _pmdgCommandedLogoLightOn,
                    _pmdgCommandedLogoLightUtc,
                    pmdg.LogoLightOn)
                : raw.LogoLights != 0,
            NavLogoSelectorPosition = isFlyByWireAirbus
                ? ResolveFbwNavLogoSelectorPosition(_fbwNavLogoSelectorTyped, _fbwNavLogoSelector)
                : isIniBuildsA330 && _a330NavLogoInputState.HasValue
                    ? _a330NavLogoInputState.Value
                : isPmdg737 && pmdg != null
                    ? ResolvePmdgCommandedBoolState(
                        _pmdgCommandedLogoLightOn,
                        _pmdgCommandedLogoLightUtc,
                        pmdg.LogoLightOn) ? 0 : 2
                : _nativeNavLogoSelectorPosition,
            ApuRpmPercent = raw.ApuRpm,
            ApuStarterPercent = raw.ApuStarter,
            ApuMasterSwitchOn = isFlyByWireAirbus
                ? _fbwApuMasterSwitch == true
                : isIniBuildsA330 && _a330ApuInputStates[0].HasValue
                    ? _a330ApuInputStates[0]!.Value >= 0.5
                : isPmdg737 && pmdg != null
                    ? pmdg.ApuSelector >= 1
                : _nativeApuMasterSwitch.HasValue
                    ? _nativeApuMasterSwitch.Value != 0
                    : raw.ApuMasterSwitch != 0,
            ApuAvailable = isFlyByWireAirbus
                ? _fbwApuStartAvailable == true
                : isIniBuildsA330
                    ? raw.ApuRpm >= 95
                : isPmdg737 && pmdg != null
                    ? pmdgApuAvailable
                : _nativeApuAvailable.HasValue && _nativeApuAvailable.Value != 0,
            ApuStartButtonOn = isFlyByWireAirbus
                ? _fbwApuStartButton == true || _fbwApuStartAvailable == true
                : isIniBuildsA330 && _a330ApuInputStates[1].HasValue
                    ? _a330ApuInputStates[1]!.Value >= 0.5
                : isPmdg737 && pmdg != null
                    ? pmdg.ApuSelector == 2 || raw.ApuStarter > 0
                : _nativeApuStartButton.HasValue && _nativeApuStartButton.Value != 0,
            ApuSpoolingOrAvailable = isPmdg737 && pmdg != null
                ? pmdg.ApuEgtNeedle > 0 || pmdgApuAvailable
                : raw.ApuRpm > 5 || raw.ApuStarter > 0,
            ApuBleedOn = isFlyByWireAirbus
                ? _fbwApuBleedButton == true
                : isIniBuildsA330 && _a330ApuInputStates[2].HasValue
                    ? _a330ApuInputStates[2]!.Value >= 0.5
                : isPmdg737 && pmdg != null
                    ? pmdg.ApuBleedOn
                : _nativeApuBleedButton.HasValue && _nativeApuBleedButton.Value != 0,
            ApuBleedWarmupComplete = isPmdg737
                ? pmdgApuBleedWarmupComplete
                : true,
            LeftPackSwitchPosition = isPmdg737 && pmdg != null
                ? pmdg.LeftPackSwitch
                : null,
            RightPackSwitchPosition = isPmdg737 && pmdg != null
                ? pmdg.RightPackSwitch
                : null,
            IsolationValvePosition = isPmdg737 && pmdg != null
                ? pmdg.IsolationValveSwitch
                : null,
            LeftDuctPressurePsi = isPmdg737 && pmdg != null
                ? pmdg.LeftDuctPressurePsi
                : 0,
            RightDuctPressurePsi = isPmdg737 && pmdg != null
                ? pmdg.RightDuctPressurePsi
                : 0,
            ApuFlapPercent = _nativeApuFlapPercent ?? 0,
            ApuGeneratorActive = raw.ApuGeneratorActive != 0,
            ApuGeneratorSwitchOn = _nativeApuGeneratorOn.HasValue
                                   && !isPmdg737
                                   ? _nativeApuGeneratorOn.Value != 0
                                   : isPmdg737 && pmdg != null
                                       ? pmdg.ApuGen1On && pmdg.ApuGen2On
                                       : raw.ApuGeneratorSwitch != 0,
            ApuGeneratorPowerEstablished = isPmdg737
                ? pmdgApuPowerEstablished
                : raw.ApuGeneratorActive != 0,
            EngineGeneratorsOn = isPmdg737 && pmdg != null
                ? pmdg.EngineGen1On
                  && pmdg.EngineGen2On
                  && !pmdg.GenBus1Off
                  && !pmdg.GenBus2Off
                : raw.Engine1Combustion != 0 && raw.Engine2Combustion != 0,
            ApuGenOffBus = isPmdg737 && pmdg != null && pmdg.ApuGenOffBus,
            AcTransferBus1Powered = isPmdg737 && pmdg != null && pmdg.AcTransferBus1Powered,
            AcTransferBus2Powered = isPmdg737 && pmdg != null && pmdg.AcTransferBus2Powered,
            TransferBus1Off = isPmdg737 && pmdg != null && pmdg.TransferBus1Off,
            TransferBus2Off = isPmdg737 && pmdg != null && pmdg.TransferBus2Off,
            BoeingElectricHydraulicPumpsOn = isPmdg737 && pmdg != null
                && pmdg.ElectricHydraulicPump1On
                && pmdg.ElectricHydraulicPump2On,
            BoeingElectricHydraulicPump1On = isPmdg737 && pmdg != null && pmdg.ElectricHydraulicPump1On,
            BoeingElectricHydraulicPump2On = isPmdg737 && pmdg != null && pmdg.ElectricHydraulicPump2On,
            BoeingElectricHydraulicPump1LowPressure = isPmdg737 && pmdg != null && pmdg.ElectricHydraulicPump1LowPressure,
            BoeingElectricHydraulicPump2LowPressure = isPmdg737 && pmdg != null && pmdg.ElectricHydraulicPump2LowPressure,
            ApuVolts = raw.ApuVolts,
            CenterFuelQuantityPounds = isPmdg737 && pmdg != null
                ? pmdg.CenterFuelQuantityPounds
                : 0,
            FuelPumpsConfigured = isFlyByWireAirbus
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
                      && (pmdg.CenterFuelQuantityPounds <= PmdgCenterFuelPumpRequiredThresholdPounds
                          || pmdg.LeftCenterFuelPump && pmdg.RightCenterFuelPump)
                : isIniBuildsA330 && A330FuelPumpInputEventsReady()
                    ? A330FuelPumpsConfigured()
                : (_nativeFuelPump1 ?? (float)raw.FuelPump1) != 0
                  && (_nativeFuelPump2 ?? (float)raw.FuelPump2) != 0
                  && (_nativeFuelPump3 ?? (float)raw.FuelPump3) != 0
                  && (_nativeFuelPump4 ?? (float)raw.FuelPump4) != 0
                  && (_nativeFuelPump5 ?? 0) != 0
                  && (_nativeFuelPump6 ?? 0) != 0,
            FuelPump1State = isFlyByWireAirbus ? raw.FuelPump2 : isPmdg737 && pmdg != null ? (pmdg.LeftAftFuelPump ? 1 : 0) : isIniBuildsA330 && A330FuelPumpInputEventsReady() ? _a330FuelPumpInputStates[0]!.Value : _nativeFuelPump1 ?? raw.FuelPump1,
            FuelPump2State = isFlyByWireAirbus ? raw.FbwFuelPump5 : isPmdg737 && pmdg != null ? (pmdg.LeftForwardFuelPump ? 1 : 0) : isIniBuildsA330 && A330FuelPumpInputEventsReady() ? _a330FuelPumpInputStates[1]!.Value : _nativeFuelPump2 ?? raw.FuelPump2,
            FuelPump3State = isFlyByWireAirbus ? raw.FbwFuelValve9 : isPmdg737 && pmdg != null ? (pmdg.RightForwardFuelPump ? 1 : 0) : isIniBuildsA330 && A330FuelPumpInputEventsReady() ? _a330FuelPumpInputStates[2]!.Value : _nativeFuelPump3 ?? raw.FuelPump3,
            FuelPump4State = isFlyByWireAirbus ? raw.FbwFuelValve10 : isPmdg737 && pmdg != null ? (pmdg.RightAftFuelPump ? 1 : 0) : isIniBuildsA330 && A330FuelPumpInputEventsReady() ? _a330FuelPumpInputStates[3]!.Value : _nativeFuelPump4 ?? raw.FuelPump4,
            FuelPump5State = isFlyByWireAirbus ? raw.FuelPump3 : isPmdg737 && pmdg != null ? (pmdg.LeftCenterFuelPump ? 1 : 0) : isIniBuildsA330 && A330FuelPumpInputEventsReady() ? _a330FuelPumpInputStates[4]!.Value : _nativeFuelPump5 ?? 0,
            FuelPump6State = isFlyByWireAirbus ? raw.FbwFuelPump6 : isPmdg737 && pmdg != null ? (pmdg.RightCenterFuelPump ? 1 : 0) : isIniBuildsA330 && A330FuelPumpInputEventsReady() ? _a330FuelPumpInputStates[5]!.Value : _nativeFuelPump6 ?? 0,
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
            Engine1ReverseEngaged = isPmdg737 && pmdg != null
                ? pmdg.Engine1ReverserAnnunciated
                : raw.Engine1Reverse != 0,
            Engine2ReverseEngaged = isPmdg737 && pmdg != null
                ? pmdg.Engine2ReverserAnnunciated
                : raw.Engine2Reverse != 0,
            AutobrakesActive = isPmdg737 && pmdg != null
                ? pmdg.AutobrakeSelector >= 2 && !pmdg.AutobrakeDisarmed && pmdg.BrakePressureNeedle > 0
                : raw.AutobrakesActive != 0,
            LeftSpoilerPositionPercent = isPmdg737 && pmdg?.SpeedbrakeExtended == true
                ? 100
                : raw.LeftSpoilerPosition,
            RightSpoilerPositionPercent = isPmdg737 && pmdg?.SpeedbrakeExtended == true
                ? 100
                : raw.RightSpoilerPosition,
            FlapsHandleIndex = isIniBuildsA330 && _a330FlapsInputState.HasValue
                ? _a330FlapsInputState.Value
                : raw.FlapsHandleIndex,
            BoeingTakeoffFlaps = isPmdg737 && pmdg != null && pmdg.TakeoffFlaps > 0
                ? pmdg.TakeoffFlaps
                : null,
            BoeingLandingFlaps = isPmdg737 && pmdg != null && pmdg.LandingFlaps > 0
                ? pmdg.LandingFlaps
                : null,
            BoeingLandingVrefKnots = isPmdg737 && pmdg != null && pmdg.LandingVref > 0
                ? pmdg.LandingVref
                : null,
            LeftFlapPositionPercent = raw.LeftFlapPosition,
            RightFlapPositionPercent = raw.RightFlapPosition,
            GearHandlePosition = isPmdg737 && pmdg != null
                ? pmdg.GearLever
                : isFlyByWireAirbus
                    ? raw.GearHandle != 0 ? 2 : 0
                    : _nativeGearHandlePosition.HasValue
                        ? _nativeGearHandlePosition.Value >= 0.5 ? 2 : 0
                        : raw.GearHandle != 0 ? 2 : 0,
            GearHandleDown = isFlyByWireAirbus
                ? raw.GearHandle != 0
                : isPmdg737 && pmdg != null
                    ? pmdg.GearLever == 2
                : _nativeGearHandlePosition.HasValue
                    ? _nativeGearHandlePosition.Value >= 0.5
                    : raw.GearHandle != 0,
            PitchDegrees = raw.PitchDegrees,
            AutopilotMasterOn = raw.AutopilotMaster != 0,
            AutopilotApproachHoldOn = raw.AutopilotApproachHold != 0,
            AutopilotGlideslopeHoldOn = raw.AutopilotGlideslopeHold != 0,
            Nav1HasLocalizer = raw.Nav1HasLocalizer != 0,
            Nav1HasGlideslope = raw.Nav1HasGlideslope != 0,
            Nav2HasLocalizer = raw.Nav2HasLocalizer != 0,
            Nav2HasGlideslope = raw.Nav2HasGlideslope != 0,
            Nav1ActiveFrequencyMhz = raw.Nav1ActiveFrequency,
            Nav2ActiveFrequencyMhz = raw.Nav2ActiveFrequency,
            Nav1CourseDegrees = raw.Nav1Course,
            Nav2CourseDegrees = raw.Nav2Course,
            Adirs1SelectorState = isFlyByWireAirbus
                ? ResolveFbwSelectorState(_fbwCommandedAdirs1Selector, _fbwCommandedAdirs1SelectorUtc, _fbwAdirs1SelectorTyped, _fbwAdirs1Selector)
                : isIniBuildsA330 && _a330AdirsInputStates[0].HasValue
                    ? _a330AdirsInputStates[0]!.Value
                : isPmdg737 && pmdg != null
                    ? ResolvePmdgCommandedSelectorState(
                        _pmdgCommandedLeftIrsMode,
                        _pmdgCommandedLeftIrsModeUtc,
                        pmdg.IrsLeftMode)
                : _nativeAdirs1State ?? 0,
            Adirs2SelectorState = isFlyByWireAirbus
                ? ResolveFbwSelectorState(_fbwCommandedAdirs2Selector, _fbwCommandedAdirs2SelectorUtc, _fbwAdirs2SelectorTyped, _fbwAdirs2Selector)
                : isIniBuildsA330 && _a330AdirsInputStates[1].HasValue
                    ? _a330AdirsInputStates[1]!.Value
                : isPmdg737 && pmdg != null
                    ? ResolvePmdgCommandedSelectorState(
                        _pmdgCommandedRightIrsMode,
                        _pmdgCommandedRightIrsModeUtc,
                        pmdg.IrsRightMode)
                : _nativeAdirs2State ?? 0,
            Adirs3SelectorState = isFlyByWireAirbus
                ? ResolveFbwSelectorState(_fbwCommandedAdirs3Selector, _fbwCommandedAdirs3SelectorUtc, _fbwAdirs3SelectorTyped, _fbwAdirs3Selector)
                : isIniBuildsA330 && _a330AdirsInputStates[2].HasValue
                    ? _a330AdirsInputStates[2]!.Value
                : isPmdg737
                    ? 2
                : _nativeAdirs3State ?? 0,
            AdirsOnBattery = isFlyByWireAirbus
                ? _fbwAdirsOnBattery == true
                : _nativeAdirsOnBattery.HasValue && _nativeAdirsOnBattery.Value != 0,
            IrsLeftAlignLightOn = isPmdg737 && pmdg != null && pmdg.IrsLeftAlignLight,
            IrsRightAlignLightOn = isPmdg737 && pmdg != null && pmdg.IrsRightAlignLight,
            IrsLeftOnDcLightOn = isPmdg737 && pmdg != null && pmdg.IrsLeftOnDcLight,
            IrsRightOnDcLightOn = isPmdg737 && pmdg != null && pmdg.IrsRightOnDcLight,
            IrsLeftFault = isPmdg737 && pmdg != null && pmdg.IrsLeftFault,
            IrsRightFault = isPmdg737 && pmdg != null && pmdg.IrsRightFault,
            IrsAligned = !isPmdg737 || pmdg?.IrsAligned == true,
            CrewOxygenOn = isFlyByWireAirbus
                ? FbwStateResolvers.ResolveCrewOxygen(
                    _fbwCommandedCrewOxygen,
                    _fbwCommandedCrewOxygenUtc,
                    _fbwCrewOxygenTyped,
                    _fbwCrewOxygen)
                : isIniBuildsA330 && _a330CrewOxygenInputState.HasValue
                    ? _a330CrewOxygenInputState.Value >= 0.5
                : _nativeCrewOxygen.HasValue && _nativeCrewOxygen.Value != 0,
            StrobeSelectorPosition = isFlyByWireAirbus
                ? ResolveFbwStrobeSelectorPosition(_fbwStrobeAuto, _fbwStrobeLightState)
                : isIniBuildsA330 && _a330StrobeInputState.HasValue
                    ? _a330StrobeInputState.Value
                : isPmdg737 && pmdg != null
                    ? ResolvePmdgPositionStrobeSelector(
                        _pmdgCommandedPositionStrobeSelector,
                        _pmdgCommandedPositionStrobeUtc,
                        pmdg.PositionStrobeSelector)
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
            SeatbeltSelectorPosition = isFlyByWireAirbus
                ? ResolveFbwSeatbeltSelectorPosition(
                    _fbwSeatbeltSelector,
                    raw.CabinSeatbeltsAlert != 0)
                : isPmdg737 && pmdg != null
                    ? pmdg.FastenBeltsSelector
                : isIniBuildsA330 && A330SignInputEventsReady()
                    ? _a330SignInputStates[0]
                : _nativeSeatbeltSelector,
            SeatbeltSignsOn = isFlyByWireAirbus
                ? raw.CabinSeatbeltsAlert != 0
                : isPmdg737 && pmdg != null
                    ? pmdg.FastenBeltsSelector == 2
                : isIniBuildsA330 && A330SignInputEventsReady()
                    ? _a330SignInputStates[0] >= 0.5
                : _nativeSeatbeltSignsOn.HasValue && _nativeSeatbeltSignsOn.Value != 0,
            NoSmokingSelectorPosition = isFlyByWireAirbus
                ? _fbwNoSmokingSelector
                : isPmdg737 && pmdg != null
                    ? pmdg.NoSmokingSelector
                : isIniBuildsA330 && A330SignInputEventsReady()
                    ? _a330SignInputStates[1]
                : _nativeNoSmokingSelector,
            NoSmokingSignsOn = isFlyByWireAirbus
                ? _fbwNoSmokingSelector.HasValue && Math.Abs(_fbwNoSmokingSelector.Value) < 0.1
                : isPmdg737 && pmdg != null
                    ? pmdg.NoSmokingSelector == 2
                : isIniBuildsA330 && A330SignInputEventsReady()
                    ? _a330SignInputStates[1] >= 0.5
                : _nativeNoSmokingSignsOn.HasValue && _nativeNoSmokingSignsOn.Value != 0,
            EmergencyExitSelectorPosition = isFlyByWireAirbus
                ? _fbwEmergencyExitSelector
                : isPmdg737 && pmdg != null
                    ? ResolvePmdgCommandedSelectorState(
                        _pmdgCommandedEmergencyExitSelector,
                        _pmdgCommandedEmergencyExitUtc,
                        pmdg.EmergencyExitLights)
                : isIniBuildsA330 && A330SignInputEventsReady()
                    ? _a330SignInputStates[2]
                : _nativeEmergencyExitSelector,
            GroundSpoilersArmed = isFlyByWireAirbus
                ? ResolveFbwSpoilersArmedState(
                    _fbwCommandedSpoilersArmed,
                    _fbwCommandedSpoilersArmedUtc,
                    _fbwSpoilersArmed,
                    raw.SpoilersArmed)
                : isIniBuildsA330
                    ? _a330CommandedSpoilersArmed ?? raw.SpoilersArmed != 0
                : isPmdg737 && pmdg != null
                    ? pmdg.SpeedbrakeArmed
                : _nativeSpoilersArmed.HasValue
                    ? _nativeSpoilersArmed.Value != 0
                    : raw.SpoilersArmed != 0,
            AutobrakeLevel = isFlyByWireAirbus
                ? ResolveFbwAutobrakeLevel(
                    _fbwCommandedAutobrakeLevel,
                    _fbwCommandedAutobrakeLevelUtc,
                    _fbwAutobrakeLevel)
                : isIniBuildsA330
                    ? ResolveA330AutobrakeLevel()
                : isPmdg737 && pmdg != null
                    ? pmdg.AutobrakeSelector
                : _nativeAutobrakeLevel,
            WeatherRadarPwsSelectorPosition = isFlyByWireAirbus
                ? ResolveFbwWeatherRadarPwsSelector(
                    _fbwCommandedWeatherRadarPwsSelector,
                    _fbwCommandedWeatherRadarPwsSelectorUtc,
                    _fbwWeatherRadarPwsSelector)
                : isIniBuildsA330 && _a330WeatherRadarPwsInputState.HasValue
                    ? _a330WeatherRadarPwsInputState.Value >= 0.5 ? 0 : 1
                : _nativeWeatherRadarPwsSelector,
            NoseLightSelectorPosition = isFlyByWireAirbus
                ? FbwStateResolvers.ResolveNoseLightSelectorPosition(
                    raw.FbwNoseLightSelectorPosition,
                    _fbwCommandedNoseLightSelector,
                    _fbwCommandedNoseLightSelectorUtc,
                    raw.FbwNoseTakeoffLightCircuit,
                    raw.FbwNoseTaxiLightCircuit,
                    raw.TaxiLight)
                : isIniBuildsA330 && _a330NoseLightInputState.HasValue
                    ? _a330NoseLightInputState.Value
                : isPmdg737 && pmdg != null
                    ? pmdg.TaxiLightOn ? 1 : 2
                : _nativeNoseLightSelector,
            LeftLandingLightSelectorPosition = isFlyByWireAirbus
                ? ResolveFbwLandingLightSelectorPosition(
                    _fbwCommandedLandingLightSelector,
                    _fbwCommandedLandingLightSelectorUtc,
                    raw.FbwLeftLandingLightCircuit)
                : isPmdg737 && pmdg != null
                    ? ResolvePmdgCommandedSelectorState(
                        _pmdgCommandedLandingLightSelector,
                        _pmdgCommandedLandingLightUtc,
                        pmdg.LeftLandingLight)
                : _nativeLeftLandingLightSelector,
            RightLandingLightSelectorPosition = isFlyByWireAirbus
                ? ResolveFbwLandingLightSelectorPosition(
                    _fbwCommandedLandingLightSelector,
                    _fbwCommandedLandingLightSelectorUtc,
                    raw.FbwRightLandingLightCircuit)
                : isPmdg737 && pmdg != null
                    ? ResolvePmdgCommandedSelectorState(
                        _pmdgCommandedLandingLightSelector,
                        _pmdgCommandedLandingLightUtc,
                        pmdg.RightLandingLight)
                : _nativeRightLandingLightSelector,
            RunwayTurnoffLightsOn = isPmdg737 && pmdg != null
                && pmdg.LeftRunwayTurnoffLight
                && pmdg.RightRunwayTurnoffLight,
            TcasAltitudeReportingOn = isFlyByWireAirbus
                ? ResolveFbwTcasAltitudeReporting(
                    _fbwCommandedTcasAltitudeReporting,
                    _fbwCommandedTcasAltitudeReportingUtc,
                    _fbwTcasAltitudeReporting)
                : isIniBuildsA330
                    ? _a330TcasAltitudeInputState.HasValue
                        ? _a330TcasAltitudeInputState.Value >= 0.5
                        : null
                : _nativeTcasAltitudeReporting.HasValue
                    ? _nativeTcasAltitudeReporting.Value == 0
                    : null,
            TransponderAtcState = _nativeTransponderAtcState,
            TcasMode = isFlyByWireAirbus
                ? ResolveFbwSelectorWithCommand(
                    _fbwCommandedTcasMode,
                    _fbwCommandedTcasModeUtc,
                    _fbwTcasMode)
                : isIniBuildsA330 && _a330TcasTrafficInputState.HasValue
                    ? _a330TcasTrafficInputState.Value
                : isPmdg737 && pmdg != null
                    ? pmdg.TransponderMode
                : _nativeTcasMode,
            TransponderModeSelectorPosition = isFlyByWireAirbus
                ? _fbwTransponderMode
                : isIniBuildsA330 && _a330TransponderModeInputState.HasValue
                    ? _a330TransponderModeInputState.Value
                : isPmdg737 && pmdg != null
                    ? pmdg.TransponderMode
                : _nativeTransponderStandby,
            TransponderStandby = isPmdg737 && pmdg != null
                ? pmdg.TransponderMode == 0
                : isIniBuildsA330 && _a330TransponderModeInputState.HasValue
                    ? _a330TransponderModeInputState.Value < 0.5
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
            Console.Error.WriteLine("Warning: this build supports the iniBuilds A320neo V2, iniBuilds A321LR, iniBuilds A330, FlyByWire A32NX, and PMDG 737-800.");
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
        return FbwStateResolvers.ResolveBattery(
            commandedPushbuttonAuto,
            typedPushbuttonAuto,
            untypedPushbuttonAuto,
            genericMasterBattery);
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
        return FbwStateResolvers.ResolveBool(commandedValue, typedValue, untypedValue);
    }

    private static bool ResolveFbwAnyTrueState(
        bool? typedValue,
        bool? untypedValue,
        double genericUnindexedValue,
        double genericIndexedValue,
        params bool?[] typedIndexedValues)
    {
        return typedValue == true
               || untypedValue == true
               || typedIndexedValues.Any(value => value == true)
               || genericUnindexedValue != 0
               || genericIndexedValue != 0;
    }

    private static double ResolveFbwSelectorState(
        float? commandedValue,
        DateTime? commandedUtc,
        float? typedValue,
        float? untypedValue)
    {
        return FbwStateResolvers.ResolveSelector(
            commandedValue,
            commandedUtc,
            typedValue,
            untypedValue);
    }

    private static double ResolvePmdgCommandedSelectorState(
        float? commandedValue,
        DateTime? commandedUtc,
        byte sdkValue)
    {
        if (commandedValue.HasValue
            && commandedUtc.HasValue
            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromMinutes(2))
        {
            return commandedValue.Value;
        }

        return sdkValue;
    }

    private static bool ResolvePmdgCommandedBoolState(
        bool? commandedValue,
        DateTime? commandedUtc,
        bool sdkValue)
    {
        if (commandedValue.HasValue
            && commandedUtc.HasValue
            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromMinutes(2))
        {
            return commandedValue.Value;
        }

        return sdkValue;
    }

    private static bool ResolvePmdgCommandedPositionLightState(
        float? commandedValue,
        DateTime? commandedUtc,
        byte sdkValue)
    {
        if (commandedValue.HasValue
            && commandedUtc.HasValue
            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromMinutes(2))
        {
            // PMDG position/strobe selector: 0=steady, 1=off, 2=strobe & steady.
            return Math.Abs(commandedValue.Value - 1) >= 0.1f;
        }

        return sdkValue != 1;
    }

    private static double ResolvePmdgPositionStrobeSelector(
        float? commandedValue,
        DateTime? commandedUtc,
        byte sdkValue)
    {
        var value = sdkValue;
        if (commandedValue.HasValue
            && commandedUtc.HasValue
            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromMinutes(2))
        {
            value = (byte)Math.Round(commandedValue.Value);
        }

        // App flow semantics: 0=ON/strobe, 1=AUTO/steady, 2=OFF.
        return value == 2 ? 0 : value == 0 ? 1 : 2;
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
        if (fbwLVarValue.HasValue)
        {
            return fbwLVarValue.Value;
        }

        if (genericSpoilersArmed != 0)
        {
            return true;
        }

        if (commandedValue.HasValue
            && commandedUtc.HasValue
            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromSeconds(10))
        {
            return commandedValue.Value;
        }

        return false;
    }

    private static double? ResolveFbwAutobrakeLevel(
        float? commandedValue,
        DateTime? commandedUtc,
        float? fbwLVarValue)
    {
        if (fbwLVarValue.HasValue)
        {
            return fbwLVarValue;
        }

        if (commandedValue.HasValue
            && commandedUtc.HasValue
            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromSeconds(10))
        {
            return commandedValue.Value;
        }

        return null;
    }

    private static double? ResolveFbwWeatherRadarPwsSelector(
        float? commandedValue,
        DateTime? commandedUtc,
        float? fbwLVarValue)
    {
        if (fbwLVarValue.HasValue)
        {
            return fbwLVarValue;
        }

        if (commandedValue.HasValue
            && commandedUtc.HasValue
            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromSeconds(10))
        {
            return commandedValue.Value;
        }

        return null;
    }

    private static bool? ResolveFbwTcasAltitudeReporting(
        bool? commandedValue,
        DateTime? commandedUtc,
        bool? fbwLVarValue)
    {
        if (fbwLVarValue.HasValue)
        {
            return fbwLVarValue;
        }

        if (commandedValue.HasValue
            && commandedUtc.HasValue
            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromSeconds(10))
        {
            return commandedValue.Value;
        }

        return null;
    }

    private static double? ResolveFbwSelectorWithCommand(
        float? commandedValue,
        DateTime? commandedUtc,
        float? fbwLVarValue)
    {
        if (fbwLVarValue.HasValue)
        {
            return fbwLVarValue;
        }

        if (commandedValue.HasValue
            && commandedUtc.HasValue
            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromSeconds(10))
        {
            return commandedValue.Value;
        }

        return null;
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
        if (circuitOn != 0)
        {
            return 0;
        }

        if (commandedValue.HasValue
            && commandedUtc.HasValue
            && DateTime.UtcNow - commandedUtc.Value < TimeSpan.FromSeconds(10))
        {
            return commandedValue.Value;
        }

        return 2;
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

    private static void LogChangedFloat(string label, double value, ref double? previousValue)
    {
        if (!previousValue.HasValue || Math.Abs(previousValue.Value - value) >= 0.1)
        {
            AppLog.Write($"{label} changed to {value:F0}.");
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

    private void LogPmdgElectricalChanges(PmdgNg3State state)
    {
        if (!_loggedPmdgBatterySelector.HasValue
            || _loggedPmdgBatterySelector.Value != state.BatterySelector)
        {
            AppLog.Write($"PMDG battery selector changed to {state.BatterySelector}.");
            _loggedPmdgBatterySelector = state.BatterySelector;
        }

        if (!_loggedPmdgGroundPowerAvailable.HasValue
            || _loggedPmdgGroundPowerAvailable.Value != state.GroundPowerAvailable)
        {
            AppLog.Write(
                $"PMDG ground power available changed to {(state.GroundPowerAvailable ? 1 : 0)}.");
            _loggedPmdgGroundPowerAvailable = state.GroundPowerAvailable;
        }

        if (!_loggedPmdgGroundPowerOn.HasValue
            || _loggedPmdgGroundPowerOn.Value != state.GroundPowerOn)
        {
            AppLog.Write($"PMDG ground power switch changed to {(state.GroundPowerOn ? 1 : 0)}.");
            _loggedPmdgGroundPowerOn = state.GroundPowerOn;
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
        if (!string.Equals(_loggedPmdgElectricalBytes, powerSignature, StringComparison.Ordinal))
        {
            AppLog.Write($"PMDG power source: {powerSignature} apuEgt={state.ApuEgtNeedle:F0}.");
            _loggedPmdgElectricalBytes = powerSignature;
        }
    }

    private void LogPmdgElectricalBytes(byte[] data)
    {
    }

    private void LogPmdgAirStartChanges(PmdgNg3State state)
    {
        var signature =
            $"packL={state.LeftPackSwitch} packR={state.RightPackSwitch} " +
            $"apuBleed={(state.ApuBleedOn ? 1 : 0)} iso={state.IsolationValveSwitch} " +
            $"ductL={Math.Round(state.LeftDuctPressurePsi / 5f) * 5f:F0} " +
            $"ductR={Math.Round(state.RightDuctPressurePsi / 5f) * 5f:F0} " +
            $"engStartL={state.Engine1StartSelector} engStartR={state.Engine2StartSelector} " +
            $"startValveL={(state.Engine1StartValveOpen ? 1 : 0)} startValveR={(state.Engine2StartValveOpen ? 1 : 0)}";
        if (string.Equals(_loggedPmdgAirStartSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        _loggedPmdgAirStartSignature = signature;
        AppLog.Write($"PMDG air/start: {signature}.");
    }

    private static PmdgNg3State ParsePmdgNg3State(byte[] data)
    {
        byte ByteAt(int offset) =>
            data.Length > offset ? data[offset] : (byte)0;

        bool BoolAt(int offset) => ByteAt(offset) != 0;
        float FloatAt(int offset) =>
            data.Length >= offset + sizeof(float)
                ? BitConverter.ToSingle(data, offset)
                : 0;

        return new PmdgNg3State
        {
            IrsLeftMode = ByteAt(11),
            IrsRightMode = ByteAt(12),
            IrsLeftAlignLight = BoolAt(3),
            IrsRightAlignLight = BoolAt(4),
            IrsLeftOnDcLight = BoolAt(5),
            IrsRightOnDcLight = BoolAt(6),
            IrsLeftFault = BoolAt(7),
            IrsRightFault = BoolAt(8),
            Engine1StartValveOpen = BoolAt(44),
            Engine2StartValveOpen = BoolAt(45),
            Engine1ReverserAnnunciated = BoolAt(38),
            Engine2ReverserAnnunciated = BoolAt(39),
            LeftForwardFuelPump = BoolAt(89),
            RightForwardFuelPump = BoolAt(90),
            LeftAftFuelPump = BoolAt(91),
            RightAftFuelPump = BoolAt(92),
            LeftCenterFuelPump = BoolAt(93),
            RightCenterFuelPump = BoolAt(94),
            CenterFuelQuantityPounds = FloatAt(116),
            BatterySelector = ByteAt(133),
            GroundPowerAvailable = BoolAt(142),
            GroundPowerOn = BoolAt(143),
            DcBus1Powered = BoolAt(186),
            DcBus2Powered = BoolAt(187),
            AcTransferBus1Powered = BoolAt(189),
            AcTransferBus2Powered = BoolAt(190),
            EngineGen1On = BoolAt(145),
            EngineGen2On = BoolAt(146),
            ApuGen1On = BoolAt(147),
            ApuGen2On = BoolAt(148),
            TransferBus1Off = BoolAt(149),
            TransferBus2Off = BoolAt(150),
            Source1Off = BoolAt(151),
            Source2Off = BoolAt(152),
            GenBus1Off = BoolAt(153),
            GenBus2Off = BoolAt(154),
            ApuGenOffBus = BoolAt(155),
            ApuEgtNeedle = FloatAt(200),
            ElectricHydraulicPump1LowPressure = BoolAt(262),
            ElectricHydraulicPump2LowPressure = BoolAt(263),
            ElectricHydraulicPump1On = BoolAt(268),
            ElectricHydraulicPump2On = BoolAt(269),
            EmergencyExitLights = ByteAt(217),
            NoSmokingSelector = ByteAt(218),
            FastenBeltsSelector = ByteAt(219),
            LeftPackSwitch = ByteAt(280),
            RightPackSwitch = ByteAt(281),
            ApuBleedOn = BoolAt(284),
            IsolationValveSwitch = ByteAt(285),
            LeftDuctPressurePsi = FloatAt(296),
            RightDuctPressurePsi = FloatAt(300),
            LeftLandingLight = ByteAt(372),
            RightLandingLight = ByteAt(373),
            LeftRunwayTurnoffLight = BoolAt(376),
            RightRunwayTurnoffLight = BoolAt(377),
            TaxiLightOn = BoolAt(378),
            ApuSelector = ByteAt(379),
            Engine1StartSelector = ByteAt(380),
            Engine2StartSelector = ByteAt(381),
            LogoLightOn = BoolAt(383),
            PositionStrobeSelector = ByteAt(384),
            AntiCollisionOn = BoolAt(385),
            SpeedbrakeArmed = BoolAt(477),
            SpeedbrakeExtended = BoolAt(479),
            AutobrakeSelector = ByteAt(487),
            AutobrakeDisarmed = BoolAt(489),
            BrakePressureNeedle = FloatAt(508),
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
            LandingVref = ByteAt(625),
            FmcPerfInputComplete = BoolAt(634),
            IrsAligned = BoolAt(654),
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
                _state?.IsFlyByWireAirbus == true
                    ? _mobiFlightRuntimeReady
                    : _nativeApuAvailable.HasValue
                      && _nativeApuMasterSwitch.HasValue
                      && _nativeApuStartButton.HasValue
                      && _nativeApuBleedButton.HasValue
                      && _nativeApuGeneratorOn.HasValue
                      && _nativeApuFlapPercent.HasValue,
            var command when command.StartsWith("fuel-pumps ") =>
                _state?.IsFlyByWireAirbus == true
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
                _state?.IsFlyByWireAirbus == true
                    ? _mobiFlightRuntimeReady
                    : _nativeStrobeSelector.HasValue,
            var command when command == "fire-test apu" =>
                _state?.IsFlyByWireAirbus == true || _nativeApuFireTest.HasValue,
            var command when command == "fire-test engine-1" =>
                _state?.IsFlyByWireAirbus == true || _nativeEngine1FireTest.HasValue,
            var command when command == "fire-test engine-2" =>
                _state?.IsFlyByWireAirbus == true || _nativeEngine2FireTest.HasValue,
            var command when command.StartsWith("seatbelts ") =>
                _state?.IsFlyByWireAirbus == true
                    ? _mobiFlightRuntimeReady
                    : _nativeSeatbeltSelector.HasValue,
            var command when command.StartsWith("no-smoking ") =>
                _state?.IsFlyByWireAirbus == true
                    ? _mobiFlightRuntimeReady
                    : _nativeNoSmokingSelector.HasValue,
            var command when command.StartsWith("emergency-exit ") =>
                _state?.IsFlyByWireAirbus == true
                    ? _mobiFlightRuntimeReady
                    : _nativeEmergencyExitSelector.HasValue,
            var command when command.StartsWith("transponder ") =>
                _state?.IsFlyByWireAirbus == true
                    ? _mobiFlightRuntimeReady
                    : _nativeTransponderStandby.HasValue,
            var command when command.StartsWith("atc-system ") => _nativeTransponderAtcState.HasValue,
            var command when command.StartsWith("tcas altitude-reporting ") =>
                _state?.IsFlyByWireAirbus == true
                    ? _mobiFlightRuntimeReady
                    : _nativeTcasAltitudeReporting.HasValue,
            var command when command.StartsWith("tcas traffic ") =>
                _state?.IsFlyByWireAirbus == true
                    ? _mobiFlightRuntimeReady
                    : _nativeTcasMode.HasValue,
            var command when command.StartsWith("wxr-pws ") =>
                _state?.IsFlyByWireAirbus == true
                    ? _mobiFlightRuntimeReady
                    : _nativeWeatherRadarPwsSelector.HasValue,
            var command when command.StartsWith("nose-light ") =>
                _state?.IsFlyByWireAirbus == true
                    || _nativeNoseLightSelector.HasValue,
            var command when command.StartsWith("landing-lights ") =>
                _state?.IsFlyByWireAirbus == true
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
            case "pmdg logo off":
                SetPmdgLogoLight(false);
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
            case "pmdg ground-power off":
                SetPmdgGroundPower(false);
                break;
            case "pmdg engine-generators on":
                SetPmdgEngineGenerators(true);
                break;
            case "pmdg electric-hydraulic-pumps on":
                SetPmdgElectricHydraulicPumps(true);
                break;
            case "pmdg packs off":
                SetPmdgPacks(0);
                break;
            case "pmdg packs auto":
                SetPmdgPacks(1);
                break;
            case "pmdg isolation open":
                SetPmdgIsolationValve(2);
                break;
            case "pmdg isolation auto":
                SetPmdgIsolationValve(1);
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
            case "pmdg flaps 15":
                SetPmdgFlapsDetent(15);
                break;
            case "pmdg flaps takeoff":
                SetPmdgTakeoffFlaps();
                break;
            case "pmdg flaps 1":
                SetPmdgFlapsDetent(1);
                break;
            case "pmdg flaps 5":
                SetPmdgFlapsDetent(5);
                break;
            case "pmdg flaps landing":
                SetPmdgLandingFlaps();
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
            case "pmdg taxi-light off":
                SetPmdgTaxiLight(false);
                break;
            case "pmdg runway-turnoff on":
                SetPmdgRunwayTurnoffLights(true);
                break;
            case "pmdg runway-turnoff off":
                SetPmdgRunwayTurnoffLights(false);
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
            case "pmdg mcp autothrottle arm":
                SetPmdgMcpSwitch(380, "autothrottle ARM");
                break;
            case "pmdg mcp lnav arm":
                SetPmdgMcpSwitch(397, "LNAV ARM");
                break;
            case "pmdg mcp vnav arm":
                SetPmdgMcpSwitch(386, "VNAV ARM");
                break;
            case "pmdg tcas traffic":
                SetPmdgTcasTrafficDisplay();
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
            string.Equals(definition.Id, "cruise", StringComparison.OrdinalIgnoreCase)
            && !_state.IsIniBuildsA321Lr;
        _smoothCruiseSinceUtc = null;
        _nextCruiseSeatbeltCommandUtc = DateTime.MinValue;
        _procedureRunner.Start(definition, _state);
        FinishProcedureOneShotIfTerminal();
    }

    private void SetPmdgIrsSelector(bool left, uint position)
    {
        SendPmdgNg3Control(left ? 255u : 256u, position);
        var now = DateTime.UtcNow;
        if (left)
        {
            _pmdgCommandedLeftIrsMode = position;
            _pmdgCommandedLeftIrsModeUtc = now;
            if (_state != null)
            {
                _state.Adirs1SelectorState = position;
            }
        }
        else
        {
            _pmdgCommandedRightIrsMode = position;
            _pmdgCommandedRightIrsModeUtc = now;
            if (_state != null)
            {
                _state.Adirs2SelectorState = position;
            }
        }
        AppLog.Write(
            $"Executed PMDG IRS {(left ? "left" : "right")} selector command: position {position}; command-backed verification active.");
        FinishOneShot();
    }

    private void SetPmdgLogoLight(bool on)
    {
        if (_pmdgNg3State?.LogoLightOn == on)
        {
            AppLog.Write(
                $"PMDG logo light already {(on ? "ON" : "OFF")}.");
            FinishOneShot();
            return;
        }

        // PMDG's LOGO switch is a three-position physical switch. The SDK
        // direct bool/position command can leave it in the centre detent, so
        // drive the actual cockpit mouse rectangle through ROTOR_BRAKE switch
        // id 122 and wait for PMDG's real bool readback.
        const uint switchId = 122;
        var actionCode = on ? 7u : 8u;
        var clicks = 3;

        for (var i = 0; i < clicks; i++)
        {
            if (i == 0)
            {
                SendPmdgRotorBrakeSwitch(switchId, actionCode);
            }
            else
            {
                SchedulePmdgRotorBrakeSwitch(switchId, actionCode, 300 * i);
            }
        }

        SchedulePmdgNg3Control(122, on ? 2u : 0u, 1000);
        _pmdgCommandedLogoLightOn = null;
        _pmdgCommandedLogoLightUtc = null;
        AppLog.Write(
            $"Executed PMDG logo light ROTOR_BRAKE command: switch id {switchId} action {actionCode}, {clicks} click(s) toward {(on ? "ON" : "OFF")}.");
        FinishOneShot();
    }

    private void SetPmdgPositionStrobe(uint position)
    {
        var current = _pmdgNg3State?.PositionStrobeSelector;
        var actionCode = !current.HasValue || position >= current.Value
            ? 7u
            : 8u;
        var clicks = current.HasValue
            ? Math.Max(1, Math.Abs((int)position - current.Value))
            : 2;
        clicks = Math.Min(2, clicks);

        for (var i = 0; i < clicks; i++)
        {
            if (i == 0)
            {
                SendPmdgRotorBrakeSwitch(123, actionCode);
            }
            else
            {
                SchedulePmdgRotorBrakeSwitch(123, actionCode, 300 * i);
            }
        }

        // Do not send the SDK direct-position fallback here. This PMDG switch
        // has three physical detents and can cycle; extra commands can move it
        // past STROBE & STEADY and back to STEADY.
        _pmdgCommandedPositionStrobeSelector = null;
        _pmdgCommandedPositionStrobeUtc = null;

        AppLog.Write(
            $"Executed PMDG position/strobe ROTOR_BRAKE command: switch id 123 action {actionCode}, {clicks} click(s) toward position {position}.");
        FinishOneShot();
    }

    private void SetPmdgEmergencyExitLights(uint position)
    {
        if (position == 1)
        {
            // PMDG 737 normal workflow: closing the emergency-light guard arms the switch.
            SendPmdgNg3Control(101, PmdgMouseLeftSingle);
        }
        else
        {
            SendPmdgNg3Control(100, position);
        }

        _pmdgCommandedEmergencyExitSelector = position;
        _pmdgCommandedEmergencyExitUtc = DateTime.UtcNow;
        if (_state != null)
        {
            _state.EmergencyExitSelectorPosition = position;
        }

        AppLog.Write(
            position == 1
                ? "Executed PMDG emergency-exit guard close command; command-backed verification active."
                : $"Executed PMDG emergency-exit switch command: position {position}; command-backed verification active.");
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
        var offsets = new List<uint> { 37u, 38u, 39u, 40u };
        var centerPumpsRequired =
            _pmdgNg3State?.CenterFuelQuantityPounds > PmdgCenterFuelPumpRequiredThresholdPounds;
        if (!on || centerPumpsRequired)
        {
            offsets.Add(45u);
            offsets.Add(46u);
        }

        foreach (var offset in offsets)
        {
            SendPmdgNg3Control(offset, parameter);
        }
        AppLog.Write(
            on
                ? $"Executed PMDG fuel pump command: main pumps ON; center pumps {(centerPumpsRequired ? "ON" : "left OFF because center fuel is below threshold")}."
                : "Executed PMDG fuel pump command: all pumps OFF.");
        FinishOneShot();
    }

    private void SetPmdgApuSelector(uint position)
    {
        if (position == 2)
        {
            SendPmdgNg3Control(118, 1);
            SchedulePmdgNg3Control(118, 2, 600);
            SchedulePmdgNg3Control(118, 1, 1800);
            AppLog.Write("Executed PMDG APU selector sequence: ON, momentary START, release to ON.");
            FinishOneShot();
            return;
        }

        SendPmdgNg3Control(118, position);
        AppLog.Write($"Executed PMDG APU selector command: position {position}.");
        FinishOneShot();
    }

    private void SetPmdgApuBleed(bool on)
    {
        SendPmdgNg3Control(211, on ? 1u : 0u);
        FinishOneShot();
    }

    private void SetPmdgApuGenerators(bool on)
    {
        var parameter = on ? PmdgMouseLeftSingle : PmdgMouseRightSingle;
        SendPmdgNg3Control(28, parameter);
        SendPmdgNg3Control(29, parameter);
        SchedulePmdgNg3Control(28, parameter, 500);
        SchedulePmdgNg3Control(29, parameter, 500);
        SchedulePmdgNg3Control(28, parameter, 1000);
        SchedulePmdgNg3Control(29, parameter, 1000);
        AppLog.Write($"Executed PMDG APU generator switch command: {(on ? "ON/transfer" : "OFF")}.");
        FinishOneShot();
    }

    private void SetPmdgGroundPower(bool on)
    {
        SendPmdgNg3Control(17, on ? PmdgMouseLeftSingle : PmdgMouseRightSingle);
        AppLog.Write($"Executed PMDG ground power switch command: {(on ? "ON" : "OFF")}.");
        FinishOneShot();
    }

    private void SetPmdgEngineGenerators(bool on)
    {
        var parameter = on ? PmdgMouseLeftSingle : PmdgMouseRightSingle;
        SendPmdgNg3Control(27, parameter);
        SendPmdgNg3Control(30, parameter);
        AppLog.Write($"Executed PMDG engine generator command: {(on ? "ON" : "OFF")}.");
        FinishOneShot();
    }

    private void SetPmdgElectricHydraulicPumps(bool on)
    {
        if (!on)
        {
            AppLog.Write("Skipped PMDG electric hydraulic pump OFF command; shutdown flow does not currently manage hydraulic pumps.");
            FinishOneShot();
            return;
        }

        var clicked = new List<string>();
        if (_pmdgNg3State?.ElectricHydraulicPump1On != true)
        {
            SendPmdgNg3Control(168, PmdgMouseLeftSingle);
            clicked.Add("ELEC 1");
        }

        if (_pmdgNg3State?.ElectricHydraulicPump2On != true)
        {
            SendPmdgNg3Control(167, PmdgMouseLeftSingle);
            clicked.Add("ELEC 2");
        }

        AppLog.Write(clicked.Count == 0
            ? "PMDG electric hydraulic pumps already ON."
            : $"Executed PMDG electric hydraulic pump command: {string.Join(", ", clicked)} ON.");
        FinishOneShot();
    }

    private void SetPmdgPacks(uint position)
    {
        SendPmdgNg3Control(200, position);
        SendPmdgNg3Control(201, position);
        AppLog.Write($"Executed PMDG pack switch command: position {position}.");
        FinishOneShot();
    }

    private void SetPmdgIsolationValve(uint position)
    {
        SendPmdgNg3Control(202, position);
        AppLog.Write($"Executed PMDG isolation valve command: position {position}.");
        FinishOneShot();
    }

    private void SetPmdgAutobrake(uint position)
    {
        SendPmdgNg3Control(460, position);
        FinishOneShot();
    }

    private void SetPmdgSpeedbrakeArm()
    {
        SendPmdgNg3Control(6792, PmdgMouseLeftSingle);
        AppLog.Write("Executed PMDG speedbrake ARM command.");
        FinishOneShot();
    }

    private void SetPmdgSpeedbrakeDown()
    {
        SendPmdgNg3Control(6791, PmdgMouseLeftSingle);
        AppLog.Write("Executed PMDG speedbrake DOWN command.");
        FinishOneShot();
    }

    private void SetPmdgGear(uint position)
    {
        var current = _pmdgNg3State?.GearLever;
        if (current.HasValue && current.Value == position)
        {
            AppLog.Write($"PMDG gear lever already at target position {position}.");
            FinishOneShot();
            return;
        }

        // PMDG SDK readback:
        // MAIN_GearLever = 0 UP, 1 OFF, 2 DOWN.
        //
        // PMDG's actual cockpit behavior drives the lever through K:ROTOR_BRAKE
        // switch id 455. The SDK event-offset/direct target path can be ignored.
        //
        // The aircraft behavior applies the vertical detent movement through a
        // drag gesture. Sending only LeftSingle/LeftRelease can be ignored by
        // the PMDG cockpit, so reproduce the whole sequence:
        //   upper-half LeftSingle/Move/LeftRelease = actions 1/3/4, toward UP
        //   lower-half LeftSingle/Move/LeftRelease = actions 2/3/5, toward DOWN
        var pressAction = position == 0 ? 1u : 2u;
        var releaseAction = position == 0 ? 4u : 5u;
        const uint moveAction = 3;
        var clicks = current.HasValue
            ? Math.Max(1, Math.Abs((int)position - current.Value))
            : 3;

        // Add one extra bounded click as insurance against the OFF detent. The
        // PMDG lever clamps at UP/DOWN, so this is safe and prevents a one-notch
        // move from DOWN -> OFF or UP -> OFF.
        clicks = Math.Min(3, clicks + 1);

        SendPmdgGearFallback(position, 0);
        SendPmdgNg3Control(455, position);
        for (var i = 0; i < clicks; i++)
        {
            var baseDelay = 420 * i;
            if (baseDelay == 0)
            {
                SendPmdgRotorBrakeSwitch(455, pressAction);
                SchedulePmdgRotorBrakeSwitch(455, moveAction, 90);
                SchedulePmdgRotorBrakeSwitch(455, releaseAction, 180);
                continue;
            }

            SchedulePmdgRotorBrakeSwitch(455, pressAction, baseDelay);
            SchedulePmdgRotorBrakeSwitch(455, moveAction, baseDelay + 90);
            SchedulePmdgRotorBrakeSwitch(455, releaseAction, baseDelay + 180);
        }
        SendPmdgGearFallback(position, 1150);
        SchedulePmdgNg3Control(455, position, 1250);

        AppLog.Write(
            $"Executed PMDG gear lever command: switch id 455 press/move/release {pressAction}/{moveAction}/{releaseAction}, {clicks} detent move(s), fallback {(position == 0 ? "GEAR_UP" : "GEAR_DOWN")}.");
        FinishOneShot();
    }

    private void SendPmdgGearFallback(uint position, int delayMs)
    {
        var eventId = position == 0 ? CopilotEvent.GearUp : CopilotEvent.GearDown;
        if (delayMs <= 0)
        {
            TransmitGearEvent(eventId);
            return;
        }

        var timer = new System.Windows.Forms.Timer { Interval = delayMs };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            TransmitGearEvent(eventId);
            _nativePulseTimers.Remove(timer);
            timer.Dispose();
        };
        _nativePulseTimers.Add(timer);
        timer.Start();
    }

    private void TransmitGearEvent(CopilotEvent eventId)
    {
        if (_simConnect == null)
        {
            return;
        }

        _simConnect.TransmitClientEvent(
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            eventId,
            0,
            Priority.Highest,
            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
    }

    private void SetPmdgTaxiLight(bool on)
    {
        SendPmdgNg3Control(117, on ? 1u : 0u);
        FinishOneShot();
    }

    private void SetPmdgRunwayTurnoffLights(bool on)
    {
        SendPmdgNg3Control(115, on ? 1u : 0u);
        SendPmdgNg3Control(116, on ? 1u : 0u);
        AppLog.Write($"Executed PMDG runway turnoff light command: {(on ? "ON" : "OFF")}.");
        FinishOneShot();
    }

    private void SetPmdgLandingLights(bool on)
    {
        var commandedPosition = on ? 2f : 0f;
        _pmdgCommandedLandingLightSelector = commandedPosition;
        _pmdgCommandedLandingLightUtc = DateTime.UtcNow;
        if (_state != null)
        {
            _state.LeftLandingLightSelectorPosition = commandedPosition;
            _state.RightLandingLightSelectorPosition = commandedPosition;
        }

        if (on)
        {
            SendPmdgRotorBrakeSwitch(113, 1);
            SendPmdgRotorBrakeSwitch(114, 1);
            SchedulePmdgRotorBrakeSwitch(113, 1, 350);
            SchedulePmdgRotorBrakeSwitch(114, 1, 350);
            SchedulePmdgRotorBrakeSwitch(113, 1, 700);
            SchedulePmdgRotorBrakeSwitch(114, 1, 700);
            SendPmdgNg3Control(113, 1);
            SendPmdgNg3Control(114, 1);
            AppLog.Write(
                "Executed PMDG landing light ROTOR_BRAKE command: retractable switch ids 113/114 left-single toward ON; command-backed verification active.");
        }
        else
        {
            SendPmdgNg3Control(111, 0);
            SendPmdgNg3Control(112, 0);
            SchedulePmdgNg3Control(111, 0, 500);
            SchedulePmdgNg3Control(112, 0, 500);
            SendPmdgNg3Control(113, 0);
            SendPmdgNg3Control(114, 0);
            AppLog.Write(
                "Executed PMDG landing light command: retractable target RETRACT (0), fixed lights OFF; command-backed verification active.");
        }
        FinishOneShot();
    }

    private void SendPmdgRotorBrakeSwitch(uint switchId, uint actionCode)
    {
        if (_simConnect == null)
        {
            return;
        }

        _simConnect.TransmitClientEvent(
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            CopilotEvent.RotorBrake,
            switchId * 100u + actionCode,
            Priority.Highest,
            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
    }

    private void SchedulePmdgRotorBrakeSwitch(uint switchId, uint actionCode, int delayMs)
    {
        var timer = new System.Windows.Forms.Timer { Interval = delayMs };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            SendPmdgRotorBrakeSwitch(switchId, actionCode);
            _nativePulseTimers.Remove(timer);
            timer.Dispose();
        };
        _nativePulseTimers.Add(timer);
        timer.Start();
    }

    private void SchedulePmdgNg3Control(uint sdkEventOffset, uint parameter, int delayMs)
    {
        var timer = new System.Windows.Forms.Timer { Interval = delayMs };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            SendPmdgNg3Control(sdkEventOffset, parameter);
            _nativePulseTimers.Remove(timer);
            timer.Dispose();
        };
        _nativePulseTimers.Add(timer);
        timer.Start();
    }

    private void SetPmdgThreePositionSwitch(uint eventOffset, byte? currentPosition, int targetPosition)
    {
        var current = currentPosition ?? 1;
        var clicks = Math.Abs(targetPosition - current);
        if (clicks <= 0)
        {
            return;
        }

        var parameter = targetPosition > current
            ? PmdgMouseLeftSingle
            : PmdgMouseRightSingle;
        for (var i = 0; i < clicks; i++)
        {
            SendPmdgNg3Control(eventOffset, parameter);
        }
    }

    private void SetPmdgTransponderMode(uint mode)
    {
        var current = _pmdgNg3State?.TransponderMode;
        if (current.HasValue && current.Value == mode)
        {
            AppLog.Write($"PMDG transponder mode already at target {mode}.");
            FinishOneShot();
            return;
        }

        var actionCode = mode >= (current ?? 0) ? 7u : 8u;
        var clicks = current.HasValue
            ? Math.Max(1, Math.Abs((int)mode - current.Value))
            : 4;

        // PMDG's TCAS mode selector is not driven reliably through the SDK
        // mouse flags. The aircraft behavior sends K:ROTOR_BRAKE with
        // switch id 800 and wheel action codes 7/8, so use that path directly.
        for (var i = 0; i < clicks; i++)
        {
            if (i == 0)
            {
                SendPmdgRotorBrakeSwitch(800, actionCode);
            }
            else
            {
                SchedulePmdgRotorBrakeSwitch(800, actionCode, 650 * i);
            }
        }

        AppLog.Write(
            $"Executed PMDG TCAS ROTOR_BRAKE command: switch id 800 action {actionCode}, {clicks} click(s) toward mode {mode}.");
        FinishOneShot();
    }

    private void SetPmdgMcpSwitch(uint eventOffset, string label)
    {
        SendPmdgNg3Control(eventOffset, PmdgMouseLeftSingle);
        AppLog.Write($"Executed PMDG MCP command: {label}.");
        FinishOneShot();
    }

    private void SetPmdgTcasTrafficDisplay()
    {
        SendPmdgNg3Control(362, PmdgMouseLeftSingle);
        SendPmdgNg3Control(418, PmdgMouseLeftSingle);
        AppLog.Write("Executed PMDG EFIS TFC display command for both sides.");
        FinishOneShot();
    }

    private void SetPmdgAntiCollision(bool on)
    {
        SendPmdgNg3Control(124, on ? 1u : 0u);
        FinishOneShot();
    }

    private void SetPmdgTakeoffFlaps()
    {
        var takeoffFlaps = _state?.BoeingTakeoffFlaps ?? _pmdgNg3State?.TakeoffFlaps ?? 5;
        if (takeoffFlaps <= 0)
        {
            takeoffFlaps = 5;
        }

        SetPmdgFlapsDetent(takeoffFlaps);
    }

    private void SetPmdgLandingFlaps()
    {
        var landingFlaps = _state?.BoeingLandingFlaps ?? _pmdgNg3State?.LandingFlaps ?? 30;
        if (landingFlaps <= 0)
        {
            landingFlaps = 30;
        }

        SetPmdgFlapsDetent(landingFlaps);
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
        SendPmdgNg3Control(offset, PmdgMouseLeftSingle);
        AppLog.Write($"Executed PMDG flaps command: detent {detent}, event offset {offset}.");
        FinishOneShot();
    }

    private void TryRestoreProcedureSession()
    {
        if (_procedureSessionRestoreAttempted)
        {
            return;
        }

        _procedureSessionRestoreAttempted = true;
        if (!string.IsNullOrWhiteSpace(_procedureSession.ActiveProcedureId))
        {
            var previousActiveProcedureId = _procedureSession.ActiveProcedureId;
            _procedureSession.ActiveProcedureId = null;
            _procedureSession.ActiveStepIndex = 0;
            SaveProcedureSession();
            AppendDashboardLog(
                $"Saved active flow '{previousActiveProcedureId}' was cleared on startup. Select a flow when ready.");
        }
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

    private void ClearCommandedAircraftState()
    {
        _pmdgCommandedLeftIrsMode = null;
        _pmdgCommandedRightIrsMode = null;
        _pmdgCommandedLeftIrsModeUtc = null;
        _pmdgCommandedRightIrsModeUtc = null;
        _pmdgCommandedLogoLightOn = null;
        _pmdgCommandedLogoLightUtc = null;
        _pmdgCommandedPositionStrobeSelector = null;
        _pmdgCommandedPositionStrobeUtc = null;
        _pmdgCommandedLandingLightSelector = null;
        _pmdgCommandedLandingLightUtc = null;
        _pmdgCommandedEmergencyExitSelector = null;
        _pmdgCommandedEmergencyExitUtc = null;

        _fbwCommandedBattery1Auto = null;
        _fbwCommandedBattery2Auto = null;
        _fbwCommandedSpoilersArmed = null;
        _fbwCommandedSpoilersArmedUtc = null;
        _fbwCommandedAutobrakeLevel = null;
        _fbwCommandedAutobrakeLevelUtc = null;
        _fbwCommandedWeatherRadarPwsSelector = null;
        _fbwCommandedWeatherRadarPwsSelectorUtc = null;
        _fbwCommandedNoseLightSelector = null;
        _fbwCommandedNoseLightSelectorUtc = null;
        _fbwCommandedTcasAltitudeReporting = null;
        _fbwCommandedTcasAltitudeReportingUtc = null;
        _fbwCommandedTcasMode = null;
        _fbwCommandedTcasModeUtc = null;
        _fbwCommandedLandingLightSelector = null;
        _fbwCommandedLandingLightSelectorUtc = null;
        _fbwCommandedAdirs1Selector = null;
        _fbwCommandedAdirs2Selector = null;
        _fbwCommandedAdirs3Selector = null;
        _fbwCommandedAdirs1SelectorUtc = null;
        _fbwCommandedAdirs2SelectorUtc = null;
        _fbwCommandedAdirs3SelectorUtc = null;
        _fbwCommandedCrewOxygen = null;
        _fbwCommandedCrewOxygenUtc = null;

        _a330CommandedSpoilersArmed = null;
    }

    private void ResetFlightProgress()
    {
        CancelFuelPumpSequence();
        _procedureRunner.Cancel();
        ClearCommandedAircraftState();
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
        if (_settings.SimBriefAutoImportOnNewFlight
            && (!string.IsNullOrWhiteSpace(_settings.SimBriefPilotId)
                || !string.IsNullOrWhiteSpace(_settings.SimBriefUsername)))
        {
            _ = ImportLatestSimBriefAsync(showReview: true, automatic: true);
        }
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

        if (_state.IsFlyByWireAirbus)
        {
            SetFlyByWireNavLogoSelector(nativePosition);
            return;
        }

        if (!_state.IsIniBuildsAirbusFamily || !_mobiFlightReady)
        {
            Console.Error.WriteLine("NAV & LOGO procedure blocked: iniBuilds adapter is unavailable.");
            FinishOneShot(4);
            return;
        }

        if (_state.IsIniBuildsA330)
        {
            if (_a330NavLogoInputState.HasValue
                && Math.Abs(_a330NavLogoInputState.Value - nativePosition) < 0.1)
            {
                AppendDashboardLog($"NAV & LOGO selector already {FormatNavLogoPosition(nativePosition)}.");
                FinishOneShot();
                return;
            }
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
            0 when _state.IsIniBuildsA330 => "AIRLINER_NAVLOGO_TOGGLE_0",
            1 when _state.IsIniBuildsA330 => "AIRLINER_NAVLOGO_TOGGLE_1",
            2 when _state.IsIniBuildsA330 => "AIRLINER_NAVLOGO_TOGGLE_2",
            0 => "AIRLINER_LT_NAVLOGO_STATE1",
            1 => "AIRLINER_LT_NAVLOGO_STATE2",
            2 => "AIRLINER_LT_NAVLOGO_STATE3",
            _ => throw new ArgumentOutOfRangeException(
                nameof(nativePosition),
                nativePosition,
                "NAV & LOGO selector position must be 0, 1, or 2.")
        };
        if (_state.IsIniBuildsA330)
        {
            _simConnect!.SetInputEvent(A330NavLogoInputEventHash, (double)nativePosition);
        }
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

        if (_state.IsFlyByWireAirbus)
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
        if (_state?.IsFlyByWireAirbus == true)
        {
            SetFlyByWireBoolLVarAction(
                "APU master",
                "A32NX_OVHD_APU_MASTER_SW_PB_IS_ON",
                desiredOn,
                state => state.ApuMasterSwitchOn == desiredOn);
            return;
        }

        if (_state?.IsIniBuildsA330 == true)
        {
            if (_simConnect == null)
            {
                AppendDashboardLog("APU master blocked: simulator state is unavailable.");
                FinishOneShot(4);
                return;
            }
            if (_state.ApuMasterSwitchOn == desiredOn)
            {
                AppendDashboardLog($"APU master already {desiredOn.ToOnOff()}.");
                FinishOneShot();
                return;
            }

            _simConnect.SetInputEvent(
                A330ApuInputEventHashes[0],
                desiredOn ? 1.0 : 0.0);
            BeginNativeAction(
                "APU master",
                state => state.ApuMasterSwitchOn == desiredOn,
                desiredOn,
                TimeSpan.FromSeconds(10));
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
        if (_state?.IsFlyByWireAirbus == true)
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
        Func<AircraftState, bool> verify,
        string? alternateLVarName = null,
        IEnumerable<string>? additionalAlternateLVarNames = null)
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
        if (!string.IsNullOrWhiteSpace(alternateLVarName))
        {
            SendMobiFlightCommand($"MF.SimVars.Set.{value} (>L:{alternateLVarName})");
            SendMobiFlightCommand($"MF.SimVars.Set.{value} (>L:{alternateLVarName}, Bool)");
        }
        if (additionalAlternateLVarNames != null)
        {
            foreach (var additionalAlternateLVarName in additionalAlternateLVarNames)
            {
                if (string.IsNullOrWhiteSpace(additionalAlternateLVarName))
                {
                    continue;
                }

                SendMobiFlightCommand($"MF.SimVars.Set.{value} (>L:{additionalAlternateLVarName})");
                SendMobiFlightCommand($"MF.SimVars.Set.{value} (>L:{additionalAlternateLVarName}, Bool)");
            }
        }
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
        if (_state?.IsFlyByWireAirbus == true)
        {
            SetFlyByWireBoolLVarAction(
                "APU bleed",
                "A32NX_OVHD_PNEU_APU_BLEED_PB_IS_ON",
                desiredOn,
                state => state.ApuBleedOn == desiredOn);
            return;
        }

        if (_state?.IsIniBuildsA330 == true)
        {
            if (_simConnect == null)
            {
                AppendDashboardLog("APU bleed blocked: simulator state is unavailable.");
                FinishOneShot(4);
                return;
            }
            if (_state.ApuBleedOn == desiredOn)
            {
                AppendDashboardLog($"APU bleed already {desiredOn.ToOnOff()}.");
                FinishOneShot();
                return;
            }

            _simConnect.SetInputEvent(
                A330ApuInputEventHashes[2],
                desiredOn ? 1.0 : 0.0);
            BeginNativeAction(
                "APU bleed",
                state => state.ApuBleedOn == desiredOn,
                desiredOn,
                TimeSpan.FromSeconds(10));
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
        if (_state?.IsA320NeoV2 == true)
        {
            SetIniBuildsA320FuelPumps(desiredOn);
            return;
        }
        if (_state?.IsFlyByWireAirbus == true)
        {
            SetFlyByWireFuelPumps(desiredOn);
            return;
        }
        if (_state?.IsIniBuildsA330 == true)
        {
            SetA330FuelPumps(desiredOn);
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

    private void SetIniBuildsA320FuelPumps(bool desiredOn)
    {
        if (_state?.IsA320NeoV2 != true)
        {
            AppendDashboardLog(
                "A320 fuel pumps blocked: the loaded aircraft is not the iniBuilds A320neo V2.");
            FinishOneShot(3);
            return;
        }
        if (!ValidateNativeInputAction("A320 fuel pumps"))
        {
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
        var alreadyDesired = desiredOn
            ? A320FuelPumpProfile.AreConfigured(states)
            : A320FuelPumpProfile.AreAllOff(states);
        if (alreadyDesired)
        {
            AppendDashboardLog(
                $"A320 fuel pumps already {(desiredOn ? "ON" : "OFF")}.");
            FinishOneShot();
            return;
        }

        var toggles = new Queue<FuelPumpToggle>();
        for (var index = 0; index < states.Length; index++)
        {
            if (A320FuelPumpProfile.IsOn(states[index]) == desiredOn)
            {
                continue;
            }

            toggles.Enqueue(
                new FuelPumpToggle(
                    index + 1,
                    A320FuelPumpProfile.BuildToggleCommand(index)));
        }

        _pendingFuelPumpSequence = new PendingFuelPumpSequence(toggles, desiredOn);
        _fuelPumpSequenceTimer?.Dispose();
        _fuelPumpSequenceTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _fuelPumpSequenceTimer.Tick += (_, _) => ExecuteNextFuelPumpToggle();
        ExecuteNextFuelPumpToggle();
    }

    private void SetA330FuelPumps(bool desiredOn)
    {
        if (_simConnect == null || _state == null)
        {
            AppendDashboardLog("A330 fuel pumps blocked: simulator state is unavailable.");
            FinishOneShot(4);
            return;
        }
        if (!_state.IsIniBuildsA330)
        {
            AppendDashboardLog("A330 fuel pumps blocked: the loaded aircraft is not the iniBuilds A330.");
            FinishOneShot(3);
            return;
        }
        if (!_state.OnGround || _state.GroundSpeedKnots > 0.5)
        {
            AppendDashboardLog("A330 fuel pumps blocked: aircraft must be stationary on the ground.");
            FinishOneShot(3);
            return;
        }
        if (!A330FuelPumpInputEventsReady())
        {
            AppendDashboardLog("A330 fuel pumps blocked: fuel pump InputEvent readback is not ready yet.");
            FinishOneShot(4);
            return;
        }

        var alreadyDesired = desiredOn
            ? _state!.FuelPumpsConfigured
            : AreAllFuelPumpsOff(_state!);
        if (alreadyDesired)
        {
            AppendDashboardLog($"A330 fuel pumps already {(desiredOn ? "ON" : "OFF")}.");
            FinishOneShot();
            return;
        }

        var pumpStates = new[]
        {
            _state!.FuelPump1State,
            _state.FuelPump2State,
            _state.FuelPump3State,
            _state.FuelPump4State,
            _state.FuelPump5State,
            _state.FuelPump6State
        };

        var toggles = new Queue<FuelPumpToggle>();
        for (var index = 0; index < pumpStates.Length; index++)
        {
            var isOn = Math.Abs(pumpStates[index]) >= 0.1;
            if (isOn == desiredOn)
            {
                continue;
            }

            toggles.Enqueue(new FuelPumpToggle(index + 1, A330FuelPumpInputEventHashes[index]));
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
        if (toggle.InputEventHash.HasValue)
        {
            _simConnect!.SetInputEvent(
                toggle.InputEventHash.Value,
                _pendingFuelPumpSequence.DesiredOn ? 1.0 : 0.0);
        }
        else
        {
            SendMobiFlightCommand($"MF.SimVars.Set.{toggle.CalculatorCode}");
            SendMobiFlightCommand("MF.DummyCmd");
        }
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

    private bool A330FuelPumpInputEventsReady() =>
        _a330FuelPumpInputStates.All(state => state.HasValue);

    private bool A330FuelPumpsConfigured() =>
        A330FuelPumpInputEventsReady()
        && _a330FuelPumpInputStates.All(state => state!.Value >= 0.5);

    private bool A330SignInputEventsReady() =>
        _a330SignInputStates.All(state => state.HasValue);

    private double? ResolveA330AutobrakeLevel()
    {
        if (_a330AutobrakeInputStates[2].HasValue
            && _a330AutobrakeInputStates[2]!.Value >= 0.5)
        {
            return 3;
        }
        if (_a330AutobrakeInputStates[1].HasValue
            && _a330AutobrakeInputStates[1]!.Value >= 0.5)
        {
            return 2;
        }
        if (_a330AutobrakeInputStates[0].HasValue
            && _a330AutobrakeInputStates[0]!.Value >= 0.5)
        {
            return 1;
        }
        return _a330AutobrakeInputStates.All(state => state.HasValue) ? 0 : null;
    }

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
        if (_state?.IsFlyByWireAirbus == true)
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
        if (_state!.IsIniBuildsA330)
        {
            // The A330 uses AIRLINER_ADIRSn_MODE (OFF=0/NAV=1/ATT=2).
            // Command that live InputEvent and use the same event for readback.
            _simConnect!.SetInputEvent(
                A330AdirsInputEventHashes[selector - 1],
                (double)position);
            SendMobiFlightCommand(
                $"MF.SimVars.Set.(>B:AIRLINER_ADIRS{selector}_MODE_{position})");
            SendMobiFlightCommand("MF.DummyCmd");
            AppLog.Write(
                $"A330 ADIRS {selector} command sent: AIRLINER_ADIRS{selector}_MODE={position}.");
        }
        else
        {
            _simConnect!.SetInputEvent(inputEventHash, (double)position);
        }
        BeginNativeAction(
            $"ADIRS {selector} selector",
            Verify,
            position != 0,
            TimeSpan.FromSeconds(10),
            logProgressToDashboard: !_state.IsIniBuildsA330);
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
        try
        {
            _simConnect.SetInputEvent(inputEventHash, (double)position);
        }
        catch (COMException ex)
        {
            AppLog.Write($"FBW ADIRS {selector} SetInputEvent failed; falling back to calculator commands: {ex.Message}");
        }

        SendMobiFlightCommand($"MF.SimVars.Set.(>B:AIRLINER_ADIRS{selector}_MODE_{position})");
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

        if (_state?.IsIniBuildsA330 == true)
        {
            if (_simConnect == null || !_a330CrewOxygenInputState.HasValue)
            {
                AppendDashboardLog("Crew oxygen blocked: A330 InputEvent readback is unavailable.");
                FinishOneShot(4);
                return;
            }
            if (_state.CrewOxygenOn == desiredOn)
            {
                AppendDashboardLog($"Crew oxygen already {desiredOn.ToOnOff()}.");
                FinishOneShot();
                return;
            }

            _simConnect.SetInputEvent(
                A330CrewOxygenInputEventHash,
                desiredOn ? 1.0 : 0.0);
            BeginNativeAction(
                "Crew oxygen",
                state => state.CrewOxygenOn == desiredOn,
                desiredOn,
                TimeSpan.FromSeconds(10));
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
        if (_simConnect == null)
        {
            AppendDashboardLog("Crew oxygen blocked: simulator connection is unavailable.");
            FinishOneShot(3);
            return;
        }

        var plan = FbwA320CrewOxygenAdapter.CreatePlan(
            _state,
            desiredOn,
            _fbwCrewOxygenTyped,
            _fbwCrewOxygen);
        if (plan.Kind == FbwA320CrewOxygenCommandPlanKind.Blocked)
        {
            AppendDashboardLog(plan.Message!);
            FinishOneShot(plan.ExitCode);
            return;
        }

        if (plan.Kind == FbwA320CrewOxygenCommandPlanKind.AlreadySet)
        {
            AppendDashboardLog(plan.Message!);
            FinishOneShot();
            return;
        }

        try
        {
            _simConnect.SetInputEvent(plan.InputEventHash, plan.RawState);
        }
        catch (COMException ex)
        {
            AppLog.Write($"FBW crew oxygen SetInputEvent failed; falling back to calculator commands: {ex.Message}");
        }

        foreach (var command in plan.MobiFlightCommands)
        {
            SendMobiFlightCommand(command);
        }
        _fbwCommandedCrewOxygen = desiredOn;
        _fbwCommandedCrewOxygenUtc = DateTime.UtcNow;
        AppLog.Write(
            $"Executed FBW A320 crew oxygen command: AIRLINER_OXY_CREW/PUSH_OVHD_OXYGEN_CREW={plan.RawState}");
        BeginNativeAction(
            "Crew oxygen",
            state => state.CrewOxygenOn == desiredOn,
            desiredOn,
            TimeSpan.FromSeconds(10));
    }

    private void SetStrobeSelector(int desiredPosition)
    {
        if (_state?.IsFlyByWireAirbus == true)
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
        if (_state!.IsIniBuildsA330)
        {
            _simConnect!.SetInputEvent(A330StrobeInputEventHash, (double)desiredPosition);
            SendMobiFlightCommand($"MF.SimVars.Set.(>B:AIRLINER_STROBE_TOGGLE_{desiredPosition})");
            SendMobiFlightCommand("MF.DummyCmd");
            AppLog.Write(
                $"A330 strobe command sent: {FormatStrobePosition(desiredPosition)}.");
        }
        else
        {
            _simConnect!.SetInputEvent(8986586253276960537UL, (double)desiredPosition);
        }
        BeginNativeAction(
            "Strobe selector",
            state => state.StrobeSelectorPosition.HasValue
                     && Math.Abs(state.StrobeSelectorPosition.Value - desiredPosition) < 0.1,
            desiredPosition != 2,
            TimeSpan.FromSeconds(10),
            logProgressToDashboard: !_state.IsIniBuildsA330);
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
        if (_state?.IsFlyByWireAirbus == true)
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
        var state = _state;
        if (state?.IsIniBuildsA330 == true)
        {
            StartIniBuildsA330FireTest(system, inputEventHash, name);
            return;
        }

        SetFireTestPressed(system, inputEventHash, true);
        _pendingFireTest = new PendingFireTest(
            system,
            inputEventHash,
            DateTime.UtcNow.AddSeconds(10));
        AppendDashboardLog($"{name} button held; awaiting active test readback.");
    }

    private void StartIniBuildsA330FireTest(
        FireTestSystem system,
        ulong inputEventHash,
        string name)
    {
        SetFireTestPressed(system, inputEventHash, true);
        AppendDashboardLog($"{name} button held for A330 fire test.");

        var releaseTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        releaseTimer.Tick += (_, _) =>
        {
            releaseTimer.Stop();
            SetFireTestPressed(system, inputEventHash, false);
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

            _nativePulseTimers.Remove(releaseTimer);
            releaseTimer.Dispose();
            AppendDashboardLog($"{name} completed and released safely.");
            FinishOneShot();
        };
        _nativePulseTimers.Add(releaseTimer);
        releaseTimer.Start();
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
        if (_state?.IsFlyByWireAirbus == true)
        {
            SetFlyByWireSignSelector(selector, desiredPosition);
            return;
        }
        if (_state?.IsIniBuildsA321Lr == true)
        {
            SetA321SignSelector(selector, desiredPosition);
            return;
        }
        if (_state?.IsIniBuildsA330 == true)
        {
            SetA330SignSelector(selector, desiredPosition);
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
            if (selector == SignSelector.Seatbelts)
            {
                return desiredPosition == 2
                    ? !state.SeatbeltSignsOn
                    : state.SeatbeltSignsOn;
            }

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

    private void SetA321SignSelector(SignSelector selector, int desiredPosition)
    {
        if (_simConnect == null || _state == null)
        {
            AppendDashboardLog(
                $"{FormatSignSelectorName(selector)} blocked: simulator state is unavailable.");
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

        bool Verify(AircraftState state) =>
            A321ControlProfile.SignSelectorAtPosition(
                ReadPosition(state),
                desiredPosition);

        if (Verify(_state))
        {
            AppendDashboardLog(
                $"{FormatSignSelectorName(selector)} already " +
                $"{FormatSignSelectorPosition(selector, desiredPosition)}.");
            FinishOneShot();
            return;
        }

        _simConnect.SetInputEvent(
            A321ControlProfile.GetSignInputEventHash((int)selector),
            (double)desiredPosition);
        BeginNativeAction(
            FormatSignSelectorName(selector),
            Verify,
            desiredPosition != 2,
            TimeSpan.FromSeconds(10));
    }

    private void SetA330SignSelector(SignSelector selector, int desiredPosition)
    {
        if (_simConnect == null || _state == null)
        {
            AppendDashboardLog($"{FormatSignSelectorName(selector)} blocked: simulator state is unavailable.");
            FinishOneShot(4);
            return;
        }
        if (!A330SignInputEventsReady())
        {
            AppendDashboardLog($"{FormatSignSelectorName(selector)} blocked: A330 sign InputEvent readback is not ready yet.");
            FinishOneShot(4);
            return;
        }

        var index = selector switch
        {
            SignSelector.Seatbelts => 0,
            SignSelector.NoSmoking => 1,
            SignSelector.EmergencyExit => 2,
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
            return position.HasValue && Math.Abs(position.Value - desiredPosition) < 0.1;
        }

        if (Verify(_state))
        {
            AppendDashboardLog(
                $"{FormatSignSelectorName(selector)} already " +
                $"{FormatSignSelectorPosition(selector, desiredPosition)}.");
            FinishOneShot();
            return;
        }

        _simConnect.SetInputEvent(A330SignInputEventHashes[index], (double)desiredPosition);
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
            if (selector == SignSelector.Seatbelts)
            {
                return desiredPosition == 2
                    ? !state.SeatbeltSignsOn
                    : state.SeatbeltSignsOn;
            }

            var position = ReadPosition(state);
            if (!position.HasValue || Math.Abs(position.Value - desiredPosition) >= 0.1)
            {
                return false;
            }

            return selector switch
            {
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
                var desiredSeatbeltsOn = desiredPosition != 2;
                if (_state.SeatbeltSignsOn != desiredSeatbeltsOn)
                {
                    SendMobiFlightCommand(
                        desiredSeatbeltsOn
                            ? "MF.SimVars.Set.(A:CABIN SEATBELTS ALERT SWITCH,bool) 0 == if{ 1 (>K:CABIN_SEATBELTS_ALERT_SWITCH_TOGGLE) }"
                            : "MF.SimVars.Set.(A:CABIN SEATBELTS ALERT SWITCH,bool) 0 != if{ 0 (>K:CABIN_SEATBELTS_ALERT_SWITCH_TOGGLE) }");
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
        if (_state?.IsFlyByWireAirbus == true)
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

        if (_state!.IsIniBuildsA330)
        {
            _simConnect!.SetInputEvent(
                A330TransponderModeInputEventHash,
                (double)desiredPosition);
        }
        else
        {
            var a330StateEvent = desiredPosition switch
            {
                0 => "AIRLINER_TCAS_MODE_State1",
                1 => "AIRLINER_TCAS_MODE_State2",
                2 => "AIRLINER_TCAS_MODE_State3",
                _ => throw new ArgumentOutOfRangeException(nameof(desiredPosition))
            };
            SendMobiFlightCommand($"MF.SimVars.Set.(>B:{a330StateEvent})");
            SendMobiFlightCommand("MF.DummyCmd");
        }
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
        if (_state?.IsFlyByWireAirbus == true)
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

        if (_state?.IsIniBuildsA330 == true)
        {
            if (_simConnect == null || !_a330TcasTrafficInputState.HasValue)
            {
                AppendDashboardLog("TCAS traffic mode blocked: A330 readback is unavailable.");
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

            var a330TcasStateEvent = desiredPosition switch
            {
                0 => "AIRLINER_TCAS_STBY_0",
                1 => "AIRLINER_TCAS_STBY_1",
                2 => "AIRLINER_TCAS_STBY_2",
                _ => throw new ArgumentOutOfRangeException(nameof(desiredPosition))
            };
            SendMobiFlightCommand($"MF.SimVars.Set.(>B:{a330TcasStateEvent})");
            SendMobiFlightCommand("MF.DummyCmd");
            BeginNativeAction(
                "TCAS traffic mode",
                state => state.TcasMode.HasValue
                         && Math.Abs(state.TcasMode.Value - desiredPosition) < 0.1,
                desiredPosition != 0,
                TimeSpan.FromSeconds(10),
                desiredPosition switch
                {
                    0 => "STBY",
                    1 => "TA",
                    _ => "TA/RA"
                });
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
        if (_state?.IsFlyByWireAirbus == true)
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

        if (_state?.IsIniBuildsA330 == true)
        {
            if (_simConnect == null || !_a330TcasAltitudeInputState.HasValue)
            {
                AppendDashboardLog("TCAS altitude reporting blocked: A330 readback is unavailable.");
                FinishOneShot(4);
                return;
            }
            if (_state.TcasAltitudeReportingOn == desiredOn)
            {
                AppendDashboardLog($"TCAS altitude reporting already {desiredOn.ToOnOff()}.");
                FinishOneShot();
                return;
            }

            _simConnect.SetInputEvent(
                A330TcasAltitudeInputEventHash,
                desiredOn ? 1.0 : 0.0);
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

        SendMobiFlightCommand(_state.IsFlyByWireAirbus
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

        SendMobiFlightCommand(_state.IsFlyByWireAirbus
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
        if (_state.IsFlyByWireAirbus)
        {
            SendMobiFlightCommand("MF.SimVars.Set.0 (>K:SPOILERS_ARM_SET)");
            _fbwCommandedSpoilersArmed = false;
            _fbwCommandedSpoilersArmedUtc = DateTime.UtcNow;
        }
        else if (_state.IsIniBuildsA330)
        {
            SendMobiFlightCommand("MF.SimVars.Set.0 (>K:SPOILERS_ARM_SET)");
            _a330CommandedSpoilersArmed = false;
            _state.GroundSpoilersArmed = false;
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
        if (_state?.IsFlyByWireAirbus == true)
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

        if (_state?.IsIniBuildsA330 == true)
        {
            if (_simConnect == null || !_a330WeatherRadarPwsInputState.HasValue)
            {
                AppendDashboardLog("WXR/PWS selector blocked: A330 readback is unavailable.");
                FinishOneShot(4);
                return;
            }
            if (desiredPosition is not (0 or 1))
            {
                AppendDashboardLog("WXR/PWS selector blocked: A330 supports OFF or AUTO.");
                FinishOneShot(4);
                return;
            }
            if (_state.WeatherRadarPwsSelectorPosition.HasValue
                && Math.Abs(_state.WeatherRadarPwsSelectorPosition.Value - desiredPosition) < 0.1)
            {
                AppendDashboardLog($"WXR/PWS selector already at position {desiredPosition}.");
                FinishOneShot();
                return;
            }

            SendMobiFlightCommand("MF.SimVars.Set.(>B:AIRLINER_WX_PWS_Toggle)");
            SendMobiFlightCommand("MF.DummyCmd");
            BeginNativeAction(
                "WXR/PWS selector",
                state => state.WeatherRadarPwsSelectorPosition.HasValue
                         && Math.Abs(
                             state.WeatherRadarPwsSelectorPosition.Value
                             - desiredPosition) < 0.1,
                desiredPosition == 1,
                TimeSpan.FromSeconds(10),
                desiredPosition == 1 ? "AUTO" : "OFF");
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
        if (_state?.IsFlyByWireAirbus == true)
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
                0 => "0 (>L:LIGHTING_LANDING_1) (A:CIRCUIT SWITCH ON:20, Bool) ! if{ 20 (>K:ELECTRICAL_CIRCUIT_TOGGLE) } (A:CIRCUIT SWITCH ON:17, Bool) ! if{ 17 (>K:ELECTRICAL_CIRCUIT_TOGGLE) }",
                1 => "1 (>L:LIGHTING_LANDING_1) (A:CIRCUIT SWITCH ON:17, Bool) if{ 17 (>K:ELECTRICAL_CIRCUIT_TOGGLE) } (A:CIRCUIT SWITCH ON:20, Bool) ! if{ 20 (>K:ELECTRICAL_CIRCUIT_TOGGLE) }",
                2 => "2 (>L:LIGHTING_LANDING_1) (A:CIRCUIT SWITCH ON:17, Bool) if{ 17 (>K:ELECTRICAL_CIRCUIT_TOGGLE) } (A:CIRCUIT SWITCH ON:20, Bool) if{ 20 (>K:ELECTRICAL_CIRCUIT_TOGGLE) }",
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

        if (_state?.IsIniBuildsA330 == true)
        {
            if (_simConnect == null || !_a330NoseLightInputState.HasValue)
            {
                AppendDashboardLog("Nose light selector blocked: A330 readback is unavailable.");
                FinishOneShot(4);
                return;
            }
            if (_state.NoseLightSelectorPosition.HasValue
                && Math.Abs(_state.NoseLightSelectorPosition.Value - desiredPosition) < 0.1)
            {
                AppendDashboardLog($"Nose light already at position {desiredPosition}.");
                FinishOneShot();
                return;
            }

            var a330NoseLightStateEvent = desiredPosition switch
            {
                0 => "AIRLINER_TAXILIGHT_TOGGLE_0",
                1 => "AIRLINER_TAXILIGHT_TOGGLE_1",
                2 => "AIRLINER_TAXILIGHT_TOGGLE_2",
                _ => throw new ArgumentOutOfRangeException(nameof(desiredPosition))
            };
            SendMobiFlightCommand($"MF.SimVars.Set.(>B:{a330NoseLightStateEvent})");
            SendMobiFlightCommand("MF.DummyCmd");
            BeginNativeAction(
                "Nose light selector",
                state => state.NoseLightSelectorPosition.HasValue
                         && Math.Abs(
                             state.NoseLightSelectorPosition.Value
                             - desiredPosition) < 0.1,
                desiredPosition != 2,
                TimeSpan.FromSeconds(10),
                desiredPosition switch
                {
                    0 => "T.O.",
                    1 => "TAXI",
                    _ => "OFF"
                });
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
        if (_state?.IsFlyByWireAirbus == true)
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
                && !_state.SeatbeltSignsOn
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
            || !_state.SeatbeltSignsOn
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
        if (_state?.IsFlyByWireAirbus == true)
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

        if (_state.IsFlyByWireAirbus)
        {
            SendMobiFlightCommand("MF.SimVars.Set.1 (>K:SPOILERS_ARM_SET)");
            _fbwCommandedSpoilersArmed = true;
            _fbwCommandedSpoilersArmedUtc = DateTime.UtcNow;
        }
        else if (_state.IsIniBuildsA330)
        {
            SendMobiFlightCommand("MF.SimVars.Set.1 (>K:SPOILERS_ARM_SET)");
            _a330CommandedSpoilersArmed = true;
            _state.GroundSpoilersArmed = true;
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

        if (_state.IsIniBuildsA321Lr)
        {
            SendMobiFlightCommand(A321ControlProfile.BuildTakeoffFlapsCommand());
            SendMobiFlightCommand("MF.DummyCmd");
        }
        else if (_state.IsIniBuildsA330)
        {
            SendMobiFlightCommand(
                "MF.SimVars.Set.16384 (A:FLAPS NUM HANDLE POSITIONS, Number) / " +
                "(>B:AIRLINER_Flaps_Inc)");
            SendMobiFlightCommand("MF.DummyCmd");
        }
        else
        {
            SendMobiFlightCommand(
                "MF.SimVars.Set.16384 (A:FLAPS NUM HANDLE POSITIONS, Number) / " +
                "(>B:HANDLING_Flaps_Inc)");
            SendMobiFlightCommand("MF.DummyCmd");
        }
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

        if (_state.IsFlyByWireAirbus)
        {
            SendMobiFlightCommand(
                $"MF.SimVars.Set.{desiredPosition} (>L:A32NX_FLAPS_HANDLE_INDEX)");
        }
        else if (_state.IsIniBuildsA321Lr)
        {
            SendMobiFlightCommand(A321ControlProfile.BuildFlapsExtensionCommand());
        }
        else if (_state.IsIniBuildsA330)
        {
            var currentPosition = Math.Max(0, (int)Math.Round(_state.FlapsHandleIndex));
            var stepCount = Math.Abs((int)desiredPosition - currentPosition);
            var directionEvent = desiredPosition >= currentPosition
                ? "AIRLINER_Flaps_Inc"
                : "AIRLINER_Flaps_Dec";
            var stepCode = string.Join(
                " ",
                Enumerable.Repeat(
                    $"16384 (A:FLAPS NUM HANDLE POSITIONS, Number) / (>B:{directionEvent})",
                    Math.Max(1, stepCount)));
            SendMobiFlightCommand($"MF.SimVars.Set.{stepCode}");
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

        if (_state.IsFlyByWireAirbus)
        {
            SendMobiFlightCommand(
                "MF.SimVars.Set.0 (>L:A32NX_FLAPS_HANDLE_INDEX)");
        }
        else if (_state.IsIniBuildsA321Lr)
        {
            SendMobiFlightCommand(
                A321ControlProfile.BuildFlapsCleanCommand(_state.OnGround));
        }
        else if (_state.IsIniBuildsA330)
        {
            var stepCount = Math.Max(1, (int)Math.Ceiling(_state.FlapsHandleIndex));
            var stepCode = string.Join(
                " ",
                Enumerable.Repeat(
                    "16384 (A:FLAPS NUM HANDLE POSITIONS, Number) / (>B:AIRLINER_Flaps_Dec)",
                    stepCount));
            SendMobiFlightCommand($"MF.SimVars.Set.{stepCode}");
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

        if (_state.IsIniBuildsA330)
        {
            var selectedLevel = desiredLevel;
            if (desiredLevel == 0)
            {
                selectedLevel = (int)Math.Round(ResolveA330AutobrakeLevel() ?? 0);
                if (selectedLevel == 0)
                {
                    FinishOneShot();
                    return;
                }
            }

            var toggleEvent = selectedLevel switch
            {
                1 => "AIRLINER_AUTOBRK_LO_Toggle",
                2 => "AIRLINER_AUTOBRK_MED_Toggle",
                3 => "AIRLINER_AUTOBRK_HI_Toggle",
                _ => throw new ArgumentOutOfRangeException(nameof(desiredLevel))
            };
            SendMobiFlightCommand($"MF.SimVars.Set.(>B:{toggleEvent})");
        }
        else if (_state.IsFlyByWireAirbus)
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
        if (!_state.IsIniBuildsAirbusFamily)
        {
            AppendDashboardLog($"{name} blocked: the loaded aircraft is not a supported iniBuilds Airbus aircraft.");
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
        string? desiredLabel = null,
        bool logProgressToDashboard = true)
    {
        _pendingNativeAction = new PendingNativeAction(
            name,
            verify,
            desiredOn,
            desiredLabel ?? desiredOn.ToOnOff(),
            DateTime.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(8)),
            logProgressToDashboard);
        var message =
            $"{name} command sent: {_pendingNativeAction.DesiredLabel}; awaiting native readback.";
        if (logProgressToDashboard)
        {
            AppendDashboardLog(message);
        }
        else
        {
            AppLog.Write(message);
        }
    }

    private void SetExternalPower(bool desiredOn)
    {
        if (_simConnect == null || _state == null)
        {
            Console.Error.WriteLine("External-power procedure blocked: aircraft state is unavailable.");
            FinishOneShot(3);
            return;
        }

        if (_state.IsFlyByWireAirbus)
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
                state => state.ExternalPowerOn == desiredOn,
                alternateLVarName: "A32NX_OVHD_ELEC_EXT_PWR_1_PB_IS_ON",
                additionalAlternateLVarNames: new[]
                {
                    "A32NX_OVHD_ELEC_EXT_PWR_2_PB_IS_ON",
                    "A32NX_OVHD_ELEC_EXT_PWR_3_PB_IS_ON",
                    "A32NX_OVHD_ELEC_EXT_PWR_4_PB_IS_ON"
                });
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

        TransmitExternalPowerCommand(1, desiredOn);
        if (_state.IsIniBuildsA330)
        {
            TransmitExternalPowerCommand(2, desiredOn);
            AppendDashboardLog(
                $"A330 external power command sent: EXT A and EXT B {desiredOn.ToOnOff()}.");
        }

        _pendingProcedure = new PendingExternalPowerProcedure(
            desiredOn,
            DateTime.UtcNow.AddSeconds(5));
        Console.WriteLine($"External power command sent: {(desiredOn ? "ON" : "OFF")}; awaiting readback.");
    }

    private void TransmitExternalPowerCommand(uint index, bool desiredOn)
    {
        _simConnect!.TransmitClientEvent_EX1(
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            CopilotEvent.SetExternalPower,
            Priority.Highest,
            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY,
            index,
            desiredOn ? 1u : 0u,
            0,
            0,
            0);
    }

    private static string? ValidateExternalPowerProcedure(AircraftState state, bool desiredOn)
    {
        if (!state.IsIniBuildsAirbusFamily)
        {
            return "the loaded aircraft is not a supported iniBuilds Airbus aircraft.";
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

        if (_state.IsFlyByWireAirbus)
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
            var message =
                $"{_pendingNativeAction.Name} verified {_pendingNativeAction.DesiredLabel}.";
            if (_pendingNativeAction.LogProgressToDashboard)
            {
                AppendDashboardLog(message);
            }
            else
            {
                AppLog.Write(message);
            }
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
            $"Engine start 1 - starter/N1/EGT/fuel: " +
            $"{_state.Engine1StarterActive.ToOnOff()}/{_state.Engine1N1Percent:F1}%/" +
            $"{_state.Engine1EgtCelsius:F0}C/{_state.Engine1FuelFlowPph:F0} pph");
        Console.WriteLine(
            $"Engine start 2 - starter/N1/EGT/fuel: " +
            $"{_state.Engine2StarterActive.ToOnOff()}/{_state.Engine2N1Percent:F1}%/" +
            $"{_state.Engine2EgtCelsius:F0}C/{_state.Engine2FuelFlowPph:F0} pph");
        Console.WriteLine($"Batteries 1/2: {_state.Battery1On.ToOnOff()}/{_state.Battery2On.ToOnOff()}");
        Console.WriteLine($"External power: {FormatExternalPowerSummary(_state)}");
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
            $"After-start configuration - spoilers/flaps/autobrake: " +
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

        Console.WriteLine("Cockpit preparation - electrical power");
        foreach (var step in CockpitPreparationProcedure.Evaluate(_state))
        {
            PrintChecklistItem(step.Label, step.Complete, step.ActionHint);
        }
    }

    private static void PrintChecklistItem(string label, bool complete, string? note = null)
    {
        Console.WriteLine($"[{(complete ? "x" : " ")}] {label}{(note == null || complete ? "" : $" - {note}")}");
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
            $"  Detected FBW Airbus: {_state.IsFlyByWireAirbus.ToYesNo()}",
            $"  Detected A32NX/A380X: {_state.IsFlyByWireA320Neo.ToYesNo()}/{_state.IsFlyByWireA380X.ToYesNo()}",
            $"  App BAT 1/2: {_state.Battery1On.ToOnOff()}/{_state.Battery2On.ToOnOff()}",
            $"  FBW BAT 1 AUTO untyped/typed/commanded: {FormatOptionalBool(_fbwBattery1Auto)}/{FormatOptionalBool(_fbwBattery1AutoTyped)}/{FormatOptionalBool(_fbwCommandedBattery1Auto)}",
            $"  FBW BAT 2 AUTO untyped/typed/commanded: {FormatOptionalBool(_fbwBattery2Auto)}/{FormatOptionalBool(_fbwBattery2AutoTyped)}/{FormatOptionalBool(_fbwCommandedBattery2Auto)}",
            $"  FBW BAT potential 1/2: {FormatOptionalFloat(_fbwBattery1Potential, "F1")}/{FormatOptionalFloat(_fbwBattery2Potential, "F1")} V",
            $"  App EXT PWR available/on: {_state.ExternalPowerAvailable.ToYesNo()}/{_state.ExternalPowerOn.ToOnOff()}",
            $"  FBW EXT PWR available untyped/typed: {FormatOptionalBool(_fbwExternalPowerAvailable)}/{FormatOptionalBool(_fbwExternalPowerAvailableTyped)}",
            $"  FBW EXT PWR ON untyped/typed: {FormatOptionalBool(_fbwExternalPowerOn)}/{FormatOptionalBool(_fbwExternalPowerOnTyped)}",
            $"  FBW A380 EXT PWR available 1/2/3/4: {FormatOptionalBool(_fbwA380ExternalPower1AvailableTyped)}/{FormatOptionalBool(_fbwA380ExternalPower2AvailableTyped)}/{FormatOptionalBool(_fbwA380ExternalPower3AvailableTyped)}/{FormatOptionalBool(_fbwA380ExternalPower4AvailableTyped)}",
            $"  FBW A380 EXT PWR ON 1/2/3/4: {FormatOptionalBool(_fbwA380ExternalPower1OnTyped)}/{FormatOptionalBool(_fbwA380ExternalPower2OnTyped)}/{FormatOptionalBool(_fbwA380ExternalPower3OnTyped)}/{FormatOptionalBool(_fbwA380ExternalPower4OnTyped)}",
            $"  FBW A380 direct EXT PWR available 1/2/3/4: {_state.FbwA380ExternalPower1Available.ToYesNo()}/{_state.FbwA380ExternalPower2Available.ToYesNo()}/{_state.FbwA380ExternalPower3Available.ToYesNo()}/{_state.FbwA380ExternalPower4Available.ToYesNo()}",
            $"  FBW A380 direct EXT PWR ON 1/2/3/4: {_state.FbwA380ExternalPower1On.ToOnOff()}/{_state.FbwA380ExternalPower2On.ToOnOff()}/{_state.FbwA380ExternalPower3On.ToOnOff()}/{_state.FbwA380ExternalPower4On.ToOnOff()}",
            $"  FBW A380 AC buses powered 1/2/3/4: {_state.FbwA380AcBus1Powered.ToYesNo()}/{_state.FbwA380AcBus2Powered.ToYesNo()}/{_state.FbwA380AcBus3Powered.ToYesNo()}/{_state.FbwA380AcBus4Powered.ToYesNo()}",
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
            Text = "MSFS 2024 Virtual First Officer",
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

        var statusShell = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 14)
        };
        statusShell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusShell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        root.Controls.Add(statusShell);

        var statusPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 12, 0)
        };
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusShell.Controls.Add(statusPanel, 0, 0);
        statusShell.Controls.Add(BuildAircraftCard(), 1, 0);

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
            $"{GetApplicationVersion()} - checking GitHub releases...");

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

        _simBriefImportButton = new Button
        {
            Text = SimBriefButtonText(),
            AutoSize = true,
            Margin = new Padding(10, 2, 0, 0)
        };
        _simBriefImportButton.Click += (_, _) => ShowSimBriefDialog();
        settingsPanel.Controls.Add(_simBriefImportButton);

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
                && _takeoffRotateBox.Value < _takeoffV1Box.Value)
            {
                _takeoffRotateBox.Value = Math.Min(
                    _takeoffRotateBox.Maximum,
                    _takeoffV1Box.Value);
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
                _settings.TakeoffV1SpeedKnots,
                Math.Min(220, _settings.TakeoffRotateSpeedKnots)),
            Width = 64
        };
        _takeoffRotateBox.ValueChanged += (_, _) =>
        {
            if (_takeoffV1Box != null
                && _takeoffRotateBox.Value < _takeoffV1Box.Value)
            {
                _takeoffRotateBox.Value = Math.Min(
                    _takeoffRotateBox.Maximum,
                    _takeoffV1Box.Value);
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
            Text = "Checklist and assistance flow - gate to gate",
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
        var startPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 1,
            Margin = new Padding(12, 0, 0, 0)
        };
        startPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _startSelectedFlowButton = new Button
        {
            Text = "Start selected flow",
            Width = 190,
            Height = 58,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(0, 4, 0, 6),
            BackColor = System.Drawing.Color.FromArgb(39, 130, 87),
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new System.Drawing.Font(
                Font.FontFamily,
                10,
                System.Drawing.FontStyle.Bold),
            UseVisualStyleBackColor = false
        };
        _startSelectedFlowButton.FlatAppearance.BorderSize = 0;
        _startSelectedFlowButton.FlatAppearance.MouseDownBackColor =
            System.Drawing.Color.FromArgb(22, 101, 52);
        _startSelectedFlowButton.FlatAppearance.MouseOverBackColor =
            System.Drawing.Color.FromArgb(34, 148, 96);
        _startSelectedFlowButton.Click += (_, _) =>
        {
            if (IsProcedureActive(_procedureRunner.Status))
            {
                return;
            }

            if (_flowList.SelectedItem is ProcedureListItem item)
            {
                _commands.Enqueue($"procedure start {item.Definition.Id}");
            }
        };
        startPanel.Controls.Add(_startSelectedFlowButton, 0, 0);
        timelineLayout.Controls.Add(startPanel, 1, 0);
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
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 6, 0, 0)
        };
        procedureButtons.Controls.Add(NewProcedureButton(
            "Start first flow",
            "procedure start power-up-initial-setup",
            132,
            System.Drawing.Color.FromArgb(39, 130, 87),
            System.Drawing.Color.FromArgb(22, 101, 52),
            System.Drawing.Color.FromArgb(34, 148, 96),
            emphasize: true));
        _confirmCompletedButton = NewProcedureButton(
            "Confirm completed",
            "procedure confirm",
            150,
            System.Drawing.Color.FromArgb(39, 130, 87),
            System.Drawing.Color.FromArgb(22, 101, 52),
            System.Drawing.Color.FromArgb(34, 148, 96),
            emphasize: true);
        procedureButtons.Controls.Add(_confirmCompletedButton);
        procedureButtons.Controls.Add(NewProcedureButton("Pause", "procedure pause"));
        procedureButtons.Controls.Add(NewProcedureButton("Resume", "procedure resume"));
        procedureButtons.Controls.Add(NewProcedureButton("Cancel", "procedure cancel"));
        var resetProgressButton = new Button
        {
            Text = "New flight / Reset progress",
            Width = 170,
            Height = 34,
            AutoSize = false,
            Margin = new Padding(4, 3, 4, 3),
            FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.FromArgb(243, 244, 246),
            ForeColor = System.Drawing.Color.FromArgb(31, 41, 55),
            UseVisualStyleBackColor = false
        };
        resetProgressButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(209, 213, 219);
        resetProgressButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(209, 213, 219);
        resetProgressButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(229, 231, 235);
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

    private string SimBriefButtonText() => _simBriefFlightPlan == null
        ? "SimBrief"
        : $"SimBrief: {_simBriefFlightPlan.RouteLabel}";

    private void ShowSimBriefDialog()
    {
        using var dialog = new Form
        {
            Text = "SimBrief flight plan",
            Width = 560,
            Height = 390,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 7
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        dialog.Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            Text = "Free, read-only import of your latest generated SimBrief OFP. No API key or subscription is required.",
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(500, 0),
            Margin = new Padding(0, 0, 0, 14)
        }, 0, 0);
        layout.SetColumnSpan(layout.GetControlFromPosition(0, 0), 2);

        layout.Controls.Add(new Label { Text = "Pilot ID (preferred)", AutoSize = true, Margin = new Padding(0, 7, 8, 0) }, 0, 1);
        var pilotIdBox = new TextBox { Text = _settings.SimBriefPilotId, Dock = DockStyle.Top };
        layout.Controls.Add(pilotIdBox, 1, 1);
        layout.Controls.Add(new Label { Text = "Username", AutoSize = true, Margin = new Padding(0, 7, 8, 0) }, 0, 2);
        var usernameBox = new TextBox { Text = _settings.SimBriefUsername, Dock = DockStyle.Top };
        layout.Controls.Add(usernameBox, 1, 2);

        var autoImportBox = new CheckBox
        {
            Text = "Import latest OFP when starting a new flight",
            Checked = _settings.SimBriefAutoImportOnNewFlight,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 8)
        };
        layout.Controls.Add(autoImportBox, 1, 3);

        var summary = new Label
        {
            Text = SimBriefSummary(_simBriefFlightPlan),
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(500, 0),
            Margin = new Padding(0, 10, 0, 12)
        };
        layout.Controls.Add(summary, 0, 4);
        layout.SetColumnSpan(summary, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight
        };
        var importButton = new Button
        {
            Text = "Import latest flight",
            AutoSize = true,
            BackColor = System.Drawing.Color.FromArgb(39, 130, 87),
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false
        };
        var saveButton = new Button { Text = "Save settings", AutoSize = true };
        var closeButton = new Button { Text = "Close", AutoSize = true };
        Action saveSettings = () =>
        {
            _settings.SimBriefPilotId = pilotIdBox.Text.Trim();
            _settings.SimBriefUsername = usernameBox.Text.Trim();
            _settings.SimBriefAutoImportOnNewFlight = autoImportBox.Checked;
            SettingsStore.Save(_settings);
        };
        saveButton.Click += (_, _) =>
        {
            saveSettings();
            summary.Text = "SimBrief settings saved. " + SimBriefSummary(_simBriefFlightPlan);
        };
        importButton.Click += async (_, _) =>
        {
            saveSettings();
            importButton.Enabled = false;
            importButton.Text = "Importing...";
            await ImportLatestSimBriefAsync(showReview: true, automatic: false);
            summary.Text = SimBriefSummary(_simBriefFlightPlan);
            importButton.Text = "Import latest flight";
            importButton.Enabled = true;
        };
        closeButton.Click += (_, _) => dialog.Close();
        buttons.Controls.Add(importButton);
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(closeButton);
        layout.Controls.Add(buttons, 0, 5);
        layout.SetColumnSpan(buttons, 2);
        dialog.AcceptButton = importButton;
        dialog.CancelButton = closeButton;
        dialog.ShowDialog(this);
    }

    private async Task ImportLatestSimBriefAsync(bool showReview, bool automatic)
    {
        if (_simBriefImportInProgress) return;
        if (IsProcedureActive(_procedureRunner.Status))
        {
            if (!automatic)
            {
                MessageBox.Show(this,
                    "Finish, pause, or cancel the active procedure before reviewing a new SimBrief flight.",
                    "SimBrief import",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            return;
        }

        _simBriefImportInProgress = true;
        try
        {
            var plan = await new SimBriefClient().FetchLatestAsync(
                _settings.SimBriefPilotId,
                _settings.SimBriefUsername);
            _simBriefFlightPlan = plan;
            SimBriefCacheStore.Save(plan);
            if (_simBriefImportButton != null) _simBriefImportButton.Text = SimBriefButtonText();
            AppendDashboardLog($"SimBrief imported: {plan.RouteLabel} {plan.FlightNumber}".Trim());
            if (showReview) ReviewAndApplySimBrief(plan);
        }
        catch (Exception ex)
        {
            AppLog.Write($"SimBrief import failed: {ex}");
            if (!automatic)
            {
                MessageBox.Show(this,
                    $"The SimBrief flight could not be imported.\n\n{ex.Message}\n\nYour existing settings and cockpit flows were not changed.",
                    "SimBrief unavailable",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            AppendDashboardLog("SimBrief import unavailable; existing flight settings kept.");
        }
        finally
        {
            _simBriefImportInProgress = false;
        }
    }

    private void ReviewAndApplySimBrief(ImportedFlightPlan plan)
    {
        var warnings = SimBriefImportValidator.Validate(
            plan,
            ExpectedSimBriefAircraftIcaos(),
            DateTime.UtcNow);
        var changes = new List<string>();
        if (plan.TransitionAltitudeFeet is >= 1000 and <= 20000)
            changes.Add($"Transition altitude: {plan.TransitionAltitudeFeet:N0} ft");
        if (plan.TakeoffV1Knots is >= 80 and <= 219)
            changes.Add($"V1: {plan.TakeoffV1Knots} kt");
        if (plan.TakeoffVrKnots is >= 80 and <= 220)
            changes.Add($"VR: {plan.TakeoffVrKnots} kt");

        var message = SimBriefSummary(plan)
            + (warnings.Count > 0 ? "\n\nWarnings:\n- " + string.Join("\n- ", warnings) : "")
            + (changes.Count > 0
                ? "\n\nApply these reviewed values to the app?\n- " + string.Join("\n- ", changes)
                : "\n\nThis OFP contains no supported takeoff values to apply. It will remain available as a flight summary.");
        if (changes.Count == 0)
        {
            MessageBox.Show(this, message, "SimBrief flight imported", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show(this, message, "Review SimBrief import", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        if (plan.TransitionAltitudeFeet is >= 1000 and <= 20000)
            _settings.TransitionAltitudeFeet = plan.TransitionAltitudeFeet.Value;
        if (plan.TakeoffV1Knots is >= 80 and <= 219)
            _settings.TakeoffV1SpeedKnots = plan.TakeoffV1Knots.Value;
        if (plan.TakeoffVrKnots is >= 80 and <= 220)
            _settings.TakeoffRotateSpeedKnots = Math.Max(_settings.TakeoffV1SpeedKnots, plan.TakeoffVrKnots.Value);
        SettingsStore.Save(_settings);

        if (_transitionAltitudeBox != null) _transitionAltitudeBox.Value = _settings.TransitionAltitudeFeet;
        if (_takeoffV1Box != null) _takeoffV1Box.Value = _settings.TakeoffV1SpeedKnots;
        if (_takeoffRotateBox != null) _takeoffRotateBox.Value = _settings.TakeoffRotateSpeedKnots;
        if (_state != null)
        {
            _state.TransitionAltitudeFeet = _settings.TransitionAltitudeFeet;
            _state.TakeoffV1SpeedKnots = _settings.TakeoffV1SpeedKnots;
            _state.TakeoffRotateSpeedKnots = _settings.TakeoffRotateSpeedKnots;
        }
        AppendDashboardLog("Reviewed SimBrief takeoff settings applied.");
    }

    private IReadOnlyList<string> ExpectedSimBriefAircraftIcaos()
    {
        if (_state?.IsPmdg737800 == true) return new[] { "B738" };
        if (_state?.IsIniBuildsA321Lr == true) return new[] { "A21N", "A321" };
        if (_state?.IsIniBuildsA330 == true) return new[] { "A333", "A339" };
        if (_state?.IsA320NeoV2 == true || _state?.IsFlyByWireA320Neo == true)
            return new[] { "A20N" };
        return Array.Empty<string>();
    }

    private static string SimBriefSummary(ImportedFlightPlan? plan)
    {
        if (plan == null) return "No SimBrief flight has been imported yet.";
        var generated = plan.GeneratedUtc.HasValue
            ? $"generated {plan.GeneratedUtc.Value.ToLocalTime():g}"
            : "generation time unavailable";
        var cruise = plan.CruiseAltitudeFeet.HasValue ? $"FL{plan.CruiseAltitudeFeet.Value / 100:000}" : "cruise n/a";
        var runways = $"{(string.IsNullOrWhiteSpace(plan.OriginRunway) ? "--" : plan.OriginRunway)} -> {(string.IsNullOrWhiteSpace(plan.DestinationRunway) ? "--" : plan.DestinationRunway)}";
        return $"{plan.RouteLabel}  {plan.FlightNumber}\nAircraft {plan.AircraftIcao} {plan.AircraftRegistration} | {cruise} | runways {runways}\n{generated}";
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

    private Control BuildAircraftCard()
    {
        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(8),
            BackColor = System.Drawing.Color.White,
            Margin = new Padding(0, 0, 0, 0)
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _aircraftThumbnailBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = System.Drawing.Color.FromArgb(226, 232, 240),
            SizeMode = PictureBoxSizeMode.Zoom,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 8)
        };
        _aircraftThumbnailBox.Click += (_, _) => CycleAircraftCardImage();
        card.Controls.Add(_aircraftThumbnailBox, 0, 0);

        _aircraftCardTitleLabel = NewDashboardLabel("Aircraft loading...");
        _aircraftCardTitleLabel.Font = new System.Drawing.Font(
            Font.FontFamily,
            9,
            System.Drawing.FontStyle.Bold);
        _aircraftCardTitleLabel.MaximumSize = new System.Drawing.Size(235, 0);
        card.Controls.Add(_aircraftCardTitleLabel, 0, 1);

        _aircraftCardVariationLabel = NewDashboardLabel("");
        _aircraftCardVariationLabel.MaximumSize = new System.Drawing.Size(235, 0);
        card.Controls.Add(_aircraftCardVariationLabel, 0, 2);

        _aircraftCardSourceLabel = NewDashboardLabel("Waiting for simulator aircraft");
        _aircraftCardSourceLabel.ForeColor = System.Drawing.Color.DimGray;
        _aircraftCardSourceLabel.MaximumSize = new System.Drawing.Size(235, 0);
        card.Controls.Add(_aircraftCardSourceLabel, 0, 3);

        return card;
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

    private Button NewProcedureButton(string label, string command) =>
        NewProcedureButton(
            label,
            command,
            86,
            System.Drawing.Color.FromArgb(243, 244, 246),
            System.Drawing.Color.FromArgb(209, 213, 219),
            System.Drawing.Color.FromArgb(229, 231, 235),
            emphasize: false);

    private Button NewProcedureButton(
        string label,
        string command,
        int width,
        System.Drawing.Color backColor,
        System.Drawing.Color mouseDownColor,
        System.Drawing.Color mouseOverColor,
        bool emphasize)
    {
        var button = new Button
        {
            Text = label,
            Width = width,
            Height = 34,
            AutoSize = false,
            Margin = new Padding(4, 3, 4, 3),
            BackColor = backColor,
            ForeColor = emphasize
                ? System.Drawing.Color.White
                : System.Drawing.Color.FromArgb(31, 41, 55),
            FlatStyle = FlatStyle.Flat,
            Font = new System.Drawing.Font(
                Font.FontFamily,
                emphasize ? 9 : 8.5f,
                emphasize ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular),
            UseVisualStyleBackColor = false
        };

        button.FlatAppearance.BorderSize = emphasize ? 0 : 1;
        button.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(209, 213, 219);
        button.FlatAppearance.MouseDownBackColor = mouseDownColor;
        button.FlatAppearance.MouseOverBackColor = mouseOverColor;
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
        UpdateAircraftCard(_state);
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
                    : _state.IsIniBuildsA330
                    ? "iniBuilds A330"
                    : _state.IsFlyByWireA320Neo
                    ? "FBW A32NX"
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
            $"{(_state.IsIniBuildsA330 ? $"APU BAT {_state.ApuBatteryOn.ToOnOff()} | " : "")}" +
            $"{FormatExternalPowerSummary(_state)} | " +
            $"Beacon {_state.BeaconOn.ToOnOff()} | NAV&LOGO " +
            $"{(_state.NavLogoSelectorPosition.HasValue ? FormatNavLogoPosition((int)Math.Round(_state.NavLogoSelectorPosition.Value)) : "UNKNOWN")} | " +
            $"APU {_state.ApuMasterSwitchOn.ToOnOff()}/{_state.ApuRpmPercent:F0}%";
        _adapterLabel!.Text = _state.IsSupportedBoeing737
            ? _pmdgNg3DataReady
                ? "PMDG NG3 SDK data connected"
                : "PMDG NG3 SDK waiting - enable [SDK] EnableDataBroadcast=1 in 737_Options.ini"
            : _mobiFlightReady
                ? "MobiFlight connected"
                : "MobiFlight not connected - aircraft controls unavailable";
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
        UpdateProcedureActionButtons();
        _procedureLabel!.Text =
            definition == null
                ? "None"
                : $"{definition.Name} - {_procedureRunner.Status} - {definition.AutomationSummary}";
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
            $"{recommendation.Procedure.Name} - {recommendation.Reason}";
        _recommendationLabel.ForeColor = recommendation.Overdue
            ? System.Drawing.Color.DarkRed
            : System.Drawing.Color.DarkBlue;
        RefreshFlowList(recommendation.Procedure.Id, definition?.Id);
    }

    private void UpdateAircraftCard(AircraftState state)
    {
        if (_aircraftCardTitleLabel == null
            || _aircraftCardVariationLabel == null
            || _aircraftCardSourceLabel == null
            || _aircraftThumbnailBox == null
            || string.Equals(_aircraftCardTitle, state.Title, StringComparison.Ordinal)
            && string.Equals(_aircraftCardResolvedTitle, state.Title, StringComparison.Ordinal))
        {
            return;
        }

        _aircraftCardTitle = state.Title;
        _aircraftCardResolvedTitle = null;
        _aircraftCardImagePaths = Array.Empty<string>();
        _aircraftCardImageIndex = 0;
        _aircraftIdentityLookupCancellation?.Cancel();
        _aircraftIdentityLookupCancellation?.Dispose();
        _aircraftIdentityLookupCancellation = new CancellationTokenSource();
        var cancellation = _aircraftIdentityLookupCancellation.Token;
        var requestedTitle = state.Title;

        SetAircraftThumbnail(null);
        _aircraftCardTitleLabel.Text = state.AircraftFamilyLabel;
        _aircraftCardVariationLabel.Text = state.Title;
        _aircraftCardSourceLabel.Text = "Searching aircraft thumbnail...";

        Task.Run(
            () =>
            {
                var identity = _aircraftIdentityResolver.Resolve(requestedTitle);
                System.Drawing.Image? image = null;
                var imagePaths = identity?.ThumbnailPaths ?? Array.Empty<string>();
                if (imagePaths.FirstOrDefault() is { Length: > 0 } thumbnailPath
                    && File.Exists(thumbnailPath))
                {
                    image = LoadImageWithoutLocking(thumbnailPath);
                }

                return new AircraftCardLookupResult(requestedTitle, identity, imagePaths, image);
            },
            cancellation).ContinueWith(
            task =>
            {
                if (task.IsCanceled || cancellation.IsCancellationRequested)
                {
                    return;
                }
                if (task.IsFaulted)
                {
                    ApplyAircraftCardResult(new AircraftCardLookupResult(requestedTitle, null, Array.Empty<string>(), null));
                    return;
                }

                ApplyAircraftCardResult(task.Result);
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void ApplyAircraftCardResult(AircraftCardLookupResult result)
    {
        if (_aircraftCardTitleLabel == null
            || _aircraftCardVariationLabel == null
            || _aircraftCardSourceLabel == null
            || !string.Equals(_aircraftCardTitle, result.Title, StringComparison.Ordinal))
        {
            result.Image?.Dispose();
            return;
        }

        _aircraftCardResolvedTitle = result.Title;
        _aircraftCardImagePaths = result.ImagePaths;
        _aircraftCardImageIndex = 0;
        var identity = result.Identity;
        if (identity == null)
        {
            var fallback = TryLoadFallbackAircraftPhoto(result.Title, null);
            SetAircraftThumbnail(result.Image ?? fallback ?? CreateAircraftPlaceholderImage(_aircraftCardTitleLabel.Text));
            _aircraftCardSourceLabel.Text = fallback == null
                ? "No package thumbnail available"
                : "Fallback aircraft photo";
            return;
        }

        _aircraftCardTitleLabel.Text = identity.DisplayName;
        _aircraftCardVariationLabel.Text =
            string.IsNullOrWhiteSpace(identity.DisplayVariation)
                ? identity.Title
                : identity.DisplayVariation;

        if (result.Image != null)
        {
            SetAircraftThumbnail(result.Image);
            _aircraftCardSourceLabel.Text = result.ImagePaths.Count > 1
                ? $"Aircraft package image 1/{result.ImagePaths.Count} - click to cycle"
                : "Aircraft package thumbnail";
        }
        else
        {
            var fallback = TryLoadFallbackAircraftPhoto(result.Title, identity);
            SetAircraftThumbnail(fallback ?? CreateAircraftPlaceholderImage(identity.DisplayName));
            _aircraftCardSourceLabel.Text = fallback == null
                ? "Package matched, no thumbnail available"
                : "Fallback aircraft photo";
        }
    }

    private System.Drawing.Image? TryLoadFallbackAircraftPhoto(
        string title,
        Msfs2024Ai.Copilot.AircraftIdentity.AircraftIdentity? identity)
    {
        var fileName = ResolveFallbackAircraftPhotoFileName(title, identity);
        if (fileName == null)
        {
            return null;
        }

        var path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Assets",
            "AircraftFallbacks",
            fileName);

        return File.Exists(path)
            ? LoadImageWithoutLocking(path)
            : null;
    }

    private static string? ResolveFallbackAircraftPhotoFileName(
        string title,
        Msfs2024Ai.Copilot.AircraftIdentity.AircraftIdentity? identity)
    {
        var probe = string.Join(
            " ",
            new[]
            {
                title,
                identity?.Title,
                identity?.Variation,
                identity?.DisplayName,
                identity?.DisplayVariation
            }.Where(value => !string.IsNullOrWhiteSpace(value))).ToUpperInvariant();

        if (probe.Contains("737") || probe.Contains("B738") || probe.Contains("PMDG"))
        {
            return "boeing-737-800.jpg";
        }

        if (probe.Contains("A321"))
        {
            return "airbus-a321lr.jpg";
        }

        if (probe.Contains("A330") || probe.Contains("E330"))
        {
            return "airbus-a330.jpg";
        }

        if (probe.Contains("A320") || probe.Contains("A32N") || probe.Contains("A20N"))
        {
            return "airbus-a320neo.jpg";
        }

        return null;
    }

    private static System.Drawing.Image CreateAircraftPlaceholderImage(string label)
    {
        var bitmap = new System.Drawing.Bitmap(360, 150);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(System.Drawing.Color.FromArgb(223, 229, 237));

        using var skyBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
            new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
            System.Drawing.Color.FromArgb(232, 239, 247),
            System.Drawing.Color.FromArgb(205, 216, 230),
            90f);
        graphics.FillRectangle(skyBrush, 0, 0, bitmap.Width, bitmap.Height);

        using var wingBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(88, 112, 140));
        using var bodyBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(52, 74, 101));
        using var accentBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(39, 130, 87));
        using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(39, 58, 82), 5f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };

        var centerY = 75;
        graphics.DrawLine(pen, 62, centerY, 292, centerY - 14);
        graphics.FillEllipse(bodyBrush, 270, centerY - 28, 52, 30);
        graphics.FillPolygon(
            wingBrush,
            new[]
            {
                new System.Drawing.Point(150, centerY - 8),
                new System.Drawing.Point(220, centerY - 58),
                new System.Drawing.Point(236, centerY - 46),
                new System.Drawing.Point(184, centerY + 4)
            });
        graphics.FillPolygon(
            wingBrush,
            new[]
            {
                new System.Drawing.Point(144, centerY + 4),
                new System.Drawing.Point(218, centerY + 46),
                new System.Drawing.Point(230, centerY + 34),
                new System.Drawing.Point(184, centerY - 4)
            });
        graphics.FillPolygon(
            accentBrush,
            new[]
            {
                new System.Drawing.Point(70, centerY - 2),
                new System.Drawing.Point(35, centerY - 38),
                new System.Drawing.Point(84, centerY - 20)
            });

        using var font = new System.Drawing.Font("Segoe UI", 14f, System.Drawing.FontStyle.Bold);
        using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(39, 58, 82));
        var text = string.IsNullOrWhiteSpace(label) ? "Aircraft" : label.Trim();
        graphics.DrawString(text, font, textBrush, new System.Drawing.RectangleF(16, 112, bitmap.Width - 32, 28));

        return bitmap;
    }

    private void SetAircraftThumbnail(System.Drawing.Image? image)
    {
        if (_aircraftThumbnailBox == null)
        {
            image?.Dispose();
            return;
        }

        var previous = _aircraftThumbnailBox.Image;
        _aircraftThumbnailBox.Image = image;
        previous?.Dispose();
    }

    private void CycleAircraftCardImage()
    {
        if (_aircraftCardImagePaths.Count <= 1
            || _aircraftCardSourceLabel == null)
        {
            return;
        }

        for (var attempt = 0; attempt < _aircraftCardImagePaths.Count; attempt++)
        {
            _aircraftCardImageIndex =
                (_aircraftCardImageIndex + 1) % _aircraftCardImagePaths.Count;
            var path = _aircraftCardImagePaths[_aircraftCardImageIndex];
            if (!File.Exists(path))
            {
                continue;
            }

            var image = LoadImageWithoutLocking(path);
            if (image == null)
            {
                continue;
            }

            SetAircraftThumbnail(image);
            _aircraftCardSourceLabel.Text =
                $"Aircraft package image {_aircraftCardImageIndex + 1}/{_aircraftCardImagePaths.Count} - click to cycle";
            return;
        }
    }

    private static System.Drawing.Image? LoadImageWithoutLocking(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var image = System.Drawing.Image.FromStream(stream);
            return new System.Drawing.Bitmap(image);
        }
        catch
        {
            return null;
        }
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

    private void UpdateProcedureActionButtons()
    {
        var status = _procedureRunner.Status;
        var active = IsProcedureActive(status);

        if (_startSelectedFlowButton != null)
        {
            _startSelectedFlowButton.Text = status == ProcedureStatus.Paused
                ? "Flow paused"
                : active
                    ? "Flow running"
                    : "Start selected flow";
            _startSelectedFlowButton.BackColor = status switch
            {
                ProcedureStatus.Paused => System.Drawing.Color.FromArgb(151, 110, 35),
                ProcedureStatus.Running => System.Drawing.Color.FromArgb(30, 64, 175),
                ProcedureStatus.WaitingForVerification => System.Drawing.Color.FromArgb(29, 78, 216),
                ProcedureStatus.WaitingForManualAction => System.Drawing.Color.FromArgb(190, 126, 37),
                _ => System.Drawing.Color.FromArgb(39, 130, 87)
            };
            _startSelectedFlowButton.ForeColor = System.Drawing.Color.White;
            _startSelectedFlowButton.FlatAppearance.BorderSize = active ? 2 : 0;
            _startSelectedFlowButton.FlatAppearance.BorderColor =
                active ? System.Drawing.Color.FromArgb(15, 23, 42) : _startSelectedFlowButton.BackColor;
        }

        if (_confirmCompletedButton != null)
        {
            var waitingForPilot = status == ProcedureStatus.WaitingForManualAction;
            _confirmCompletedButton.BackColor = waitingForPilot
                ? System.Drawing.Color.FromArgb(194, 65, 12)
                : System.Drawing.Color.FromArgb(39, 130, 87);
            _confirmCompletedButton.FlatAppearance.MouseDownBackColor = waitingForPilot
                ? System.Drawing.Color.FromArgb(146, 64, 14)
                : System.Drawing.Color.FromArgb(22, 101, 52);
            _confirmCompletedButton.FlatAppearance.MouseOverBackColor = waitingForPilot
                ? System.Drawing.Color.FromArgb(245, 158, 11)
                : System.Drawing.Color.FromArgb(34, 148, 96);
            _confirmCompletedButton.Text = waitingForPilot
                ? "Confirm now"
                : "Confirm completed";
        }
    }

    private static bool IsProcedureActive(ProcedureStatus status) =>
        status is ProcedureStatus.Running
            or ProcedureStatus.WaitingForManualAction
            or ProcedureStatus.WaitingForVerification
            or ProcedureStatus.Paused;

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
            "apu-bleed-warmup" =>
                "APU available; waiting briefly before applying APU bleed load.",
            "irs-on-dc-extinguished" =>
                $"IRS ON DC lights. Left {state.IrsLeftOnDcLightOn.ToOnOff()}, right {state.IrsRightOnDcLightOn.ToOnOff()}.",
            "irs-aligned" =>
                $"IRS ready: aligned {state.IrsAligned.ToYesNo()}, ALIGN L/R {state.IrsLeftAlignLightOn.ToOnOff()}/{state.IrsRightAlignLightOn.ToOnOff()}, ON DC L/R {state.IrsLeftOnDcLightOn.ToOnOff()}/{state.IrsRightOnDcLightOn.ToOnOff()}.",
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
                state.IsSupportedBoeing737
                    ? $"landing flaps speed safe: IAS {state.IndicatedAirspeedKnots:F0} kt <= 195 kt; landing target VREF+5 {state.EffectiveBoeingApproachTargetSpeedKnots} kt."
                    : $"Landing configuration speed safe: IAS {state.IndicatedAirspeedKnots:F0} kt <= {state.EffectiveApproachFlaps3SpeedKnots} kt.",
            "flaps-full-speed" =>
                $"Flaps FULL speed safe: IAS {state.IndicatedAirspeedKnots:F0} kt <= {state.EffectiveApproachFlapsFullSpeedKnots} kt.",
            "landing-data-set" =>
                $"FMC landing data: flaps {(state.BoeingLandingFlaps.HasValue ? state.BoeingLandingFlaps.Value.ToString() : "not set")}, VREF {(state.BoeingLandingVrefKnots.HasValue ? state.BoeingLandingVrefKnots.Value.ToString() : "not set")}.",
            "stable-approach" =>
                $"stable by 1,000 ft AGL: RA {state.RadioHeightFeet:F0} ft, IAS {state.IndicatedAirspeedKnots:F0} kt, target {state.EffectiveBoeingApproachTargetSpeedKnots} kt, gear {(state.GearHandleDown ? "DOWN" : "not down")}, flaps {(state.BoeingLandingFlapsSet ? "landing" : "not landing")}, speedbrake {(state.GroundSpoilersArmed ? "ARMED" : "not armed")}.",
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
            return "READBACK INCONSISTENT - " +
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
                $"{flight} | trigger <={state.ApproachFlaps1AltitudeFeet:N0} ft indicated or distance gate",
            "flaps-one-speed" =>
                $"{flight} | wait IAS <={state.EffectiveApproachFlaps1SpeedKnots} kt for CONFIG 1",
            "flaps-two-speed" =>
                $"{flight} | wait IAS <={state.EffectiveApproachFlaps2SpeedKnots} kt for CONFIG 2",
            "gear-down-point" =>
                $"{flight} | trigger <={state.ApproachGearAltitudeAglFeet:N0} ft AGL or distance gate",
            "landing-config-point" =>
                $"{flight} | trigger <={state.ApproachLandingConfigAltitudeAglFeet:N0} ft AGL or distance gate",
            "landing-config-speed" =>
                state.IsSupportedBoeing737
                    ? $"{flight} | wait IAS <=195 kt for landing flaps | target VREF+5 {state.EffectiveBoeingApproachTargetSpeedKnots} kt"
                    : $"{flight} | wait IAS <={state.EffectiveApproachFlaps3SpeedKnots} kt for CONFIG 3",
            "flaps-full-speed" =>
                $"{flight} | wait IAS <={state.EffectiveApproachFlapsFullSpeedKnots} kt for FULL",
            "landing-data-set" =>
                $"{flight} | FMC landing flaps {(state.BoeingLandingFlaps.HasValue ? state.BoeingLandingFlaps.Value.ToString() : "not set")} | VREF {(state.BoeingLandingVrefKnots.HasValue ? state.BoeingLandingVrefKnots.Value.ToString() : "not set")}",
            "stable-approach" =>
                $"{flight} | RA {state.RadioHeightFeet:F0} ft | target {state.EffectiveBoeingApproachTargetSpeedKnots} kt | stable {(state.BoeingApproachStable ? "YES" : "NO")}",
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

    private static string FormatExternalPowerSummary(AircraftState state) =>
        state.IsIniBuildsA330
            ? $"EXT A {state.ExternalPower1On.ToOnOff()} ({state.ExternalPower1Available.ToYesNo()} avail) | " +
              $"EXT B {state.ExternalPower2On.ToOnOff()} ({state.ExternalPower2Available.ToYesNo()} avail)"
            : $"EXT PWR {state.ExternalPowerOn.ToOnOff()} ({state.ExternalPowerAvailable.ToYesNo()} available)";

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
                    $"{GetApplicationVersion()} - no GitHub release published";
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
                    $"{GetApplicationVersion()} - release status unavailable";
                return;
            }

            var current =
                Assembly.GetExecutingAssembly().GetName().Version
                ?? new Version();
            _versionLabel.Text = latest > current
                ? $"{GetApplicationVersion()} - update available: {latest}"
                : $"{GetApplicationVersion()} - up to date";
            _versionLabel.ForeColor = latest > current
                ? System.Drawing.Color.DarkOrange
                : System.Drawing.Color.DarkGreen;
        }
        catch (Exception ex)
        {
            _versionLabel.Text =
                $"{GetApplicationVersion()} - update check unavailable";
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
        ResetMobiFlightRuntimeAfterDisconnect();
        _simConnect?.Dispose();
        _simConnect = null;
        if (_connectionLabel != null)
        {
            _connectionLabel.Text = "Disconnected; waiting for MSFS...";
            _connectionLabel.ForeColor = System.Drawing.Color.DarkRed;
        }
        ScheduleReconnect();
    }

    private void ResetMobiFlightRuntimeAfterDisconnect()
    {
        // The WASM module survives a simulator connection restart, but this
        // SimConnect client's data definitions do not. Force the complete
        // runtime client and ordered SimVar table to be recreated after every
        // reconnect instead of accepting values left from the previous
        // session as current aircraft readback.
        _mobiFlightRuntimeReady = false;
        _mobiFlightRuntimeInitializedUtc = null;

        _nativeBattery1On = null;
        _nativeBattery2On = null;
        _nativeFuelPump1 = null;
        _nativeFuelPump2 = null;
        _nativeFuelPump3 = null;
        _nativeFuelPump4 = null;
        _nativeFuelPump5 = null;
        _nativeFuelPump6 = null;
        _nativeNavLogoSelectorPosition = null;
        _nativeApuAvailable = null;
        _nativeApuMasterSwitch = null;
        _nativeApuStartButton = null;
        _nativeApuBleedButton = null;
        _nativeApuGeneratorOn = null;
        _nativeApuFlapPercent = null;
        _nativeAdirs1State = null;
        _nativeAdirs2State = null;
        _nativeAdirs3State = null;
        _nativeAdirsOnBattery = null;
        _nativeCrewOxygen = null;
        _nativeStrobeSelector = null;
        _nativeSeatbeltSelector = null;
        _nativeSeatbeltSignsOn = null;
        _nativeNoSmokingSelector = null;
        _nativeNoSmokingSignsOn = null;
        _nativeEmergencyExitSelector = null;
        _nativeSpoilersArmed = null;
        _nativeAutobrakeLevel = null;
        _nativeTcasAltitudeReporting = null;
        _nativeGearHandlePosition = null;
        _nativeWeatherRadarPwsSelector = null;
        _nativeNoseLightSelector = null;
        _nativeLeftLandingLightSelector = null;
        _nativeRightLandingLightSelector = null;
        _nativeTransponderAtcState = null;
        _nativeTcasMode = null;
        _nativeTransponderStandby = null;

        AppLog.Write(
            "MobiFlight runtime state cleared; full native readback registration required after reconnect.");
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
            _aircraftIdentityLookupCancellation?.Cancel();
            _aircraftIdentityLookupCancellation?.Dispose();
            _aircraftIdentityLookupCancellation = null;
            SetAircraftThumbnail(null);
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

    private sealed class AircraftCardLookupResult
    {
        public AircraftCardLookupResult(
            string title,
            Msfs2024Ai.Copilot.AircraftIdentity.AircraftIdentity? identity,
            IReadOnlyList<string> imagePaths,
            System.Drawing.Image? image)
        {
            Title = title;
            Identity = identity;
            ImagePaths = imagePaths;
            Image = image;
        }

        public string Title { get; }
        public Msfs2024Ai.Copilot.AircraftIdentity.AircraftIdentity? Identity { get; }
        public IReadOnlyList<string> ImagePaths { get; }
        public System.Drawing.Image? Image { get; }
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
            DateTime deadlineUtc,
            bool logProgressToDashboard)
        {
            Name = name;
            Verify = verify;
            DesiredOn = desiredOn;
            DesiredLabel = desiredLabel;
            DeadlineUtc = deadlineUtc;
            LogProgressToDashboard = logProgressToDashboard;
        }

        public string Name { get; }
        public Func<AircraftState, bool> Verify { get; }
        public bool DesiredOn { get; }
        public string DesiredLabel { get; }
        public DateTime DeadlineUtc { get; }
        public bool LogProgressToDashboard { get; }
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

        public FuelPumpToggle(
            int number,
            ulong inputEventHash)
        {
            Number = number;
            InputEventHash = inputEventHash;
            CalculatorCode = string.Empty;
        }

        public int Number { get; }
        public string CalculatorCode { get; }
        public ulong? InputEventHash { get; }
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
            $"{(Completed ? "[DONE]" : Active ? "[ACTIVE]" : Recommended ? "[NEXT]" : " ")} " +
            $"{Definition.Name} - {Definition.AutomationSummary}";
    }
}

internal static class DisplayExtensions
{
    public static string ToOnOff(this bool value) => value ? "ON" : "OFF";
    public static string ToYesNo(this bool value) => value ? "YES" : "NO";
    public static string ToSetReleased(this bool value) => value ? "SET" : "RELEASED";
}


