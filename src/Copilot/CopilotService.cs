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
    // Change the schema suffix whenever the ordered runtime LVar list changes.
    // MobiFlight client-data layouts persist for the simulator session.
    private const string MobiFlightRuntimeClientName = "MSFS2024_AI_Copilot_v21";
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
    private PendingFuelPumpSequence? _pendingFuelPumpSequence;
    private System.Windows.Forms.Timer? _fuelPumpSequenceTimer;
    private readonly List<System.Windows.Forms.Timer> _nativePulseTimers = new();
    private bool _mobiFlightReady;
    private bool _mobiFlightRuntimeReady;
    private DateTime? _mobiFlightRuntimeInitializedUtc;
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
    private Label? _procedureLabel;
    private Label? _stepLabel;
    private Label? _messageLabel;
    private ProgressBar? _procedureProgress;
    private ComboBox? _automationPolicyBox;
    private ComboBox? _pilotFlyingBox;
    private NumericUpDown? _transitionAltitudeBox;
    private NumericUpDown? _takeoffV1Box;
    private NumericUpDown? _takeoffRotateBox;
    private CheckBox? _voiceCalloutsBox;
    private ComboBox? _replayFlightBox;
    private ListBox? _eventLog;
    private ListBox? _flowList;
    private ListBox? _checklistList;

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
        NativeRightLandingLightSelector = 156
    }

    private enum ClientDataArea
    {
        MobiFlightCommand = 100,
        MobiFlightResponse = 101,
        MobiFlightRuntimeLVars = 110,
        MobiFlightRuntimeCommand = 111,
        MobiFlightRuntimeResponse = 112
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
        NativeRightLandingLightSelector = 156
    }

    private enum CopilotEvent
    {
        SetExternalPower,
        SetBeacon,
        StartApu,
        SetApuBleed,
        SetApuGenerator,
        SetFuelPump
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
        public double Battery1;
        public double Battery2;
        public double ExternalPowerAvailable;
        public double ExternalPowerOn;
        public double ParkingBrake;
        public double Beacon;
        public double NavigationLights;
        public double LogoLights;
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
            $"Connected — SimConnect {data.dwSimConnectVersionMajor}.{data.dwSimConnectVersionMinor}, " +
            $"simulator {data.dwApplicationVersionMajor}.{data.dwApplicationVersionMinor}.");
        AppendDashboardLog(
            $"Connected to MSFS — SimConnect {data.dwSimConnectVersionMajor}.{data.dwSimConnectVersionMinor}");
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
        sender.AddToDataDefinition(Definition.AircraftState, "ELECTRICAL MASTER BATTERY:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "ELECTRICAL MASTER BATTERY:2", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "EXTERNAL POWER AVAILABLE:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "EXTERNAL POWER ON:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "BRAKE PARKING POSITION", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "LIGHT BEACON", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "LIGHT NAV", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "LIGHT LOGO", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
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
        sender.RegisterDataDefineStruct<AircraftData>(Definition.AircraftState);
        sender.MapClientEventToSimEvent(CopilotEvent.SetExternalPower, "SET_EXTERNAL_POWER");
        sender.MapClientEventToSimEvent(CopilotEvent.SetBeacon, "BEACON_LIGHTS_SET");
        sender.MapClientEventToSimEvent(CopilotEvent.StartApu, "APU_STARTER");
        sender.MapClientEventToSimEvent(CopilotEvent.SetApuBleed, "APU_BLEED_AIR_SOURCE_SET");
        sender.MapClientEventToSimEvent(CopilotEvent.SetApuGenerator, "APU_GENERATOR_SWITCH_SET");
        sender.MapClientEventToSimEvent(CopilotEvent.SetFuelPump, "FUELSYSTEM_PUMP_SET");
        InitializeMobiFlight(sender);

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

    private void OnClientData(SimConnect sender, SIMCONNECT_RECV_CLIENT_DATA data)
    {
        if (data.dwData.Length == 0)
        {
            return;
        }

        var request = (Request)data.dwRequestID;
        if (request is >= Request.NativeBattery1 and <= Request.NativeRightLandingLightSelector)
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
        SendMobiFlightRuntimeCommand("MF.DummyCmd");
        AppendDashboardLog("iniBuilds native state monitoring connected.");
    }

    private static void RegisterMobiFlightFloat(
        SimConnect sender,
        ClientDataDefinition definition,
        Request request,
        int offset)
    {
        sender.AddToClientDataDefinition(definition, (uint)offset, sizeof(float), 0, 0);
        sender.RegisterStruct<SIMCONNECT_RECV_CLIENT_DATA, MobiFlightFloat>(definition);
        sender.RequestClientData(
            ClientDataArea.MobiFlightRuntimeLVars,
            request,
            definition,
            SIMCONNECT_CLIENT_DATA_PERIOD.ON_SET,
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

        if (_nativeBattery1On.HasValue)
        {
            _state.Battery1On = _nativeBattery1On.Value;
        }
        if (_nativeBattery2On.HasValue)
        {
            _state.Battery2On = _nativeBattery2On.Value;
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
        _state = new AircraftState
        {
            Title = raw.Title,
            OnGround = raw.OnGround != 0,
            GroundSpeedKnots = raw.GroundSpeed,
            Engine1Running = raw.Engine1Combustion != 0,
            Engine2Running = raw.Engine2Combustion != 0,
            Engine1StarterActive = raw.Engine1Starter != 0,
            Engine2StarterActive = raw.Engine2Starter != 0,
            Engine1N1Percent = raw.Engine1N1,
            Engine2N1Percent = raw.Engine2N1,
            Engine1EgtCelsius = raw.Engine1Egt,
            Engine2EgtCelsius = raw.Engine2Egt,
            Engine1FuelFlowPph = raw.Engine1FuelFlow,
            Engine2FuelFlowPph = raw.Engine2FuelFlow,
            Battery1On = _nativeBattery1On ?? raw.Battery1 != 0,
            Battery2On = _nativeBattery2On ?? raw.Battery2 != 0,
            ExternalPowerAvailable = raw.ExternalPowerAvailable != 0,
            ExternalPowerOn = raw.ExternalPowerOn != 0,
            ParkingBrakeSet = raw.ParkingBrake != 0,
            BeaconOn = raw.Beacon != 0,
            NavigationLightsOn = raw.NavigationLights != 0,
            LogoLightsOn = raw.LogoLights != 0,
            NavLogoSelectorPosition = _nativeNavLogoSelectorPosition,
            ApuRpmPercent = raw.ApuRpm,
            ApuStarterPercent = raw.ApuStarter,
            ApuMasterSwitchOn = _nativeApuMasterSwitch.HasValue
                                ? _nativeApuMasterSwitch.Value != 0
                                : raw.ApuMasterSwitch != 0,
            ApuAvailable = _nativeApuAvailable.HasValue && _nativeApuAvailable.Value != 0,
            ApuStartButtonOn = _nativeApuStartButton.HasValue && _nativeApuStartButton.Value != 0,
            ApuBleedOn = _nativeApuBleedButton.HasValue && _nativeApuBleedButton.Value != 0,
            ApuFlapPercent = _nativeApuFlapPercent ?? 0,
            ApuGeneratorActive = raw.ApuGeneratorActive != 0,
            ApuGeneratorSwitchOn = _nativeApuGeneratorOn.HasValue
                                   ? _nativeApuGeneratorOn.Value != 0
                                   : raw.ApuGeneratorSwitch != 0,
            ApuVolts = raw.ApuVolts,
            FuelPumpsConfigured = (_nativeFuelPump1 ?? (float)raw.FuelPump1) != 0
                                  && (_nativeFuelPump2 ?? (float)raw.FuelPump2) != 0
                                  && (_nativeFuelPump3 ?? (float)raw.FuelPump3) != 0
                                  && (_nativeFuelPump4 ?? (float)raw.FuelPump4) != 0
                                  && (_nativeFuelPump5 ?? 0) != 0
                                  && (_nativeFuelPump6 ?? 0) != 0,
            FuelPump1State = _nativeFuelPump1 ?? raw.FuelPump1,
            FuelPump2State = _nativeFuelPump2 ?? raw.FuelPump2,
            FuelPump3State = _nativeFuelPump3 ?? raw.FuelPump3,
            FuelPump4State = _nativeFuelPump4 ?? raw.FuelPump4,
            FuelPump5State = _nativeFuelPump5 ?? 0,
            FuelPump6State = _nativeFuelPump6 ?? 0,
            AltitudeAboveGroundFeet = raw.AltitudeAboveGround,
            IndicatedAltitudeFeet = raw.IndicatedAltitude,
            TransitionAltitudeFeet = _settings.TransitionAltitudeFeet,
            CaptainAltimeterStandard = raw.CaptainBaroStandard != 0,
            FirstOfficerAltimeterStandard = raw.FirstOfficerBaroStandard != 0,
            IndicatedAirspeedKnots = raw.IndicatedAirspeed,
            TakeoffV1SpeedKnots = _settings.TakeoffV1SpeedKnots,
            TakeoffRotateSpeedKnots = _settings.TakeoffRotateSpeedKnots,
            ApproachFlaps1AltitudeFeet = _settings.ApproachFlaps1AltitudeFeet,
            ApproachFlaps1SpeedKnots = _settings.ApproachFlaps1SpeedKnots,
            ApproachGearAltitudeAglFeet = _settings.ApproachGearAltitudeAglFeet,
            ApproachGearSpeedKnots = _settings.ApproachGearSpeedKnots,
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
            GearHandleDown = _nativeGearHandlePosition.HasValue
                ? _nativeGearHandlePosition.Value >= 0.5
                : raw.GearHandle != 0,
            PitchDegrees = raw.PitchDegrees,
            AutopilotMasterOn = raw.AutopilotMaster != 0,
            Adirs1SelectorState = _nativeAdirs1State ?? 0,
            Adirs2SelectorState = _nativeAdirs2State ?? 0,
            Adirs3SelectorState = _nativeAdirs3State ?? 0,
            AdirsOnBattery = _nativeAdirsOnBattery.HasValue && _nativeAdirsOnBattery.Value != 0,
            CrewOxygenOn = _nativeCrewOxygen.HasValue && _nativeCrewOxygen.Value != 0,
            StrobeSelectorPosition = _nativeStrobeSelector,
            ApuFireTestActive = _nativeApuFireTest.HasValue && _nativeApuFireTest.Value != 0,
            ApuFireWarningLit = _nativeApuFireWarningLit.HasValue && _nativeApuFireWarningLit.Value != 0,
            ApuFireSoundActive = _nativeApuFireSound.HasValue && _nativeApuFireSound.Value != 0,
            Engine1FireTestActive = _nativeEngine1FireTest.HasValue && _nativeEngine1FireTest.Value != 0,
            Engine1FireWarningLit = _nativeEngine1FireWarningLit.HasValue && _nativeEngine1FireWarningLit.Value != 0,
            Engine1FireSoundActive = _nativeEngine1FireSound.HasValue && _nativeEngine1FireSound.Value != 0,
            Engine2FireTestActive = _nativeEngine2FireTest.HasValue && _nativeEngine2FireTest.Value != 0,
            Engine2FireWarningLit = _nativeEngine2FireWarningLit.HasValue && _nativeEngine2FireWarningLit.Value != 0,
            Engine2FireSoundActive = _nativeEngine2FireSound.HasValue && _nativeEngine2FireSound.Value != 0,
            SeatbeltSelectorPosition = _nativeSeatbeltSelector,
            SeatbeltSignsOn = _nativeSeatbeltSignsOn.HasValue && _nativeSeatbeltSignsOn.Value != 0,
            NoSmokingSelectorPosition = _nativeNoSmokingSelector,
            NoSmokingSignsOn = _nativeNoSmokingSignsOn.HasValue && _nativeNoSmokingSignsOn.Value != 0,
            EmergencyExitSelectorPosition = _nativeEmergencyExitSelector,
            GroundSpoilersArmed = _nativeSpoilersArmed.HasValue
                ? _nativeSpoilersArmed.Value != 0
                : raw.SpoilersArmed != 0,
            AutobrakeLevel = _nativeAutobrakeLevel,
            WeatherRadarPwsSelectorPosition = _nativeWeatherRadarPwsSelector,
            NoseLightSelectorPosition = _nativeNoseLightSelector,
            LeftLandingLightSelectorPosition = _nativeLeftLandingLightSelector,
            RightLandingLightSelectorPosition = _nativeRightLandingLightSelector,
            TcasAltitudeReportingOn =
                _nativeTcasAltitudeReporting.HasValue
                    ? _nativeTcasAltitudeReporting.Value == 0
                    : null,
            TransponderAtcState = _nativeTransponderAtcState,
            TcasMode = _nativeTcasMode,
            TransponderModeSelectorPosition = _nativeTransponderStandby,
            TransponderStandby = _nativeTransponderStandby.HasValue
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
        if (!_state.IsA320NeoV2)
        {
            Console.Error.WriteLine("Warning: this build currently supports only the iniBuilds A320neo V2.");
        }

        if (_oneShotCommand == null)
        {
            PrintHelp();
            Console.Write("> ");
        }
        TryExecuteOneShotCommand();
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
                _nativeApuAvailable.HasValue
                && _nativeApuMasterSwitch.HasValue
                && _nativeApuStartButton.HasValue
                && _nativeApuBleedButton.HasValue
                && _nativeApuGeneratorOn.HasValue
                && _nativeApuFlapPercent.HasValue,
            var command when command.StartsWith("fuel-pumps ") =>
                _nativeFuelPump1.HasValue
                && _nativeFuelPump2.HasValue
                && _nativeFuelPump3.HasValue
                && _nativeFuelPump4.HasValue
                && _nativeFuelPump5.HasValue
                && _nativeFuelPump6.HasValue,
            var command when command.StartsWith("adirs-1 ") => _nativeAdirs1State.HasValue,
            var command when command.StartsWith("adirs-2 ") => _nativeAdirs2State.HasValue,
            var command when command.StartsWith("adirs-3 ") => _nativeAdirs3State.HasValue,
            var command when command.StartsWith("crew-oxygen ") => true,
            var command when command.StartsWith("strobe ") => _nativeStrobeSelector.HasValue,
            var command when command == "fire-test apu" => _nativeApuFireTest.HasValue,
            var command when command == "fire-test engine-1" => _nativeEngine1FireTest.HasValue,
            var command when command == "fire-test engine-2" => _nativeEngine2FireTest.HasValue,
            var command when command.StartsWith("seatbelts ") => _nativeSeatbeltSelector.HasValue,
            var command when command.StartsWith("no-smoking ") => _nativeNoSmokingSelector.HasValue,
            var command when command.StartsWith("emergency-exit ") => _nativeEmergencyExitSelector.HasValue,
            var command when command.StartsWith("transponder ") => _nativeTransponderStandby.HasValue,
            var command when command.StartsWith("atc-system ") => _nativeTransponderAtcState.HasValue,
            var command when command.StartsWith("tcas altitude-reporting ") =>
                _nativeTcasAltitudeReporting.HasValue,
            var command when command.StartsWith("tcas traffic ") => _nativeTcasMode.HasValue,
            var command when command.StartsWith("wxr-pws ") =>
                _nativeWeatherRadarPwsSelector.HasValue,
            var command when command.StartsWith("nose-light ") =>
                _nativeNoseLightSelector.HasValue,
            var command when command.StartsWith("landing-lights ") =>
                _nativeLeftLandingLightSelector.HasValue
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

    private void TryRestoreProcedureSession()
    {
        if (_procedureSessionRestoreAttempted
            || _state == null
            || !_state.IsA320NeoV2
            || !NativeStateReady)
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
            A320ProcedureLibrary.Find(activeProcedureId!);
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
        var definition = A320ProcedureLibrary.Find(id);
        if (definition == null)
        {
            Console.Error.WriteLine($"Unknown procedure: {id}");
            FinishOneShot(2);
            return;
        }

        StartProcedure(definition);
    }

    private ProcedureDefinition? GetAutomaticNextFlow(string completedId)
    {
        var flows = A320ProcedureLibrary.GateToGate;
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
        UpdateDashboard();
    }

    private void SpeakProcedureCallout(ProcedureStep step)
    {
        if (!_settings.EnableStandardCallouts || _voiceCalloutQueue == null)
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

        if (!_state.IsA320NeoV2)
        {
            Console.Error.WriteLine("Beacon procedure blocked: the loaded aircraft is not A320neo V2.");
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

        if (!_state.IsA320NeoV2 || !_mobiFlightReady)
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

        if (!_state.IsA320NeoV2 || !_state.OnGround || !_state.EnginesOff)
        {
            Console.Error.WriteLine(
                "Battery procedure blocked: requires A320neo V2 on the ground with engines off.");
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

        var preset = $"Battery_{batteryNumber}_{(desiredOn ? "On" : "Off")}";
        if (!ExecuteDocumentedPreset(preset))
        {
            FinishOneShot(4);
            return;
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

    private void SetApuMaster(bool desiredOn) =>
        PulseNativeCommand(
            "APU master",
            "INI_APU_MASTER_SWITCH_CMD",
            desiredOn,
            state => state.ApuMasterSwitchOn == desiredOn);

    private void SetApuStart(bool desiredOn) =>
        PulseNativeCommand(
            "APU start",
            "INI_APU_START_BUTTON_CMD",
            desiredOn,
            state => state.ApuStartButtonOn == desiredOn);

    private void SetApuBleed(bool desiredOn) =>
        ToggleNativeMouserect(
            "APU bleed",
            "INI_APU_BLEED_BUTTON",
            "__APU_BLEEDIsPressed",
            desiredOn,
            state => state.ApuBleedOn == desiredOn);

    private void SetApuGenerator(bool desiredOn)
        => PulseInputEvent(
            "APU generator",
            3205083420795941787UL,
            desiredOn,
            state => state.ApuGeneratorSwitchOn == desiredOn);

    private void SetFuelPumps(bool desiredOn)
    {
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
                    selectors[index],
                    pressStates[index]));
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
        // Exact Behavior Viewer Mouserect code: toggle one pump selector and
        // its press-animation state. Buttons are spaced one second apart.
        SendMobiFlightCommand(
            $"MF.SimVars.Set.(L:{toggle.SelectorLVar}) ! (>L:{toggle.SelectorLVar}) " +
            $"(L:{toggle.PressLVar}) ! (>L:{toggle.PressLVar})");
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

    private void SetCrewOxygen(bool desiredOn)
    {
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

    private void SetStrobeSelector(int desiredPosition)
    {
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
        if (!ValidateNativeInputAction("Transponder mode selector"))
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
            TimeSpan.FromSeconds(10));
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
        if (!ValidateNativeInputAction("TCAS traffic mode"))
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
        if (!ValidateNativeInputAction("TCAS altitude reporting"))
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

        SendMobiFlightCommand(
            "MF.SimVars.Set.(>B:LANDING_GEAR_Gear_Inc) " +
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

        SendMobiFlightCommand(
            "MF.SimVars.Set.(>B:LANDING_GEAR_Gear_Dec) " +
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
        if (_simConnect == null || _state == null || !_state.IsA320NeoV2)
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
        SendMobiFlightCommand(
            "MF.SimVars.Set.0 'INI.SPOILERS_SET' (>F:KeyEvent) " +
            "'INI.SPOILERS_ARM_OFF' (>F:KeyEvent) " +
            "(>B:AIRLINER_SPEEDBRAKE_Set)");
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
        if (!ValidateNativeInputAction(
                "WXR/PWS selector",
                requireCompleteNativeState: true,
                requireStationary: false))
        {
            return;
        }

        // Always transmit the actual selector event. INI_WX_SYS_SWITCH can
        // retain a stale value of 1 while the physical selector is still OFF.
        SendMobiFlightCommand(
            "MF.SimVars.Set.(>B:AIRLINER_WER_SWITCH_PWS_State2)");
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            "WXR/PWS selector",
            state => state.WeatherRadarPwsSelectorPosition.HasValue
                     && Math.Abs(
                         state.WeatherRadarPwsSelectorPosition.Value
                         - desiredPosition) < 0.1,
            true);
    }

    private void SetNoseLightSelector(int desiredPosition)
    {
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
            desiredPosition == 2);
    }

    private void UpdateCruiseSeatbeltMonitoring()
    {
        if (!_cruiseSeatbeltMonitoring
            || _state == null
            || _simConnect == null
            || !_state.IsA320NeoV2
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
            || !_state.IsA320NeoV2
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
        if (!ValidateNativeInputAction(
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

        SendMobiFlightCommand(
            "MF.SimVars.Set.0 'INI.SPOILERS_SET' (>F:KeyEvent) " +
            "'INI.SPOILERS_ARM_ON' (>F:KeyEvent) " +
            "(>B:AIRLINER_SPEEDBRAKE_Set)");
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
        if (_simConnect == null || _state == null || !_state.IsA320NeoV2)
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
        if (_state.FlapsHandleIndex > desiredPosition)
        {
            AppendDashboardLog(
                $"Flap extension blocked: current position {_state.FlapsHandleIndex:F0} exceeds target {desiredPosition}.");
            FinishOneShot(3);
            return;
        }

        SendMobiFlightCommand(
            "MF.SimVars.Set.16384 (A:FLAPS NUM HANDLE POSITIONS, Number) / " +
            "(>B:HANDLING_Flaps_Inc)");
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            $"Flaps CONFIG {desiredPosition}",
            state => state.FlapsAtDetent((int)desiredPosition),
            true,
            TimeSpan.FromSeconds(15));
    }

    private void SetFlapsClean()
    {
        if (_simConnect == null || _state == null || !_state.IsA320NeoV2)
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

        SendMobiFlightCommand(_state.OnGround
            ? "MF.SimVars.Set.0 (>B:HANDLING_Flaps_Set)"
            : "MF.SimVars.Set.16384 (A:FLAPS NUM HANDLE POSITIONS, Number) / " +
              "(>B:HANDLING_Flaps_Dec)");
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
            || !_state.IsA320NeoV2
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

        SendMobiFlightCommand(
            $"MF.SimVars.Set.{desiredLevel} (>L:INI_AUTOBRAKE_LEVEL)");
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
        if (!_state.IsA320NeoV2)
        {
            AppendDashboardLog($"{name} blocked: the loaded aircraft is not A320neo V2.");
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
        TimeSpan? timeout = null)
    {
        _pendingNativeAction = new PendingNativeAction(
            name,
            verify,
            desiredOn,
            DateTime.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(8)));
        AppendDashboardLog(
            $"{name} command sent: {desiredOn.ToOnOff()}; awaiting native readback.");
    }

    private void SetExternalPower(bool desiredOn)
    {
        if (_simConnect == null || _state == null)
        {
            Console.Error.WriteLine("External-power procedure blocked: aircraft state is unavailable.");
            FinishOneShot(3);
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
        if (!state.IsA320NeoV2)
        {
            return "the loaded aircraft is not A320neo V2.";
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
            Console.Error.WriteLine(
                $"External power verification failed; aircraft still reports {_state.ExternalPowerOn.ToOnOff()}.");
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
            Console.Error.WriteLine($"Beacon verification failed; aircraft still reports {_state.BeaconOn.ToOnOff()}.");
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
                $"{_pendingNativeAction.Name} verified {_pendingNativeAction.DesiredOn.ToOnOff()}.");
            _pendingNativeAction = null;
            FinishOneShot();
            return;
        }
        if (DateTime.UtcNow >= _pendingNativeAction.DeadlineUtc)
        {
            var message = $"{_pendingNativeAction.Name} native verification failed.";
            AppendDashboardLog(message);
            _pendingNativeAction = null;
            if (_procedureRunner.Status == ProcedureStatus.WaitingForVerification)
            {
                _procedureRunner.Fail(message);
            }
            FinishOneShot(4);
        }
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
            $"Engine start 1 — starter/N1/EGT/fuel: " +
            $"{_state.Engine1StarterActive.ToOnOff()}/{_state.Engine1N1Percent:F1}%/" +
            $"{_state.Engine1EgtCelsius:F0}C/{_state.Engine1FuelFlowPph:F0} pph");
        Console.WriteLine(
            $"Engine start 2 — starter/N1/EGT/fuel: " +
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
            $"After-start configuration — spoilers/flaps/autobrake: " +
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

        Console.WriteLine("Cockpit preparation — electrical power");
        foreach (var step in CockpitPreparationProcedure.Evaluate(_state))
        {
            PrintChecklistItem(step.Label, step.Complete, step.ActionHint);
        }
    }

    private static void PrintChecklistItem(string label, bool complete, string? note = null)
    {
        Console.WriteLine($"[{(complete ? "x" : " ")}] {label}{(note == null || complete ? "" : $" — {note}")}");
    }

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
        Console.WriteLine("Commands: status | phase | checklist | capabilities");
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
        Console.WriteLine("          procedure confirm | procedure pause | procedure resume | procedure cancel");
        Console.WriteLine("          help | quit");
    }

    private void BuildDashboard()
    {
        Width = 920;
        Height = 700;
        MinimumSize = new System.Drawing.Size(780, 580);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = System.Drawing.Color.FromArgb(242, 245, 248);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 7
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var title = new Label
        {
            Text = "iniBuilds A320neo Virtual First Officer",
            AutoSize = true,
            Font = new System.Drawing.Font(Font.FontFamily, 16, System.Drawing.FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10)
        };
        root.Controls.Add(title);

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
            $"{GetApplicationVersion()} — checking GitHub releases...");

        var settingsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 14)
        };
        settingsPanel.Controls.Add(new Label
        {
            Text = "Pilot flying:",
            AutoSize = true,
            Margin = new Padding(0, 7, 4, 0)
        });
        _pilotFlyingBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 120
        };
        _pilotFlyingBox.Items.AddRange(new object[] { CrewRole.Captain, CrewRole.FirstOfficer });
        _pilotFlyingBox.SelectedItem = _settings.PilotFlying;
        _pilotFlyingBox.SelectedIndexChanged += (_, _) =>
        {
            _settings.PilotFlying = (CrewRole)_pilotFlyingBox.SelectedItem;
            SettingsStore.Save(_settings);
        };
        settingsPanel.Controls.Add(_pilotFlyingBox);

        settingsPanel.Controls.Add(new Label
        {
            Text = "Automation:",
            AutoSize = true,
            Margin = new Padding(18, 7, 4, 0)
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
            Text = "Checklist and assistance flow — gate to gate",
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
            IntegralHeight = false
        };
        foreach (var procedure in A320ProcedureLibrary.GateToGate)
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
            RowCount = 6
        };
        procedureLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        procedureLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        procedureLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        procedureLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        procedureLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        procedureLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        procedureGroup.Controls.Add(procedureLayout);

        _procedureLabel = NewDashboardLabel("None");
        _stepLabel = NewDashboardLabel("No active step");
        _messageLabel = NewDashboardLabel("");
        _messageLabel.MaximumSize = new System.Drawing.Size(680, 0);
        _procedureProgress = new ProgressBar { Dock = DockStyle.Top, Height = 22 };
        procedureLayout.Controls.Add(_procedureLabel);
        procedureLayout.Controls.Add(_stepLabel);
        procedureLayout.Controls.Add(_messageLabel);
        procedureLayout.Controls.Add(_procedureProgress);
        _checklistList = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            Font = new System.Drawing.Font("Segoe UI", 9)
        };
        procedureLayout.Controls.Add(_checklistList);

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
        procedureLayout.Controls.Add(procedureButtons);

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

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 14, 0, 0)
        };
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
        root.Controls.Add(actions);
    }

    private void ShowFeatureSettingsDialog()
    {
        using var dialog = new Form
        {
            Text = "Approach schedule and flow chaining",
            Width = 540,
            Height = 470,
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

        var flapAltitude = AddNumber(
            "Flaps 1 maximum indicated altitude",
            _settings.ApproachFlaps1AltitudeFeet, 1000, 20000, "ft");
        var flapSpeed = AddNumber(
            "Flaps 1 target speed",
            _settings.ApproachFlaps1SpeedKnots, 100, 250, "kt");
        var gearAltitude = AddNumber(
            "Gear-down maximum radio altitude",
            _settings.ApproachGearAltitudeAglFeet, 500, 5000, "ft");
        var gearSpeed = AddNumber(
            "Gear-down target speed",
            _settings.ApproachGearSpeedKnots, 100, 250, "kt");
        var landingAltitude = AddNumber(
            "Landing configuration maximum radio altitude",
            _settings.ApproachLandingConfigAltitudeAglFeet, 300, 3000, "ft");
        var landingSpeed = AddNumber(
            "Landing configuration target speed",
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
            "Automatically chain Flows 1 through 9",
            _settings.AutoChainEarlierFlows);
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
            flapAltitude.Value = 10000;
            flapSpeed.Value = 220;
            gearAltitude.Value = 2000;
            gearSpeed.Value = 210;
            landingAltitude.Value = 1200;
            landingSpeed.Value = 185;
            earlierChains.Checked = false;
            flow10Chain.Checked = true;
            flow11Chain.Checked = false;
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

        _settings.ApproachFlaps1AltitudeFeet = (int)flapAltitude.Value;
        _settings.ApproachFlaps1SpeedKnots = (int)flapSpeed.Value;
        _settings.ApproachGearAltitudeAglFeet = (int)gearAltitude.Value;
        _settings.ApproachGearSpeedKnots = (int)gearSpeed.Value;
        _settings.ApproachLandingConfigAltitudeAglFeet = (int)landingAltitude.Value;
        _settings.ApproachLandingConfigSpeedKnots = (int)landingSpeed.Value;
        _settings.AutoChainEarlierFlows = earlierChains.Checked;
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
        _state.ApproachFlaps1AltitudeFeet = _settings.ApproachFlaps1AltitudeFeet;
        _state.ApproachFlaps1SpeedKnots = _settings.ApproachFlaps1SpeedKnots;
        _state.ApproachGearAltitudeAglFeet = _settings.ApproachGearAltitudeAglFeet;
        _state.ApproachGearSpeedKnots = _settings.ApproachGearSpeedKnots;
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
        _electricalLabel!.Text =
            $"BAT 1 {_state.Battery1On.ToOnOff()} | BAT 2 {_state.Battery2On.ToOnOff()} | " +
            $"EXT PWR {_state.ExternalPowerOn.ToOnOff()} ({_state.ExternalPowerAvailable.ToYesNo()} available) | " +
            $"Beacon {_state.BeaconOn.ToOnOff()} | NAV&LOGO " +
            $"{(_state.NavLogoSelectorPosition.HasValue ? FormatNavLogoPosition((int)Math.Round(_state.NavLogoSelectorPosition.Value)) : "UNKNOWN")} | " +
            $"APU {_state.ApuMasterSwitchOn.ToOnOff()}/{_state.ApuRpmPercent:F0}%";
        _adapterLabel!.Text = _mobiFlightReady
            ? "MobiFlight connected"
            : "MobiFlight not connected — iniBuilds controls unavailable";
        _adapterLabel.ForeColor = _mobiFlightReady
            ? System.Drawing.Color.DarkGreen
            : System.Drawing.Color.DarkRed;
        _telemetryLabel!.Text = FormatCurrentStepTelemetry(_state);
        _telemetryLabel.ForeColor = _state.TelemetryIssues.Count == 0
            ? System.Drawing.Color.DarkSlateBlue
            : System.Drawing.Color.DarkRed;

        var definition = _procedureRunner.Definition;
        _procedureLabel!.Text =
            definition == null
                ? "None"
                : $"{definition.Name} — {_procedureRunner.Status} — {definition.AutomationSummary}";
        _stepLabel!.Text =
            _procedureRunner.CurrentStep == null
                ? "No active step"
                : $"Current step: {_procedureRunner.CurrentStep.Label} " +
                  $"({_procedureRunner.CurrentStep.AssignedRole})";
        _messageLabel!.Text = _procedureRunner.Message ?? "";
        _procedureProgress!.Maximum = Math.Max(1, definition?.Steps.Count ?? 1);
        _procedureProgress.Value = Math.Min(
            _procedureProgress.Maximum,
            _procedureRunner.CompletedStepCount);
        RefreshChecklist(definition?.Id);

        var recommendation = FlowRecommendationEngine.Recommend(
            _state,
            _completedProcedureIds);
        _recommendationLabel!.Text =
            $"{recommendation.Procedure.Name} — {recommendation.Reason}";
        _recommendationLabel.ForeColor = recommendation.Overdue
            ? System.Drawing.Color.DarkRed
            : System.Drawing.Color.DarkBlue;
        RefreshFlowList(recommendation.Procedure.Id, definition?.Id);
    }

    private string FormatCurrentStepTelemetry(AircraftState state)
    {
        if (state.TelemetryIssues.Count > 0)
        {
            return "READBACK INCONSISTENT — " +
                   string.Join(" ", state.TelemetryIssues);
        }

        var stepId = _procedureRunner.CurrentStep?.Id;
        var flight =
            $"AGL {state.AltitudeAboveGroundFeet:F0} ft | " +
            $"ALT {state.IndicatedAltitudeFeet:F0} ft | " +
            $"IAS {state.IndicatedAirspeedKnots:F0} kt | " +
            $"VS {state.VerticalSpeedFeetPerMinute:F0} fpm";
        return stepId switch
        {
            "fo-v1" => $"{flight} | target V1 {state.TakeoffV1SpeedKnots} kt",
            "fo-rotate" => $"{flight} | target VR {state.TakeoffRotateSpeedKnots} kt",
            "approach-config-start" =>
                $"{flight} | trigger ≤{state.ApproachFlaps1AltitudeFeet:N0} ft indicated and ≤{state.ApproachFlaps1SpeedKnots} kt",
            "gear-down-point" =>
                $"{flight} | trigger ≤{state.ApproachGearAltitudeAglFeet:N0} ft AGL and ≤{state.ApproachGearSpeedKnots} kt",
            "landing-config-point" =>
                $"{flight} | trigger ≤{state.ApproachLandingConfigAltitudeAglFeet:N0} ft AGL and ≤{state.ApproachLandingConfigSpeedKnots} kt",
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
                    $"{GetApplicationVersion()} — no GitHub release published";
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
                    $"{GetApplicationVersion()} — release status unavailable";
                return;
            }

            var current =
                Assembly.GetExecutingAssembly().GetName().Version
                ?? new Version();
            _versionLabel.Text = latest > current
                ? $"{GetApplicationVersion()} — update available: {latest}"
                : $"{GetApplicationVersion()} — up to date";
            _versionLabel.ForeColor = latest > current
                ? System.Drawing.Color.DarkOrange
                : System.Drawing.Color.DarkGreen;
        }
        catch (Exception ex)
        {
            _versionLabel.Text =
                $"{GetApplicationVersion()} — update check unavailable";
            AppLog.Write($"GitHub update check failed: {ex.Message}");
        }
    }

    private void RefreshChecklist(string? procedureId)
    {
        if (_checklistList == null || _state == null)
        {
            return;
        }

        _checklistList.BeginUpdate();
        _checklistList.Items.Clear();
        var checklist = procedureId == null
            ? null
            : A320ChecklistLibrary.FindForProcedure(procedureId);
        if (checklist == null)
        {
            _checklistList.Items.Add("Select or start a flow to view its verification checklist.");
            _checklistList.EndUpdate();
            return;
        }

        _checklistList.Items.Add(checklist.Name);
        foreach (var item in checklist.Items)
        {
            var result = item.Verify(_state);
            var marker = result == true ? "✓" : result == false ? "✗" : "?";
            _checklistList.Items.Add(
                $"{marker}  {item.Challenge,-24} {item.ExpectedResponse}");
        }
        _checklistList.EndUpdate();
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
        for (var index = 0; index < A320ProcedureLibrary.GateToGate.Count; index++)
        {
            var procedure = A320ProcedureLibrary.GateToGate[index];
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
        while (_flowList.Items.Count > A320ProcedureLibrary.GateToGate.Count)
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
            DateTime deadlineUtc)
        {
            Name = name;
            Verify = verify;
            DesiredOn = desiredOn;
            DeadlineUtc = deadlineUtc;
        }

        public string Name { get; }
        public Func<AircraftState, bool> Verify { get; }
        public bool DesiredOn { get; }
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
            string selectorLVar,
            string pressLVar)
        {
            Number = number;
            SelectorLVar = selectorLVar;
            PressLVar = pressLVar;
        }

        public int Number { get; }
        public string SelectorLVar { get; }
        public string PressLVar { get; }
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

        public override string ToString() =>
            $"{(Completed ? "✓" : Active ? "▶" : Recommended ? "→" : " ")} " +
            $"{Definition.Name} — {Definition.AutomationSummary}";
    }
}

internal static class DisplayExtensions
{
    public static string ToOnOff(this bool value) => value ? "ON" : "OFF";
    public static string ToYesNo(this bool value) => value ? "YES" : "NO";
    public static string ToSetReleased(this bool value) => value ? "SET" : "RELEASED";
}
