using Microsoft.FlightSimulator.SimConnect;
using Msfs2024Ai.Copilot.AircraftAdapters;
using Msfs2024Ai.Copilot.AircraftAdapters.FbwA320;
using Msfs2024Ai.Copilot.AircraftAdapters.IniBuildsA320;
using Msfs2024Ai.Copilot.AircraftAdapters.IniBuildsA321;
using Msfs2024Ai.Copilot.AircraftAdapters.IniBuildsA330;
using Msfs2024Ai.Copilot.AircraftAdapters.IniBuildsA310;
using Msfs2024Ai.Copilot.AircraftAdapters.Pmdg777;
using Msfs2024Ai.Copilot.Automation;
using Msfs2024Ai.Copilot.AircraftIdentity;
using Msfs2024Ai.Copilot.Checklists;
using Msfs2024Ai.Copilot.Controls;
using Msfs2024Ai.Copilot.Diagnostics;
using Msfs2024Ai.Copilot.Domain;
using Msfs2024Ai.Copilot.Efb;
using Msfs2024Ai.Copilot.AircraftAdapters.Asobo737Max;
using Msfs2024Ai.Copilot.Gsx;
using Msfs2024Ai.Copilot.Procedures;
using Msfs2024Ai.Copilot.Settings;
using Msfs2024Ai.Copilot.SayIntentions;
using Msfs2024Ai.Copilot.SimBrief;
using Msfs2024Ai.Copilot.Simulation;
using Msfs2024Ai.Copilot.Telemetry;
using Msfs2024Ai.Copilot.Voice;
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
    private const uint ThirdPartyEventIdMin = 0x00011000;
    private const uint PmdgMouseRightSingle = 0x80000000;
    private const uint PmdgMouseLeftSingle = 0x20000000;
    private const double PmdgCenterFuelPumpRequiredThresholdPounds = 500;
    private readonly EfbCompanionTransport _efbTransport = new();
    private readonly string? _oneShotCommand;
    private readonly bool _showUi;
    private readonly CopilotSettings _settings;
    private readonly ProcedureSession _procedureSession;
    private readonly CockpitAutomationScheduler _automation;
    private readonly ProcedureRunner _procedureRunner;
    private VoiceCalloutQueue? _voiceCalloutQueue;
    private readonly SayIntentionsClient _sayIntentionsClient = new();
    private readonly GsxInstallation? _gsxInstallation = GsxInstallation.Discover();
    private readonly GsxIntegrationController _gsx;
    private GsxFileReader? _gsxFileReader;
    private ProcedureDefinition? _pendingGsxEngineStartProcedure;
    private readonly SayIntentionsRuntimeState _sayIntentionsRuntime = new();
    private Form? _gsxChoiceDialog;
    private readonly object _sayIntentionsVoiceQueueSync = new();
    private Task _sayIntentionsVoiceTail = Task.CompletedTask;
    private int _sayIntentionsIntercomReceivingMask;
    private int _sayIntentionsIntercomSignalObserved;
    private readonly SemaphoreSlim _sayIntentionsCommsModeGate = new(1, 1);
    private readonly CancellationTokenSource _sayIntentionsCancellation = new();
    private bool _disposingOrDisposed;
    private System.Windows.Forms.Timer? _sayIntentionsTimer;
    private bool _sayIntentionsRefreshInProgress;
    private string? _pendingSayIntentionsAtcStepId;
    private long _pendingSayIntentionsAtcBaselineId;
    private DateTime? _pendingSayIntentionsAtcStartedUtc;
    private bool _taxiClearanceReceived;
    private bool _takeoffClearanceReceived;
    private bool _pmdg777TaxiLightsCommandedThisFlow;
    private readonly Pmdg777RuntimeState _pmdg777Runtime = new();
    private readonly FlightTelemetryStore _flightTelemetryStore;
    private readonly AircraftIdentityResolver _aircraftIdentityResolver = new();
    private System.Windows.Forms.Timer? _replayTimer;
    private IReadOnlyList<AircraftState> _replayStates = Array.Empty<AircraftState>();
    private int _replayIndex;
    private bool _replayActive;
    private readonly HashSet<string> _completedProcedureIds =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _calloutsSpokenAtCommand =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _forwardTaxiObservedThisFlight;
    private bool _taxiToRunwayArmed;
    private bool _pendingAutomaticBeforeTakeoffFlow;
    private bool _pendingAutomaticTakeoffFlow;
    private readonly SimConnectSessionManager _simConnectSession;
    private SimConnect? Connection => _simConnectSession.Connection;
    private AircraftState? _state;
    private readonly NativeAircraftRuntimeState _nativeRuntime = new();
    private PendingExternalPowerProcedure? _pendingProcedure;
    private PendingBeaconProcedure? _pendingBeaconProcedure;
    private PendingNavLogoSelectorProcedure? _pendingNavLogoSelectorProcedure;
    private PendingBatteryProcedure? _pendingBatteryProcedure;
    private PendingNativeAction? _pendingNativeAction;
    private PendingFireTest? _pendingFireTest;
    private FireTestSystem? _pendingFlyByWireFireTest;
    private PendingFuelPumpSequence? _pendingFuelPumpSequence;
    private System.Windows.Forms.Timer? _fuelPumpSequenceTimer;
    private System.Windows.Forms.Timer? _commandTimer;
    private System.Windows.Forms.Timer? _a330InputEventPollingTimer;
    private readonly MobiFlightAdapterSession _mobiFlightSession = new();
    private readonly PmdgNg3RuntimeState _pmdgNg3Runtime = new();
    private bool _pmdg777SdkInitialized;
    private readonly Queue<(uint EventId, uint Parameter, string Label)> _pmdg777ControlQueue = new();
    private System.Windows.Forms.Timer? _pmdg777ControlQueueTimer;
    private GenerationBoundCockpitAction? _pmdg777ControlQueueAction;
    private bool? _loggedPmdg777GenericBattery;
    private System.Windows.Forms.Timer? _pmdg777AdiruOnTimer;
    private double? _lastLoggedA380ExternalPowerDirectSignature;
    private double? _lastLoggedA380AcPowerSignature;
    private double? _lastLoggedIniBuildsIgnitionKnob;
    private double? _lastLoggedIniBuildsTurnoffLightSwitch;
    private double? _lastLoggedA310Engine1IgnitionSwitch;
    private double? _lastLoggedA310Engine2IgnitionSwitch;
    private double? _lastLoggedBattery1Voltage;
    private double? _lastLoggedBattery2Voltage;
    private readonly Asobo737MaxRuntimeState _asobo737MaxRuntime = new();
    private bool _a310InputEventsEnumerated;
    private bool _asobo737MaxFireTestsInProgress;
    private const ulong Asobo737MaxApuBleedInputEventHash = 12724114040502922703UL;
    private const ulong Asobo737MaxIsolationValveInputEventHash = 16328424800018055689UL;
    private const ulong Asobo737MaxLeftPackInputEventHash = 8444549763148178477UL;
    private const ulong Asobo737MaxRightPackInputEventHash = 13421230506292110701UL;
    private static readonly ulong[] Asobo737MaxEngineBleedInputEventHashes =
    {
        9471077644541106401UL,
        4927259929824871252UL
    };
    private static readonly ulong[] Asobo737MaxEngineGeneratorInputEventHashes =
    {
        1520419289578202539UL,
        15134557057728626206UL
    };
    private static readonly ulong[] Asobo737MaxElectricHydraulicPumpInputEventHashes =
    {
        8722400555850582198UL,
        2874449211756725945UL
    };
    private const ulong Asobo737MaxTaxiLightInputEventHash = 4631196187589075821UL;
    private static readonly ulong[] Asobo737MaxRunwayTurnoffInputEventHashes =
    {
        5474553189875403266UL,
        8318312309918504874UL
    };
    private static readonly ulong[] Asobo737MaxLandingLightInputEventHashes =
    {
        15095974817856027149UL,
        16858870040215037861UL
    };
    private const ulong Asobo737MaxAntiCollisionInputEventHash = 15622846362031305197UL;
    private const ulong Asobo737MaxExternalPowerInputEventHash = 10160635751565732413UL;
    private const ulong Asobo737MaxFlapsInputEventHash = 13998713293320135111UL;
    private const ulong Asobo737MaxAutobrakeInputEventHash = 7142783048944440595UL;
    private const ulong Asobo737MaxAutothrottleInputEventHash = 8838131474811569287UL;
    private const ulong Asobo737MaxLnavInputEventHash = 5252030825318025405UL;
    private const ulong Asobo737MaxVnavInputEventHash = 7249118735569564221UL;
    private const ulong Asobo737MaxTransponderModeInputEventHash = 1428665342216455960UL;
    private const ulong Asobo737MaxTransponderOperatingModeInputEventHash = 15256658846979713216UL;
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
    private static readonly ulong[] A330AdirsInputEventHashes =
    {
        13492439889652946135UL, // AIRLINER_ADIRS1_MODE
        16561688374715259608UL, // AIRLINER_ADIRS2_MODE
        1287651589091488428UL   // AIRLINER_ADIRS3_MODE
    };
    private const ulong A330StrobeInputEventHash = 10028340691099543317UL;
    private const ulong A330NavLogoInputEventHash = 10348631634011558414UL;
    private static readonly ulong[] A330ApuInputEventHashes =
    {
        4080745756015573070UL, // AIRLINER_APU_MASTER
        9344724743939237602UL, // AIRLINER_APU_START
        8638866639146676618UL  // AIRLINER_AIR_APU_BLEED
    };
    private const ulong A310ApuMasterInputEventHash = 2081785778858810395UL; // AIRLINER_APU_MASTERSWITCH
    private const ulong A310ApuStartInputEventHash = 9344724743939237602UL; // AIRLINER_APU_START
    private const ulong A310ApuGeneratorInputEventHash = 11164808654641869089UL; // AIRLINER_APU_GEN
    private const ulong A310ApuBleedInputEventHash = 3757581603635492448UL; // AIRLINER_APU_BLEEDSWITCH
    private const ulong A330TransponderModeInputEventHash = 14182293921746398447UL;
    private const ulong A330CrewOxygenInputEventHash = 8814143036634973369UL;
    private const ulong A330SpoilerLeverInputEventHash = 1712305263919831311UL;
    private const ulong A330FlapsInputEventHash = 10630178068256299397UL;
    private static readonly ulong[] A330AutobrakeInputEventHashes =
    {
        7289021414699629450UL,  // AIRLINER_AUTOBRK_LO
        3008453113287741137UL,  // AIRLINER_AUTOBRK_MED
        10376295413381294961UL  // AIRLINER_AUTOBRK_HI
    };
    private const ulong A330WeatherRadarPwsInputEventHash = 16710120045550625168UL;
    private const ulong A330NoseLightInputEventHash = 7704909914815877606UL;
    private const ulong A330LandingLightInputEventHash = 6747014075822747692UL;
    private const ulong A330TcasTrafficInputEventHash = 11751227568307765711UL;
    private const ulong A330TcasAltitudeInputEventHash = 8240611082898456697UL;
    private const ulong Asobo737MaxOverheatDetectorTestInputEventHash = 13636199590092324351UL;
    private const ulong Asobo737MaxExtinguisherTestInputEventHash = 12566998620356730068UL;
    private const ulong Asobo737MaxProcedureFireWarningInputEventHash = 9944065992900094384UL;
    private const ulong Asobo737MaxCargoFireTestInputEventHash = 2235982354068596994UL;
    private bool _apuFireTestCompleted;
    private bool _engine1FireTestCompleted;
    private bool _engine2FireTestCompleted;
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
    private Label? _simBriefStatusLabel;
    private Label? _sayIntentionsStatusLabel;
    private Label? _gsxStatusLabel;
    private Label? _gsxLiveSummaryLabel;
    private Label? _gsxLiveActionLabel;
    private Label? _gsxPassengerLabel;
    private ProgressBar? _gsxPassengerProgress;
    private Button? _manageGsxButton;
    private Label? _simBadgeLabel;
    private Label? _aircraftBadgeLabel;
    private Label? _adapterBadgeLabel;
    private Label? _flowBadgeLabel;
    private Label? _versionBadgeLabel;
    private Label? _simBriefBadgeLabel;
    private Label? _sayIntentionsBadgeLabel;
    private Label? _gsxBadgeLabel;
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
    private Label? _stepProgressLabel;
    private ProgressBar? _procedureProgress;
    private ComboBox? _automationPolicyBox;
    private NumericUpDown? _transitionAltitudeBox;
    private NumericUpDown? _takeoffV1Box;
    private NumericUpDown? _takeoffRotateBox;
    private NumericUpDown? _takeoffV2Box;
    private CheckBox? _voiceCalloutsBox;
    private ComboBox? _calloutDetailBox;
    private CheckBox? _sayIntentionsVoiceBox;
    private ComboBox? _replayFlightBox;
    private ListBox? _eventLog;
    private ListBox? _flowList;
    private Button? _startFirstFlowButton;
    private Button? _startSelectedFlowButton;
    private Button? _confirmCompletedButton;
    private bool _sayIntentionsHandoffInProgress;
    private ImportedFlightPlan? _simBriefFlightPlan;
    private bool _simBriefImportInProgress;

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

    public CopilotService(string? oneShotCommand, bool showUi)
    {
        _oneShotCommand = oneShotCommand;
        _showUi = showUi;
        _settings = SettingsStore.Load();
        _flightTelemetryStore = new FlightTelemetryStore();
        _procedureSession = ProcedureSessionStore.Load();
        _simConnectSession = new SimConnectSessionManager(
            () => new SimConnectSessionConnection(
                "MSFS 2024 Virtual First Officer",
                Handle,
                WmUserSimConnect),
            RegisterSimConnectHandlers);
        _simConnectSession.Connected += HandleSimConnectOpened;
        _simConnectSession.Disconnected += HandleSimConnectDisconnected;
        _simConnectSession.SimConnectException += HandleSimConnectException;
        _simConnectSession.ConnectionFailed += OnConnectionFailed;
        _automation = new CockpitAutomationScheduler(
            runtimeAvailable: () => !_disposingOrDisposed
                                    && Connection != null
                                    && _state != null,
            currentVariant: () => _state?.Variant,
            log: AppLog.Write,
            delayedActionCompleted: () => FinishOneShot());
        _gsx = new GsxIntegrationController(
            new GsxRuntimeEffects(
                setRemoteControl: enabled =>
                {
                    try
                    {
                        SetGsxValue(
                            Definition.GsxRemoteControl,
                            enabled ? 1 : 0);
                    }
                    catch (COMException exception) when (!enabled)
                    {
                        AppLog.Write(
                            $"Could not release GSX Remote Control: {exception.Message}");
                    }
                },
                requestMenuOpen: delay =>
                {
                    if (delay <= TimeSpan.Zero)
                    {
                        SetGsxValue(Definition.GsxMenuOpen, 1);
                        return;
                    }

                    _automation.Schedule(
                        (int)delay.TotalMilliseconds,
                        () => SetGsxValue(Definition.GsxMenuOpen, 1),
                        "GSX menu request",
                        _state?.Variant);
                },
                sendMenuChoice: choice => SetGsxValue(
                    Definition.GsxMenuChoice,
                    choice),
                log: AppLog.Write,
                dashboardLog: AppendDashboardLog,
                sendCommandResult: SendEfbCommandResult));
        _simBriefFlightPlan = _procedureSession.ActiveFlightPlan;
        if (SimBriefOperationalContext.ApplyTakeoffSettings(
                _procedureSession.ActiveFlightPlan,
                _settings))
        {
            SettingsStore.Save(_settings);
        }
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
                _automation.Enqueue(command);
            },
            () => _settings.AutomationPolicy);
        _procedureRunner.Changed += OnProcedureChanged;
        _procedureRunner.StepCompleted += SpeakProcedureCallout;
        _procedureRunner.StepCompleted += OnProcedureStepCompleted;
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
            Shown += async (_, _) =>
            {
                StartSayIntentionsMonitoring();
                await RefreshSayIntentionsStatusAsync();
                await CheckForUpdatesAsync();
            };
        }
    }

    public void Connect()
    {
        _simConnectSession.Connect();
    }

    private void RegisterSimConnectHandlers(SimConnect connection)
    {
        connection.OnRecvSimobjectData += OnAircraftData;
        connection.OnRecvClientData += OnClientData;
        connection.OnRecvEnumerateInputEvents += OnEnumerateInputEvents;
        connection.OnRecvGetInputEvent += OnGetInputEvent;
        connection.OnRecvEvent += OnGsxEvent;
        connection.OnRecvCommBus += OnEfbCommBusEvent;
    }

    private void OnConnectionFailed(COMException exception)
    {
        Console.Error.WriteLine($"Could not connect to SimConnect: {exception.Message}");
        AppLog.Write($"SimConnect connection failed: {exception}");
        if (_connectionLabel != null)
        {
            _connectionLabel.Text = "Waiting for MSFS SimConnect...";
            _connectionLabel.ForeColor = System.Drawing.Color.DarkRed;
        }
        AppendDashboardLog("SimConnect unavailable; retrying in 5 seconds.");
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmUserSimConnect)
        {
            _simConnectSession.ReceivePendingMessage();
        }

        base.WndProc(ref message);
    }

    private void HandleSimConnectOpened(SIMCONNECT_RECV_OPEN data)
    {
        var sender = Connection
                     ?? throw new InvalidOperationException(
                         "SimConnect opened without an active connection.");
        InvalidateAircraftAutomation(
            AutomationInvalidationReason.NewSimConnectSession);
        ResetMobiFlightRuntimeAfterDisconnect();
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

        SimConnectRegistrationService.RegisterCore(sender);
        InitializeGsxProtocol(sender);
        InitializeMobiFlight(sender);
        InitializePmdgNg3Sdk(sender);
        SimConnectRegistrationService.RegisterEfb(sender);
        AppLog.Write(
            "MSFS EFB companion CommBus command bridge ready "
            + $"(event {EfbCompanionProtocol.CommandEventName}, "
            + $"send ID {sender.GetLastSentPacketID()}).");

        SimConnectRegistrationService.RegisterCoreRequests(sender);

        StartConnectionTimers(sender);
    }

    private void StartConnectionTimers(SimConnect sender)
    {
        if (_commandTimer == null)
        {
            _commandTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _commandTimer.Tick += (_, _) => DrainCommands();
        }
        _commandTimer.Start();

        _a330InputEventPollingTimer?.Stop();
        _a330InputEventPollingTimer?.Dispose();
        _nativeRuntime.ResetA330AutobrakeReadback();
        _a330InputEventPollingTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _a330InputEventPollingTimer.Tick += (_, _) =>
        {
            if (_state?.IsAsobo737Max8 == true)
            {
                try
                {
                    if (_asobo737MaxRuntime.BatteryCoverInputEventHash.HasValue)
                    {
                        sender.GetInputEvent(
                            Request.Asobo737MaxBatteryCoverInputEvent,
                            _asobo737MaxRuntime.BatteryCoverInputEventHash.Value);
                    }
                    if (_asobo737MaxRuntime.BatteryInputEventHash.HasValue)
                    {
                        sender.GetInputEvent(
                            Request.Asobo737MaxBatteryInputEvent,
                            _asobo737MaxRuntime.BatteryInputEventHash.Value);
                    }
                    if (_asobo737MaxRuntime.LeftIrsInputEventHash.HasValue)
                    {
                        sender.GetInputEvent(
                            Request.Asobo737MaxLeftIrsInputEvent,
                            _asobo737MaxRuntime.LeftIrsInputEventHash.Value);
                    }
                    if (_asobo737MaxRuntime.RightIrsInputEventHash.HasValue)
                    {
                        sender.GetInputEvent(
                            Request.Asobo737MaxRightIrsInputEvent,
                            _asobo737MaxRuntime.RightIrsInputEventHash.Value);
                    }
                    if (_asobo737MaxRuntime.PositionLightInputEventHash.HasValue)
                    {
                        sender.GetInputEvent(
                            Request.Asobo737MaxPositionLightInputEvent,
                            _asobo737MaxRuntime.PositionLightInputEventHash.Value);
                    }
                    if (_asobo737MaxRuntime.LogoLightInputEventHash.HasValue)
                    {
                        sender.GetInputEvent(
                            Request.Asobo737MaxLogoLightInputEvent,
                            _asobo737MaxRuntime.LogoLightInputEventHash.Value);
                    }
                    if (_asobo737MaxRuntime.EmergencyExitInputEventHash.HasValue)
                    {
                        sender.GetInputEvent(
                            Request.Asobo737MaxEmergencyExitInputEvent,
                            _asobo737MaxRuntime.EmergencyExitInputEventHash.Value);
                    }
                    if (_asobo737MaxRuntime.EmergencyExitCoverInputEventHash.HasValue)
                    {
                        sender.GetInputEvent(
                            Request.Asobo737MaxEmergencyExitCoverInputEvent,
                            _asobo737MaxRuntime.EmergencyExitCoverInputEventHash.Value);
                    }
                    if (_asobo737MaxRuntime.SeatbeltsInputEventHash.HasValue)
                    {
                        sender.GetInputEvent(
                            Request.Asobo737MaxSeatbeltsInputEvent,
                            _asobo737MaxRuntime.SeatbeltsInputEventHash.Value);
                    }
                    if (_asobo737MaxRuntime.NoSmokingInputEventHash.HasValue)
                    {
                        sender.GetInputEvent(
                            Request.Asobo737MaxNoSmokingInputEvent,
                            _asobo737MaxRuntime.NoSmokingInputEventHash.Value);
                    }
                    if (_asobo737MaxRuntime.ApuInputEventHash.HasValue)
                    {
                        sender.GetInputEvent(
                            Request.Asobo737MaxApuInputEvent,
                            _asobo737MaxRuntime.ApuInputEventHash.Value);
                    }
                    if (_asobo737MaxRuntime.ApuBleedInputEventHash.HasValue)
                    {
                        sender.GetInputEvent(
                            Request.Asobo737MaxApuBleedInputEvent,
                            _asobo737MaxRuntime.ApuBleedInputEventHash.Value);
                    }
                    sender.GetInputEvent(Request.Asobo737MaxIsolationValveInputEvent, Asobo737MaxIsolationValveInputEventHash);
                    sender.GetInputEvent(Request.Asobo737MaxLeftPackInputEvent, Asobo737MaxLeftPackInputEventHash);
                    sender.GetInputEvent(Request.Asobo737MaxRightPackInputEvent, Asobo737MaxRightPackInputEventHash);
                    sender.GetInputEvent(Request.Asobo737MaxEngineBleed1InputEvent, Asobo737MaxEngineBleedInputEventHashes[0]);
                    sender.GetInputEvent(Request.Asobo737MaxEngineBleed2InputEvent, Asobo737MaxEngineBleedInputEventHashes[1]);
                    sender.GetInputEvent(Request.Asobo737MaxEngineGenerator1InputEvent, Asobo737MaxEngineGeneratorInputEventHashes[0]);
                    sender.GetInputEvent(Request.Asobo737MaxEngineGenerator2InputEvent, Asobo737MaxEngineGeneratorInputEventHashes[1]);
                    sender.GetInputEvent(Request.Asobo737MaxElectricHydraulicPump1InputEvent, Asobo737MaxElectricHydraulicPumpInputEventHashes[0]);
                    sender.GetInputEvent(Request.Asobo737MaxElectricHydraulicPump2InputEvent, Asobo737MaxElectricHydraulicPumpInputEventHashes[1]);
                    sender.GetInputEvent(Request.Asobo737MaxTaxiLightInputEvent, Asobo737MaxTaxiLightInputEventHash);
                    sender.GetInputEvent(Request.Asobo737MaxRunwayTurnoffLeftInputEvent, Asobo737MaxRunwayTurnoffInputEventHashes[0]);
                    sender.GetInputEvent(Request.Asobo737MaxRunwayTurnoffRightInputEvent, Asobo737MaxRunwayTurnoffInputEventHashes[1]);
                    sender.GetInputEvent(Request.Asobo737MaxLandingLightLeftInputEvent, Asobo737MaxLandingLightInputEventHashes[0]);
                    sender.GetInputEvent(Request.Asobo737MaxLandingLightRightInputEvent, Asobo737MaxLandingLightInputEventHashes[1]);
                    sender.GetInputEvent(Request.Asobo737MaxAntiCollisionInputEvent, Asobo737MaxAntiCollisionInputEventHash);
                    sender.GetInputEvent(Request.Asobo737MaxFlapsInputEvent, Asobo737MaxFlapsInputEventHash);
                    sender.GetInputEvent(Request.Asobo737MaxAutobrakeInputEvent, Asobo737MaxAutobrakeInputEventHash);
                    sender.GetInputEvent(Request.Asobo737MaxAutothrottleInputEvent, Asobo737MaxAutothrottleInputEventHash);
                    sender.GetInputEvent(
                        Request.Asobo737MaxTransponderOperatingModeInputEvent,
                        Asobo737MaxTransponderOperatingModeInputEventHash);
                    sender.GetInputEvent(
                        Request.Asobo737MaxTransponderModeInputEvent,
                        Asobo737MaxTransponderModeInputEventHash);
                    for (var index = 0; index < _asobo737MaxRuntime.ApuGeneratorInputEventHashes.Count; index++)
                    {
                        if (_asobo737MaxRuntime.ApuGeneratorInputEventHashes[index].HasValue)
                        {
                            sender.GetInputEvent(
                                (Request)((int)Request.Asobo737MaxApuGenerator1InputEvent + index),
                                _asobo737MaxRuntime.ApuGeneratorInputEventHashes[index]!.Value);
                        }
                    }
                    for (var index = 0; index < _asobo737MaxRuntime.FuelPumpInputEventHashes.Count; index++)
                    {
                        if (_asobo737MaxRuntime.FuelPumpInputEventHashes[index].HasValue)
                        {
                            sender.GetInputEvent(
                                (Request)((int)Request.Asobo737MaxFuelPump1InputEvent + index),
                                _asobo737MaxRuntime.FuelPumpInputEventHashes[index]!.Value);
                        }
                    }
                    if (!_asobo737MaxRuntime.InputEventsEnumerated)
                    {
                        sender.EnumerateInputEvents(Request.Asobo737MaxEnumerateInputEvents);
                    }
                }
                catch (Exception exception)
                {
                    AppLog.Write($"Asobo 737 MAX InputEvent poll failed: {exception.Message}");
                }

                return;
            }

            if (_state?.IsIniBuildsA310 == true)
            {
                try
                {
                    sender.GetInputEvent(Request.A310ApuMasterInputEvent, A310ApuMasterInputEventHash);
                    sender.GetInputEvent(Request.A310ApuStartInputEvent, A310ApuStartInputEventHash);
                    sender.GetInputEvent(Request.A310ApuGeneratorInputEvent, A310ApuGeneratorInputEventHash);
                    sender.GetInputEvent(Request.A310ApuBleedInputEvent, A310ApuBleedInputEventHash);
                }
                catch (Exception exception)
                {
                    AppLog.Write($"A310 APU InputEvent poll failed: {exception.Message}");
                }
                if (!_a310InputEventsEnumerated)
                {
                    try
                    {
                        sender.EnumerateInputEvents(Request.A310EnumerateInputEvents);
                    }
                    catch (Exception exception)
                    {
                        AppLog.Write($"A310 InputEvent enumeration failed: {exception.Message}");
                    }
                }
                return;
            }

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
                sender.GetInputEvent(Request.A330LandingLightInputEvent, A330LandingLightInputEventHash);
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

    private void InitializeGsxProtocol(SimConnect sender)
    {
        if (_gsxInstallation == null)
        {
            return;
        }

        SimConnectRegistrationService.RegisterGsx(sender);

        AppLog.Write(
            "GSX protocol registered in passive mode; Remote Control has not been claimed.");
    }

    private void OnGsxEvent(SimConnect sender, SIMCONNECT_RECV_EVENT data)
    {
        if (_gsxInstallation == null || !_settings.EnableGsxIntegration)
        {
            return;
        }

        switch ((CopilotEvent)data.uEventID)
        {
            case CopilotEvent.GsxExternalSystemToggle:
                HandleGsxMenuEvent(data.dwData);
                break;
            case CopilotEvent.GsxExternalSystemSet:
                var tooltip = _gsxFileReader?.ReadTooltip()
                              ?? Array.Empty<string>();
                var menuInvalidated = _gsx.OnStatusEvent(
                    data.dwData,
                    tooltip,
                    DateTime.UtcNow,
                    _state != null && BothEnginesStartStabilized(_state));
                if (menuInvalidated || !_gsx.Snapshot.MenuOpen)
                {
                    CloseGsxChoiceDialog();
                }
                UpdateGsxStatus();
                UpdateDashboard();
                PublishEfbState();
                break;
        }
    }

    private void HandleGsxMenuEvent(uint eventData)
    {
        switch (eventData)
        {
            case 1:
                // A new GSX question supersedes any dialog created for the
                // previous menu. This helper clears the tracked dialog before
                // closing it, so the old question is not sent as cancelled.
                CloseGsxChoiceDialog();
                var menu = _gsxFileReader?.ReadMenu()
                           ?? new GsxMenuSnapshot(
                               string.Empty,
                               Array.Empty<string>());
                if (_gsx.OnMenuOpened(menu, DateTime.UtcNow))
                {
                    CloseGsxChoiceDialog();
                    UpdateDashboard();
                    PublishEfbState(force: true);
                    break;
                }
                TryAutoSelectGsxArrivalStand();
                if (TryAutoAcceptGsxAttachPushbackTug())
                {
                    UpdateDashboard();
                    PublishEfbState(force: true);
                    break;
                }
                if (TryAutoContinueGsxPushback())
                {
                    UpdateDashboard();
                    PublishEfbState(force: true);
                    break;
                }
                TryAutoSelectSayIntentionsPushbackDirection();
                var snapshot = _gsx.Snapshot;
                if (snapshot.MenuOpen)
                {
                    if (TryAutoConfirmGsxGoodEngineStart())
                    {
                        break;
                    }
                    if (GsxPromptPolicy.IsRootServicesMenu(snapshot.CurrentMenu))
                    {
                        AppLog.Write(
                            "GSX root services menu detected; leaving it informational.");
                    }
                    else
                    {
                        ShowGsxChoiceDialog(snapshot.CurrentMenu);
                    }
                }
                UpdateDashboard();
                PublishEfbState(force: true);
                break;
            case 2:
                // The official GSX Remote Control sample makes a hidden menu
                // non-selectable. Never retain it as an actionable EFB prompt:
                // Couatl will reject choices submitted after this event.
                if (_gsx.OnMenuHidden()
                    == GsxMenuHiddenResult.UnansweredMenuClosed)
                {
                    CloseGsxChoiceDialog();
                    UpdateDashboard();
                    PublishEfbState(force: true);
                }
                break;
            case 3:
                _gsx.OnMenuCancelledOrTimedOut();
                CloseGsxChoiceDialog();
                UpdateDashboard();
                PublishEfbState(force: true);
                break;
            case 4:
                // Closing the stock toolbar panel does not cancel a question
                // delegated to the registered Remote Control client.
                _gsx.OnToolbarPanelClosed();
                break;
        }
    }

    private void ShowGsxChoiceDialog(GsxMenuSnapshot menu)
    {
        if (!_showUi || menu.IsEmpty || _gsxChoiceDialog != null)
        {
            return;
        }

        var dialog = new Form
        {
            Text = "GSX response required",
            Width = 620,
            Height = 430,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false
        };
        _gsxChoiceDialog = dialog;
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 4
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        dialog.Controls.Add(layout);
        layout.Controls.Add(new Label
        {
            Text = menu.Title,
            AutoSize = true,
            Font = new System.Drawing.Font(
                Font.FontFamily,
                12,
                System.Drawing.FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        });
        layout.Controls.Add(new Label
        {
            Text = "GSX is waiting for a response. Select the required option to continue the active service.",
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(560, 0),
            ForeColor = System.Drawing.Color.DimGray,
            Margin = new Padding(0, 0, 0, 10)
        });
        var choices = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false
        };
        foreach (var choice in menu.Choices)
        {
            choices.Items.Add(choice);
        }
        if (choices.Items.Count > 0)
        {
            choices.SelectedIndex = 0;
        }
        layout.Controls.Add(choices);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 12, 0, 0)
        };
        var send = new Button
        {
            Text = "Send selection to GSX",
            AutoSize = true,
            BackColor = System.Drawing.Color.FromArgb(39, 130, 87),
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false
        };
        var cancel = new Button { Text = "Cancel GSX prompt", AutoSize = true };
        send.Click += (_, _) =>
        {
            if (choices.SelectedIndex < 0)
            {
                return;
            }

            RequestGsxMenuChoice(
                choices.SelectedIndex,
                choices.SelectedItem?.ToString() ?? string.Empty,
                null);
            CloseGsxChoiceDialog();
        };
        cancel.Click += (_, _) =>
        {
            _gsx.CancelMenu(DateTime.UtcNow);
            dialog.Close();
        };
        buttons.Controls.Add(send);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons);
        dialog.FormClosing += (_, _) =>
        {
            if (_gsx.Snapshot.MenuOpen
                && ReferenceEquals(_gsxChoiceDialog, dialog))
            {
                _gsx.CancelMenu(DateTime.UtcNow);
            }
        };
        dialog.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_gsxChoiceDialog, dialog))
            {
                _gsxChoiceDialog = null;
            }
        };
        AppendDashboardLog($"GSX response required: {menu.Title}.");
        dialog.Show(this);
        dialog.Activate();
    }

    private void SendGsxMenuChoice(int choice, string? label)
    {
        _gsx.SendChoiceWithoutAcknowledgement(
            choice,
            label,
            DateTime.UtcNow);
    }

    private void RequestGsxMenuChoice(
        int choice,
        string label,
        string? requestId)
    {
        var pendingBefore = _gsx.Snapshot.PendingChoice;
        _gsx.RequestMenuChoice(
            choice,
            label,
            requestId,
            DateTime.UtcNow);
        if (!pendingBefore && _gsx.Snapshot.PendingChoice)
        {
            PublishEfbState(force: true);
        }
    }

    private void SubmitLiveGsxChoice(
        int choice,
        string label,
        string? requestId)
    {
        _gsx.SubmitLiveChoice(
            choice,
            label,
            requestId,
            DateTime.UtcNow);
    }

    private bool TryAutoConfirmGsxGoodEngineStart()
    {
        if (_state == null)
        {
            return false;
        }

        var handled = _gsx.TryAutoConfirmGoodEngineStart(
            BothEnginesStartStabilized(_state),
            DateTime.UtcNow);
        var gsx = _gsx.Snapshot;
        if (handled && (!gsx.MenuOpen || gsx.PendingChoice))
        {
            CloseGsxChoiceDialog();
        }
        return handled;
    }

    private static bool BothEnginesStartStabilized(AircraftState state) =>
        state.Engine1StartStabilized && state.Engine2StartStabilized;

    private void CloseGsxChoiceDialog()
    {
        if (_gsxChoiceDialog == null)
        {
            return;
        }

        var dialog = _gsxChoiceDialog;
        _gsxChoiceDialog = null;
        if (!dialog.IsDisposed)
        {
            dialog.Close();
        }
    }

    private bool BeginGsxAction(GsxDepartureAction action)
    {
        if (!_settings.EnableGsxIntegration)
        {
            AppendDashboardLog("GSX integration is disabled; use GSX manually if desired.");
            return false;
        }
        if (_gsxInstallation == null)
        {
            AppendDashboardLog("GSX is not installed; the normal flight flow remains available.");
            return false;
        }
        if (!_gsx.Snapshot.CouatlStarted || Connection == null)
        {
            AppendDashboardLog("GSX Couatl is not ready; use GSX manually or retry shortly.");
            return false;
        }
        var started = _gsx.BeginAction(action, DateTime.UtcNow);
        if (started)
        {
            UpdateGsxStatus();
        }
        return started;
    }

    private void SetGsxValue(Definition definition, double value)
    {
        Connection?.SetDataOnSimObject(
            definition,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_DATA_SET_FLAG.DEFAULT,
            new GsxValue(value));
    }

    private void CheckGsxPendingTimeout()
    {
        _gsx.Update(DateTime.UtcNow);
    }

    private void ReleaseGsxRemoteControl()
    {
        _gsx.ReleaseRemoteControl(Connection != null);
    }

    private void RecoverGsxRemoteControl()
    {
        if (Connection == null)
        {
            return;
        }

        if (_gsx.RecoverRemoteControl(DateTime.UtcNow))
        {
            UpdateGsxStatus();
        }
    }

    private void InitializeMobiFlight(SimConnect sender)
    {
        SimConnectRegistrationService.RegisterMobiFlight(sender);

        SendMobiFlightCommand("MF.DummyCmd");
        SendMobiFlightCommand("MF.Ping");
        SendMobiFlightCommand("MF.DummyCmd");
        AppendDashboardLog("Connecting to installed MobiFlight aircraft adapter...");
    }

    private void SendMobiFlightCommand(string command)
    {
        Connection?.SetClientData(
            ClientDataArea.MobiFlightCommand,
            ClientDataDefinition.MobiFlightMessage,
            SIMCONNECT_CLIENT_DATA_SET_FLAG.DEFAULT,
            0,
            new MobiFlightMessage(command));
    }

    private void SendMobiFlightRuntimeCommand(string command)
    {
        Connection?.SetClientData(
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
            SimConnectRegistrationService.RegisterPmdgNg3(sender);
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

        var runtimeChange = _nativeRuntime.TryApplyInputEvent(request, numericValue.Value);
        if (runtimeChange.Handled)
        {
            if (runtimeChange.Diagnostic != null)
            {
                AppLog.Write(runtimeChange.Diagnostic);
            }
            if (runtimeChange.ApplyToAircraftState)
            {
                ApplyNativeAircraftState();
            }
            return;
        }
        var maxRuntimeChange = _asobo737MaxRuntime.ApplyInputEvent(request, numericValue.Value);
        if (maxRuntimeChange.Handled)
        {
            if (maxRuntimeChange.Diagnostic != null)
            {
                AppLog.Write(maxRuntimeChange.Diagnostic);
            }
            ApplyNativeAircraftState();
            return;
        }
    }

    private void OnEnumerateInputEvents(SimConnect sender, SIMCONNECT_RECV_ENUMERATE_INPUT_EVENTS data)
    {
        var request = (Request)data.dwRequestID;
        if (request == Request.A310EnumerateInputEvents)
        {
            _a310InputEventsEnumerated = true;
            foreach (var item in data.rgData ?? Array.Empty<object>())
            {
                if (item is not SIMCONNECT_INPUT_EVENT_DESCRIPTOR descriptor)
                {
                    continue;
                }
                var name = descriptor.Name ?? string.Empty;
                if (name.IndexOf("APU", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("AUX", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("START", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("FUEL", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("PUMP", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("INNER", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AppLog.Write($"A310 system InputEvent candidate: {name} hash={descriptor.Hash}.");
                }
            }
            return;
        }
        if (request != Request.Asobo737MaxEnumerateInputEvents)
        {
            return;
        }

        _asobo737MaxRuntime.MarkInputEventsEnumerated();
        foreach (var item in data.rgData ?? Array.Empty<object>())
        {
            if (item is not SIMCONNECT_INPUT_EVENT_DESCRIPTOR descriptor)
            {
                continue;
            }

            var name = descriptor.Name ?? string.Empty;
            if (IsAsobo737MaxFireInputEventName(name))
            {
                AppLog.Write($"Asobo 737 MAX fire input event candidate: {name} hash={descriptor.Hash}.");
            }
            if (IsAsobo737MaxSystemInputEventCandidate(name))
            {
                AppLog.Write($"Asobo 737 MAX system input event candidate: {name} hash={descriptor.Hash}.");
            }
            foreach (var diagnostic in _asobo737MaxRuntime.RecordEnumeratedInputEvent(name, descriptor.Hash))
            {
                AppLog.Write(diagnostic);
            }
        }
        if (_asobo737MaxRuntime.BatteryCoverInputEventHash.HasValue)
        {
            AppLog.Write($"Asobo 737 MAX battery cover readback bound to InputEvent hash {_asobo737MaxRuntime.BatteryCoverInputEventHash.Value}.");
            sender.GetInputEvent(
                Request.Asobo737MaxBatteryCoverInputEvent,
                _asobo737MaxRuntime.BatteryCoverInputEventHash.Value);
        }
        else
        {
            AppLog.Write("Asobo 737 MAX battery cover InputEvent was not found during enumeration.");
        }

        if (_asobo737MaxRuntime.BatteryInputEventHash.HasValue)
        {
            AppLog.Write($"Asobo 737 MAX battery readback bound to InputEvent hash {_asobo737MaxRuntime.BatteryInputEventHash.Value}.");
            sender.GetInputEvent(
                Request.Asobo737MaxBatteryInputEvent,
                _asobo737MaxRuntime.BatteryInputEventHash.Value);
        }
        else
        {
            AppLog.Write("Asobo 737 MAX battery InputEvent was not found during enumeration.");
        }
    }

    private static bool IsAsobo737MaxFireInputEventName(string name) =>
        name.IndexOf("FIRE", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("EXTING", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("OVERHEAT", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("OVHT", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("FAULT", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("INOP", StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool IsAsobo737MaxSystemInputEventCandidate(string name) =>
        name.IndexOf("GEN", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("APU", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("BLEED", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("PACK", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("ISOLATION", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("PNEUMATIC", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("HYD", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("LIGHT", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("TRANSPONDER", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("TCAS", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("GEAR", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("FLAP", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("SPEEDBRAKE", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("AUTOBRAKE", StringComparison.OrdinalIgnoreCase) >= 0;

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
        if (request == Request.Pmdg777Control)
        {
            if (data.dwData[0] is Pmdg777Control control)
            {
                var becameReady = _pmdg777Runtime.ApplyControlData(control);
                if (becameReady)
                {
                    AppLog.Write("PMDG 777X control channel ready.");
                }
            }
            return;
        }
        if (request == Request.Pmdg777Data)
        {
            if (data.dwData[0] is not Pmdg777RawData raw)
            {
                AppLog.Write(
                    $"PMDG 777X SDK payload rejected: expected {nameof(Pmdg777RawData)}, "
                    + $"received {data.dwData[0]?.GetType().FullName ?? "null"}.");
                return;
            }
            var update = _pmdg777Runtime.ApplyAircraftData(raw.Data);
            if (!update.Accepted)
            {
                AppLog.Write(
                    $"PMDG 777X SDK data block rejected: no published 777-300ER state "
                    + $"(expected {Pmdg777ControlProfile.DataSize} bytes with model ID 6; received {raw.Data?.Length ?? 0} bytes).");
                return;
            }

            LogPmdg777FlowOneState(_pmdg777Runtime.State!);
            if (update.BecameDataReady)
            {
                AppendDashboardLog("PMDG 777X SDK data broadcast received; Flow 1 readbacks active.");
                AppLog.Write("PMDG 777X SDK data broadcast received; Flow 1 readbacks active.");
            }
            ApplyPmdg777SdkState();
            return;
        }

        if (request == Request.PmdgNg3Data)
        {
            var raw = (PmdgNg3RawData)data.dwData[0];
            var update = _pmdgNg3Runtime.ApplyData(raw.Data);
            foreach (var diagnostic in update.Diagnostics)
            {
                AppLog.Write(diagnostic);
            }
            if (update.BecameReady)
            {
                AppendDashboardLog("PMDG 737 NG3 SDK data broadcast received.");
                AppLog.Write("PMDG 737 NG3 SDK data broadcast received.");
            }
            return;
        }

        if (request == Request.PmdgNg3Control)
        {
            return;
        }

        if (data.dwData[0] is MobiFlightFloat mobiFlightValue)
        {
            var runtimeChange = _nativeRuntime.TryApplyMobiFlightReadback(
                request,
                mobiFlightValue.Value);
            if (runtimeChange.Handled)
            {
                if (runtimeChange.Diagnostic != null)
                {
                    AppLog.Write(runtimeChange.Diagnostic);
                }
                if (runtimeChange.ApplyToAircraftState)
                {
                    ApplyNativeAircraftState();
                }
                return;
            }
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
            _mobiFlightSession.MarkAdapterReady();
            AppendDashboardLog("MobiFlight aircraft adapter connected.");
            AppLog.Write("MobiFlight aircraft adapter connected (MF.Pong).");
            SendMobiFlightCommand("MF.DummyCmd");
            SendMobiFlightCommand($"MF.Clients.Add.{MobiFlightAdapterSession.RuntimeClientName}");
            UpdateDashboard();
            TryExecuteOneShotCommand();
            return;
        }

        if (request == Request.MobiFlightResponse
            && response.IndexOf(MobiFlightAdapterSession.RuntimeClientName, StringComparison.OrdinalIgnoreCase) >= 0)
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
        if (_mobiFlightSession.RuntimeReady)
        {
            return;
        }

        SimConnectRegistrationService.RegisterMobiFlightRuntime(
            sender,
            MobiFlightAdapterSession.RuntimeClientName);
        _mobiFlightSession.MarkRuntimeReady(DateTime.UtcNow);
        foreach (var command in _mobiFlightSession.RuntimeRegistrationCommands)
        {
            SendMobiFlightRuntimeCommand(command);
            if (string.Equals(command, "MF.SimVars.Clear", StringComparison.Ordinal))
            {
                AppLog.Write("MobiFlight runtime SimVar table clear requested before registering app variables.");
            }
        }
        AppLog.Write("FBW runtime offsets registered: ADIRS 1/2/3=56/57/58, typed=59/60/61, crew oxygen=63/64, NAV/LOGO=65/66, strobe=67/68.");
        AppendDashboardLog("iniBuilds native state monitoring connected.");
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

        if (_state.IsIniBuildsA310)
        {
            if (_nativeRuntime.A310.Battery1Auto.HasValue)
            {
                _state.Battery1On = _nativeRuntime.A310.Battery1Auto.Value;
            }
            if (_nativeRuntime.A310.Battery2Auto.HasValue)
            {
                _state.Battery2On = _nativeRuntime.A310.Battery2Auto.Value;
            }
            if (_nativeRuntime.A310.Battery3Auto.HasValue)
            {
                _state.Battery3On = _nativeRuntime.A310.Battery3Auto.Value;
            }
            _state.A310HydraulicPanelSafe = A310HydraulicPanelSafe();
            _state.A310WipersAndWeatherRadarOff = A310WipersAndWeatherRadarOff();
            _state.A310ApuFireTestCompleted =
                _nativeRuntime.A310.ApuFireTestObserved && _nativeRuntime.A310.ApuLoopTestObserved;
            _state.A310AnnunciatorTestCompleted = _nativeRuntime.A310.AnnunciatorTestObserved;
            _state.A310InitialExteriorLightsSet = A310InitialExteriorLightsSet();
            _state.A310PreflightSignsSet = A310PreflightSignsSet();
            _state.A310AutoflightComputersSet = A310AutoflightComputersSet();
            _state.A310PreflightHeatSet = A310PreflightHeatSet();
            _state.A310EmergencyExitArmed = A310EmergencyExitArmed();
            _state.A310CargoSmokeTestCompleted = _nativeRuntime.A310.CargoSmokeTestObserved;
            _state.A310EgpwsTestCompleted = _nativeRuntime.A310.EgpwsTestObserved;
            _state.A310PreflightPedestalSet = A310PreflightPedestalSet();
            _state.ApuMasterSwitchOn = _nativeRuntime.A310.Flow3ApuStates[0] > 0.5f;
            _state.ApuStartButtonOn = _nativeRuntime.A310.Flow3ApuStates[1] > 0.5f;
            _state.ApuAvailable = _nativeRuntime.A310.Flow3ApuStates[2] > 0.5f;
            _state.ApuBleedOn = _nativeRuntime.A310.Flow3ApuStates[3] > 0.5f;
            _state.ApuGeneratorSwitchOn = _nativeRuntime.A310.Flow3ApuStates[4] > 0.5f;
            ApplyA310EngineStartState(_state);
            _state.A310FuelPumpsOn = A310FuelPumpsOn();
            ApplyA310AfterStartState(_state);
            _state.A310ApuPowerAndBleedSet = A310ApuPowerAndBleedSet();
            _state.A310TransponderXpdrSet = A310TransponderXpdrSet();
            if (_nativeRuntime.A310.InitialLightStates[1].HasValue)
            {
                _state.BeaconOn = _nativeRuntime.A310.InitialLightStates[1] > 0.5f;
            }
            _state.A310TakeoffExteriorLightsSet = A310TakeoffExteriorLightsSet();
            _state.A310IgnitionContinuousRelight = A310IgnitionContinuousRelight();
            _state.A310PacksOn = A310PacksOn();
            _state.A310TcasTaRaSet = A310TcasTaRaSet();
            _state.A310ClimbLightsSet = A310ClimbLightsSet();
            _state.A310LandingLightsRetracted = A310LandingLightsRetracted();
            _state.A310ApproachLightsSet = A310ApproachLightsSet();
            _state.A310NoseLightTakeoff = A310NoseLightTakeoff();
            _state.A310AfterLandingLightsSet = A310AfterLandingLightsSet();
            _state.A310TransponderStandby = A310TransponderStandby();
            _state.A310WeatherRadarOff = A310WeatherRadarOff();
            _state.A310NoseLightOff = A310NoseLightOff();
            _state.A310SeatbeltsOff = A310SeatbeltsOff();
            _state.A310FuelPumpsParkingSet = A310FuelPumpsParkingSet();
            _state.A310ProbeHeatOff = A310ProbeHeatOff();
            _state.A310IrsOff = A310IrsOff();
            _state.A310OxygenOff = A310OxygenOff();
            _state.A310ExteriorLightsOff = A310ExteriorLightsOff();
            _state.A310EmergencyExitDisarmed = A310EmergencyExitDisarmed();
            _state.A310BatteriesOff = A310BatteriesOff();
            _state.Adirs1SelectorState = _nativeRuntime.A310.Irs1 ?? _state.Adirs1SelectorState;
            _state.Adirs2SelectorState = _nativeRuntime.A310.Irs2 ?? _state.Adirs2SelectorState;
            _state.Adirs3SelectorState = _nativeRuntime.A310.Irs3 ?? _state.Adirs3SelectorState;
            _state.CrewOxygenOn = _nativeRuntime.A310.OxygenSupply > 0.5f;
        }

        if (_state.IsIniBuildsAirbusFamily && _nativeRuntime.NativeAirbus.Battery1On.HasValue)
        {
            _state.Battery1On = _nativeRuntime.NativeAirbus.Battery1On.Value;
        }
        if (_state.IsIniBuildsAirbusFamily && _nativeRuntime.NativeAirbus.Battery2On.HasValue)
        {
            _state.Battery2On = _nativeRuntime.NativeAirbus.Battery2On.Value;
        }
        if (_state.IsFlyByWireAirbus
            && (_nativeRuntime.Fbw.Battery1AutoTyped.HasValue || _nativeRuntime.Fbw.Battery1Auto.HasValue))
        {
            _state.Battery1On = ResolveFbwBatteryState(
                _nativeRuntime.Fbw.CommandedBattery1Auto,
                _nativeRuntime.Fbw.Battery1AutoTyped,
                _nativeRuntime.Fbw.Battery1Auto,
                _state.Battery1On ? 1 : 0);
        }
        if (_state.IsFlyByWireAirbus
            && (_nativeRuntime.Fbw.Battery2AutoTyped.HasValue || _nativeRuntime.Fbw.Battery2Auto.HasValue))
        {
            _state.Battery2On = ResolveFbwBatteryState(
                _nativeRuntime.Fbw.CommandedBattery2Auto,
                _nativeRuntime.Fbw.Battery2AutoTyped,
                _nativeRuntime.Fbw.Battery2Auto,
                _state.Battery2On ? 1 : 0);
        }
        if (_state.IsFlyByWireAirbus)
        {
            if (_nativeRuntime.Fbw.SeatbeltSelector.HasValue)
            {
                _state.SeatbeltSelectorPosition =
                    ResolveFbwSeatbeltSelectorPosition(
                        _nativeRuntime.Fbw.SeatbeltSelector,
                        _state.SeatbeltSignsOn);
            }
            if (_nativeRuntime.Fbw.NoSmokingSelector.HasValue)
            {
                _state.NoSmokingSelectorPosition = _nativeRuntime.Fbw.NoSmokingSelector.Value;
                _state.NoSmokingSignsOn = Math.Abs(_nativeRuntime.Fbw.NoSmokingSelector.Value) < 0.1;
            }
            if (_nativeRuntime.Fbw.EmergencyExitSelector.HasValue)
            {
                _state.EmergencyExitSelectorPosition = _nativeRuntime.Fbw.EmergencyExitSelector.Value;
            }
            if (_nativeRuntime.Fbw.ApuMasterSwitch.HasValue)
            {
                _state.ApuMasterSwitchOn = _nativeRuntime.Fbw.ApuMasterSwitch.Value;
            }
            if (_nativeRuntime.Fbw.ApuStartAvailable.HasValue)
            {
                _state.ApuAvailable = _nativeRuntime.Fbw.ApuStartAvailable.Value;
            }
            if (_nativeRuntime.Fbw.ApuStartButton.HasValue || _nativeRuntime.Fbw.ApuStartAvailable.HasValue)
            {
                _state.ApuStartButtonOn =
                    _nativeRuntime.Fbw.ApuStartButton == true || _nativeRuntime.Fbw.ApuStartAvailable == true;
            }
            if (_nativeRuntime.Fbw.ApuBleedButton.HasValue)
            {
                _state.ApuBleedOn = _nativeRuntime.Fbw.ApuBleedButton.Value;
            }
            if (_nativeRuntime.Fbw.TransponderMode.HasValue)
            {
                _state.TransponderModeSelectorPosition = _nativeRuntime.Fbw.TransponderMode.Value;
            }
            if (_nativeRuntime.Fbw.TcasAltitudeReporting.HasValue
                || _nativeRuntime.Fbw.CommandedTcasAltitudeReporting.HasValue)
            {
                _state.TcasAltitudeReportingOn = ResolveFbwTcasAltitudeReporting(
                    _nativeRuntime.Fbw.CommandedTcasAltitudeReporting,
                    _nativeRuntime.Fbw.CommandedTcasAltitudeReportingUtc,
                    _nativeRuntime.Fbw.TcasAltitudeReporting);
            }
            if (_nativeRuntime.Fbw.TcasMode.HasValue || _nativeRuntime.Fbw.CommandedTcasMode.HasValue)
            {
                _state.TcasMode = ResolveFbwSelectorWithCommand(
                    _nativeRuntime.Fbw.CommandedTcasMode,
                    _nativeRuntime.Fbw.CommandedTcasModeUtc,
                    _nativeRuntime.Fbw.TcasMode);
            }
            if (_nativeRuntime.Fbw.ParkingBrake.HasValue)
            {
                _state.ParkingBrakeSet = _nativeRuntime.Fbw.ParkingBrake.Value;
            }
            if (_nativeRuntime.Fbw.Engine1State.HasValue || _nativeRuntime.Fbw.Engine1N1.HasValue)
            {
                _state.FbwEngine1State = _nativeRuntime.Fbw.Engine1State;
                _state.Engine1Running =
                    _nativeRuntime.Fbw.Engine1State == 1
                    || (_nativeRuntime.Fbw.Engine1N1 ?? (float)_state.Engine1N1Percent) >= 15;
            }
            if (_nativeRuntime.Fbw.Engine2State.HasValue || _nativeRuntime.Fbw.Engine2N1.HasValue)
            {
                _state.FbwEngine2State = _nativeRuntime.Fbw.Engine2State;
                _state.Engine2Running =
                    _nativeRuntime.Fbw.Engine2State == 1
                    || (_nativeRuntime.Fbw.Engine2N1 ?? (float)_state.Engine2N1Percent) >= 15;
            }
            if (_nativeRuntime.Fbw.Engine1StarterValveOpen.HasValue || _nativeRuntime.Fbw.Engine1State.HasValue)
            {
                _state.Engine1StarterActive =
                    _nativeRuntime.Fbw.Engine1StarterValveOpen == true
                    || _nativeRuntime.Fbw.Engine1State == 2
                    || _nativeRuntime.Fbw.Engine1State == 3;
            }
            if (_nativeRuntime.Fbw.Engine2StarterValveOpen.HasValue || _nativeRuntime.Fbw.Engine2State.HasValue)
            {
                _state.Engine2StarterActive =
                    _nativeRuntime.Fbw.Engine2StarterValveOpen == true
                    || _nativeRuntime.Fbw.Engine2State == 2
                    || _nativeRuntime.Fbw.Engine2State == 3;
            }
            if (_nativeRuntime.Fbw.Engine1N1.HasValue)
            {
                _state.Engine1N1Percent = _nativeRuntime.Fbw.Engine1N1.Value;
            }
            if (_nativeRuntime.Fbw.Engine2N1.HasValue)
            {
                _state.Engine2N1Percent = _nativeRuntime.Fbw.Engine2N1.Value;
            }
            _state.GroundSpoilersArmed = ResolveFbwSpoilersArmedState(
                _nativeRuntime.Fbw.CommandedSpoilersArmed,
                _nativeRuntime.Fbw.CommandedSpoilersArmedUtc,
                _nativeRuntime.Fbw.SpoilersArmed,
                _state.GroundSpoilersArmed ? 1 : 0);
            if (_nativeRuntime.Fbw.FlapsHandleIndex.HasValue)
            {
                _state.FlapsHandleIndex = _nativeRuntime.Fbw.FlapsHandleIndex.Value;
            }
            if (_nativeRuntime.Fbw.AutobrakeLevel.HasValue || _nativeRuntime.Fbw.CommandedAutobrakeLevel.HasValue)
            {
                _state.AutobrakeLevel = ResolveFbwAutobrakeLevel(
                    _nativeRuntime.Fbw.CommandedAutobrakeLevel,
                    _nativeRuntime.Fbw.CommandedAutobrakeLevelUtc,
                    _nativeRuntime.Fbw.AutobrakeLevel);
            }
            if (_nativeRuntime.Fbw.WeatherRadarPwsSelector.HasValue
                || _nativeRuntime.Fbw.CommandedWeatherRadarPwsSelector.HasValue)
            {
                _state.WeatherRadarPwsSelectorPosition =
                    ResolveFbwWeatherRadarPwsSelector(
                        _nativeRuntime.Fbw.CommandedWeatherRadarPwsSelector,
                        _nativeRuntime.Fbw.CommandedWeatherRadarPwsSelectorUtc,
                        _nativeRuntime.Fbw.WeatherRadarPwsSelector);
            }
            // From here down the fields are iniBuilds-native LVars. Do not let
            // their default/stale values masquerade as valid FBW cockpit state.
            return;
        }
        if (_state.IsIniBuildsA330 && A330FuelPumpInputEventsReady())
        {
            _state.FuelPump1State = _nativeRuntime.A330.FuelPumpInputStates[0]!.Value;
            _state.FuelPump2State = _nativeRuntime.A330.FuelPumpInputStates[1]!.Value;
            _state.FuelPump3State = _nativeRuntime.A330.FuelPumpInputStates[2]!.Value;
            _state.FuelPump4State = _nativeRuntime.A330.FuelPumpInputStates[3]!.Value;
            _state.FuelPump5State = _nativeRuntime.A330.FuelPumpInputStates[4]!.Value;
            _state.FuelPump6State = _nativeRuntime.A330.FuelPumpInputStates[5]!.Value;
            _state.FuelPumpsConfigured = A330FuelPumpsConfigured();
        }
        else if (_nativeRuntime.NativeAirbus.FuelPump1.HasValue
            && _nativeRuntime.NativeAirbus.FuelPump2.HasValue
            && _nativeRuntime.NativeAirbus.FuelPump3.HasValue
            && _nativeRuntime.NativeAirbus.FuelPump4.HasValue
            && _nativeRuntime.NativeAirbus.FuelPump5.HasValue
            && _nativeRuntime.NativeAirbus.FuelPump6.HasValue)
        {
            _state.FuelPump1State = _nativeRuntime.NativeAirbus.FuelPump1.Value;
            _state.FuelPump2State = _nativeRuntime.NativeAirbus.FuelPump2.Value;
            _state.FuelPump3State = _nativeRuntime.NativeAirbus.FuelPump3.Value;
            _state.FuelPump4State = _nativeRuntime.NativeAirbus.FuelPump4.Value;
            _state.FuelPump5State = _nativeRuntime.NativeAirbus.FuelPump5.Value;
            _state.FuelPump6State = _nativeRuntime.NativeAirbus.FuelPump6.Value;
            _state.FuelPumpsConfigured = _nativeRuntime.NativeAirbus.FuelPump1.Value != 0
                                         && _nativeRuntime.NativeAirbus.FuelPump2.Value != 0
                                         && _nativeRuntime.NativeAirbus.FuelPump3.Value != 0
                                         && _nativeRuntime.NativeAirbus.FuelPump4.Value != 0
                                         && _nativeRuntime.NativeAirbus.FuelPump5.Value != 0
                                         && _nativeRuntime.NativeAirbus.FuelPump6.Value != 0;
        }
        if (_nativeRuntime.NativeAirbus.NavLogoSelectorPosition.HasValue)
        {
            _state.NavLogoSelectorPosition = _nativeRuntime.NativeAirbus.NavLogoSelectorPosition.Value;
        }
        if (_state.IsIniBuildsA330 && _nativeRuntime.A330.NavLogoInputState.HasValue)
        {
            _state.NavLogoSelectorPosition = _nativeRuntime.A330.NavLogoInputState.Value;
        }
        if (_state.IsAsobo737Max8)
        {
            _state.ApuAvailable = IsAsobo737MaxApuAvailable(_state.ApuRpmPercent, _state.ApuVolts);
        }
        else if (_nativeRuntime.NativeAirbus.ApuAvailable.HasValue)
        {
            _state.ApuAvailable = _nativeRuntime.NativeAirbus.ApuAvailable.Value != 0;
        }
        if (_nativeRuntime.NativeAirbus.ApuMasterSwitch.HasValue)
        {
            _state.ApuMasterSwitchOn = _nativeRuntime.NativeAirbus.ApuMasterSwitch.Value != 0;
        }
        if (_nativeRuntime.NativeAirbus.ApuStartButton.HasValue)
        {
            _state.ApuStartButtonOn = _nativeRuntime.NativeAirbus.ApuStartButton.Value != 0;
        }
        if (_nativeRuntime.NativeAirbus.ApuBleedButton.HasValue)
        {
            _state.ApuBleedOn = _nativeRuntime.NativeAirbus.ApuBleedButton.Value != 0;
        }
        if (_state.IsIniBuildsA330)
        {
            if (_nativeRuntime.A330.ApuInputStates[0].HasValue)
            {
                _state.ApuMasterSwitchOn = _nativeRuntime.A330.ApuInputStates[0]!.Value >= 0.5;
            }
            if (_nativeRuntime.NativeAirbus.ApuStartButton.HasValue)
            {
                _state.ApuStartButtonOn = _nativeRuntime.NativeAirbus.ApuStartButton.Value != 0;
            }
            if (_nativeRuntime.A330.ApuInputStates[2].HasValue)
            {
                _state.ApuBleedOn = _nativeRuntime.A330.ApuInputStates[2]!.Value >= 0.5;
            }
        }
        if (_nativeRuntime.NativeAirbus.ApuGeneratorOn.HasValue)
        {
            _state.ApuGeneratorSwitchOn = _nativeRuntime.NativeAirbus.ApuGeneratorOn.Value != 0;
        }
        if (_nativeRuntime.NativeAirbus.ApuFlapPercent.HasValue)
        {
            _state.ApuFlapPercent = _nativeRuntime.NativeAirbus.ApuFlapPercent.Value;
        }
        if (_nativeRuntime.NativeAirbus.Adirs1State.HasValue)
        {
            _state.Adirs1SelectorState = _nativeRuntime.NativeAirbus.Adirs1State.Value;
        }
        if (_nativeRuntime.NativeAirbus.Adirs2State.HasValue)
        {
            _state.Adirs2SelectorState = _nativeRuntime.NativeAirbus.Adirs2State.Value;
        }
        if (_nativeRuntime.NativeAirbus.Adirs3State.HasValue)
        {
            _state.Adirs3SelectorState = _nativeRuntime.NativeAirbus.Adirs3State.Value;
        }
        if (_state.IsIniBuildsA330)
        {
            if (_nativeRuntime.A330.AdirsInputStates[0].HasValue)
            {
                _state.Adirs1SelectorState = _nativeRuntime.A330.AdirsInputStates[0]!.Value;
            }
            if (_nativeRuntime.A330.AdirsInputStates[1].HasValue)
            {
                _state.Adirs2SelectorState = _nativeRuntime.A330.AdirsInputStates[1]!.Value;
            }
            if (_nativeRuntime.A330.AdirsInputStates[2].HasValue)
            {
                _state.Adirs3SelectorState = _nativeRuntime.A330.AdirsInputStates[2]!.Value;
            }
        }
        if (_state.IsAsobo737Max8)
        {
            var nowUtc = DateTime.UtcNow;
            var leftIrsState = _asobo737MaxRuntime.ResolveIrsState(true, nowUtc);
            if (leftIrsState.HasValue)
            {
                _state.Adirs1SelectorState = leftIrsState.Value;
            }
            var rightIrsState = _asobo737MaxRuntime.ResolveIrsState(false, nowUtc);
            if (rightIrsState.HasValue)
            {
                _state.Adirs2SelectorState = rightIrsState.Value;
            }
            if (_asobo737MaxRuntime.PositionLightInputState.HasValue)
            {
                _state.NavigationLightsOn = Math.Abs(_asobo737MaxRuntime.PositionLightInputState.Value) < 0.1
                    || Math.Abs(_asobo737MaxRuntime.PositionLightInputState.Value - 2) < 0.1;
            }
            if (_asobo737MaxRuntime.LogoLightInputState.HasValue)
            {
                _state.LogoLightsOn = Math.Abs(_asobo737MaxRuntime.LogoLightInputState.Value) < 0.1;
            }
            if (_asobo737MaxRuntime.EmergencyExitInputState.HasValue
                && _asobo737MaxRuntime.EmergencyExitCoverInputState.HasValue)
            {
                _state.EmergencyExitSelectorPosition =
                    Math.Abs(_asobo737MaxRuntime.EmergencyExitInputState.Value - 1) < 0.1
                    && Math.Abs(_asobo737MaxRuntime.EmergencyExitCoverInputState.Value) < 0.1
                        ? 1
                        : _asobo737MaxRuntime.EmergencyExitInputState.Value;
            }
            if (Asobo737MaxFuelPumpInputEventsReady())
            {
                _state.FuelPump1State = Asobo737MaxFuelPumpIsOn(_asobo737MaxRuntime.FuelPumpInputStates[0]!.Value) ? 1 : 0;
                _state.FuelPump2State = Asobo737MaxFuelPumpIsOn(_asobo737MaxRuntime.FuelPumpInputStates[1]!.Value) ? 1 : 0;
                _state.FuelPump3State = Asobo737MaxFuelPumpIsOn(_asobo737MaxRuntime.FuelPumpInputStates[2]!.Value) ? 1 : 0;
                _state.FuelPump4State = Asobo737MaxFuelPumpIsOn(_asobo737MaxRuntime.FuelPumpInputStates[3]!.Value) ? 1 : 0;
                _state.FuelPump5State = Asobo737MaxFuelPumpIsOn(_asobo737MaxRuntime.FuelPumpInputStates[4]!.Value) ? 1 : 0;
                _state.FuelPump6State = Asobo737MaxFuelPumpIsOn(_asobo737MaxRuntime.FuelPumpInputStates[5]!.Value) ? 1 : 0;
                _state.FuelPumpsConfigured = Asobo737MaxFuelPumpsConfigured();
            }
        }
        if (_nativeRuntime.NativeAirbus.AdirsOnBattery.HasValue)
        {
            _state.AdirsOnBattery = _nativeRuntime.NativeAirbus.AdirsOnBattery.Value != 0;
        }
        if (_nativeRuntime.NativeAirbus.CrewOxygen.HasValue)
        {
            _state.CrewOxygenOn = _nativeRuntime.NativeAirbus.CrewOxygen.Value != 0;
        }
        if (_state.IsIniBuildsA330 && _nativeRuntime.A330.CrewOxygenInputState.HasValue)
        {
            _state.CrewOxygenOn = _nativeRuntime.A330.CrewOxygenInputState.Value >= 0.5;
        }
        if (_nativeRuntime.NativeAirbus.StrobeSelector.HasValue)
        {
            _state.StrobeSelectorPosition = _nativeRuntime.NativeAirbus.StrobeSelector.Value;
        }
        if (_state.IsIniBuildsA330 && _nativeRuntime.A330.StrobeInputState.HasValue)
        {
            _state.StrobeSelectorPosition = _nativeRuntime.A330.StrobeInputState.Value;
        }
        _state.ApuFireTestActive = _nativeRuntime.NativeAirbus.ApuFireTest.HasValue && _nativeRuntime.NativeAirbus.ApuFireTest.Value != 0;
        _state.ApuFireWarningLit = _nativeRuntime.NativeAirbus.ApuFireWarningLit.HasValue && _nativeRuntime.NativeAirbus.ApuFireWarningLit.Value != 0;
        _state.ApuFireSoundActive = _nativeRuntime.NativeAirbus.ApuFireSound.HasValue && _nativeRuntime.NativeAirbus.ApuFireSound.Value != 0;
        _state.Engine1FireTestActive = _nativeRuntime.NativeAirbus.Engine1FireTest.HasValue && _nativeRuntime.NativeAirbus.Engine1FireTest.Value != 0;
        _state.Engine1FireWarningLit = _nativeRuntime.NativeAirbus.Engine1FireWarningLit.HasValue && _nativeRuntime.NativeAirbus.Engine1FireWarningLit.Value != 0;
        _state.Engine1FireSoundActive = _nativeRuntime.NativeAirbus.Engine1FireSound.HasValue && _nativeRuntime.NativeAirbus.Engine1FireSound.Value != 0;
        _state.Engine2FireTestActive = _nativeRuntime.NativeAirbus.Engine2FireTest.HasValue && _nativeRuntime.NativeAirbus.Engine2FireTest.Value != 0;
        _state.Engine2FireWarningLit = _nativeRuntime.NativeAirbus.Engine2FireWarningLit.HasValue && _nativeRuntime.NativeAirbus.Engine2FireWarningLit.Value != 0;
        _state.Engine2FireSoundActive = _nativeRuntime.NativeAirbus.Engine2FireSound.HasValue && _nativeRuntime.NativeAirbus.Engine2FireSound.Value != 0;
        _state.SeatbeltSelectorPosition = _nativeRuntime.NativeAirbus.SeatbeltSelector;
        _state.SeatbeltSignsOn = _nativeRuntime.NativeAirbus.SeatbeltSignsOn.HasValue && _nativeRuntime.NativeAirbus.SeatbeltSignsOn.Value != 0;
        _state.NoSmokingSelectorPosition = _nativeRuntime.NativeAirbus.NoSmokingSelector;
        _state.NoSmokingSignsOn = _nativeRuntime.NativeAirbus.NoSmokingSignsOn.HasValue && _nativeRuntime.NativeAirbus.NoSmokingSignsOn.Value != 0;
        _state.EmergencyExitSelectorPosition = _nativeRuntime.NativeAirbus.EmergencyExitSelector;
        if (_state.IsIniBuildsA330 && A330SignInputEventsReady())
        {
            _state.SeatbeltSelectorPosition = _nativeRuntime.A330.SignInputStates[0];
            _state.SeatbeltSignsOn = _nativeRuntime.A330.SignInputStates[0] >= 0.5;
            _state.NoSmokingSelectorPosition = _nativeRuntime.A330.SignInputStates[1];
            _state.NoSmokingSignsOn = _nativeRuntime.A330.SignInputStates[1] >= 0.5;
            _state.EmergencyExitSelectorPosition = _nativeRuntime.A330.SignInputStates[2];
        }
        if (_state.IsAsobo737Max8)
        {
            if (_asobo737MaxRuntime.SeatbeltsInputState.HasValue)
            {
                _state.SeatbeltSelectorPosition = _asobo737MaxRuntime.SeatbeltsInputState.Value;
                _state.SeatbeltSignsOn = Math.Abs(_asobo737MaxRuntime.SeatbeltsInputState.Value - 1) < 0.1;
            }
            if (_asobo737MaxRuntime.NoSmokingInputState.HasValue)
            {
                _state.NoSmokingSelectorPosition = _asobo737MaxRuntime.NoSmokingInputState.Value;
                _state.NoSmokingSignsOn = Math.Abs(_asobo737MaxRuntime.NoSmokingInputState.Value - 1) < 0.1;
            }
            if (_asobo737MaxRuntime.ApuInputState.HasValue)
            {
                _state.ApuMasterSwitchOn = Math.Abs(_asobo737MaxRuntime.ApuInputState.Value - 1) < 0.1;
                _state.ApuStartButtonOn = Math.Abs(_asobo737MaxRuntime.ApuInputState.Value) < 0.1 || _state.ApuStarterPercent > 0;
            }
            _state.ApuAvailable = IsAsobo737MaxApuAvailable(_state.ApuRpmPercent, _state.ApuVolts);
            if (Asobo737MaxApuGeneratorsReady())
            {
                _state.ApuGeneratorSwitchOn = Asobo737MaxApuGeneratorsOn();
                _state.ApuGeneratorPowerEstablished =
                    _state.ApuGeneratorActive
                    || (_state.ApuAvailable && Asobo737MaxApuGeneratorsOn());
            }
            if (_asobo737MaxRuntime.ApuBleedInputState.HasValue)
            {
                _state.ApuBleedOn = Asobo737MaxBinarySwitchIsOn(_asobo737MaxRuntime.ApuBleedInputState.Value);
            }
            if (_asobo737MaxRuntime.LeftPackInputState.HasValue)
            {
                _state.LeftPackSwitchPosition = Asobo737MaxPackPosition(_asobo737MaxRuntime.LeftPackInputState.Value);
            }
            if (_asobo737MaxRuntime.RightPackInputState.HasValue)
            {
                _state.RightPackSwitchPosition = Asobo737MaxPackPosition(_asobo737MaxRuntime.RightPackInputState.Value);
            }
            if (_asobo737MaxRuntime.EngineBleedInputStates.All(state => state.HasValue))
            {
                _state.BoeingEngineBleed1On =
                    _asobo737MaxRuntime.EngineBleedInputStates[0]!.Value >= 0.5;
                _state.BoeingEngineBleed2On =
                    _asobo737MaxRuntime.EngineBleedInputStates[1]!.Value >= 0.5;
            }
            if (_asobo737MaxRuntime.IsolationValveInputState.HasValue)
            {
                _state.IsolationValvePosition = Asobo737MaxIsolationValvePosition(_asobo737MaxRuntime.IsolationValveInputState.Value);
            }
            if (_asobo737MaxRuntime.EngineGeneratorInputStates.All(state => state.HasValue))
            {
                _state.EngineGeneratorsOn = _asobo737MaxRuntime.EngineGeneratorInputStates.All(state => state!.Value >= 0.5);
            }
            if (_asobo737MaxRuntime.ElectricHydraulicPumpInputStates.All(state => state.HasValue))
            {
                _state.BoeingElectricHydraulicPump1On =
                    Asobo737MaxControlProfile.IsElectricHydraulicPumpOn(
                        _asobo737MaxRuntime.ElectricHydraulicPumpInputStates[0]!.Value);
                _state.BoeingElectricHydraulicPump2On =
                    Asobo737MaxControlProfile.IsElectricHydraulicPumpOn(
                        _asobo737MaxRuntime.ElectricHydraulicPumpInputStates[1]!.Value);
                _state.BoeingElectricHydraulicPumpsOn =
                    _state.BoeingElectricHydraulicPump1On
                    && _state.BoeingElectricHydraulicPump2On;
            }
            if (_asobo737MaxRuntime.FlapsInputState.HasValue)
            {
                _state.FlapsHandleIndex = Asobo737MaxFlapsHandleIndex(_asobo737MaxRuntime.FlapsInputState.Value);
            }
            if (_asobo737MaxRuntime.AutobrakeInputState.HasValue)
            {
                _state.AutobrakeLevel = _asobo737MaxRuntime.AutobrakeInputState.Value;
            }
            if (_asobo737MaxRuntime.AutothrottleInputState.HasValue)
            {
                _state.BoeingAutothrottleArmed =
                    Asobo737MaxControlProfile.IsAutothrottleArmed(
                        _asobo737MaxRuntime.AutothrottleInputState.Value);
            }
            if (_asobo737MaxRuntime.TransponderOperatingModeInputState.HasValue)
            {
                _state.BoeingTransponderOperatingMode =
                    _asobo737MaxRuntime.TransponderOperatingModeInputState.Value;
            }
            if (_asobo737MaxRuntime.TransponderModeInputState.HasValue)
            {
                _state.TransponderModeSelectorPosition =
                    _asobo737MaxRuntime.TransponderModeInputState.Value;
                _state.TransponderStandby =
                    Math.Abs(
                        _asobo737MaxRuntime.TransponderModeInputState.Value
                        - Asobo737MaxControlProfile.TransponderStandby) < 0.1;
            }
            if (_asobo737MaxRuntime.TaxiLightInputState.HasValue)
            {
                _state.NoseLightSelectorPosition =
                    Asobo737MaxControlProfile.NormalizeTaxiLightPosition(
                        _asobo737MaxRuntime.TaxiLightInputState.Value);
            }
            if (_asobo737MaxRuntime.RunwayTurnoffInputStates.All(state => state.HasValue))
            {
                _state.RunwayTurnoffLightsOn =
                    _asobo737MaxRuntime.RunwayTurnoffInputStates.All(
                        state => Asobo737MaxControlProfile.IsRunwayTurnoffLightOn(state!.Value));
            }
            if (_asobo737MaxRuntime.LandingLightInputStates.All(state => state.HasValue))
            {
                var landingLightsOn = _asobo737MaxRuntime.LandingLightInputStates.All(
                    state => Asobo737MaxControlProfile.IsLandingLightOn(state!.Value));
                _state.LeftLandingLightSelectorPosition = landingLightsOn ? 0 : 1;
                _state.RightLandingLightSelectorPosition = landingLightsOn ? 0 : 1;
            }
            if (_asobo737MaxRuntime.AntiCollisionInputState.HasValue)
            {
                _state.BeaconOn = Asobo737MaxControlProfile.IsAntiCollisionOn(
                    _asobo737MaxRuntime.AntiCollisionInputState.Value);
            }
            if (_asobo737MaxRuntime.PositionLightInputState.HasValue)
            {
                _state.StrobeSelectorPosition = _asobo737MaxRuntime.PositionLightInputState.Value;
            }
        }
        if (_state.IsIniBuildsA330)
        {
            if (_nativeRuntime.A330.CommandedSpoilersArmed.HasValue)
            {
                _state.GroundSpoilersArmed = _nativeRuntime.A330.CommandedSpoilersArmed.Value;
            }
        }
        else if (_nativeRuntime.NativeAirbus.SpoilersArmed.HasValue)
        {
            _state.GroundSpoilersArmed = _nativeRuntime.NativeAirbus.SpoilersArmed.Value != 0;
        }
        _state.AutobrakeLevel = _nativeRuntime.NativeAirbus.AutobrakeLevel;
        if (_state.IsIniBuildsA330)
        {
            _state.AutobrakeLevel = ResolveA330AutobrakeLevel();
        }
        _state.WeatherRadarPwsSelectorPosition = _nativeRuntime.NativeAirbus.WeatherRadarPwsSelector;
        if (_state.IsIniBuildsA330 && _nativeRuntime.A330.WeatherRadarPwsInputState.HasValue)
        {
            // A330 Boolean is inverted: 1=OFF, 0=AUTO.
            _state.WeatherRadarPwsSelectorPosition =
                _nativeRuntime.A330.WeatherRadarPwsInputState.Value >= 0.5 ? 0 : 1;
        }
        if (!_state.IsAsobo737Max8 || !_asobo737MaxRuntime.TaxiLightInputState.HasValue)
        {
            _state.NoseLightSelectorPosition = _nativeRuntime.NativeAirbus.NoseLightSelector;
        }
        if (_state.IsIniBuildsA330 && _nativeRuntime.A330.NoseLightInputState.HasValue)
        {
            _state.NoseLightSelectorPosition = _nativeRuntime.A330.NoseLightInputState.Value;
        }
        if (!_state.IsAsobo737Max8
            || !_asobo737MaxRuntime.LandingLightInputStates.All(state => state.HasValue))
        {
            _state.LeftLandingLightSelectorPosition = _nativeRuntime.NativeAirbus.LeftLandingLightSelector;
            _state.RightLandingLightSelectorPosition = _nativeRuntime.NativeAirbus.RightLandingLightSelector;
        }
        if (_state.IsIniBuildsA330 && _nativeRuntime.A330.LandingLightInputState.HasValue)
        {
            var a330LandingLightPosition =
                _nativeRuntime.A330.LandingLightInputState.Value >= 0.5 ? 0d : 1d;
            _state.LeftLandingLightSelectorPosition = a330LandingLightPosition;
            _state.RightLandingLightSelectorPosition = a330LandingLightPosition;
        }
        _state.TcasAltitudeReportingOn =
            _nativeRuntime.NativeAirbus.TcasAltitudeReporting.HasValue
                ? _state.IsIniBuildsA330
                    ? _nativeRuntime.A330.TcasAltitudeInputState.HasValue
                        ? _nativeRuntime.A330.TcasAltitudeInputState.Value >= 0.5
                        : null
                    : _nativeRuntime.NativeAirbus.TcasAltitudeReporting.Value == 0
                : null;
        if (_state.IsIniBuildsA330 && _nativeRuntime.A330.TcasAltitudeInputState.HasValue)
        {
            _state.TcasAltitudeReportingOn = _nativeRuntime.A330.TcasAltitudeInputState.Value >= 0.5;
        }
        _state.TransponderAtcState = _nativeRuntime.NativeAirbus.TransponderAtcState;
        _state.TcasMode = _nativeRuntime.NativeAirbus.TcasMode;
        if (_state.IsIniBuildsA330 && _nativeRuntime.A330.TcasTrafficInputState.HasValue)
        {
            _state.TcasMode = _nativeRuntime.A330.TcasTrafficInputState.Value;
        }
        if (!_state.IsAsobo737Max8 || !_asobo737MaxRuntime.TransponderModeInputState.HasValue)
        {
            _state.TransponderModeSelectorPosition = _nativeRuntime.NativeAirbus.TransponderStandby;
            _state.TransponderStandby = _nativeRuntime.NativeAirbus.TransponderStandby.HasValue
                                        && _nativeRuntime.NativeAirbus.TransponderStandby.Value != 0;
        }
        if (_state.IsIniBuildsA330 && _nativeRuntime.A330.TransponderModeInputState.HasValue)
        {
            _state.TransponderModeSelectorPosition = _nativeRuntime.A330.TransponderModeInputState.Value;
            _state.TransponderStandby = _nativeRuntime.A330.TransponderModeInputState.Value < 0.5;
        }
        _state.ApuFireTestCompleted = _apuFireTestCompleted;
        _state.Engine1FireTestCompleted = _engine1FireTestCompleted;
        _state.Engine2FireTestCompleted = _engine2FireTestCompleted;
        UpdateTelemetrySanity(_state);
        UpdateCockpitDisplayReadiness(_state);

        VerifyPendingBatteryProcedure();
        VerifyPendingFireTest();
        TryAutoConfirmGsxGoodEngineStart();
        TryAutoContinueGsxPushback();
        UpdateDashboard();
        TryExecuteOneShotCommand();
    }

    private void OnAircraftData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (_replayActive)
        {
            return;
        }
        if ((Request)data.dwRequestID == Request.FlightCalloutState)
        {
            if (data.dwData.Length > 0
                && _state != null
                && string.Equals(
                    _procedureRunner.Definition?.Id,
                    "takeoff-climb",
                    StringComparison.OrdinalIgnoreCase)
                && IsProcedureActive(_procedureRunner.Status))
            {
                var callout = (FlightCalloutData)data.dwData[0];
                _state.OnGround = callout.OnGround != 0;
                _state.IndicatedAirspeedKnots = callout.IndicatedAirspeed;
                _state.VerticalSpeedFeetPerMinute = callout.VerticalSpeed;
                _state.AltitudeAboveGroundFeet = callout.AltitudeAboveGround;
                _state.Engine1N1Percent = callout.Engine1N1;
                _state.Engine2N1Percent = callout.Engine2N1;
                _procedureRunner.Update(_state);
            }
            return;
        }
        if ((Request)data.dwRequestID != Request.AircraftState || data.dwData.Length == 0)
        {
            return;
        }

        var raw = (AircraftData)data.dwData[0];
        var sayIntentionsIntercomMask =
            (raw.SayIntentionsIntercom1Receiving != 0 ? 1 : 0)
            | (raw.SayIntentionsIntercom2Receiving != 0 ? 2 : 0)
            | (raw.SayIntentionsIntercom3Receiving != 0 ? 4 : 0);
        var previousIntercomMask = Volatile.Read(
            ref _sayIntentionsIntercomReceivingMask);
        Volatile.Write(
            ref _sayIntentionsIntercomReceivingMask,
            sayIntentionsIntercomMask);
        if (sayIntentionsIntercomMask != 0)
        {
            Volatile.Write(ref _sayIntentionsIntercomSignalObserved, 1);
        }
        if (sayIntentionsIntercomMask != previousIntercomMask)
        {
            AppLog.Write(
                $"SayIntentions intercom receive mask changed from {previousIntercomMask} to {sayIntentionsIntercomMask}.");
        }
        _gsx.ObserveTelemetry(
            raw.GsxCouatlStarted != 0,
            raw.GsxRemoteControl != 0,
            DateTime.UtcNow);
        UpdateGsxStatus();
        CheckGsxPendingTimeout();
        var approachDistance = ResolveApproachDistance(raw);
        var aircraftVariant = AircraftVariantResolver.Resolve(
            raw.Title,
            EnableExperimentalFlyByWireA380X);
        if (_state != null
            && (_state.Variant != aircraftVariant
                || !string.Equals(_state.Title, raw.Title, StringComparison.Ordinal)))
        {
            var previousAircraft = $"{_state.Title} ({_state.Variant})";
            InvalidateAircraftAutomation(
                AutomationInvalidationReason.AircraftChanged,
                $"from {previousAircraft} to {raw.Title} ({aircraftVariant})");
            ResetMobiFlightRuntimeAfterDisconnect(aircraftChanged: true);
        }
        var isIniBuildsA310 = aircraftVariant == AircraftVariant.IniBuildsA310;
        var isIniBuildsA330 = aircraftVariant == AircraftVariant.IniBuildsA330;
        var isIniBuildsAirbusFamily =
            aircraftVariant is AircraftVariant.IniBuildsA320NeoV2
                or AircraftVariant.IniBuildsA321Lr
                or AircraftVariant.IniBuildsA330;
        var isFlyByWireA380X =
            aircraftVariant == AircraftVariant.FlyByWireA380XExperimental;
        var isFlyByWireA320Neo =
            aircraftVariant == AircraftVariant.FlyByWireA320Neo;
        var isFlyByWireAirbus = isFlyByWireA320Neo || isFlyByWireA380X;
        var isPmdg777 = aircraftVariant == AircraftVariant.Pmdg777300Er;
        var isPmdg737 = aircraftVariant == AircraftVariant.Pmdg737800;
        var isAsobo737Max = aircraftVariant == AircraftVariant.Asobo737Max8;
        var pmdg = _pmdgNg3Runtime.State;
        var pmdg777 = _pmdg777Runtime.State;
        if (isPmdg777 && !_pmdg777SdkInitialized)
        {
            InitializePmdg777Sdk(sender);
        }
        if (isPmdg777)
        {
            SetLoggedBool(
                ref _loggedPmdg777GenericBattery,
                (float)raw.Battery1,
                "PMDG 777 native ELECTRICAL MASTER BATTERY:1");
        }
        if (isIniBuildsAirbusFamily)
        {
            LogChangedFloat(
                "iniBuilds direct INI_IGNITION_KNOB",
                raw.IniBuildsIgnitionKnob,
                ref _lastLoggedIniBuildsIgnitionKnob);
        }
        if (isIniBuildsA310)
        {
            LogChangedFloat(
                "A310 standard engine 1 ignition",
                raw.Engine1IgnitionSwitch,
                ref _lastLoggedA310Engine1IgnitionSwitch);
            LogChangedFloat(
                "A310 standard engine 2 ignition",
                raw.Engine2IgnitionSwitch,
                ref _lastLoggedA310Engine2IgnitionSwitch);
        }
        if (isIniBuildsAirbusFamily)
        {
            LogChangedFloat(
                "iniBuilds direct INI_TURNOFF_LIGHT_SWITCH",
                raw.IniBuildsTurnoffLightSwitch,
                ref _lastLoggedIniBuildsTurnoffLightSwitch);
        }
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
        var nowUtc = DateTime.UtcNow;
        var pmdgApuRuntime = _pmdgNg3Runtime.ObserveAircraftFrame(isPmdg737, nowUtc);
        var pmdgApuPowerEstablished = pmdgApuRuntime.PowerEstablished;
        var pmdgApuAvailable = pmdgApuRuntime.Available;
        var pmdgApuBleedWarmupComplete = pmdgApuRuntime.BleedWarmupComplete;
        var approachSchedule = AircraftApproachProfiles.EffectiveSchedule(
            raw.Title,
            _settings.AircraftApproachOverrides);
        var activePlan = _procedureSession.ActiveFlightPlan;
        var plannedBlockFuelKg = SimBriefOperationalContext.BlockFuelKilograms(activePlan);
        var actualFuelKg = raw.TotalFuelWeightPounds / 2.20462262185;
        var plannedTakeoffFlaps = SimBriefOperationalContext.TakeoffFlapSetting(activePlan, aircraftVariant);
        int? cockpitV1 = isPmdg777 && pmdg777?.FmcV1 > 0
            ? pmdg777.FmcV1
            : isPmdg737 && pmdg?.V1 > 0 ? pmdg.V1 : null;
        int? cockpitVr = isPmdg777 && pmdg777?.FmcVr > 0
            ? pmdg777.FmcVr
            : isPmdg737 && pmdg?.Vr > 0 ? pmdg.Vr : null;
        int? cockpitFlaps = isPmdg777 && pmdg777?.FmcTakeoffFlaps > 0
            ? pmdg777.FmcTakeoffFlaps
            : isPmdg737 && pmdg?.TakeoffFlaps > 0 ? pmdg.TakeoffFlaps : null;
        var effectiveV1 = cockpitV1
            ?? _settings.TakeoffV1SpeedKnots;
        var effectiveVr = cockpitVr
            ?? _settings.TakeoffRotateSpeedKnots;
        effectiveVr = Math.Max(effectiveV1, effectiveVr);
        var effectiveV2 = Math.Max(
            effectiveVr,
            isPmdg777 && pmdg777?.FmcV2 > 0
                ? pmdg777.FmcV2
                : _settings.TakeoffV2SpeedKnots);
        var engineModeSelectorPosition = ResolveEngineModeSelectorPosition(
            isIniBuildsAirbusFamily
                ? raw.IniBuildsIgnitionKnob
                : null,
            isIniBuildsAirbusFamily ? _nativeRuntime.NativeAirbus.EngineModeSelector : null,
            raw.Engine1IgnitionSwitch,
            raw.Engine2IgnitionSwitch);

        if (isPmdg777)
        {
            _pmdg777Runtime.RecordFireAndOxygenObservations(
                raw.Pmdg777FireOverheatTestSwitch,
                raw.Pmdg777FirstOfficerOxygenTestSwitch);
        }

        _state = new AircraftState
        {
            Title = raw.Title,
            Pmdg777SdkDataReady = isPmdg777 && _pmdg777Runtime.DataReady,
            Pmdg777BatteryOn = isPmdg777 && pmdg777?.BatteryOn == true,
            Pmdg777IfePassengerSeatsOn = isPmdg777 && pmdg777?.IfePassengerSeatsOn == true,
            Pmdg777CabinUtilityOn = isPmdg777 && pmdg777?.CabinUtilityOn == true,
            Pmdg777BusTiesAuto = isPmdg777 && pmdg777?.BusTiesAuto == true,
            Pmdg777HydraulicPanelSafe = isPmdg777
                                          && pmdg777?.CenterPrimaryPumpsOff == true
                                          && pmdg777.DemandPumpsOff,
            Pmdg777WipersOff = isPmdg777 && pmdg777?.WipersOff == true,
            Pmdg777GearLeverDown = isPmdg777 && pmdg777?.GearLeverDown == true,
            Pmdg777AlternateFlapsOff = isPmdg777 && pmdg777?.AlternateFlapsOff == true,
            Pmdg777ExternalPowerAvailable = isPmdg777
                                             && pmdg777?.PrimaryExternalPowerAvailable == true
                                             && pmdg777.SecondaryExternalPowerAvailable,
            Pmdg777ExternalPowerOn = isPmdg777
                                      && pmdg777?.PrimaryExternalPowerOn == true
                                      && pmdg777.SecondaryExternalPowerOn,
            Pmdg777PrimaryExternalPowerAvailable = isPmdg777
                                                     && pmdg777?.PrimaryExternalPowerAvailable == true,
            Pmdg777SecondaryExternalPowerAvailable = isPmdg777
                                                       && pmdg777?.SecondaryExternalPowerAvailable == true,
            Pmdg777PrimaryExternalPowerOn = isPmdg777
                                             && pmdg777?.PrimaryExternalPowerOn == true,
            Pmdg777SecondaryExternalPowerOn = isPmdg777
                                               && pmdg777?.SecondaryExternalPowerOn == true,
            Pmdg777NavigationLightOn = isPmdg777 && pmdg777?.NavigationLightOn == true,
            Pmdg777LogoLightOn = isPmdg777 && pmdg777?.LogoLightOn == true,
            Pmdg777GroundAirConfigurationSet = isPmdg777
                                                && pmdg777?.PacksOff == true
                                                && pmdg777.RecirculationFansOff,
            Pmdg777AdiruOn = isPmdg777 && pmdg777?.AdiruOn == true,
            Pmdg777EmergencyLightsArmed = isPmdg777
                                          && pmdg777?.EmergencyLightsSelector == 1,
            Pmdg777EmergencyLightsGuardClosed = isPmdg777
                                                 && raw.Pmdg777EmergencyLightsGuard < 0.5,
            Pmdg777PassengerOxygenGuardClosed = isPmdg777
                                                && raw.Pmdg777PassengerOxygenGuard < 0.5,
            Pmdg777PrimaryFlightComputersGuardClosed = isPmdg777
                                                       && raw.Pmdg777PrimaryFlightComputersGuard < 0.5,
            Pmdg777FirstOfficerFlightDirectorOn = isPmdg777 && pmdg777?.FirstOfficerFlightDirectorOn == true,
            Pmdg777ServiceInterphoneOff = isPmdg777 && pmdg777?.ServiceInterphoneOff == true,
            Pmdg777PassengerOxygenNormal = isPmdg777 && pmdg777?.PassengerOxygenNormal == true,
            Pmdg777ThrustAsymmetryCompensationAuto = isPmdg777 && pmdg777?.ThrustAsymmetryCompensationAuto == true,
            Pmdg777PrimaryFlightComputersAuto = isPmdg777 && pmdg777?.PrimaryFlightComputersAuto == true,
            Pmdg777ApuGeneratorSwitchOn = isPmdg777 && pmdg777?.ApuGeneratorSwitchOn == true,
            Pmdg777ApuRunning = isPmdg777 && pmdg777?.ApuRunning == true,
            Pmdg777ApuGeneratorPowerEstablished = isPmdg777 && pmdg777?.ApuGeneratorPowerEstablished == true,
            Pmdg777ApuBleedAirAvailable = isPmdg777 && pmdg777?.ApuBleedAirAvailable == true,
            Pmdg777BeforeStartChecklistComplete = isPmdg777 && pmdg777?.BeforeStartChecklistComplete == true,
            Pmdg777BeaconOn = isPmdg777 && pmdg777?.BeaconOn == true,
            Pmdg777HydraulicsBeforeStart = isPmdg777 && pmdg777?.HydraulicsBeforeStart == true,
            Pmdg777FuelPumpsBeforeStart = isPmdg777 && pmdg777?.FuelPumpsBeforeStart == true,
            Pmdg777CenterFuelPumpsRequired = isPmdg777 && pmdg777?.CenterFuelPumpsRequired == true,
            Pmdg777TransponderXpndr = isPmdg777 && pmdg777?.TransponderXpndr == true,
            Pmdg777SecondaryEngineDisplaySelected = isPmdg777 && pmdg777?.SecondaryEngineDisplaySelected == true,
            Pmdg777EngineOneStartSelectorStart = isPmdg777 && pmdg777?.EngineOneStartSelectorStart == true,
            Pmdg777EngineTwoStartSelectorStart = isPmdg777 && pmdg777?.EngineTwoStartSelectorStart == true,
            Pmdg777EngineOneStartValveOpen = isPmdg777 && pmdg777?.EngineOneStartValveOpen == true,
            Pmdg777EngineTwoStartValveOpen = isPmdg777 && pmdg777?.EngineTwoStartValveOpen == true,
            Pmdg777EngineOneFuelControlRun = isPmdg777 && pmdg777?.EngineOneFuelControlRun == true,
            Pmdg777EngineTwoFuelControlRun = isPmdg777 && pmdg777?.EngineTwoFuelControlRun == true,
            Pmdg777WheelChocksSet = isPmdg777 && pmdg777?.WheelChocksSet == true,
            Pmdg777ApuSelectorOff = isPmdg777 && pmdg777?.ApuSelectorOff == true,
            Pmdg777EngineBleedsAuto = isPmdg777 && pmdg777?.EngineBleedsAuto == true,
            Pmdg777PacksAuto = isPmdg777 && pmdg777?.PacksAuto == true,
            Pmdg777ApuBleedOff = isPmdg777 && pmdg777?.ApuBleedOff == true,
            Pmdg777ApuBleedAuto = isPmdg777 && pmdg777?.ApuBleedAuto == true,
            Pmdg777TakeoffFlapsSet = isPmdg777 && pmdg777?.TakeoffFlapsSet == true,
            Pmdg777TransponderTaRa = isPmdg777 && pmdg777?.TransponderTaRa == true,
            Pmdg777TaxiLightsSet = isPmdg777 && pmdg777?.TaxiLightsSet == true,
            Pmdg777TaxiLightsCommandedThisFlow = _pmdg777TaxiLightsCommandedThisFlow,
            Pmdg777TakeoffLightsSet = isPmdg777 && pmdg777?.TakeoffLightsSet == true,
            Pmdg777ClimbLightsSet = isPmdg777 && pmdg777?.ClimbLightsSet == true,
            Pmdg777GearLeverUp = isPmdg777 && pmdg777?.GearLeverUp == true,
            Pmdg777BeforeTaxiChecklistComplete = isPmdg777 && pmdg777?.BeforeTaxiChecklistComplete == true,
            Pmdg777BeforeTakeoffChecklistComplete = isPmdg777 && pmdg777?.BeforeTakeoffChecklistComplete == true,
            Pmdg777AfterTakeoffChecklistComplete = isPmdg777 && pmdg777?.AfterTakeoffChecklistComplete == true,
            Pmdg777LnavArmed = isPmdg777 && pmdg777?.LnavArmed == true,
            Pmdg777VnavArmed = isPmdg777 && pmdg777?.VnavArmed == true,
            Pmdg777FmcLandingFlaps = isPmdg777 ? pmdg777?.FmcLandingFlaps ?? 0 : 0,
            Pmdg777FmcLandingVref = isPmdg777 ? pmdg777?.FmcLandingVref ?? 0 : 0,
            Pmdg777LandingFlapsSet = isPmdg777 && pmdg777?.LandingFlapsSet == true,
            Pmdg777SpeedbrakeArmed = isPmdg777 && pmdg777?.SpeedbrakeArmed == true,
            Pmdg777AutobrakeSelector = isPmdg777 ? pmdg777?.AutobrakeSelector ?? 0 : 0,
            Pmdg777LandingLightsOn = isPmdg777 && pmdg777?.LandingLightsOn == true,
            Pmdg777AfterLandingLightsSet = isPmdg777 && pmdg777?.AfterLandingLightsSet == true,
            Pmdg777FuelPumpsOff = isPmdg777 && pmdg777?.FuelPumpsOff == true,
            Pmdg777HydraulicsShutdown = isPmdg777 && pmdg777?.HydraulicsShutdown == true,
            Pmdg777FlapsLever = isPmdg777 ? pmdg777?.FlapsLever ?? 0 : 0,
            TaxiClearanceReceived = _taxiClearanceReceived,
            TakeoffClearanceReceived = _takeoffClearanceReceived,
            Pmdg777EngineGeneratorOneSwitchOn = isPmdg777 && pmdg777?.EngineGeneratorOneSwitchOn == true,
            Pmdg777EngineGeneratorTwoSwitchOn = isPmdg777 && pmdg777?.EngineGeneratorTwoSwitchOn == true,
            Pmdg777BackupGeneratorOneSwitchOn = isPmdg777 && pmdg777?.BackupGeneratorOneSwitchOn == true,
            Pmdg777BackupGeneratorTwoSwitchOn = isPmdg777 && pmdg777?.BackupGeneratorTwoSwitchOn == true,
            Pmdg777LeftSideWindowHeatOn = isPmdg777 && pmdg777?.LeftSideWindowHeatOn == true,
            Pmdg777LeftForwardWindowHeatOn = isPmdg777 && pmdg777?.LeftForwardWindowHeatOn == true,
            Pmdg777RightForwardWindowHeatOn = isPmdg777 && pmdg777?.RightForwardWindowHeatOn == true,
            Pmdg777RightSideWindowHeatOn = isPmdg777 && pmdg777?.RightSideWindowHeatOn == true,
            Pmdg777LeftEnginePrimaryHydraulicPumpOn = isPmdg777 && pmdg777?.LeftEnginePrimaryHydraulicPumpOn == true,
            Pmdg777RightEnginePrimaryHydraulicPumpOn = isPmdg777 && pmdg777?.RightEnginePrimaryHydraulicPumpOn == true,
            Pmdg777FirePanelNormal = isPmdg777 && pmdg777?.FirePanelNormal == true,
            Pmdg777EngineControlPanelNormal = isPmdg777 && pmdg777?.EngineControlPanelNormal == true,
            Pmdg777FuelPanelPreflight = isPmdg777 && pmdg777?.FuelPanelPreflight == true,
            Pmdg777AntiIceAuto = isPmdg777 && pmdg777?.AntiIceAuto == true,
            Pmdg777ExteriorLightsPreflight = isPmdg777 && pmdg777?.ExteriorLightsPreflight == true,
            Pmdg777AirPanelPreflight = isPmdg777 && pmdg777?.AirPanelPreflight == true,
            Pmdg777AutobrakeRto = isPmdg777 && pmdg777?.AutobrakeRto == true,
            Pmdg777TransponderAltitudeSourceNormal = isPmdg777 && pmdg777?.TransponderAltitudeSourceNormal == true,
            Pmdg777SeatBeltsOff = isPmdg777 && pmdg777?.SeatBeltsOff == true,
            Pmdg777SeatBeltsAuto = isPmdg777 && pmdg777?.SeatBeltsAuto == true,
            Pmdg777NoSmokingAuto = isPmdg777 && pmdg777?.NoSmokingAuto == true,
            Pmdg777FuelToRemainSelectorIn = isPmdg777 && pmdg777?.FuelToRemainSelectorIn == true,
            Pmdg777TemperatureControlsPreflight = isPmdg777 && pmdg777?.TemperatureControlsPreflight == true,
            Pmdg777FirstOfficerNdMap = isPmdg777 && pmdg777?.FirstOfficerNdMap == true,
            Pmdg777FireOverheatTestComplete = isPmdg777 && _pmdg777Runtime.FireOverheatTestObserved,
            Pmdg777FirstOfficerOxygenTestComplete = isPmdg777 && _pmdg777Runtime.FirstOfficerOxygenTestObserved,
            Pmdg777FirstOfficerSourcesNormal = isPmdg777 && pmdg777?.FirstOfficerSourcesNormal == true,
            Pmdg777FirstOfficerDisplaysReady = isPmdg777 && pmdg777?.FirstOfficerDisplaysReady == true,
            Pmdg777SpeedbrakeDown = isPmdg777 && pmdg777?.SpeedbrakeDown == true,
            Pmdg777FlapsUp = isPmdg777 && pmdg777?.FlapsUp == true,
            Pmdg777FuelControlsCutoff = isPmdg777 && pmdg777?.FuelControlsCutoff == true,
            Pmdg777TransponderStandby = isPmdg777 && pmdg777?.TransponderStandby == true,
            Pmdg777McpAltitude = isPmdg777 ? pmdg777?.McpAltitude ?? 0 : 0,
            Pmdg777FmcPerformanceInputComplete = isPmdg777 && pmdg777?.FmcPerformanceInputComplete == true,
            Pmdg777FmcTakeoffFlaps = isPmdg777 ? pmdg777?.FmcTakeoffFlaps ?? 0 : 0,
            Pmdg777FmcV1 = isPmdg777 ? pmdg777?.FmcV1 ?? 0 : 0,
            Pmdg777FmcVr = isPmdg777 ? pmdg777?.FmcVr ?? 0 : 0,
            Pmdg777FmcV2 = isPmdg777 ? pmdg777?.FmcV2 ?? 0 : 0,
            Pmdg777FmcCruiseAltitude = isPmdg777 ? pmdg777?.FmcCruiseAltitude ?? 0 : 0,
            Pmdg777FmcDistanceToDestination = isPmdg777 ? pmdg777?.FmcDistanceToDestination ?? -1 : -1,
            Pmdg777FmcFlightNumber = isPmdg777 ? pmdg777?.FmcFlightNumber ?? string.Empty : string.Empty,
            Pmdg777PreflightChecklistComplete = isPmdg777 && pmdg777?.PreflightChecklistComplete == true,
            Pmdg777IrsAligned = isPmdg777 && pmdg777?.IrsAligned == true,
            OnGround = raw.OnGround != 0,
            GroundSpeedKnots = raw.GroundSpeed,
            LongitudinalVelocityKnots = raw.LongitudinalVelocity * 0.592483801,
            MagneticHeadingDegrees = raw.MagneticHeading,
            Engine1Running = isFlyByWireAirbus
                ? _nativeRuntime.Fbw.Engine1State == 1 || (_nativeRuntime.Fbw.Engine1N1 ?? (float)raw.Engine1N1) >= 15
                : isPmdg737
                    ? raw.Engine1Combustion != 0 || raw.Engine1N1 >= 15
                : raw.Engine1Combustion != 0,
            Engine2Running = isFlyByWireAirbus
                ? _nativeRuntime.Fbw.Engine2State == 1 || (_nativeRuntime.Fbw.Engine2N1 ?? (float)raw.Engine2N1) >= 15
                : isPmdg737
                    ? raw.Engine2Combustion != 0 || raw.Engine2N1 >= 15
                : raw.Engine2Combustion != 0,
            Engine1StarterActive = isPmdg777
                ? pmdg777?.EngineOneStartValveOpen == true || raw.Engine1Starter != 0
                : isFlyByWireAirbus
                ? _nativeRuntime.Fbw.Engine1StarterValveOpen == true
                  || _nativeRuntime.Fbw.Engine1State == 2
                  || _nativeRuntime.Fbw.Engine1State == 3
                  || raw.Engine1Starter != 0
                : isPmdg737
                    ? pmdg?.Engine1StartValveOpen == true || raw.Engine1Starter != 0
                : raw.Engine1Starter != 0,
            Engine2StarterActive = isPmdg777
                ? pmdg777?.EngineTwoStartValveOpen == true || raw.Engine2Starter != 0
                : isFlyByWireAirbus
                ? _nativeRuntime.Fbw.Engine2StarterValveOpen == true
                  || _nativeRuntime.Fbw.Engine2State == 2
                  || _nativeRuntime.Fbw.Engine2State == 3
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
                ? _nativeRuntime.Fbw.Engine1N1 ?? raw.Engine1N1
                : raw.Engine1N1,
            Engine2N1Percent = isFlyByWireAirbus
                ? _nativeRuntime.Fbw.Engine2N1 ?? raw.Engine2N1
                : raw.Engine2N1,
            Engine1N2Percent = raw.Engine1N2,
            Engine2N2Percent = raw.Engine2N2,
            Engine1EgtCelsius = raw.Engine1Egt,
            Engine2EgtCelsius = raw.Engine2Egt,
            Engine1FuelFlowPph = raw.Engine1FuelFlow,
            Engine2FuelFlowPph = raw.Engine2FuelFlow,
            EngineModeSelectorPosition = engineModeSelectorPosition,
            FbwEngine1State = _nativeRuntime.Fbw.Engine1State,
            FbwEngine2State = _nativeRuntime.Fbw.Engine2State,
            Battery1On = isIniBuildsA310
                ? _nativeRuntime.A310.Battery1Auto == true
                : isIniBuildsAirbusFamily
                ? _nativeRuntime.NativeAirbus.Battery1On ?? raw.Battery1 != 0
                : isFlyByWireAirbus
                    ? ResolveFbwBatteryState(
                        _nativeRuntime.Fbw.CommandedBattery1Auto,
                        _nativeRuntime.Fbw.Battery1AutoTyped,
                        _nativeRuntime.Fbw.Battery1Auto,
                        raw.Battery1)
                : isPmdg737
                    ? pmdg != null && pmdg.BatterySelector != 0
                : isAsobo737Max
                    ? _asobo737MaxRuntime.BatteryCoverInputEventOn == true
                : raw.Battery1 != 0,
            Battery2On = isIniBuildsA310
                ? _nativeRuntime.A310.Battery2Auto == true
                : isIniBuildsAirbusFamily
                ? _nativeRuntime.NativeAirbus.Battery2On ?? raw.Battery2 != 0
                : isFlyByWireAirbus
                    ? ResolveFbwBatteryState(
                        _nativeRuntime.Fbw.CommandedBattery2Auto,
                        _nativeRuntime.Fbw.Battery2AutoTyped,
                        _nativeRuntime.Fbw.Battery2Auto,
                        raw.Battery2)
                : isPmdg737
                    ? pmdg != null && pmdg.BatterySelector != 0
                : isAsobo737Max
                    ? _asobo737MaxRuntime.BatteryCoverInputEventOn == true
                : raw.Battery2 != 0,
            Battery3On = isIniBuildsA310 && _nativeRuntime.A310.Battery3Auto == true,
            A310HydraulicPanelSafe = isIniBuildsA310 && A310HydraulicPanelSafe(),
            A310WipersAndWeatherRadarOff = isIniBuildsA310 && A310WipersAndWeatherRadarOff(),
            A310ApuFireTestCompleted = isIniBuildsA310
                && _nativeRuntime.A310.ApuFireTestObserved
                && _nativeRuntime.A310.ApuLoopTestObserved,
            A310AnnunciatorTestCompleted = isIniBuildsA310 && _nativeRuntime.A310.AnnunciatorTestObserved,
            A310InitialExteriorLightsSet = isIniBuildsA310 && A310InitialExteriorLightsSet(),
            A310PreflightSignsSet = isIniBuildsA310 && A310PreflightSignsSet(),
            A310AutoflightComputersSet = isIniBuildsA310 && A310AutoflightComputersSet(),
            A310PreflightHeatSet = isIniBuildsA310 && A310PreflightHeatSet(),
            A310EmergencyExitArmed = isIniBuildsA310 && A310EmergencyExitArmed(),
            A310CargoSmokeTestCompleted = isIniBuildsA310
                && _nativeRuntime.A310.CargoSmokeTestObserved,
            A310EgpwsTestCompleted = isIniBuildsA310 && _nativeRuntime.A310.EgpwsTestObserved,
            A310PreflightPedestalSet = isIniBuildsA310 && A310PreflightPedestalSet(),
            A310ApuPowerAndBleedSet = isIniBuildsA310 && A310ApuPowerAndBleedSet(),
            A310TransponderXpdrSet = isIniBuildsA310 && A310TransponderXpdrSet(),
            A310IgnitionSelectedForStart = isIniBuildsA310 && A310IgnitionSelectedForStart(),
            A310PacksClosedForStart = isIniBuildsA310 && A310PacksClosedForStart(),
            A310Engine1StarterSelected = isIniBuildsA310 && _nativeRuntime.A310.Flow4EngineStartStates[3] > 0.5f,
            A310Engine2StarterSelected = isIniBuildsA310 && _nativeRuntime.A310.Flow4EngineStartStates[4] > 0.5f,
            A310Engine1FuelLeverOn = isIniBuildsA310 && _nativeRuntime.A310.Flow4EngineStartStates[5] > 0.5f,
            A310Engine2FuelLeverOn = isIniBuildsA310 && _nativeRuntime.A310.Flow4EngineStartStates[6] > 0.5f,
            A310FuelPumpsOn = isIniBuildsA310 && A310FuelPumpsOn(),
            A310IgnitionOff = isIniBuildsA310
                              && IsA310IgnitionOff(
                                  _nativeRuntime.A310.Flow4EngineStartStates[0],
                                  engineModeSelectorPosition),
            A310RudderTrimCentered = isIniBuildsA310
                                      && _nativeRuntime.A310.Flow2States[19].HasValue
                                      && Math.Abs(_nativeRuntime.A310.Flow2States[19]!.Value) < 0.05,
            A310TaxiLightTaxi = isIniBuildsA310
                                 && _nativeRuntime.A310.InitialLightStates[2].HasValue
                                 && Math.Abs(_nativeRuntime.A310.InitialLightStates[2]!.Value - 1) < 0.1,
            A310AutobrakeMax = isIniBuildsA310 && A310AutobrakeMaxSelected(),
            A310WeatherRadarOn = isIniBuildsA310
                                  && _nativeRuntime.A310.WeatherRadarSystem.HasValue
                                  && Math.Abs(_nativeRuntime.A310.WeatherRadarSystem.Value) < 0.1
                                  && _nativeRuntime.A310.Flow5States[0].HasValue
                                  && Math.Abs(_nativeRuntime.A310.Flow5States[0]!.Value - 2) < 0.1,
            A310TakeoffExteriorLightsSet = isIniBuildsA310 && A310TakeoffExteriorLightsSet(),
            A310IgnitionContinuousRelight = isIniBuildsA310 && A310IgnitionContinuousRelight(),
            A310PacksOn = isIniBuildsA310 && A310PacksOn(),
            A310TcasTaRaSet = isIniBuildsA310 && A310TcasTaRaSet(),
            A310ClimbLightsSet = isIniBuildsA310 && A310ClimbLightsSet(),
            A310LandingLightsRetracted = isIniBuildsA310 && A310LandingLightsRetracted(),
            A310ApproachLightsSet = isIniBuildsA310 && A310ApproachLightsSet(),
            A310NoseLightTakeoff = isIniBuildsA310 && A310NoseLightTakeoff(),
            A310AfterLandingLightsSet = isIniBuildsA310 && A310AfterLandingLightsSet(),
            A310TransponderStandby = isIniBuildsA310 && A310TransponderStandby(),
            A310WeatherRadarOff = isIniBuildsA310 && A310WeatherRadarOff(),
            A310NoseLightOff = isIniBuildsA310 && A310NoseLightOff(),
            A310SeatbeltsOff = isIniBuildsA310 && A310SeatbeltsOff(),
            A310FuelPumpsParkingSet = isIniBuildsA310 && A310FuelPumpsParkingSet(),
            A310ProbeHeatOff = isIniBuildsA310 && A310ProbeHeatOff(),
            A310IrsOff = isIniBuildsA310 && A310IrsOff(),
            A310OxygenOff = isIniBuildsA310 && A310OxygenOff(),
            A310ExteriorLightsOff = isIniBuildsA310 && A310ExteriorLightsOff(),
            A310EmergencyExitDisarmed = isIniBuildsA310 && A310EmergencyExitDisarmed(),
            A310BatteriesOff = isIniBuildsA310 && A310BatteriesOff(),
            Battery1Voltage = raw.Battery1Voltage,
            Battery2Voltage = raw.Battery2Voltage,
            ApuBatteryOn = !isIniBuildsA330
                || _nativeRuntime.A330.ApuBatteryInputEventOn == true,
            ExternalPowerAvailable = isFlyByWireAirbus
                ? ResolveFbwAnyTrueState(
                    _nativeRuntime.Fbw.ExternalPowerAvailableTyped,
                    _nativeRuntime.Fbw.ExternalPowerAvailable,
                    raw.ExternalPowerAvailableUnindexed,
                    raw.ExternalPowerAvailable,
                    _nativeRuntime.Fbw.A380ExternalPower1AvailableTyped,
                    _nativeRuntime.Fbw.A380ExternalPower2AvailableTyped,
                    _nativeRuntime.Fbw.A380ExternalPower3AvailableTyped,
                    _nativeRuntime.Fbw.A380ExternalPower4AvailableTyped,
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
                    _nativeRuntime.Fbw.ExternalPowerOnTyped,
                    _nativeRuntime.Fbw.ExternalPowerOn,
                    raw.ExternalPowerOnUnindexed,
                    raw.ExternalPowerOn,
                    _nativeRuntime.Fbw.A380ExternalPower1OnTyped,
                    _nativeRuntime.Fbw.A380ExternalPower2OnTyped,
                    _nativeRuntime.Fbw.A380ExternalPower3OnTyped,
                    _nativeRuntime.Fbw.A380ExternalPower4OnTyped,
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
                ? _nativeRuntime.Fbw.ParkingBrake == true
                : isPmdg737 && pmdg != null
                    ? pmdg.ParkingBrakeAnnunciated || raw.ParkingBrake != 0
                : raw.ParkingBrake != 0,
            BeaconOn = isIniBuildsA310 && _nativeRuntime.A310.InitialLightStates[1].HasValue
                ? _nativeRuntime.A310.InitialLightStates[1] > 0.5f
                : isPmdg737 && pmdg != null
                ? pmdg.AntiCollisionOn
                : isAsobo737Max && _asobo737MaxRuntime.AntiCollisionInputState.HasValue
                    ? Asobo737MaxControlProfile.IsAntiCollisionOn(
                        _asobo737MaxRuntime.AntiCollisionInputState.Value)
                : raw.Beacon != 0,
            NavigationLightsOn = isPmdg737 && pmdg != null
                ? _pmdgNg3Runtime.ResolveNavigationLightsOn(nowUtc)
                : isAsobo737Max && _asobo737MaxRuntime.PositionLightInputState.HasValue
                    ? Math.Abs(_asobo737MaxRuntime.PositionLightInputState.Value - 0) < 0.1
                      || Math.Abs(_asobo737MaxRuntime.PositionLightInputState.Value - 2) < 0.1
                : raw.NavigationLights != 0,
            LogoLightsOn = isPmdg737 && pmdg != null
                ? _pmdgNg3Runtime.ResolveLogoLightsOn(nowUtc)
                : raw.LogoLights != 0,
            NavLogoSelectorPosition = isFlyByWireAirbus
                ? ResolveFbwNavLogoSelectorPosition(_nativeRuntime.Fbw.NavLogoSelectorTyped, _nativeRuntime.Fbw.NavLogoSelector)
                : isIniBuildsA330 && _nativeRuntime.A330.NavLogoInputState.HasValue
                    ? _nativeRuntime.A330.NavLogoInputState.Value
                : isPmdg737 && pmdg != null
                    ? _pmdgNg3Runtime.ResolveNavLogoSelectorPosition(nowUtc)
                : _nativeRuntime.NativeAirbus.NavLogoSelectorPosition,
            ApuRpmPercent = raw.ApuRpm,
            ApuStarterPercent = raw.ApuStarter,
            ApuMasterSwitchOn = isFlyByWireAirbus
                ? _nativeRuntime.Fbw.ApuMasterSwitch == true
                : isIniBuildsA330 && _nativeRuntime.A330.ApuInputStates[0].HasValue
                    ? _nativeRuntime.A330.ApuInputStates[0]!.Value >= 0.5
                : isPmdg737 && pmdg != null
                    ? pmdg.ApuSelector >= 1
                : isAsobo737Max && _asobo737MaxRuntime.ApuInputState.HasValue
                    ? Math.Abs(_asobo737MaxRuntime.ApuInputState.Value - 1) < 0.1
                : _nativeRuntime.NativeAirbus.ApuMasterSwitch.HasValue
                    ? _nativeRuntime.NativeAirbus.ApuMasterSwitch.Value != 0
                    : raw.ApuMasterSwitch != 0,
            ApuAvailable = isIniBuildsA310
                ? _nativeRuntime.A310.Flow3ApuStates[2] > 0.5f
                : isFlyByWireAirbus
                ? _nativeRuntime.Fbw.ApuStartAvailable == true
                : isIniBuildsA330
                    ? _nativeRuntime.NativeAirbus.ApuAvailable.HasValue
                      && _nativeRuntime.NativeAirbus.ApuAvailable.Value != 0
                : isPmdg737 && pmdg != null
                    ? pmdgApuAvailable
                : isPmdg777
                    ? pmdg777?.ApuRunning == true
                : isAsobo737Max
                    ? IsAsobo737MaxApuAvailable(raw.ApuRpm, raw.ApuVolts)
                : _nativeRuntime.NativeAirbus.ApuAvailable.HasValue && _nativeRuntime.NativeAirbus.ApuAvailable.Value != 0,
            ApuStartButtonOn = isFlyByWireAirbus
                ? _nativeRuntime.Fbw.ApuStartButton == true || _nativeRuntime.Fbw.ApuStartAvailable == true
                : isIniBuildsA330
                    ? _nativeRuntime.NativeAirbus.ApuStartButton.HasValue
                      && _nativeRuntime.NativeAirbus.ApuStartButton.Value != 0
                : isPmdg737 && pmdg != null
                    ? pmdg.ApuSelector == 2 || raw.ApuStarter > 0
                : isAsobo737Max && _asobo737MaxRuntime.ApuInputState.HasValue
                    ? Math.Abs(_asobo737MaxRuntime.ApuInputState.Value) < 0.1 || raw.ApuStarter > 0
                : _nativeRuntime.NativeAirbus.ApuStartButton.HasValue && _nativeRuntime.NativeAirbus.ApuStartButton.Value != 0,
            ApuSpoolingOrAvailable = isPmdg737 && pmdg != null
                ? pmdg.ApuEgtNeedle > 0 || pmdgApuAvailable
                : raw.ApuRpm > 5 || raw.ApuStarter > 0,
            ApuBleedOn = isIniBuildsA310
                ? _nativeRuntime.A310.Flow3ApuStates[3] > 0.5f
                : isFlyByWireAirbus
                ? _nativeRuntime.Fbw.ApuBleedButton == true
                : isIniBuildsA330 && _nativeRuntime.A330.ApuInputStates[2].HasValue
                    ? _nativeRuntime.A330.ApuInputStates[2]!.Value >= 0.5
                : isPmdg737 && pmdg != null
                    ? pmdg.ApuBleedOn
                : isAsobo737Max && _asobo737MaxRuntime.ApuBleedInputState.HasValue
                    ? Asobo737MaxBinarySwitchIsOn(_asobo737MaxRuntime.ApuBleedInputState.Value)
                : _nativeRuntime.NativeAirbus.ApuBleedButton.HasValue && _nativeRuntime.NativeAirbus.ApuBleedButton.Value != 0,
            ApuBleedWarmupComplete = isPmdg737
                ? pmdgApuBleedWarmupComplete
                : true,
            LeftPackSwitchPosition = isPmdg737 && pmdg != null
                ? pmdg.LeftPackSwitch
                : isAsobo737Max && _asobo737MaxRuntime.LeftPackInputState.HasValue
                    ? Asobo737MaxPackPosition(_asobo737MaxRuntime.LeftPackInputState.Value)
                : null,
            RightPackSwitchPosition = isPmdg737 && pmdg != null
                ? pmdg.RightPackSwitch
                : isAsobo737Max && _asobo737MaxRuntime.RightPackInputState.HasValue
                    ? Asobo737MaxPackPosition(_asobo737MaxRuntime.RightPackInputState.Value)
                : null,
            BoeingEngineBleed1On = isAsobo737Max
                && _asobo737MaxRuntime.EngineBleedInputStates[0] >= 0.5,
            BoeingEngineBleed2On = isAsobo737Max
                && _asobo737MaxRuntime.EngineBleedInputStates[1] >= 0.5,
            IsolationValvePosition = isPmdg737 && pmdg != null
                ? pmdg.IsolationValveSwitch
                : isAsobo737Max && _asobo737MaxRuntime.IsolationValveInputState.HasValue
                    ? Asobo737MaxIsolationValvePosition(_asobo737MaxRuntime.IsolationValveInputState.Value)
                : null,
            LeftDuctPressurePsi = isPmdg737 && pmdg != null
                ? pmdg.LeftDuctPressurePsi
                : 0,
            RightDuctPressurePsi = isPmdg737 && pmdg != null
                ? pmdg.RightDuctPressurePsi
                : 0,
            ApuFlapPercent = _nativeRuntime.NativeAirbus.ApuFlapPercent ?? 0,
            ApuGeneratorActive = raw.ApuGeneratorActive != 0,
            ApuGeneratorSwitchOn = isIniBuildsA310
                ? _nativeRuntime.A310.Flow3ApuStates[4] > 0.5f
                : _nativeRuntime.NativeAirbus.ApuGeneratorOn.HasValue
                                   && !isPmdg737
                                   ? _nativeRuntime.NativeAirbus.ApuGeneratorOn.Value != 0
                                   : isPmdg737 && pmdg != null
                                       ? pmdg.ApuGen1On && pmdg.ApuGen2On
                                   : isAsobo737Max && Asobo737MaxApuGeneratorsReady()
                                       ? Asobo737MaxApuGeneratorsOn()
                                       : raw.ApuGeneratorSwitch != 0,
            ApuGeneratorPowerEstablished = isPmdg737
                ? pmdgApuPowerEstablished
                : isAsobo737Max && Asobo737MaxApuGeneratorsReady()
                    ? raw.ApuGeneratorActive != 0
                      || (IsAsobo737MaxApuAvailable(raw.ApuRpm, raw.ApuVolts)
                          && Asobo737MaxApuGeneratorsOn())
                : raw.ApuGeneratorActive != 0,
            EngineGeneratorsOn = isPmdg737 && pmdg != null
                ? pmdg.EngineGen1On
                  && pmdg.EngineGen2On
                  && !pmdg.GenBus1Off
                  && !pmdg.GenBus2Off
                : isAsobo737Max && _asobo737MaxRuntime.EngineGeneratorInputStates.All(state => state.HasValue)
                    ? _asobo737MaxRuntime.EngineGeneratorInputStates.All(state => state!.Value >= 0.5)
                : raw.Engine1Combustion != 0 && raw.Engine2Combustion != 0,
            ApuGenOffBus = isPmdg737 && pmdg != null && pmdg.ApuGenOffBus,
            AcTransferBus1Powered = isPmdg737 && pmdg != null && pmdg.AcTransferBus1Powered,
            AcTransferBus2Powered = isPmdg737 && pmdg != null && pmdg.AcTransferBus2Powered,
            TransferBus1Off = isPmdg737 && pmdg != null && pmdg.TransferBus1Off,
            TransferBus2Off = isPmdg737 && pmdg != null && pmdg.TransferBus2Off,
            BoeingElectricHydraulicPumpsOn = isPmdg737 && pmdg != null
                && pmdg.ElectricHydraulicPump1On
                && pmdg.ElectricHydraulicPump2On
                || isAsobo737Max
                && _asobo737MaxRuntime.ElectricHydraulicPumpInputStates.All(state => state.HasValue)
                && _asobo737MaxRuntime.ElectricHydraulicPumpInputStates.All(
                    state => Asobo737MaxControlProfile.IsElectricHydraulicPumpOn(state!.Value)),
            BoeingElectricHydraulicPump1On = isPmdg737 && pmdg != null && pmdg.ElectricHydraulicPump1On
                || isAsobo737Max
                && _asobo737MaxRuntime.ElectricHydraulicPumpInputStates[0].HasValue
                && Asobo737MaxControlProfile.IsElectricHydraulicPumpOn(
                    _asobo737MaxRuntime.ElectricHydraulicPumpInputStates[0]!.Value),
            BoeingElectricHydraulicPump2On = isPmdg737 && pmdg != null && pmdg.ElectricHydraulicPump2On
                || isAsobo737Max
                && _asobo737MaxRuntime.ElectricHydraulicPumpInputStates[1].HasValue
                && Asobo737MaxControlProfile.IsElectricHydraulicPumpOn(
                    _asobo737MaxRuntime.ElectricHydraulicPumpInputStates[1]!.Value),
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
                : isAsobo737Max && Asobo737MaxFuelPumpInputEventsReady()
                    ? Asobo737MaxFuelPumpsConfigured()
                : (_nativeRuntime.NativeAirbus.FuelPump1 ?? (float)raw.FuelPump1) != 0
                  && (_nativeRuntime.NativeAirbus.FuelPump2 ?? (float)raw.FuelPump2) != 0
                  && (_nativeRuntime.NativeAirbus.FuelPump3 ?? (float)raw.FuelPump3) != 0
                  && (_nativeRuntime.NativeAirbus.FuelPump4 ?? (float)raw.FuelPump4) != 0
                  && (_nativeRuntime.NativeAirbus.FuelPump5 ?? 0) != 0
                  && (_nativeRuntime.NativeAirbus.FuelPump6 ?? 0) != 0,
            FuelPump1State = isFlyByWireAirbus ? raw.FuelPump2 : isPmdg737 && pmdg != null ? (pmdg.LeftAftFuelPump ? 1 : 0) : isIniBuildsA330 && A330FuelPumpInputEventsReady() ? _nativeRuntime.A330.FuelPumpInputStates[0]!.Value : isAsobo737Max && Asobo737MaxFuelPumpInputEventsReady() ? Asobo737MaxFuelPumpIsOn(_asobo737MaxRuntime.FuelPumpInputStates[0]!.Value) ? 1 : 0 : _nativeRuntime.NativeAirbus.FuelPump1 ?? raw.FuelPump1,
            FuelPump2State = isFlyByWireAirbus ? raw.FbwFuelPump5 : isPmdg737 && pmdg != null ? (pmdg.LeftForwardFuelPump ? 1 : 0) : isIniBuildsA330 && A330FuelPumpInputEventsReady() ? _nativeRuntime.A330.FuelPumpInputStates[1]!.Value : isAsobo737Max && Asobo737MaxFuelPumpInputEventsReady() ? Asobo737MaxFuelPumpIsOn(_asobo737MaxRuntime.FuelPumpInputStates[1]!.Value) ? 1 : 0 : _nativeRuntime.NativeAirbus.FuelPump2 ?? raw.FuelPump2,
            FuelPump3State = isFlyByWireAirbus ? raw.FbwFuelValve9 : isPmdg737 && pmdg != null ? (pmdg.RightForwardFuelPump ? 1 : 0) : isIniBuildsA330 && A330FuelPumpInputEventsReady() ? _nativeRuntime.A330.FuelPumpInputStates[2]!.Value : isAsobo737Max && Asobo737MaxFuelPumpInputEventsReady() ? Asobo737MaxFuelPumpIsOn(_asobo737MaxRuntime.FuelPumpInputStates[2]!.Value) ? 1 : 0 : _nativeRuntime.NativeAirbus.FuelPump3 ?? raw.FuelPump3,
            FuelPump4State = isFlyByWireAirbus ? raw.FbwFuelValve10 : isPmdg737 && pmdg != null ? (pmdg.RightAftFuelPump ? 1 : 0) : isIniBuildsA330 && A330FuelPumpInputEventsReady() ? _nativeRuntime.A330.FuelPumpInputStates[3]!.Value : isAsobo737Max && Asobo737MaxFuelPumpInputEventsReady() ? Asobo737MaxFuelPumpIsOn(_asobo737MaxRuntime.FuelPumpInputStates[3]!.Value) ? 1 : 0 : _nativeRuntime.NativeAirbus.FuelPump4 ?? raw.FuelPump4,
            FuelPump5State = isFlyByWireAirbus ? raw.FuelPump3 : isPmdg737 && pmdg != null ? (pmdg.LeftCenterFuelPump ? 1 : 0) : isIniBuildsA330 && A330FuelPumpInputEventsReady() ? _nativeRuntime.A330.FuelPumpInputStates[4]!.Value : isAsobo737Max && Asobo737MaxFuelPumpInputEventsReady() ? Asobo737MaxFuelPumpIsOn(_asobo737MaxRuntime.FuelPumpInputStates[4]!.Value) ? 1 : 0 : _nativeRuntime.NativeAirbus.FuelPump5 ?? 0,
            FuelPump6State = isFlyByWireAirbus ? raw.FbwFuelPump6 : isPmdg737 && pmdg != null ? (pmdg.RightCenterFuelPump ? 1 : 0) : isIniBuildsA330 && A330FuelPumpInputEventsReady() ? _nativeRuntime.A330.FuelPumpInputStates[5]!.Value : isAsobo737Max && Asobo737MaxFuelPumpInputEventsReady() ? Asobo737MaxFuelPumpIsOn(_asobo737MaxRuntime.FuelPumpInputStates[5]!.Value) ? 1 : 0 : _nativeRuntime.NativeAirbus.FuelPump6 ?? 0,
            AltitudeAboveGroundFeet = raw.AltitudeAboveGround,
            IndicatedAltitudeFeet = raw.IndicatedAltitude,
            TransitionAltitudeFeet = _settings.TransitionAltitudeFeet,
            CaptainAltimeterStandard = isIniBuildsA310
                                        && _nativeRuntime.A310.AltimeterStandardStates[0].HasValue
                ? _nativeRuntime.A310.AltimeterStandardStates[0]!.Value > 0.5f
                : raw.CaptainBaroStandard != 0,
            FirstOfficerAltimeterStandard = isIniBuildsA310
                                             && _nativeRuntime.A310.AltimeterStandardStates[1].HasValue
                ? _nativeRuntime.A310.AltimeterStandardStates[1]!.Value > 0.5f
                : raw.FirstOfficerBaroStandard != 0,
            IndicatedAirspeedKnots = raw.IndicatedAirspeed,
            AutopilotSelectedAirspeedKnots = raw.AutopilotSelectedAirspeed,
            TakeoffV1SpeedKnots = effectiveV1,
            TakeoffRotateSpeedKnots = effectiveVr,
            TakeoffV2SpeedKnots = effectiveV2,
            SimBriefFlightNumber = activePlan?.FlightNumber ?? "",
            SimBriefOriginIcao = activePlan?.OriginIcao ?? "",
            SimBriefDestinationIcao = activePlan?.DestinationIcao ?? "",
            SimBriefAlternateIcao = activePlan?.AlternateIcao ?? "",
            SimBriefOriginRunway = activePlan?.OriginRunway ?? "",
            SimBriefDestinationRunway = activePlan?.DestinationRunway ?? "",
            SimBriefRoute = activePlan?.Route ?? "",
            PlannedCruiseAltitudeFeet = activePlan?.CruiseAltitudeFeet,
            PlannedCostIndex = activePlan?.CostIndex,
            PlannedTakeoffFlaps = plannedTakeoffFlaps,
            PlannedBlockFuelKilograms = plannedBlockFuelKg,
            ActualFuelKilograms = actualFuelKg,
            SimBriefFuelStatus = SimBriefOperationalContext.FuelComparison(plannedBlockFuelKg, actualFuelKg),
            BoeingFmcV1Knots = cockpitV1,
            BoeingFmcVrKnots = cockpitVr,
            BoeingFmcTakeoffReferenceComplete = isPmdg737 && pmdg?.FmcPerfInputComplete == true,
            SimBriefTakeoffStatus = SimBriefOperationalContext.TakeoffComparison(activePlan, aircraftVariant, cockpitV1, cockpitVr, cockpitFlaps),
            SayIntentionsAtcActive = _sayIntentionsRuntime.Flight != null
                                  && _settings.UseSayIntentionsCopilotCommunications,
            SayIntentionsApproachRunway = _sayIntentionsRuntime.ApproachRunway,
            SayIntentionsApproachIsIls = _sayIntentionsRuntime.ApproachIsIls,
            ApproachDistanceToTouchdownNm = approachDistance.DistanceNm,
            ApproachDistanceSource = approachDistance.Source,
            ApproachFlaps1DistanceNm = approachSchedule.Flaps1DistanceNm,
            ApproachFlaps1AltitudeFeet = approachSchedule.Flaps1AltitudeFeet,
            ApproachFlaps1SpeedKnots = approachSchedule.Flaps1SpeedKnots,
            ApproachFlaps2DistanceNm = approachSchedule.Flaps2DistanceNm,
            ApproachFlaps2AltitudeAglFeet = approachSchedule.Flaps2AltitudeAglFeet,
            ApproachFlaps2SpeedKnots = approachSchedule.Flaps2SpeedKnots,
            ApproachGearDistanceNm = approachSchedule.GearDistanceNm,
            ApproachGearAltitudeAglFeet = approachSchedule.GearAltitudeAglFeet,
            ApproachGearSpeedKnots = approachSchedule.GearSpeedKnots,
            ApproachLandingConfigDistanceNm = approachSchedule.LandingConfigDistanceNm,
            ApproachLandingConfigAltitudeAglFeet =
                approachSchedule.LandingConfigAltitudeAglFeet,
            ApproachLandingConfigSpeedKnots =
                approachSchedule.LandingConfigSpeedKnots,
            ApproachFlapsFullSpeedKnots = approachSchedule.FlapsFullSpeedKnots,
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
            FlapsHandleIndex = isFlyByWireAirbus
                ? FbwStateResolvers.ResolveFlapsHandleIndex(
                    _nativeRuntime.Fbw.FlapsHandleIndex,
                    raw.FlapsHandleIndex)
                : isAsobo737Max && _asobo737MaxRuntime.FlapsInputState.HasValue
                    ? Asobo737MaxFlapsHandleIndex(_asobo737MaxRuntime.FlapsInputState.Value)
                    : raw.FlapsHandleIndex,
            BoeingTakeoffFlaps = cockpitFlaps ?? plannedTakeoffFlaps,
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
                : isAsobo737Max
                    ? Asobo737MaxControlProfile.NormalizeGearHandlePosition(
                        raw.GearHandle)
                : isIniBuildsA310 && _nativeRuntime.A310.GearHandleStatus.HasValue
                    ? _nativeRuntime.A310.GearHandleStatus.Value
                    : _nativeRuntime.NativeAirbus.GearHandlePosition.HasValue
                        ? _nativeRuntime.NativeAirbus.GearHandlePosition.Value >= 0.5 ? 2 : 0
                        : raw.GearHandle != 0 ? 2 : 0,
            GearHandleDown = isFlyByWireAirbus
                ? raw.GearHandle != 0
                : isPmdg737 && pmdg != null
                    ? pmdg.GearLever == 2
                : isAsobo737Max
                    ? Asobo737MaxControlProfile.IsGearHandleDown(raw.GearHandle)
                : isIniBuildsA310 && _nativeRuntime.A310.GearHandleStatus.HasValue
                    ? _nativeRuntime.A310.GearHandleStatus.Value > 1.5f
                : _nativeRuntime.NativeAirbus.GearHandlePosition.HasValue
                    ? _nativeRuntime.NativeAirbus.GearHandlePosition.Value >= 0.5
                    : raw.GearHandle != 0,
            LeftGearPosition = raw.LeftGearPosition,
            CenterGearPosition = raw.CenterGearPosition,
            RightGearPosition = raw.RightGearPosition,
            PitchDegrees = raw.PitchDegrees,
            AutopilotMasterOn = raw.AutopilotMaster != 0,
            BoeingAutothrottleArmed = isAsobo737Max
                && _asobo737MaxRuntime.AutothrottleInputState.HasValue
                && Asobo737MaxControlProfile.IsAutothrottleArmed(
                    _asobo737MaxRuntime.AutothrottleInputState.Value),
            BoeingTransponderOperatingMode = isAsobo737Max
                ? _asobo737MaxRuntime.TransponderOperatingModeInputState
                : null,
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
            Adirs1SelectorState = isIniBuildsA310
                ? _nativeRuntime.A310.Irs1 ?? 0
                : isFlyByWireAirbus
                ? _nativeRuntime.ResolveFbwAdirsSelector(1, DateTime.UtcNow)
                : isIniBuildsA330 && _nativeRuntime.A330.AdirsInputStates[0].HasValue
                    ? _nativeRuntime.A330.AdirsInputStates[0]!.Value
                : isPmdg737 && pmdg != null
                    ? _pmdgNg3Runtime.ResolveLeftIrsMode(nowUtc)
                : _nativeRuntime.NativeAirbus.Adirs1State ?? 0,
            Adirs2SelectorState = isIniBuildsA310
                ? _nativeRuntime.A310.Irs2 ?? 0
                : isFlyByWireAirbus
                ? _nativeRuntime.ResolveFbwAdirsSelector(2, DateTime.UtcNow)
                : isIniBuildsA330 && _nativeRuntime.A330.AdirsInputStates[1].HasValue
                    ? _nativeRuntime.A330.AdirsInputStates[1]!.Value
                : isPmdg737 && pmdg != null
                    ? _pmdgNg3Runtime.ResolveRightIrsMode(nowUtc)
                : _nativeRuntime.NativeAirbus.Adirs2State ?? 0,
            Adirs3SelectorState = isIniBuildsA310
                ? _nativeRuntime.A310.Irs3 ?? 0
                : isFlyByWireAirbus
                ? _nativeRuntime.ResolveFbwAdirsSelector(3, DateTime.UtcNow)
                : isIniBuildsA330 && _nativeRuntime.A330.AdirsInputStates[2].HasValue
                    ? _nativeRuntime.A330.AdirsInputStates[2]!.Value
                : isPmdg737
                    ? 2
                : _nativeRuntime.NativeAirbus.Adirs3State ?? 0,
            AdirsOnBattery = isFlyByWireAirbus
                ? _nativeRuntime.Fbw.AdirsOnBattery == true
                : _nativeRuntime.NativeAirbus.AdirsOnBattery.HasValue && _nativeRuntime.NativeAirbus.AdirsOnBattery.Value != 0,
            IrsLeftAlignLightOn = isPmdg737 && pmdg != null && pmdg.IrsLeftAlignLight,
            IrsRightAlignLightOn = isPmdg737 && pmdg != null && pmdg.IrsRightAlignLight,
            IrsLeftOnDcLightOn = isPmdg737 && pmdg != null && pmdg.IrsLeftOnDcLight,
            IrsRightOnDcLightOn = isPmdg737 && pmdg != null && pmdg.IrsRightOnDcLight,
            IrsLeftFault = isPmdg737 && pmdg != null && pmdg.IrsLeftFault,
            IrsRightFault = isPmdg737 && pmdg != null && pmdg.IrsRightFault,
            IrsAligned = !isPmdg737 || pmdg?.IrsAligned == true,
            CrewOxygenOn = isIniBuildsA310
                ? _nativeRuntime.A310.OxygenSupply > 0.5f
                : isFlyByWireAirbus
                ? FbwStateResolvers.ResolveCrewOxygen(
                    _nativeRuntime.Fbw.CommandedCrewOxygen,
                    _nativeRuntime.Fbw.CommandedCrewOxygenUtc,
                    _nativeRuntime.Fbw.CrewOxygenTyped,
                    _nativeRuntime.Fbw.CrewOxygen)
                : isIniBuildsA330 && _nativeRuntime.A330.CrewOxygenInputState.HasValue
                    ? _nativeRuntime.A330.CrewOxygenInputState.Value >= 0.5
                : _nativeRuntime.NativeAirbus.CrewOxygen.HasValue && _nativeRuntime.NativeAirbus.CrewOxygen.Value != 0,
            StrobeSelectorPosition = isFlyByWireAirbus
                ? ResolveFbwStrobeSelectorPosition(_nativeRuntime.Fbw.StrobeAuto, _nativeRuntime.Fbw.StrobeLightState)
                : isIniBuildsA330 && _nativeRuntime.A330.StrobeInputState.HasValue
                    ? _nativeRuntime.A330.StrobeInputState.Value
                : isPmdg737 && pmdg != null
                    ? _pmdgNg3Runtime.ResolvePositionStrobeSelector(nowUtc)
                : isAsobo737Max && _asobo737MaxRuntime.PositionLightInputState.HasValue
                    ? _asobo737MaxRuntime.PositionLightInputState.Value
                : _nativeRuntime.NativeAirbus.StrobeSelector,
            ApuFireTestActive = isPmdg737 && pmdg != null
                ? pmdg.FireDetectionTestSwitch == 2 || pmdg.FireExtinguisherTestApu
                : _nativeRuntime.NativeAirbus.ApuFireTest.HasValue && _nativeRuntime.NativeAirbus.ApuFireTest.Value != 0,
            ApuFireWarningLit = isPmdg737 && pmdg != null
                ? pmdg.FireExtinguisherTestApu
                : _nativeRuntime.NativeAirbus.ApuFireWarningLit.HasValue && _nativeRuntime.NativeAirbus.ApuFireWarningLit.Value != 0,
            ApuFireSoundActive = _nativeRuntime.NativeAirbus.ApuFireSound.HasValue && _nativeRuntime.NativeAirbus.ApuFireSound.Value != 0,
            Engine1FireTestActive = isPmdg737 && pmdg != null
                ? pmdg.FireDetectionTestSwitch == 2 || pmdg.FireExtinguisherTestLeft
                : _nativeRuntime.NativeAirbus.Engine1FireTest.HasValue && _nativeRuntime.NativeAirbus.Engine1FireTest.Value != 0,
            Engine1FireWarningLit = isPmdg737 && pmdg != null
                ? pmdg.FireExtinguisherTestLeft
                : _nativeRuntime.NativeAirbus.Engine1FireWarningLit.HasValue && _nativeRuntime.NativeAirbus.Engine1FireWarningLit.Value != 0,
            Engine1FireSoundActive = _nativeRuntime.NativeAirbus.Engine1FireSound.HasValue && _nativeRuntime.NativeAirbus.Engine1FireSound.Value != 0,
            Engine2FireTestActive = isPmdg737 && pmdg != null
                ? pmdg.FireDetectionTestSwitch == 2 || pmdg.FireExtinguisherTestRight
                : _nativeRuntime.NativeAirbus.Engine2FireTest.HasValue && _nativeRuntime.NativeAirbus.Engine2FireTest.Value != 0,
            Engine2FireWarningLit = isPmdg737 && pmdg != null
                ? pmdg.FireExtinguisherTestRight
                : _nativeRuntime.NativeAirbus.Engine2FireWarningLit.HasValue && _nativeRuntime.NativeAirbus.Engine2FireWarningLit.Value != 0,
            Engine2FireSoundActive = _nativeRuntime.NativeAirbus.Engine2FireSound.HasValue && _nativeRuntime.NativeAirbus.Engine2FireSound.Value != 0,
            SeatbeltSelectorPosition = isFlyByWireAirbus
                ? ResolveFbwSeatbeltSelectorPosition(
                    _nativeRuntime.Fbw.SeatbeltSelector,
                    raw.CabinSeatbeltsAlert != 0)
                : isPmdg737 && pmdg != null
                    ? pmdg.FastenBeltsSelector
                : isIniBuildsA330 && A330SignInputEventsReady()
                    ? A330ControlProfile.NormalizeSignPosition(_nativeRuntime.A330.SignInputStates[0])
                : isAsobo737Max && _asobo737MaxRuntime.SeatbeltsInputState.HasValue
                    ? _asobo737MaxRuntime.SeatbeltsInputState.Value
                : _nativeRuntime.NativeAirbus.SeatbeltSelector,
            SeatbeltSignsOn = isFlyByWireAirbus
                ? raw.CabinSeatbeltsAlert != 0
                : isPmdg737 && pmdg != null
                    ? pmdg.FastenBeltsSelector == 2
                : isIniBuildsA330 && A330SignInputEventsReady()
                    ? _nativeRuntime.A330.SignInputStates[0] >= 1.5
                : isAsobo737Max && _asobo737MaxRuntime.SeatbeltsInputState.HasValue
                    ? Math.Abs(_asobo737MaxRuntime.SeatbeltsInputState.Value - 1) < 0.1
                : _nativeRuntime.NativeAirbus.SeatbeltSignsOn.HasValue && _nativeRuntime.NativeAirbus.SeatbeltSignsOn.Value != 0,
            NoSmokingSelectorPosition = isFlyByWireAirbus
                ? _nativeRuntime.Fbw.NoSmokingSelector
                : isPmdg737 && pmdg != null
                    ? pmdg.NoSmokingSelector
                : isIniBuildsA330 && A330SignInputEventsReady()
                    ? A330ControlProfile.NormalizeSignPosition(_nativeRuntime.A330.SignInputStates[1])
                : isAsobo737Max && _asobo737MaxRuntime.NoSmokingInputState.HasValue
                    ? _asobo737MaxRuntime.NoSmokingInputState.Value
                : _nativeRuntime.NativeAirbus.NoSmokingSelector,
            NoSmokingSignsOn = isFlyByWireAirbus
                ? _nativeRuntime.Fbw.NoSmokingSelector.HasValue && Math.Abs(_nativeRuntime.Fbw.NoSmokingSelector.Value) < 0.1
                : isPmdg737 && pmdg != null
                    ? pmdg.NoSmokingSelector == 2
                : isIniBuildsA330 && A330SignInputEventsReady()
                    ? _nativeRuntime.A330.SignInputStates[1] >= 0.5
                : isAsobo737Max && _asobo737MaxRuntime.NoSmokingInputState.HasValue
                    ? Math.Abs(_asobo737MaxRuntime.NoSmokingInputState.Value - 1) < 0.1
                : _nativeRuntime.NativeAirbus.NoSmokingSignsOn.HasValue && _nativeRuntime.NativeAirbus.NoSmokingSignsOn.Value != 0,
            EmergencyExitSelectorPosition = isFlyByWireAirbus
                ? _nativeRuntime.Fbw.EmergencyExitSelector
                : isPmdg737 && pmdg != null
                    ? _pmdgNg3Runtime.ResolveEmergencyExitSelector(nowUtc)
                : isIniBuildsA330 && A330SignInputEventsReady()
                    ? A330ControlProfile.NormalizeSignPosition(_nativeRuntime.A330.SignInputStates[2])
                : _nativeRuntime.NativeAirbus.EmergencyExitSelector,
            GroundSpoilersArmed = isIniBuildsA310 && _nativeRuntime.A310.Flow5States[2].HasValue
                ? _nativeRuntime.A310.Flow5States[2]!.Value > 0.5f
                : isFlyByWireAirbus
                ? ResolveFbwSpoilersArmedState(
                    _nativeRuntime.Fbw.CommandedSpoilersArmed,
                    _nativeRuntime.Fbw.CommandedSpoilersArmedUtc,
                    _nativeRuntime.Fbw.SpoilersArmed,
                    raw.SpoilersArmed)
                : isIniBuildsA330
                    ? _nativeRuntime.A330.CommandedSpoilersArmed ?? raw.SpoilersArmed != 0
                : isPmdg737 && pmdg != null
                    ? pmdg.SpeedbrakeArmed
                : _nativeRuntime.NativeAirbus.SpoilersArmed.HasValue
                    ? _nativeRuntime.NativeAirbus.SpoilersArmed.Value != 0
                    : raw.SpoilersArmed != 0,
            AutobrakeLevel = isFlyByWireAirbus
                ? ResolveFbwAutobrakeLevel(
                    _nativeRuntime.Fbw.CommandedAutobrakeLevel,
                    _nativeRuntime.Fbw.CommandedAutobrakeLevelUtc,
                    _nativeRuntime.Fbw.AutobrakeLevel)
                : isIniBuildsA330
                    ? ResolveA330AutobrakeLevel()
                : isPmdg737 && pmdg != null
                    ? pmdg.AutobrakeSelector
                : isAsobo737Max && _asobo737MaxRuntime.AutobrakeInputState.HasValue
                    ? _asobo737MaxRuntime.AutobrakeInputState.Value
                : _nativeRuntime.NativeAirbus.AutobrakeLevel,
            WeatherRadarPwsSelectorPosition = isFlyByWireAirbus
                ? ResolveFbwWeatherRadarPwsSelector(
                    _nativeRuntime.Fbw.CommandedWeatherRadarPwsSelector,
                    _nativeRuntime.Fbw.CommandedWeatherRadarPwsSelectorUtc,
                    _nativeRuntime.Fbw.WeatherRadarPwsSelector)
                : isIniBuildsA330 && _nativeRuntime.A330.WeatherRadarPwsInputState.HasValue
                    ? _nativeRuntime.A330.WeatherRadarPwsInputState.Value >= 0.5 ? 0 : 1
                : _nativeRuntime.NativeAirbus.WeatherRadarPwsSelector,
            NoseLightSelectorPosition = isFlyByWireAirbus
                ? FbwStateResolvers.ResolveNoseLightSelectorPosition(
                    raw.FbwNoseLightSelectorPosition,
                    _nativeRuntime.Fbw.CommandedNoseLightSelector,
                    _nativeRuntime.Fbw.CommandedNoseLightSelectorUtc,
                    raw.FbwNoseTakeoffLightCircuit,
                    raw.FbwNoseTaxiLightCircuit,
                    raw.TaxiLight)
                : isIniBuildsA330 && _nativeRuntime.A330.NoseLightInputState.HasValue
                    ? _nativeRuntime.A330.NoseLightInputState.Value
                : isPmdg737 && pmdg != null
                    ? pmdg.TaxiLightOn ? 1 : 2
                : isAsobo737Max && _asobo737MaxRuntime.TaxiLightInputState.HasValue
                    ? Asobo737MaxControlProfile.NormalizeTaxiLightPosition(
                        _asobo737MaxRuntime.TaxiLightInputState.Value)
                : _nativeRuntime.NativeAirbus.NoseLightSelector,
            LeftLandingLightSelectorPosition = isFlyByWireAirbus
                ? ResolveFbwLandingLightSelectorPosition(
                    _nativeRuntime.Fbw.CommandedLandingLightSelector,
                    _nativeRuntime.Fbw.CommandedLandingLightSelectorUtc,
                    raw.FbwLeftLandingLightCircuit)
                : isIniBuildsA330 && _nativeRuntime.A330.LandingLightInputState.HasValue
                    ? _nativeRuntime.A330.LandingLightInputState.Value >= 0.5 ? 0d : 1d
                : isPmdg737 && pmdg != null
                    ? _pmdgNg3Runtime.ResolveLandingLightSelector(true, nowUtc)
                : isAsobo737Max && _asobo737MaxRuntime.LandingLightInputStates[0].HasValue
                    ? Asobo737MaxControlProfile.IsLandingLightOn(
                        _asobo737MaxRuntime.LandingLightInputStates[0]!.Value) ? 0d : 1d
                : _nativeRuntime.NativeAirbus.LeftLandingLightSelector,
            RightLandingLightSelectorPosition = isFlyByWireAirbus
                ? ResolveFbwLandingLightSelectorPosition(
                    _nativeRuntime.Fbw.CommandedLandingLightSelector,
                    _nativeRuntime.Fbw.CommandedLandingLightSelectorUtc,
                    raw.FbwRightLandingLightCircuit)
                : isIniBuildsA330 && _nativeRuntime.A330.LandingLightInputState.HasValue
                    ? _nativeRuntime.A330.LandingLightInputState.Value >= 0.5 ? 0d : 1d
                : isPmdg737 && pmdg != null
                    ? _pmdgNg3Runtime.ResolveLandingLightSelector(false, nowUtc)
                : isAsobo737Max && _asobo737MaxRuntime.LandingLightInputStates[1].HasValue
                    ? Asobo737MaxControlProfile.IsLandingLightOn(
                        _asobo737MaxRuntime.LandingLightInputStates[1]!.Value) ? 0d : 1d
                : _nativeRuntime.NativeAirbus.RightLandingLightSelector,
            RunwayTurnoffLightsOn = isPmdg737 && pmdg != null
                ? pmdg.LeftRunwayTurnoffLight && pmdg.RightRunwayTurnoffLight
                : isAsobo737Max && _asobo737MaxRuntime.RunwayTurnoffInputStates.All(state => state.HasValue)
                    ? _asobo737MaxRuntime.RunwayTurnoffInputStates.All(
                        state => Asobo737MaxControlProfile.IsRunwayTurnoffLightOn(state!.Value))
                : isIniBuildsAirbusFamily
                    ? raw.IniBuildsTurnoffLightSwitch != 0
                : raw.LeftRunwayTurnoffLightCircuit != 0
                  && raw.RightRunwayTurnoffLightCircuit != 0,
            TcasAltitudeReportingOn = isFlyByWireAirbus
                ? ResolveFbwTcasAltitudeReporting(
                    _nativeRuntime.Fbw.CommandedTcasAltitudeReporting,
                    _nativeRuntime.Fbw.CommandedTcasAltitudeReportingUtc,
                    _nativeRuntime.Fbw.TcasAltitudeReporting)
                : isIniBuildsA330
                    ? _nativeRuntime.A330.TcasAltitudeInputState.HasValue
                        ? _nativeRuntime.A330.TcasAltitudeInputState.Value >= 0.5
                        : null
                : _nativeRuntime.NativeAirbus.TcasAltitudeReporting.HasValue
                    ? _nativeRuntime.NativeAirbus.TcasAltitudeReporting.Value == 0
                    : null,
            TransponderAtcState = _nativeRuntime.NativeAirbus.TransponderAtcState,
            TcasMode = isFlyByWireAirbus
                ? ResolveFbwSelectorWithCommand(
                    _nativeRuntime.Fbw.CommandedTcasMode,
                    _nativeRuntime.Fbw.CommandedTcasModeUtc,
                    _nativeRuntime.Fbw.TcasMode)
                : isIniBuildsA330 && _nativeRuntime.A330.TcasTrafficInputState.HasValue
                    ? _nativeRuntime.A330.TcasTrafficInputState.Value
                : isPmdg737 && pmdg != null
                    ? pmdg.TransponderMode
                : _nativeRuntime.NativeAirbus.TcasMode,
            TransponderModeSelectorPosition = isFlyByWireAirbus
                ? _nativeRuntime.Fbw.TransponderMode
                : isIniBuildsA330 && _nativeRuntime.A330.TransponderModeInputState.HasValue
                    ? _nativeRuntime.A330.TransponderModeInputState.Value
                : isPmdg737 && pmdg != null
                    ? pmdg.TransponderMode
                : isAsobo737Max && _asobo737MaxRuntime.TransponderModeInputState.HasValue
                    ? _asobo737MaxRuntime.TransponderModeInputState.Value
                : _nativeRuntime.NativeAirbus.TransponderStandby,
            TransponderStandby = isPmdg737 && pmdg != null
                ? pmdg.TransponderMode == 0
                : isIniBuildsA330 && _nativeRuntime.A330.TransponderModeInputState.HasValue
                    ? _nativeRuntime.A330.TransponderModeInputState.Value < 0.5
                : isAsobo737Max && _asobo737MaxRuntime.TransponderModeInputState.HasValue
                    ? Math.Abs(
                        _asobo737MaxRuntime.TransponderModeInputState.Value
                        - Asobo737MaxControlProfile.TransponderStandby) < 0.1
                : _nativeRuntime.NativeAirbus.TransponderStandby.HasValue
                  && _nativeRuntime.NativeAirbus.TransponderStandby.Value != 0,
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
            Engine2FireTestCompleted = _engine2FireTestCompleted,
            PmdgFireFaultInopTestCompleted = _pmdgNg3Runtime.FireFaultInopTestCompleted,
            PmdgFireOverheatTestCompleted = _pmdgNg3Runtime.FireOverheatTestCompleted,
            PmdgExtinguisherTest1Completed = _pmdgNg3Runtime.ExtinguisherTest1Completed,
            PmdgExtinguisherTest2Completed = _pmdgNg3Runtime.ExtinguisherTest2Completed
        };
        if (_taxiToRunwayArmed
            && _state.ForwardTaxiDetected
            && _state.GroundSpeedKnots >= 3)
        {
            _forwardTaxiObservedThisFlight = true;
        }
        _state.BeforeTakeoffHoldEligible =
            _taxiToRunwayArmed
            && _forwardTaxiObservedThisFlight
            && _state.OnGround
            && _state.Engine1Running
            && _state.Engine2Running
            && _state.GroundSpeedKnots <= 0.5;
        UpdateTelemetrySanity(_state);
        UpdateCockpitDisplayReadiness(_state);
        _flightTelemetryStore.Record(_state, DateTime.UtcNow);

        VerifyPendingProcedure();
        VerifyPendingFireTest();
        TryRestoreProcedureSession();
        _procedureRunner.Update(_state);
        TryStartPendingGsxEngineFlow();
        TryStartPendingBeforeTakeoffFlow();
        TryStartPendingTakeoffFlow();
        UpdateCruiseSeatbeltMonitoring();
        if (_procedureRunner.Status == ProcedureStatus.Completed
            && _procedureRunner.Definition != null)
        {
            _completedProcedureIds.Add(_procedureRunner.Definition.Id);
        }
        UpdateDashboard();
        PublishEfbState();
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

    private static bool IsAsobo737MaxApuAvailable(double apuRpmPercent, double apuVolts)
    {
        // The default/Asobo 737 MAX does not reliably drive the generic "APU available"
        // bridge flag. Its cockpit shows availability once the APU is at speed and
        // producing electrical power, so keep this resolver aircraft-specific.
        return apuRpmPercent >= 95 || apuVolts >= 90;
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

    private bool ResolveFbwSpoilersArmedState(
        bool? commandedValue,
        DateTime? commandedUtc,
        bool? fbwLVarValue,
        double genericSpoilersArmed) =>
        _nativeRuntime.ResolveFbwSpoilersArmed(
            genericSpoilersArmed,
            DateTime.UtcNow);

    private double? ResolveFbwAutobrakeLevel(
        float? commandedValue,
        DateTime? commandedUtc,
        float? fbwLVarValue) =>
        _nativeRuntime.ResolveFbwAutobrake(DateTime.UtcNow);

    private double? ResolveFbwWeatherRadarPwsSelector(
        float? commandedValue,
        DateTime? commandedUtc,
        float? fbwLVarValue) =>
        _nativeRuntime.ResolveFbwWeatherRadarPws(DateTime.UtcNow);

    private bool? ResolveFbwTcasAltitudeReporting(
        bool? commandedValue,
        DateTime? commandedUtc,
        bool? fbwLVarValue) =>
        _nativeRuntime.ResolveFbwTcasAltitudeReporting(DateTime.UtcNow);

    private double? ResolveFbwSelectorWithCommand(
        float? commandedValue,
        DateTime? commandedUtc,
        float? fbwLVarValue) =>
        _nativeRuntime.ResolveFbwTcasMode(DateTime.UtcNow);

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

    private double ResolveFbwLandingLightSelectorPosition(
        float? commandedValue,
        DateTime? commandedUtc,
        double circuitOn) =>
        _nativeRuntime.ResolveFbwLandingLight(circuitOn, DateTime.UtcNow);

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
        double? directIniBuildsEngineModeSelector,
        float? nativeIniBuildsEngineModeSelector,
        double engine1IgnitionSwitch,
        double engine2IgnitionSwitch)
    {
        if (directIniBuildsEngineModeSelector.HasValue)
        {
            var directPosition = (int)Math.Round(directIniBuildsEngineModeSelector.Value);
            if (directPosition >= 0 && directPosition <= 2)
            {
                return directPosition;
            }
        }

        if (nativeIniBuildsEngineModeSelector.HasValue)
        {
            var nativePosition = (int)Math.Round(nativeIniBuildsEngineModeSelector.Value);
            if (nativePosition >= 0 && nativePosition <= 2)
            {
                return nativePosition;
            }
        }

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

    private void ApplyPmdg777SdkState()
    {
        if (_state?.IsPmdg777300Er != true || _pmdg777Runtime.State == null)
        {
            return;
        }

        var sdk = _pmdg777Runtime.State!;
        _state.Pmdg777SdkDataReady = _pmdg777Runtime.DataReady;
        _state.Pmdg777BatteryOn = sdk.BatteryOn;
        _state.Pmdg777IfePassengerSeatsOn = sdk.IfePassengerSeatsOn;
        _state.Pmdg777CabinUtilityOn = sdk.CabinUtilityOn;
        _state.Pmdg777BusTiesAuto = sdk.BusTiesAuto;
        _state.Pmdg777HydraulicPanelSafe =
            sdk.CenterPrimaryPumpsOff && sdk.DemandPumpsOff;
        _state.Pmdg777WipersOff = sdk.WipersOff;
        _state.Pmdg777GearLeverDown = sdk.GearLeverDown;
        _state.Pmdg777AlternateFlapsOff = sdk.AlternateFlapsOff;
        _state.Pmdg777ExternalPowerAvailable =
            sdk.PrimaryExternalPowerAvailable && sdk.SecondaryExternalPowerAvailable;
        _state.Pmdg777ExternalPowerOn =
            sdk.PrimaryExternalPowerOn && sdk.SecondaryExternalPowerOn;
        _state.Pmdg777PrimaryExternalPowerAvailable = sdk.PrimaryExternalPowerAvailable;
        _state.Pmdg777SecondaryExternalPowerAvailable = sdk.SecondaryExternalPowerAvailable;
        _state.Pmdg777PrimaryExternalPowerOn = sdk.PrimaryExternalPowerOn;
        _state.Pmdg777SecondaryExternalPowerOn = sdk.SecondaryExternalPowerOn;
        _state.Pmdg777NavigationLightOn = sdk.NavigationLightOn;
        _state.Pmdg777LogoLightOn = sdk.LogoLightOn;
        _state.Pmdg777GroundAirConfigurationSet =
            sdk.PacksOff && sdk.RecirculationFansOff;
        _state.Pmdg777AdiruOn = sdk.AdiruOn;
        _pmdg777Runtime.ObserveAdiruState(DateTime.UtcNow);
        _state.Pmdg777EmergencyLightsArmed = sdk.EmergencyLightsSelector == 1;
        _state.Pmdg777FirstOfficerFlightDirectorOn = sdk.FirstOfficerFlightDirectorOn;
        _state.Pmdg777ServiceInterphoneOff = sdk.ServiceInterphoneOff;
        _state.Pmdg777PassengerOxygenNormal = sdk.PassengerOxygenNormal;
        _state.Pmdg777ThrustAsymmetryCompensationAuto = sdk.ThrustAsymmetryCompensationAuto;
        _state.Pmdg777PrimaryFlightComputersAuto = sdk.PrimaryFlightComputersAuto;
        _state.Pmdg777ApuGeneratorSwitchOn = sdk.ApuGeneratorSwitchOn;
        _state.Pmdg777ApuRunning = sdk.ApuRunning;
        _state.Pmdg777ApuGeneratorPowerEstablished = sdk.ApuGeneratorPowerEstablished;
        _state.Pmdg777ApuBleedAirAvailable = sdk.ApuBleedAirAvailable;
        _state.Pmdg777BeforeStartChecklistComplete = sdk.BeforeStartChecklistComplete;
        _state.Pmdg777BeaconOn = sdk.BeaconOn;
        _state.Pmdg777HydraulicsBeforeStart = sdk.HydraulicsBeforeStart;
        _state.Pmdg777FuelPumpsBeforeStart = sdk.FuelPumpsBeforeStart;
        _state.Pmdg777CenterFuelPumpsRequired = sdk.CenterFuelPumpsRequired;
        _state.Pmdg777TransponderXpndr = sdk.TransponderXpndr;
        _state.Pmdg777SecondaryEngineDisplaySelected = sdk.SecondaryEngineDisplaySelected;
        _state.Pmdg777EngineOneStartSelectorStart = sdk.EngineOneStartSelectorStart;
        _state.Pmdg777EngineTwoStartSelectorStart = sdk.EngineTwoStartSelectorStart;
        _state.Pmdg777EngineOneStartValveOpen = sdk.EngineOneStartValveOpen;
        _state.Pmdg777EngineTwoStartValveOpen = sdk.EngineTwoStartValveOpen;
        _state.Pmdg777EngineOneFuelControlRun = sdk.EngineOneFuelControlRun;
        _state.Pmdg777EngineTwoFuelControlRun = sdk.EngineTwoFuelControlRun;
        _state.Pmdg777WheelChocksSet = sdk.WheelChocksSet;
        _state.Pmdg777ApuSelectorOff = sdk.ApuSelectorOff;
        _state.Pmdg777EngineBleedsAuto = sdk.EngineBleedsAuto;
        _state.Pmdg777PacksAuto = sdk.PacksAuto;
        _state.Pmdg777ApuBleedOff = sdk.ApuBleedOff;
        _state.Pmdg777ApuBleedAuto = sdk.ApuBleedAuto;
        _state.Pmdg777TakeoffFlapsSet = sdk.TakeoffFlapsSet;
        _state.Pmdg777TransponderTaRa = sdk.TransponderTaRa;
        _state.Pmdg777TaxiLightsSet = sdk.TaxiLightsSet;
        _state.Pmdg777TakeoffLightsSet = sdk.TakeoffLightsSet;
        _state.Pmdg777ClimbLightsSet = sdk.ClimbLightsSet;
        _state.Pmdg777GearLeverUp = sdk.GearLeverUp;
        _state.Pmdg777BeforeTaxiChecklistComplete = sdk.BeforeTaxiChecklistComplete;
        _state.Pmdg777BeforeTakeoffChecklistComplete = sdk.BeforeTakeoffChecklistComplete;
        _state.Pmdg777AfterTakeoffChecklistComplete = sdk.AfterTakeoffChecklistComplete;
        _state.Pmdg777LnavArmed = sdk.LnavArmed;
        _state.Pmdg777VnavArmed = sdk.VnavArmed;
        _state.Pmdg777FmcLandingFlaps = sdk.FmcLandingFlaps;
        _state.Pmdg777FmcLandingVref = sdk.FmcLandingVref;
        _state.Pmdg777LandingFlapsSet = sdk.LandingFlapsSet;
        _state.Pmdg777SpeedbrakeArmed = sdk.SpeedbrakeArmed;
        _state.Pmdg777AutobrakeSelector = sdk.AutobrakeSelector;
        _state.Pmdg777LandingLightsOn = sdk.LandingLightsOn;
        _state.Pmdg777AfterLandingLightsSet = sdk.AfterLandingLightsSet;
        _state.Pmdg777FuelPumpsOff = sdk.FuelPumpsOff;
        _state.Pmdg777HydraulicsShutdown = sdk.HydraulicsShutdown;
        _state.Pmdg777FlapsLever = sdk.FlapsLever;
        _state.Engine1StarterActive = sdk.EngineOneStartValveOpen;
        _state.Engine2StarterActive = sdk.EngineTwoStartValveOpen;
        _state.ApuAvailable = sdk.ApuRunning;
        _state.Pmdg777EngineGeneratorOneSwitchOn = sdk.EngineGeneratorOneSwitchOn;
        _state.Pmdg777EngineGeneratorTwoSwitchOn = sdk.EngineGeneratorTwoSwitchOn;
        _state.Pmdg777BackupGeneratorOneSwitchOn = sdk.BackupGeneratorOneSwitchOn;
        _state.Pmdg777BackupGeneratorTwoSwitchOn = sdk.BackupGeneratorTwoSwitchOn;
        _state.Pmdg777LeftSideWindowHeatOn = sdk.LeftSideWindowHeatOn;
        _state.Pmdg777LeftForwardWindowHeatOn = sdk.LeftForwardWindowHeatOn;
        _state.Pmdg777RightForwardWindowHeatOn = sdk.RightForwardWindowHeatOn;
        _state.Pmdg777RightSideWindowHeatOn = sdk.RightSideWindowHeatOn;
        _state.Pmdg777LeftEnginePrimaryHydraulicPumpOn = sdk.LeftEnginePrimaryHydraulicPumpOn;
        _state.Pmdg777RightEnginePrimaryHydraulicPumpOn = sdk.RightEnginePrimaryHydraulicPumpOn;
        _state.Pmdg777FirePanelNormal = sdk.FirePanelNormal;
        _state.Pmdg777EngineControlPanelNormal = sdk.EngineControlPanelNormal;
        _state.Pmdg777FuelPanelPreflight = sdk.FuelPanelPreflight;
        _state.Pmdg777AntiIceAuto = sdk.AntiIceAuto;
        _state.Pmdg777ExteriorLightsPreflight = sdk.ExteriorLightsPreflight;
        _state.Pmdg777AirPanelPreflight = sdk.AirPanelPreflight;
        _state.Pmdg777AutobrakeRto = sdk.AutobrakeRto;
        _state.Pmdg777TransponderAltitudeSourceNormal = sdk.TransponderAltitudeSourceNormal;
        _state.Pmdg777SeatBeltsOff = sdk.SeatBeltsOff;
        _state.Pmdg777SeatBeltsAuto = sdk.SeatBeltsAuto;
        _state.Pmdg777NoSmokingAuto = sdk.NoSmokingAuto;
        _state.Pmdg777FuelToRemainSelectorIn = sdk.FuelToRemainSelectorIn;
        _state.Pmdg777TemperatureControlsPreflight = sdk.TemperatureControlsPreflight;
        _state.Pmdg777FirstOfficerNdMap = sdk.FirstOfficerNdMap;
        _state.Pmdg777FireOverheatTestComplete = _pmdg777Runtime.FireOverheatTestObserved;
        _state.Pmdg777FirstOfficerOxygenTestComplete = _pmdg777Runtime.FirstOfficerOxygenTestObserved;
        _state.Pmdg777FirstOfficerSourcesNormal = sdk.FirstOfficerSourcesNormal;
        _state.Pmdg777FirstOfficerDisplaysReady = sdk.FirstOfficerDisplaysReady;
        _state.Pmdg777SpeedbrakeDown = sdk.SpeedbrakeDown;
        _state.Pmdg777FlapsUp = sdk.FlapsUp;
        _state.Pmdg777FuelControlsCutoff = sdk.FuelControlsCutoff;
        _state.Pmdg777TransponderStandby = sdk.TransponderStandby;
        _state.Pmdg777McpAltitude = sdk.McpAltitude;
        _state.Pmdg777FmcPerformanceInputComplete = sdk.FmcPerformanceInputComplete;
        _state.Pmdg777FmcTakeoffFlaps = sdk.FmcTakeoffFlaps;
        _state.Pmdg777FmcV1 = sdk.FmcV1;
        _state.Pmdg777FmcVr = sdk.FmcVr;
        _state.Pmdg777FmcV2 = sdk.FmcV2;
        _state.Pmdg777FmcCruiseAltitude = sdk.FmcCruiseAltitude;
        _state.Pmdg777FmcDistanceToDestination = sdk.FmcDistanceToDestination;
        _state.Pmdg777FmcFlightNumber = sdk.FmcFlightNumber;
        _state.Pmdg777PreflightChecklistComplete = sdk.PreflightChecklistComplete;
        _state.Pmdg777IrsAligned = sdk.IrsAligned;

        _state.ExternalPowerAvailable = _state.Pmdg777ExternalPowerAvailable;
        _state.ExternalPowerOn = _state.Pmdg777ExternalPowerOn;
        _state.NavigationLightsOn = sdk.NavigationLightOn;
        _state.LogoLightsOn = sdk.LogoLightOn;
        _state.ParkingBrakeSet = sdk.ParkingBrakeSet;

        _procedureRunner.Update(_state);
        UpdateDashboard();
        PublishEfbState();
    }

    private void LogPmdg777FlowOneState(Pmdg777SdkData sdk)
    {
        var diagnostic = _pmdg777Runtime.ObserveFlowOneDiagnostic(
            sdk,
            _state?.Pmdg777EmergencyLightsGuardClosed == true);
        if (diagnostic == null)
        {
            return;
        }

        AppLog.Write(diagnostic);
    }

    private void SendPmdgNg3Control(uint sdkEventOffset, uint parameter)
    {
        if (Connection == null)
        {
            return;
        }

        Connection.SetClientData(
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
        => ApproachDistanceResolver.Resolve(
            raw.AtcRunwaySelected != 0,
            raw.AtcRunwayStartDistanceMeters,
            raw.Nav1HasLocalizer != 0,
            raw.Nav1DmeNm,
            raw.Nav2HasLocalizer != 0,
            raw.Nav2DmeNm);

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
            || _oneShotCommand.StartsWith("tcas-mode ", StringComparison.OrdinalIgnoreCase)
            || _oneShotCommand.StartsWith("a310 ", StringComparison.OrdinalIgnoreCase)
            || _oneShotCommand.StartsWith("asobo737max ", StringComparison.OrdinalIgnoreCase);
        if (requiresAircraftAdapter && !_mobiFlightSession.AdapterReady)
        {
            return;
        }
        var nativeStateReady = _oneShotCommand.ToLowerInvariant() switch
        {
            "status" => _mobiFlightSession.HasRuntimeSettled(DateTime.UtcNow),
            var command when command.StartsWith("battery-1 ") => _mobiFlightSession.RuntimeReady,
            var command when command.StartsWith("battery-2 ") => _mobiFlightSession.RuntimeReady,
            "a310 batteries auto" =>
                _nativeRuntime.A310.Battery1Auto.HasValue
                && _nativeRuntime.A310.Battery2Auto.HasValue
                && _nativeRuntime.A310.Battery3Auto.HasValue,
            "a310 wipers-radar off" =>
                _nativeRuntime.A310.CaptainWiper.HasValue
                && _nativeRuntime.A310.FirstOfficerWiper.HasValue
                && _nativeRuntime.A310.WeatherRadarSystem.HasValue,
            "a310 apu-fire-test" => _nativeRuntime.A310.ApuFireTest.HasValue && _nativeRuntime.A310.ApuLoopTest.HasValue,
            "a310 irs nav" => _nativeRuntime.A310.Irs1.HasValue && _nativeRuntime.A310.Irs2.HasValue && _nativeRuntime.A310.Irs3.HasValue,
            "a310 oxygen on" => _nativeRuntime.A310.OxygenSupply.HasValue,
            "a310 annunciator-test" => _nativeRuntime.A310.AnnunciatorTest.HasValue,
            "a310 initial-lights" => _nativeRuntime.A310.InitialLightStates.All(value => value.HasValue),
            "a310 preflight-signs" => A310Flow2ReadbacksAvailable(0, 2),
            "a310 autoflight-computers" => A310Flow2ReadbacksAvailable(2, 6),
            "a310 preflight-heat" => A310Flow2ReadbacksAvailable(8, 7),
            "a310 emergency-exit arm" => A310Flow2ReadbacksAvailable(15, 1),
            "a310 cargo-smoke-test" =>
                A310Flow2ReadbacksAvailable(16, 1)
                && A310Flow2ReadbacksAvailable(21, 3),
            "a310 egpws-test" => A310Flow2ReadbacksAvailable(17, 1),
            "a310 preflight-pedestal" =>
                A310Flow2ReadbacksAvailable(18, 3) && _nativeRuntime.A310.WeatherRadarSystem.HasValue,
            "a310 fuel-pumps on" => _nativeRuntime.A310.FuelPumpStates.All(value => value.HasValue),
            "a310 apu start" => _nativeRuntime.A310.Flow3ApuStates.All(value => value.HasValue),
            "a310 apu power-bleed" => _nativeRuntime.A310.Flow3ApuStates.All(value => value.HasValue),
            "a310 beacon on" => _nativeRuntime.A310.InitialLightStates[1].HasValue,
            "a310 transponder xpdr" => _nativeRuntime.A310.Flow2States[20].HasValue,
            "a310 external-power off" => _nativeRuntime.A310.Flow3ApuStates.All(value => value.HasValue),
            "a310 ignition a" => A310Flow4ReadbacksAvailable(0, 3),
            "a310 engine-1 starter" => A310Flow4ReadbacksAvailable(3, 1),
            "a310 engine-2 starter" => A310Flow4ReadbacksAvailable(4, 1),
            "a310 ignition off" => A310Flow4ReadbacksAvailable(0, 1),
            "a310 apu off" => _nativeRuntime.A310.Flow3ApuStates.All(value => value.HasValue),
            "a310 speedbrake arm" => _nativeRuntime.A310.Flow5States[2].HasValue,
            "a310 rudder-trim reset" => _nativeRuntime.A310.Flow2States[19].HasValue,
            "a310 takeoff-flaps 15-0" => _state?.IsIniBuildsA310 == true,
            "a310 nose-light taxi" => _nativeRuntime.A310.InitialLightStates[2].HasValue,
            "a310 autobrake max" =>
                _nativeRuntime.A310.Flow2States[18].HasValue || _nativeRuntime.A310.Flow5States[1].HasValue,
            "a310 transponder-weather on" =>
                _nativeRuntime.A310.Flow2States[20].HasValue
                && _nativeRuntime.A310.WeatherRadarSystem.HasValue
                && _nativeRuntime.A310.Flow5States[0].HasValue,
            "a310 takeoff-lights" => _nativeRuntime.A310.InitialLightStates.All(value => value.HasValue),
            "a310 ignition takeoff" => A310Flow4ReadbacksAvailable(0, 1),
            "a310 packs on" => A310Flow4ReadbacksAvailable(1, 2),
            "a310 tcas tara" => _nativeRuntime.A310.Flow2States[20].HasValue,
            "a310 gear up" => _nativeRuntime.A310.GearHandleStatus.HasValue,
            "a310 speedbrake disarm" => _nativeRuntime.A310.Flow5States[2].HasValue,
            "a310 climb-lights" => _nativeRuntime.A310.InitialLightStates.All(value => value.HasValue),
            "a310 altimeters standard" =>
                _nativeRuntime.A310.AltimeterStandardStates.All(value => value.HasValue),
            "a310 landing-lights retract" =>
                _nativeRuntime.A310.InitialLightStates[3].HasValue
                && _nativeRuntime.A310.InitialLightStates[4].HasValue,
            "a310 approach-lights" =>
                _nativeRuntime.A310.InitialLightStates.All(value => value.HasValue)
                && _nativeRuntime.A310.Flow2States[0].HasValue,
            "a310 flaps 15-0" or "a310 flaps 15-15" or
                "a310 flaps 15-20" or "a310 flaps 30-40" or
                "a310 flaps retract" => _state?.IsIniBuildsA310 == true,
            "a310 gear down" => _nativeRuntime.A310.GearHandleStatus.HasValue,
            "a310 nose-light takeoff" or "a310 nose-light off" =>
                _nativeRuntime.A310.InitialLightStates[2].HasValue,
            "a310 after-landing-lights" => _nativeRuntime.A310.InitialLightStates.All(value => value.HasValue),
            "a310 transponder-radar standby" =>
                _nativeRuntime.A310.Flow2States[20].HasValue && _nativeRuntime.A310.WeatherRadarSystem.HasValue,
            "a310 beacon off" => _nativeRuntime.A310.InitialLightStates[1].HasValue,
            "a310 seatbelts off" => _nativeRuntime.A310.Flow2States[0].HasValue,
            "a310 fuel-pumps parking" => _nativeRuntime.A310.FuelPumpStates.All(value => value.HasValue),
            "a310 probe-heat off" => A310Flow2ReadbacksAvailable(12, 3),
            "a310 irs off" => _nativeRuntime.A310.Irs1.HasValue && _nativeRuntime.A310.Irs2.HasValue && _nativeRuntime.A310.Irs3.HasValue,
            "a310 oxygen off" => _nativeRuntime.A310.OxygenSupply.HasValue,
            "a310 exterior-lights off" => _nativeRuntime.A310.InitialLightStates.All(value => value.HasValue),
            "a310 emergency-exit disarm" => _nativeRuntime.A310.Flow2States[15].HasValue,
            "a310 batteries off" =>
                _nativeRuntime.A310.Battery1Auto.HasValue && _nativeRuntime.A310.Battery2Auto.HasValue && _nativeRuntime.A310.Battery3Auto.HasValue,
            var command when command.StartsWith("nav-logo ") => _mobiFlightSession.RuntimeReady,
            var command when command.StartsWith("apu-") =>
                _state?.IsFlyByWireAirbus == true
                    ? _mobiFlightSession.RuntimeReady
                    : _nativeRuntime.NativeAirbus.ApuAvailable.HasValue
                      && _nativeRuntime.NativeAirbus.ApuMasterSwitch.HasValue
                      && _nativeRuntime.NativeAirbus.ApuStartButton.HasValue
                      && _nativeRuntime.NativeAirbus.ApuBleedButton.HasValue
                      && _nativeRuntime.NativeAirbus.ApuGeneratorOn.HasValue
                      && _nativeRuntime.NativeAirbus.ApuFlapPercent.HasValue,
            var command when command.StartsWith("fuel-pumps ") =>
                _state?.IsFlyByWireAirbus == true
                    || _nativeRuntime.NativeAirbus.FuelPump1.HasValue
                && _nativeRuntime.NativeAirbus.FuelPump2.HasValue
                && _nativeRuntime.NativeAirbus.FuelPump3.HasValue
                && _nativeRuntime.NativeAirbus.FuelPump4.HasValue
                && _nativeRuntime.NativeAirbus.FuelPump5.HasValue
                && _nativeRuntime.NativeAirbus.FuelPump6.HasValue,
            var command when command.StartsWith("adirs-1 ") => _nativeRuntime.NativeAirbus.Adirs1State.HasValue,
            var command when command.StartsWith("adirs-2 ") => _nativeRuntime.NativeAirbus.Adirs2State.HasValue,
            var command when command.StartsWith("adirs-3 ") => _nativeRuntime.NativeAirbus.Adirs3State.HasValue,
            var command when command.StartsWith("crew-oxygen ") => true,
            var command when command.StartsWith("strobe ") =>
                _state?.IsFlyByWireAirbus == true
                    ? _mobiFlightSession.RuntimeReady
                    : _nativeRuntime.NativeAirbus.StrobeSelector.HasValue,
            var command when command == "fire-test apu" =>
                _state?.IsFlyByWireAirbus == true || _nativeRuntime.NativeAirbus.ApuFireTest.HasValue,
            var command when command == "fire-test engine-1" =>
                _state?.IsFlyByWireAirbus == true || _nativeRuntime.NativeAirbus.Engine1FireTest.HasValue,
            var command when command == "fire-test engine-2" =>
                _state?.IsFlyByWireAirbus == true || _nativeRuntime.NativeAirbus.Engine2FireTest.HasValue,
            var command when command == "asobo737max fire-tests" =>
                _state?.IsAsobo737Max8 == true,
            var command when command.StartsWith("seatbelts ") =>
                _state?.IsFlyByWireAirbus == true
                    ? _mobiFlightSession.RuntimeReady
                    : _nativeRuntime.NativeAirbus.SeatbeltSelector.HasValue,
            var command when command.StartsWith("no-smoking ") =>
                _state?.IsFlyByWireAirbus == true
                    ? _mobiFlightSession.RuntimeReady
                    : _nativeRuntime.NativeAirbus.NoSmokingSelector.HasValue,
            var command when command.StartsWith("emergency-exit ") =>
                _state?.IsFlyByWireAirbus == true
                    ? _mobiFlightSession.RuntimeReady
                    : _nativeRuntime.NativeAirbus.EmergencyExitSelector.HasValue,
            var command when command.StartsWith("transponder ") =>
                _state?.IsFlyByWireAirbus == true
                    ? _mobiFlightSession.RuntimeReady
                    : _nativeRuntime.NativeAirbus.TransponderStandby.HasValue,
            var command when command.StartsWith("atc-system ") => _nativeRuntime.NativeAirbus.TransponderAtcState.HasValue,
            var command when command.StartsWith("tcas altitude-reporting ") =>
                _state?.IsFlyByWireAirbus == true
                    ? _mobiFlightSession.RuntimeReady
                    : _nativeRuntime.NativeAirbus.TcasAltitudeReporting.HasValue,
            var command when command.StartsWith("tcas traffic ") =>
                _state?.IsFlyByWireAirbus == true
                    ? _mobiFlightSession.RuntimeReady
                    : _nativeRuntime.NativeAirbus.TcasMode.HasValue,
            var command when command.StartsWith("wxr-pws ") =>
                _state?.IsFlyByWireAirbus == true
                    ? _mobiFlightSession.RuntimeReady
                    : _nativeRuntime.NativeAirbus.WeatherRadarPwsSelector.HasValue,
            var command when command.StartsWith("nose-light ") =>
                _state?.IsFlyByWireAirbus == true
                    || _nativeRuntime.NativeAirbus.NoseLightSelector.HasValue,
            var command when command.StartsWith("landing-lights ") =>
                _state?.IsFlyByWireAirbus == true
                    || _nativeRuntime.NativeAirbus.LeftLandingLightSelector.HasValue
                    && _nativeRuntime.NativeAirbus.RightLandingLightSelector.HasValue,
            var command when command.StartsWith("tcas-mode ") => _nativeRuntime.NativeAirbus.TransponderStandby.HasValue,
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

                _automation.Enqueue(line);
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
        _automation.Drain(command =>
        {
            ExecuteCommand(command);
            if (_oneShotCommand == null && !IsDisposed)
            {
                Console.Write("> ");
            }
        });
    }

    private void ExecuteCommand(string command)
    {
        var normalized = command.Trim().ToLowerInvariant();
        if (normalized.StartsWith("a310 ", StringComparison.Ordinal))
        {
            if (_state?.IsIniBuildsA310 != true)
            {
                AppendDashboardLog("Blocked A310 cockpit command: a different aircraft profile is active.");
                AppLog.Write($"Blocked A310 command outside its aircraft profile: {normalized}.");
                FinishOneShot(2);
                return;
            }

            normalized = normalized.Substring("a310 ".Length);
        }
        if (normalized.StartsWith("a330 ", StringComparison.Ordinal))
        {
            if (_state?.IsIniBuildsA330 != true)
            {
                AppendDashboardLog("Blocked A330 cockpit command: a different aircraft profile is active.");
                AppLog.Write($"Blocked A330 command outside its aircraft profile: {normalized}.");
                FinishOneShot(2);
                return;
            }

            normalized = normalized.Substring("a330 ".Length);
        }
        if (normalized.StartsWith("asobo737max ", StringComparison.Ordinal))
        {
            if (_state?.IsAsobo737Max8 != true)
            {
                AppendDashboardLog("Blocked Asobo 737 MAX cockpit command: a different aircraft profile is active.");
                AppLog.Write($"Blocked Asobo 737 MAX command outside its aircraft profile: {normalized}.");
                FinishOneShot(2);
                return;
            }
        }
        if (normalized.StartsWith("pmdg777 ", StringComparison.Ordinal)
            && _state?.IsPmdg777300Er != true)
        {
            AppendDashboardLog("Blocked PMDG 777 cockpit command: a different aircraft profile is active.");
            AppLog.Write($"Blocked PMDG 777 command outside its aircraft profile: {normalized}.");
            FinishOneShot(2);
            return;
        }
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
            case "pmdg777 battery on":
                SetPmdg777BatteryOn();
                break;
            case "pmdg777 primary external power on":
                SetPmdg777PrimaryExternalPowerOn();
                break;
            case "pmdg777 secondary external power on":
                SetPmdg777SecondaryExternalPowerOn();
                break;
            case "pmdg777 adiru on":
                SetPmdg777AdiruOn();
                break;
            case "pmdg777 ife passenger seats on":
                SetPmdg777IfePassengerSeatsOn();
                break;
            case "pmdg777 cabin utility on":
                SetPmdg777CabinUtilityOn();
                break;
            case "pmdg777 emergency lights armed":
                SetPmdg777EmergencyLightsArmed();
                break;
            case "pmdg777 emergency lights guard closed":
                SetPmdg777EmergencyLightsGuardClosed();
                break;
            case "pmdg777 navigation light on":
                SetPmdg777NavigationLightOn();
                break;
            case "pmdg777 thrust asymmetry compensation auto":
                SetPmdg777SwitchOn(_state?.Pmdg777ThrustAsymmetryCompensationAuto == true, Pmdg777ControlProfile.ThrustAsymmetryCompensationEvent, "thrust-asymmetry compensation AUTO");
                break;
            case "pmdg777 primary flight computers auto":
                SetPmdg777SwitchOn(_state?.Pmdg777PrimaryFlightComputersAuto == true, Pmdg777ControlProfile.PrimaryFlightComputersEvent, "PRIMARY FLIGHT COMPUTERS AUTO");
                break;
            case "pmdg777 primary flight computers guard closed":
                SetPmdg777GuardClosed(_state?.Pmdg777PrimaryFlightComputersGuardClosed == true, Pmdg777ControlProfile.PrimaryFlightComputersGuardEvent, "PRIMARY FLIGHT COMPUTERS guard CLOSED");
                break;
            case "pmdg777 apu generator switch on":
                SetPmdg777SwitchOn(_state?.Pmdg777ApuGeneratorSwitchOn == true, Pmdg777ControlProfile.ApuGeneratorSwitchEvent, "APU GENERATOR switch ON");
                break;
            case "pmdg777 engine generator one switch on":
                SetPmdg777SwitchOn(_state?.Pmdg777EngineGeneratorOneSwitchOn == true, Pmdg777ControlProfile.EngineGeneratorOneSwitchEvent, "left GENERATOR switch ON");
                break;
            case "pmdg777 engine generator two switch on":
                SetPmdg777SwitchOn(_state?.Pmdg777EngineGeneratorTwoSwitchOn == true, Pmdg777ControlProfile.EngineGeneratorTwoSwitchEvent, "right GENERATOR switch ON");
                break;
            case "pmdg777 backup generator one switch on":
                SetPmdg777SwitchOn(_state?.Pmdg777BackupGeneratorOneSwitchOn == true, Pmdg777ControlProfile.BackupGeneratorOneSwitchEvent, "left BACKUP GENERATOR switch ON");
                break;
            case "pmdg777 backup generator two switch on":
                SetPmdg777SwitchOn(_state?.Pmdg777BackupGeneratorTwoSwitchOn == true, Pmdg777ControlProfile.BackupGeneratorTwoSwitchEvent, "right BACKUP GENERATOR switch ON");
                break;
            case "pmdg777 passenger oxygen guard closed":
                SetPmdg777GuardClosed(_state?.Pmdg777PassengerOxygenGuardClosed == true, Pmdg777ControlProfile.PassengerOxygenGuardEvent, "PASSENGER OXYGEN guard CLOSED");
                break;
            case "pmdg777 left side window heat on":
                SetPmdg777SwitchOn(_state?.Pmdg777LeftSideWindowHeatOn == true, Pmdg777ControlProfile.LeftSideWindowHeatEvent, "left side WINDOW HEAT ON");
                break;
            case "pmdg777 left forward window heat on":
                SetPmdg777SwitchOn(_state?.Pmdg777LeftForwardWindowHeatOn == true, Pmdg777ControlProfile.LeftForwardWindowHeatEvent, "left forward WINDOW HEAT ON");
                break;
            case "pmdg777 right forward window heat on":
                SetPmdg777SwitchOn(_state?.Pmdg777RightForwardWindowHeatOn == true, Pmdg777ControlProfile.RightForwardWindowHeatEvent, "right forward WINDOW HEAT ON");
                break;
            case "pmdg777 right side window heat on":
                SetPmdg777SwitchOn(_state?.Pmdg777RightSideWindowHeatOn == true, Pmdg777ControlProfile.RightSideWindowHeatEvent, "right side WINDOW HEAT ON");
                break;
            case "pmdg777 left engine primary hydraulic pump on":
                SetPmdg777SwitchOn(_state?.Pmdg777LeftEnginePrimaryHydraulicPumpOn == true, Pmdg777ControlProfile.LeftEnginePrimaryHydraulicPumpEvent, "left engine PRIMARY hydraulic pump ON");
                break;
            case "pmdg777 right engine primary hydraulic pump on":
                SetPmdg777SwitchOn(_state?.Pmdg777RightEnginePrimaryHydraulicPumpOn == true, Pmdg777ControlProfile.RightEnginePrimaryHydraulicPumpEvent, "right engine PRIMARY hydraulic pump ON");
                break;
            case "pmdg777 engine fuel fire preflight":
                ConfigurePmdg777EngineFuelFirePreflight();
                break;
            case "pmdg777 electrical hydraulic preflight":
                ConfigurePmdg777ElectricalHydraulicPreflight();
                break;
            case "pmdg777 fire overheat test":
                SendPmdg777Control(Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 89, Pmdg777ControlProfile.MouseLeftSingle, "OVHT/FIRE test");
                break;
            case "pmdg777 fo oxygen test":
                SendPmdg777Control(Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 1066, Pmdg777ControlProfile.MouseLeftSingle, "First Officer oxygen test/reset");
                break;
            case "pmdg777 exterior lights preflight":
                ConfigurePmdg777ExteriorLightsPreflight();
                break;
            case "pmdg777 air panel preflight":
                ConfigurePmdg777AirPanelPreflight();
                break;
            case "pmdg777 autobrake rto":
                QueuePmdg777Controls((Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 292, 0, "AUTOBRAKE RTO"));
                break;
            case "pmdg777 instruments preflight":
                QueuePmdg777Controls(
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 252, 2, "First Officer ND MAP"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 292, 0, "AUTOBRAKE RTO"));
                break;
            case "pmdg777 seatbelts auto":
                QueuePmdg777Controls((Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 30, 1, "seat-belt selector AUTO"));
                break;
            case "pmdg777 apu start":
                QueuePmdg777Controls((Pmdg777ControlProfile.ApuSelectorEvent, 2, "APU selector START"));
                break;
            case "pmdg777 apu power air":
                QueuePmdg777Controls(
                    (Pmdg777ControlProfile.ApuGeneratorSwitchEvent, 1, "APU GENERATOR switch ON"),
                    (Pmdg777ControlProfile.ApuBleedSwitchEvent, 1, "APU bleed AUTO"));
                break;
            case "pmdg777 external power off":
                DisconnectPmdg777ExternalPower();
                break;
            case "pmdg777 hydraulics before start":
                ConfigurePmdg777HydraulicsBeforeStart();
                break;
            case "pmdg777 fuel pumps before start":
                ConfigurePmdg777FuelPumpsBeforeStart();
                break;
            case "pmdg777 transponder xpndr":
                QueuePmdg777Controls((Pmdg777ControlProfile.TransponderModeSelectorEvent, 2, "transponder mode XPNDR"));
                break;
            case "pmdg777 transponder standby":
                SetPmdg777TransponderStandby();
                break;
            case "pmdg777 secondary engine display":
                QueuePmdg777Controls((Pmdg777ControlProfile.EngineDisplaySwitchEvent, Pmdg777ControlProfile.MouseLeftSingle, "secondary engine display"));
                break;
            case "pmdg777 engine one fuel control run":
                QueuePmdg777Controls((Pmdg777ControlProfile.EngineOneFuelControlEvent, 1, "Engine 1 fuel control RUN"));
                break;
            case "pmdg777 engine two fuel control run":
                QueuePmdg777Controls((Pmdg777ControlProfile.EngineTwoFuelControlEvent, 1, "Engine 2 fuel control RUN"));
                break;
            case "pmdg777 after start air apu":
                QueuePmdg777Controls(
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 129, 1, "left engine bleed AUTO"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 130, 1, "right engine bleed AUTO"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 135, 1, "left pack AUTO"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 136, 1, "right pack AUTO"),
                    (Pmdg777ControlProfile.ApuBleedSwitchEvent, 1, "APU bleed AUTO"),
                    (Pmdg777ControlProfile.ApuSelectorEvent, 0, "APU selector OFF"));
                break;
            case "pmdg777 takeoff flaps":
                SetPmdg777TakeoffFlaps();
                break;
            case "pmdg777 taxi lights":
                QueuePmdg777Controls(
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 119, 1, "left runway-turnoff light ON"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 120, 1, "right runway-turnoff light ON"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 121, 1, "taxi light ON"));
                _pmdg777TaxiLightsCommandedThisFlow = true;
                if (_state != null)
                {
                    _state.Pmdg777TaxiLightsCommandedThisFlow = true;
                }
                break;
            case "pmdg777 transponder tara":
                QueuePmdg777Controls((Pmdg777ControlProfile.TransponderModeSelectorEvent, 4, "transponder TA/RA"));
                break;
            case "pmdg777 lnav arm":
                QueuePmdg777Controls((Pmdg777ControlProfile.LnavSwitchEvent, Pmdg777ControlProfile.MouseLeftSingle, "LNAV arm"));
                break;
            case "pmdg777 vnav arm":
                QueuePmdg777Controls((Pmdg777ControlProfile.VnavSwitchEvent, Pmdg777ControlProfile.MouseLeftSingle, "VNAV arm"));
                break;
            case "pmdg777 takeoff lights":
                QueuePmdg777Controls(
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 22, 1, "left landing light ON"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 23, 1, "nose landing light ON"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 24, 1, "right landing light ON"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 119, 1, "left runway-turnoff light ON"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 120, 1, "right runway-turnoff light ON"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 122, 1, "strobe light ON"));
                break;
            case "pmdg777 approach lights":
                QueuePmdg777Controls(
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 22, 1, "left landing light ON"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 23, 1, "nose landing light ON"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 24, 1, "right landing light ON"));
                break;
            case "pmdg777 autobrake landing":
                QueuePmdg777Controls((Pmdg777ControlProfile.AutobrakeSelectorEvent, 4, "AUTOBRAKE 2"));
                break;
            case "pmdg777 autobrake off":
                QueuePmdg777Controls((Pmdg777ControlProfile.AutobrakeSelectorEvent, 1, "AUTOBRAKE OFF"));
                break;
            case "pmdg777 flaps one":
                SetPmdg777ApproachFlaps(1, Pmdg777ControlProfile.FlapsOneEvent, "flaps 1");
                break;
            case "pmdg777 flaps five":
                SetPmdg777ApproachFlaps(2, Pmdg777ControlProfile.FlapsFiveEvent, "flaps 5");
                break;
            case "pmdg777 flaps fifteen":
                SetPmdg777ApproachFlaps(3, Pmdg777ControlProfile.FlapsFifteenEvent, "flaps 15");
                break;
            case "pmdg777 flaps twenty":
                SetPmdg777ApproachFlaps(4, Pmdg777ControlProfile.FlapsTwentyEvent, "flaps 20");
                break;
            case "pmdg777 landing flaps":
                SetPmdg777LandingFlaps();
                break;
            case "pmdg777 gear down":
                SetPmdg777Gear(down: true);
                break;
            case "pmdg777 speedbrake arm":
                SetPmdg777SpeedbrakeArmed();
                break;
            case "pmdg777 speedbrake down":
                QueuePmdg777Controls((Pmdg777ControlProfile.SpeedbrakeDownEvent, Pmdg777ControlProfile.MouseLeftSingle, "speedbrake DOWN"));
                break;
            case "pmdg777 after landing lights":
                QueuePmdg777Controls(
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 22, 0, "left landing light OFF"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 23, 0, "nose landing light OFF"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 24, 0, "right landing light OFF"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 119, 1, "left runway-turnoff light ON"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 120, 1, "right runway-turnoff light ON"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 121, 1, "taxi light ON"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 122, 0, "strobe light OFF"));
                break;
            case "pmdg777 beacon off":
                QueuePmdg777Controls((Pmdg777ControlProfile.BeaconSwitchEvent, 0, "beacon OFF"));
                break;
            case "pmdg777 shutdown pumps":
                ConfigurePmdg777ShutdownPumps();
                break;
            case "pmdg777 gear up":
                SetPmdg777Gear(down: false);
                break;
            case "pmdg777 flaps up":
                QueuePmdg777Controls((Pmdg777ControlProfile.FlapsUpEvent, Pmdg777ControlProfile.FlapsPresetParameter, "flaps UP"));
                break;
            case "pmdg777 climb lights":
                QueuePmdg777Controls(
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 22, 0, "left landing light OFF"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 23, 0, "nose landing light OFF"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 24, 0, "right landing light OFF"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 116, 0, "logo light OFF"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 119, 0, "left runway-turnoff light OFF"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 120, 0, "right runway-turnoff light OFF"),
                    (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 121, 0, "taxi light OFF"));
                break;
            case "sayintentions taxi clearance":
            case "sayintentions takeoff clearance":
                BeginAutomatedSayIntentionsAtcStep();
                FinishOneShot();
                break;
            case "pmdg777 fo flight director on":
                SetPmdg777FirstOfficerFlightDirectorOn();
                break;
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
                CancelPendingSayIntentionsAtcRequest();
                if (_pendingGsxEngineStartProcedure != null)
                {
                    _pendingGsxEngineStartProcedure = null;
                    AppendDashboardLog("Cancelled the queued engine-start flow.");
                }
                _gsx.CancelGoodEngineStartPrompt();
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
                if (_state?.IsIniBuildsA310 == true)
                {
                    SendA310ControlValue(A310ControlProfile.BeaconLightState, 1, "beacon ON");
                    FinishOneShot();
                }
                else
                {
                    SetBeacon(true);
                }
                break;
            case "beacon off":
                if (_state?.IsIniBuildsA310 == true)
                {
                    SendA310ControlValue(A310ControlProfile.BeaconLightState, 0, "beacon OFF");
                    FinishOneShot();
                }
                else
                {
                    SetBeacon(false);
                }
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
            case "batteries auto" when _state?.IsIniBuildsA310 == true:
                SetA310BatteriesAuto();
                break;
            case "wipers-radar off" when _state?.IsIniBuildsA310 == true:
                SetA310WipersAndWeatherRadarOff();
                break;
            case "apu-fire-test" when _state?.IsIniBuildsA310 == true:
                RunA310ApuFireTest();
                break;
            case "irs nav" when _state?.IsIniBuildsA310 == true:
                SetA310IrsNav();
                break;
            case "oxygen on" when _state?.IsIniBuildsA310 == true:
                SendA310ControlValue(A310ControlProfile.OxygenLowPressureSupplyState, 1, "crew oxygen supply ON");
                FinishOneShot();
                break;
            case "annunciator-test" when _state?.IsIniBuildsA310 == true:
                RunA310AnnunciatorTest();
                break;
            case "initial-lights" when _state?.IsIniBuildsA310 == true:
                SetA310InitialExteriorLights();
                break;
            case "preflight-signs" when _state?.IsIniBuildsA310 == true:
                SetA310PreflightSigns();
                break;
            case "autoflight-computers" when _state?.IsIniBuildsA310 == true:
                SetA310AutoflightComputers();
                break;
            case "preflight-heat" when _state?.IsIniBuildsA310 == true:
                SetA310PreflightHeat();
                break;
            case "emergency-exit arm" when _state?.IsIniBuildsA310 == true:
                SendA310ControlValue(A310ControlProfile.EmergencyExitState, 1, "emergency-exit lights ARMED");
                FinishOneShot();
                break;
            case "cargo-smoke-test" when _state?.IsIniBuildsA310 == true:
                RunA310CargoSmokeTest();
                break;
            case "egpws-test" when _state?.IsIniBuildsA310 == true:
                RunA310EgpwsTest();
                break;
            case "preflight-pedestal" when _state?.IsIniBuildsA310 == true:
                SetA310PreflightPedestal();
                break;
            case "fuel-pumps on" when _state?.IsIniBuildsA310 == true:
                SetA310FuelPumpsOn();
                break;
            case "apu start" when _state?.IsIniBuildsA310 == true:
                StartA310Apu();
                break;
            case "apu power-bleed" when _state?.IsIniBuildsA310 == true:
                SetA310ApuPowerAndBleed();
                break;
            case "transponder xpdr" when _state?.IsIniBuildsA310 == true:
                SendA310ControlValue(A310ControlProfile.TcasPedestalModeState, 1, "transponder XPDR");
                FinishOneShot();
                break;
            case "ignition a" when _state?.IsIniBuildsA310 == true:
                SendA310ControlValue(
                    A310ControlProfile.IgnitionSelectorState,
                    A310ControlProfile.IgnitionStartAValue,
                    "ignition selector A");
                FinishOneShot();
                break;
            case "ignition off" when _state?.IsIniBuildsA310 == true:
                SetA310IgnitionOff();
                break;
            case "engine-1 starter" when _state?.IsIniBuildsA310 == true:
                SendA310ControlValue(A310ControlProfile.Engine1StarterState, 1, "Engine 1 START pressed");
                FinishOneShot();
                break;
            case "engine-2 starter" when _state?.IsIniBuildsA310 == true:
                SendA310ControlValue(A310ControlProfile.Engine2StarterState, 1, "Engine 2 START pressed");
                FinishOneShot();
                break;
            case "apu off" when _state?.IsIniBuildsA310 == true:
                SetA310ApuOff();
                break;
            case "speedbrake arm" when _state?.IsIniBuildsA310 == true:
                ArmA310Speedbrake();
                break;
            case "rudder-trim reset" when _state?.IsIniBuildsA310 == true:
                ResetA310RudderTrim();
                break;
            case "takeoff-flaps 15-0" when _state?.IsIniBuildsA310 == true:
                SetA310TakeoffFlaps15Zero();
                break;
            case "nose-light taxi" when _state?.IsIniBuildsA310 == true:
                SendA310ControlValue(A310ControlProfile.TaxiLightState, 1, "nose light TAXI");
                FinishOneShot();
                break;
            case "autobrake max" when _state?.IsIniBuildsA310 == true:
                SetA310AutobrakeMax();
                break;
            case "transponder-weather on" when _state?.IsIniBuildsA310 == true:
                SetA310TransponderAndWeatherRadar();
                break;
            case "takeoff-lights" when _state?.IsIniBuildsA310 == true:
                SetA310TakeoffExteriorLights();
                break;
            case "ignition takeoff" when _state?.IsIniBuildsA310 == true:
                SendA310ControlValue(
                    A310ControlProfile.IgnitionSelectorState,
                    A310ControlProfile.IgnitionContinuousRelightValue,
                    "ignition CONT RELIGHT");
                FinishOneShot();
                break;
            case "packs on" when _state?.IsIniBuildsA310 == true:
                RunA310Sequence(
                    new[]
                    {
                        (A310ControlProfile.Pack1State, 1, "Pack 1 ON"),
                        (A310ControlProfile.Pack2State, 1, "Pack 2 ON")
                    },
                    1000,
                    "A310 packs selected ON sequentially; awaiting native readback.");
                break;
            case "tcas tara" when _state?.IsIniBuildsA310 == true:
                SendA310ControlValue(A310ControlProfile.TcasPedestalModeState, 2, "TCAS TA/RA");
                FinishOneShot();
                break;
            case "speedbrake disarm" when _state?.IsIniBuildsA310 == true:
                DisarmA310Speedbrake();
                break;
            case "climb-lights" when _state?.IsIniBuildsA310 == true:
                SetA310ClimbLights();
                break;
            case "altimeters standard" when _state?.IsIniBuildsA310 == true:
                SetA310AltimetersStandard();
                break;
            case "landing-lights retract" when _state?.IsIniBuildsA310 == true:
                SetA310LandingLightsRetracted();
                break;
            case "approach-lights" when _state?.IsIniBuildsA310 == true:
                SetA310ApproachLights();
                break;
            case "speed 230" when _state?.IsIniBuildsA310 == true:
                SetA310SelectedAirspeed(230);
                break;
            case "speed 210" when _state?.IsIniBuildsA310 == true:
                SetA310SelectedAirspeed(210);
                break;
            case "speed 195" when _state?.IsIniBuildsA310 == true:
                SetA310SelectedAirspeed(195);
                break;
            case "speed 180" when _state?.IsIniBuildsA310 == true:
                SetA310SelectedAirspeed(180);
                break;
            case "flaps 15-0" when _state?.IsIniBuildsA310 == true:
                SetA310FlapsDetent(1, "15/0");
                break;
            case "flaps 15-15" when _state?.IsIniBuildsA310 == true:
                SetA310FlapsDetent(2, "15/15");
                break;
            case "flaps 15-20" when _state?.IsIniBuildsA310 == true:
                SetA310FlapsDetent(3, "15/20");
                break;
            case "flaps 30-40" when _state?.IsIniBuildsA310 == true:
                SetA310FlapsDetent(4, "30/40");
                break;
            case "flaps retract" when _state?.IsIniBuildsA310 == true:
                SetA310FlapsDetent(0, "0/0");
                break;
            case "nose-light takeoff" when _state?.IsIniBuildsA310 == true:
                SendA310ControlValue(A310ControlProfile.TaxiLightState, 0, "nose light T.O.");
                FinishOneShot();
                break;
            case "after-landing-lights" when _state?.IsIniBuildsA310 == true:
                SetA310AfterLandingLights();
                break;
            case "transponder-radar standby" when _state?.IsIniBuildsA310 == true:
                SetA310TransponderRadarStandby();
                break;
            case "nose-light off" when _state?.IsIniBuildsA310 == true:
                SendA310ControlValue(A310ControlProfile.TaxiLightState, 2, "nose light OFF");
                FinishOneShot();
                break;
            case "seatbelts off" when _state?.IsIniBuildsA310 == true:
                SendA310ControlValue(A310ControlProfile.SeatbeltsState, 0, "seat-belt signs OFF");
                FinishOneShot();
                break;
            case "fuel-pumps parking" when _state?.IsIniBuildsA310 == true:
                SetA310FuelPumpsForParking();
                break;
            case "probe-heat off" when _state?.IsIniBuildsA310 == true:
                SetA310ProbeHeatOff();
                break;
            case "irs off" when _state?.IsIniBuildsA310 == true:
                SetA310IrsOff();
                break;
            case "oxygen off" when _state?.IsIniBuildsA310 == true:
                SendA310ControlValue(A310ControlProfile.OxygenLowPressureSupplyState, 0, "crew oxygen supply OFF");
                FinishOneShot();
                break;
            case "exterior-lights off" when _state?.IsIniBuildsA310 == true:
                SetA310ExteriorLightsOff();
                break;
            case "emergency-exit disarm" when _state?.IsIniBuildsA310 == true:
                SendA310ControlValue(A310ControlProfile.EmergencyExitState, 0, "emergency-exit lights DISARMED");
                FinishOneShot();
                break;
            case "batteries off" when _state?.IsIniBuildsA310 == true:
                SetA310BatteriesOff();
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
                SpeakCurrentProcedureStepAtCommand();
                SetGearUp();
                break;
            case "gear down":
                SpeakCurrentProcedureStepAtCommand();
                SetGearDown();
                break;
            case "ground-spoilers disarm":
                SpeakCurrentProcedureStepAtCommand();
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
                SpeakCurrentProcedureStepAtCommand();
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
            case "pmdg fire-test fault-inop":
                RunPmdgFireTest(696, 0, "FAULT/INOP detection", 3000);
                break;
            case "pmdg fire-test overheat":
                RunPmdgOverheatFireTest();
                break;
            case "pmdg fire-test extinguisher-1":
                RunPmdgFireTest(715, 0, "extinguisher 1", 3000);
                break;
            case "pmdg fire-test extinguisher-2":
                RunPmdgFireTest(715, 2, "extinguisher 2", 3000);
                break;
            case "asobo737max fire-tests":
                RunAsobo737MaxFireTests();
                break;
            case "asobo737max irs left nav":
                SetAsobo737MaxIrsSelector(left: true, 2);
                break;
            case "asobo737max irs right nav":
                SetAsobo737MaxIrsSelector(left: false, 2);
                break;
            case "asobo737max position steady":
                SetAsobo737MaxPositionLightSteady();
                break;
            case "asobo737max logo on":
                SetAsobo737MaxLogoLight(true);
                break;
            case "asobo737max emergency-exit arm":
                SetAsobo737MaxEmergencyExitLightsArmed();
                break;
            case "asobo737max fuel-pumps on":
                SetAsobo737MaxFuelPumps(true);
                break;
            case "asobo737max fuel-pumps off":
                SetAsobo737MaxFuelPumps(false);
                break;
            case "asobo737max seatbelts set":
                SetAsobo737MaxPassengerSign("seatbelts", _asobo737MaxRuntime.SeatbeltsInputEventHash, () => _asobo737MaxRuntime.SeatbeltsInputState);
                break;
            case "asobo737max no-smoking set":
                SetAsobo737MaxPassengerSign("no-smoking", _asobo737MaxRuntime.NoSmokingInputEventHash, () => _asobo737MaxRuntime.NoSmokingInputState);
                break;
            case "asobo737max apu on":
                SetAsobo737MaxApuSelector(Asobo737MaxControlProfile.ApuOn);
                break;
            case "asobo737max apu start":
                SetAsobo737MaxApuSelector(Asobo737MaxControlProfile.ApuStart);
                break;
            case "asobo737max apu off":
                SetAsobo737MaxApuSelector(Asobo737MaxControlProfile.ApuOff);
                break;
            case "asobo737max apu-generator on":
                SetAsobo737MaxApuGenerator(true);
                break;
            case "asobo737max apu-generator force-on":
                ForceSetAsobo737MaxApuGenerator(true);
                break;
            case "asobo737max apu-generator off":
                SetAsobo737MaxApuGenerator(false);
                break;
            case "asobo737max apu-bleed on":
                SetAsobo737MaxApuBleed(true);
                break;
            case "asobo737max apu-bleed off":
                SetAsobo737MaxApuBleed(false);
                break;
            case "asobo737max ground-power off":
                SetAsobo737MaxGroundPowerOff();
                break;
            case "asobo737max isolation open":
                SetAsobo737MaxIsolationValve(open: true);
                break;
            case "asobo737max isolation force-open":
                ForceSetAsobo737MaxIsolationValve(open: true);
                break;
            case "asobo737max isolation auto":
                SetAsobo737MaxIsolationValve(open: false);
                break;
            case "asobo737max packs auto":
                SetAsobo737MaxPacks(auto: true);
                break;
            case "asobo737max packs force-auto":
                ForceSetAsobo737MaxPacks(auto: true);
                break;
            case "asobo737max packs off":
                SetAsobo737MaxPacks(auto: false);
                break;
            case "asobo737max engine-bleeds on":
                SetAsobo737MaxEngineBleeds(true);
                break;
            case "asobo737max engine-generators on":
                SetAsobo737MaxEngineGenerators(true);
                break;
            case "asobo737max electric-hydraulic-pumps on":
                SetAsobo737MaxElectricHydraulicPumps(true);
                break;
            case "asobo737max autothrottle arm":
                SetAsobo737MaxAutothrottleArmed();
                break;
            case "asobo737max lnav arm":
                PulseAsobo737MaxMcpButton("LNAV", Asobo737MaxLnavInputEventHash);
                break;
            case "asobo737max vnav arm":
                PulseAsobo737MaxMcpButton("VNAV", Asobo737MaxVnavInputEventHash);
                break;
            case "asobo737max transponder tara":
                SetAsobo737MaxTransponderTaRa();
                break;
            case "asobo737max transponder auto":
                SetAsobo737MaxTransponderAuto();
                break;
            case "asobo737max taxi-light auto":
                SetAsobo737MaxTaxiLight(true);
                break;
            case "asobo737max taxi-light off":
                SetAsobo737MaxTaxiLight(false);
                break;
            case "asobo737max runway-turnoff on":
                SetAsobo737MaxRunwayTurnoffLights(true);
                break;
            case "asobo737max runway-turnoff off":
                SetAsobo737MaxRunwayTurnoffLights(false);
                break;
            case "asobo737max landing-lights on":
                SetAsobo737MaxLandingLights(true);
                break;
            case "asobo737max landing-lights off":
                SetAsobo737MaxLandingLights(false);
                break;
            case "asobo737max strobes on":
                SetAsobo737MaxPositionStrobe(2);
                break;
            case "asobo737max strobes steady":
                SetAsobo737MaxPositionStrobe(0);
                break;
            case "asobo737max beacon on":
                SetAsobo737MaxAntiCollision(true);
                break;
            case "asobo737max beacon off":
                SetAsobo737MaxAntiCollision(false);
                break;
            case "asobo737max flaps takeoff":
                SetAsobo737MaxTakeoffFlaps();
                break;
            case "asobo737max flaps 1":
                SetAsobo737MaxFlaps(1);
                break;
            case "asobo737max flaps 5":
                SetAsobo737MaxFlaps(5);
                break;
            case "asobo737max flaps 15":
                SetAsobo737MaxFlaps(15);
                break;
            case "asobo737max flaps landing":
                SetAsobo737MaxLandingFlaps();
                break;
            case "asobo737max flaps clean":
                SetAsobo737MaxFlaps(0);
                break;
            case "asobo737max autobrake rto":
                SetAsobo737MaxAutobrake(1, "RTO");
                break;
            case "asobo737max autobrake off":
                SetAsobo737MaxAutobrake(0, "OFF");
                break;
            case "asobo737max gear up":
                SetAsobo737MaxGear(up: true);
                break;
            case "asobo737max gear down":
                SetAsobo737MaxGear(up: false);
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
            case "runway-turnoff on":
                SetAirbusRunwayTurnoffLights(true);
                break;
            case "runway-turnoff off":
                SetAirbusRunwayTurnoffLights(false);
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

        if (!CanStartProcedureNow(definition, _state, out var startReason))
        {
            AppendDashboardLog(startReason);
            Console.Error.WriteLine(startReason);
            FinishOneShot(3);
            return;
        }

        if (string.Equals(
                definition.Id,
                "before-takeoff",
                StringComparison.OrdinalIgnoreCase))
        {
            _pendingAutomaticBeforeTakeoffFlow = false;
        }

        if (string.Equals(
                definition.Id,
                "engine-start-sequence",
                StringComparison.OrdinalIgnoreCase)
            && ShouldCoordinateGsxDeparture()
            && !IsEngineStartPhaseStarted(_state))
        {
            if (!_gsx.Snapshot.DepartureRequestedThisFlight)
            {
                _gsx.SetDepartureRequestedThisFlight(
                    BeginGsxAction(GsxDepartureAction.PrepareForDeparture));
            }

            if (IsGsxPushbackDirectionResponsePending())
            {
                _pendingGsxEngineStartProcedure = definition;
                AppendDashboardLog(
                    "Flow 4 is waiting for the GSX pushback direction to be accepted. Select it in the EFB and keep the parking brake set.");
                UpdateDashboard();
                FinishOneShot();
                return;
            }

            // GSX being active is the sequencing gate, not ownership of its
            // Remote Control API. Another add-on can legitimately own that
            // API, in which case BeginGsxAction cannot send the request. We
            // must still wait for the aircraft to actually enter pushback;
            // otherwise Flow 4 starts the engines while the tug is idle.
            if (!IsPushbackUnderway(_state))
            {
                _pendingGsxEngineStartProcedure = definition;
                AppendDashboardLog(
                    _gsx.Snapshot.DepartureRequestedThisFlight
                    || _gsx.Snapshot.DepartureRequestAccepted
                        ? "Flow 4 is waiting for GSX pushback to begin. Engine start will begin automatically after parking-brake release and aircraft movement."
                        : "Flow 4 is waiting for GSX pushback to begin. GSX Remote Control is unavailable, so start pushback from GSX; engine start will begin automatically after parking-brake release and aircraft movement.");
                UpdateDashboard();
                FinishOneShot();
                return;
            }
        }

        _cruiseSeatbeltMonitoring =
            string.Equals(definition.Id, "cruise", StringComparison.OrdinalIgnoreCase)
            && !_state.IsIniBuildsA321Lr
            && !_state.IsA320NeoV2;
        _smoothCruiseSinceUtc = null;
        _nextCruiseSeatbeltCommandUtc = DateTime.MinValue;
        _calloutsSpokenAtCommand.Clear();
        _procedureRunner.Start(definition, _state);
        FinishProcedureOneShotIfTerminal();
    }

    private bool ShouldCoordinateGsxDeparture() =>
        _settings.EnableGsxIntegration
        && _settings.GsxAutomaticallyPrepareDeparture
        && _gsxInstallation != null
        && _gsx.Snapshot.CouatlStarted;

    private static bool CanStartProcedureNow(
        ProcedureDefinition definition,
        AircraftState state,
        out string reason)
    {
        if (string.Equals(
                definition.Id,
                "before-takeoff",
                StringComparison.OrdinalIgnoreCase)
            && state.OnGround
            && !state.BeforeTakeoffHoldEligible)
        {
            reason =
                "Flow 6 is locked until the aircraft has taxied forward and stopped at the runway holding point.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private void InvalidateAircraftAutomation(
        AutomationInvalidationReason reason,
        string? detail = null)
    {
        var policy = AutomationInvalidationPolicy.For(reason);
        var generation = _automation.InvalidateLiveWork();

        // Live connection, cockpit-command, and aircraft SDK state never
        // survives a generation boundary.
        _pendingProcedure = null;
        _pendingBeaconProcedure = null;
        _pendingNavLogoSelectorProcedure = null;
        _pendingBatteryProcedure = null;
        _pendingNativeAction = null;
        _pendingFireTest = null;
        _pendingFlyByWireFireTest = null;
        _asobo737MaxFireTestsInProgress = false;
        _pendingFuelPumpSequence = null;
        StopFuelPumpSequenceTimer();
        _gsx.CancelPendingAction();
        _pmdg777TaxiLightsCommandedThisFlow = false;
        _pmdg777ControlQueue.Clear();
        _pmdg777ControlQueueTimer?.Stop();
        _pmdg777ControlQueueTimer?.Dispose();
        _pmdg777ControlQueueTimer = null;
        _pmdg777ControlQueueAction = null;
        _pmdg777Runtime.ResetConnectionState();
        _pmdgNg3Runtime.ResetConnectionState();
        _pmdg777SdkInitialized = false;
        _pmdg777AdiruOnTimer?.Stop();
        _pmdg777AdiruOnTimer?.Dispose();
        _pmdg777AdiruOnTimer = null;

        // Logical procedure/flow state survives connection replacement, but
        // is discarded when a different aircraft makes it incompatible.
        policy.ApplyToLogicalFlowIntent(() =>
        {
            _pendingAutomaticBeforeTakeoffFlow = false;
            _pendingAutomaticTakeoffFlow = false;
            _taxiToRunwayArmed = false;
            _pendingGsxEngineStartProcedure = null;
        });
        policy.ApplyToProcedure(_procedureRunner);
        ClearCommandedAircraftState();
        _replayTimer?.Stop();
        _replayTimer?.Dispose();
        _replayTimer = null;
        _replayStates = Array.Empty<AircraftState>();
        _replayIndex = 0;
        _replayActive = false;
        _state = null;

        // Completed flows, ProcedureSession, SimBrief data, and settings are
        // persistent flight/application state and are intentionally untouched.
        AppLog.Write(
            $"Aircraft automation generation advanced to {generation}: {reason}"
            + (string.IsNullOrWhiteSpace(detail) ? "." : $" {detail}."));
    }

    private bool IsTaxiToHoldingPointTransition(AircraftState? state)
    {
        var nextFlow = state == null
            ? null
            : FlowRecommendationEngine.Recommend(
                state,
                _completedProcedureIds).Procedure;
        return state != null
               && FlightProgressTransitionPolicy.ShouldShowTaxiToHoldingPoint(
                   state,
                   IsProcedureActive(_procedureRunner.Status),
                   _completedProcedureIds.Contains("after-start-taxi"),
                   _completedProcedureIds.Contains("before-takeoff"),
                   _completedProcedureIds.Contains("parking-shutdown"),
                   nextFlow?.Id);
    }

    private const string TaxiToHoldingPointGuidance =
        "Taxi to the runway holding point. Flow 6 will start automatically when the aircraft stops.";

    private bool A310HydraulicPanelSafe() =>
        _nativeRuntime.A310.HydraulicEngine1.HasValue
        && _nativeRuntime.A310.HydraulicEngine1A.HasValue
        && _nativeRuntime.A310.HydraulicEngine2.HasValue
        && _nativeRuntime.A310.HydraulicEngine2B.HasValue
        && _nativeRuntime.A310.HydraulicElectric.HasValue
        && Math.Abs(_nativeRuntime.A310.HydraulicEngine1.Value - 1) < 0.1
        && Math.Abs(_nativeRuntime.A310.HydraulicEngine1A.Value - 1) < 0.1
        && Math.Abs(_nativeRuntime.A310.HydraulicEngine2.Value - 1) < 0.1
        && Math.Abs(_nativeRuntime.A310.HydraulicEngine2B.Value - 1) < 0.1
        && Math.Abs(_nativeRuntime.A310.HydraulicElectric.Value) < 0.1;

    private bool A310WipersAndWeatherRadarOff() =>
        _nativeRuntime.A310.CaptainWiper.HasValue
        && _nativeRuntime.A310.FirstOfficerWiper.HasValue
        && _nativeRuntime.A310.WeatherRadarSystem.HasValue
        && Math.Abs(_nativeRuntime.A310.CaptainWiper.Value) < 0.1
        && Math.Abs(_nativeRuntime.A310.FirstOfficerWiper.Value) < 0.1
        && Math.Abs(_nativeRuntime.A310.WeatherRadarSystem.Value - 1) < 0.1;

    private bool A310InitialExteriorLightsSet() =>
        _nativeRuntime.A310.InitialLightStates.All(value => value.HasValue)
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[0]!.Value - 1) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[1]!.Value) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[2]!.Value - 2) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[3]!.Value - 2) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[4]!.Value - 2) < 0.1
        && _nativeRuntime.A310.InitialLightStates.Skip(5).All(value => Math.Abs(value!.Value) < 0.1);

    private bool A310TakeoffExteriorLightsSet() =>
        _nativeRuntime.A310.InitialLightStates.All(value => value.HasValue)
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[0]!.Value - 1) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[1]!.Value - 1) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[2]!.Value) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[3]!.Value) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[4]!.Value) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[5]!.Value) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[6]!.Value - 1) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[7]!.Value - 1) < 0.1;

    private bool A310IgnitionContinuousRelight() =>
        _nativeRuntime.A310.Flow4EngineStartStates[0].HasValue
        && Math.Abs(
            _nativeRuntime.A310.Flow4EngineStartStates[0]!.Value
            - A310ControlProfile.IgnitionContinuousRelightValue) < 0.1;

    private bool A310PacksOn() =>
        _nativeRuntime.A310.Flow4EngineStartStates[1].HasValue
        && _nativeRuntime.A310.Flow4EngineStartStates[2].HasValue
        && _nativeRuntime.A310.Flow4EngineStartStates[1]!.Value > 0.5f
        && _nativeRuntime.A310.Flow4EngineStartStates[2]!.Value > 0.5f;

    private bool A310TcasTaRaSet() =>
        _nativeRuntime.A310.Flow2States[20].HasValue
        && Math.Abs(_nativeRuntime.A310.Flow2States[20]!.Value - 2) < 0.1;

    private bool A310ClimbLightsSet() =>
        _nativeRuntime.A310.InitialLightStates[2].HasValue
        && _nativeRuntime.A310.InitialLightStates[6].HasValue
        && _nativeRuntime.A310.InitialLightStates[7].HasValue
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[2]!.Value - 2) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[6]!.Value) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[7]!.Value) < 0.1;

    private bool A310LandingLightsRetracted() =>
        _nativeRuntime.A310.InitialLightStates[3].HasValue
        && _nativeRuntime.A310.InitialLightStates[4].HasValue
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[3]!.Value - 2) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[4]!.Value - 2) < 0.1;

    private bool A310ApproachLightsSet() =>
        _nativeRuntime.A310.Flow2States[0].HasValue
        && _nativeRuntime.A310.InitialLightStates.Skip(2).Take(3).All(value => value.HasValue)
        && _nativeRuntime.A310.InitialLightStates[6].HasValue
        && _nativeRuntime.A310.InitialLightStates[7].HasValue
        && _nativeRuntime.A310.Flow2States[0]!.Value > 0.5f
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[2]!.Value) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[3]!.Value) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[4]!.Value) < 0.1
        && _nativeRuntime.A310.InitialLightStates[6]!.Value > 0.5f
        && _nativeRuntime.A310.InitialLightStates[7]!.Value > 0.5f;

    private bool A310NoseLightTakeoff() =>
        _nativeRuntime.A310.InitialLightStates[2].HasValue
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[2]!.Value) < 0.1;

    private bool A310AfterLandingLightsSet() =>
        _nativeRuntime.A310.InitialLightStates.Skip(2).Take(6).All(value => value.HasValue)
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[2]!.Value - 1) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[3]!.Value - 2) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[4]!.Value - 2) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[5]!.Value) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[6]!.Value) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[7]!.Value) < 0.1;

    private bool A310TransponderStandby() =>
        _nativeRuntime.A310.Flow2States[20].HasValue
        && Math.Abs(_nativeRuntime.A310.Flow2States[20]!.Value) < 0.1;

    private bool A310WeatherRadarOff() =>
        _nativeRuntime.A310.WeatherRadarSystem.HasValue
        && Math.Abs(_nativeRuntime.A310.WeatherRadarSystem.Value - 1) < 0.1;

    private bool A310NoseLightOff() =>
        _nativeRuntime.A310.InitialLightStates[2].HasValue
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[2]!.Value - 2) < 0.1;

    private bool A310SeatbeltsOff() =>
        _nativeRuntime.A310.Flow2States[0].HasValue
        && Math.Abs(_nativeRuntime.A310.Flow2States[0]!.Value) < 0.1;

    private bool A310FuelPumpsParkingSet()
    {
        if (_nativeRuntime.A310.FuelPumpStates.Any(value => !value.HasValue))
        {
            return false;
        }
        var retainApuPump = _nativeRuntime.A310.Flow3ApuStates[0] > 0.5f
                            || _nativeRuntime.A310.Flow3ApuStates[2] > 0.5f;
        return _nativeRuntime.A310.FuelPumpStates.Select((value, index) =>
            index == 3 && retainApuPump
                ? value!.Value > 0.5f
                : Math.Abs(value!.Value) < 0.1).All(value => value);
    }

    private bool A310ProbeHeatOff() => A310Flow2StatesMatch(12, 3, 0);

    private bool A310IrsOff() =>
        _nativeRuntime.A310.Irs1.HasValue && _nativeRuntime.A310.Irs2.HasValue && _nativeRuntime.A310.Irs3.HasValue
        && Math.Abs(_nativeRuntime.A310.Irs1.Value) < 0.1
        && Math.Abs(_nativeRuntime.A310.Irs2.Value) < 0.1
        && Math.Abs(_nativeRuntime.A310.Irs3.Value) < 0.1;

    private bool A310OxygenOff() =>
        _nativeRuntime.A310.OxygenSupply.HasValue && Math.Abs(_nativeRuntime.A310.OxygenSupply.Value) < 0.1;

    private bool A310ExteriorLightsOff() =>
        _nativeRuntime.A310.InitialLightStates.Skip(1).All(value => value.HasValue)
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[1]!.Value) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[2]!.Value - 2) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[3]!.Value - 2) < 0.1
        && Math.Abs(_nativeRuntime.A310.InitialLightStates[4]!.Value - 2) < 0.1
        && _nativeRuntime.A310.InitialLightStates.Skip(5).All(value => Math.Abs(value!.Value) < 0.1);

    private bool A310EmergencyExitDisarmed() =>
        _nativeRuntime.A310.Flow2States[15].HasValue
        && Math.Abs(_nativeRuntime.A310.Flow2States[15]!.Value) < 0.1;

    private bool A310BatteriesOff() =>
        _nativeRuntime.A310.Battery1Auto == false
        && _nativeRuntime.A310.Battery2Auto == false
        && _nativeRuntime.A310.Battery3Auto == false;

    private bool A310PreflightSignsSet() =>
        A310Flow2StatesMatch(0, 2, 1);

    private bool A310AutoflightComputersSet() =>
        A310Flow2StatesMatch(2, 6, 1);

    private bool A310PreflightHeatSet() =>
        A310Flow2StatesMatch(8, 7, 1);

    private bool A310EmergencyExitArmed() =>
        _nativeRuntime.A310.Flow2States[15].HasValue
        && Math.Abs(_nativeRuntime.A310.Flow2States[15]!.Value - 1) < 0.1;

    private bool A310PreflightPedestalSet() =>
        _nativeRuntime.A310.Flow2States[18].HasValue
        && _nativeRuntime.A310.Flow2States[19].HasValue
        && _nativeRuntime.A310.Flow2States[20].HasValue
        && _nativeRuntime.A310.WeatherRadarSystem.HasValue
        && Math.Abs(_nativeRuntime.A310.Flow2States[18]!.Value) < 0.1
        && Math.Abs(_nativeRuntime.A310.Flow2States[19]!.Value) < 0.1
        && Math.Abs(_nativeRuntime.A310.Flow2States[20]!.Value) < 0.1
        && Math.Abs(_nativeRuntime.A310.WeatherRadarSystem.Value - 1) < 0.1;

    private bool A310Flow2StatesMatch(int start, int count, float target) =>
        _nativeRuntime.A310.Flow2States
            .Skip(start)
            .Take(count)
            .All(value => value.HasValue && Math.Abs(value.Value - target) < 0.1);

    private bool A310Flow2ReadbacksAvailable(int start, int count) =>
        _nativeRuntime.A310.Flow2States.Skip(start).Take(count).All(value => value.HasValue);

    private bool A310Flow4ReadbacksAvailable(int start, int count) =>
        _nativeRuntime.A310.Flow4EngineStartStates
            .Skip(start)
            .Take(count)
            .All(value => value.HasValue);

    private bool A310ApuPowerAndBleedSet() =>
        _nativeRuntime.A310.Flow3ApuStates[2] > 0.5f
        && _nativeRuntime.A310.Flow3ApuStates[3] > 0.5f
        && _nativeRuntime.A310.Flow3ApuStates[4] > 0.5f;

    private bool A310TransponderXpdrSet() =>
        _nativeRuntime.A310.Flow2States[20].HasValue
        && Math.Abs(_nativeRuntime.A310.Flow2States[20]!.Value - 1) < 0.1;

    private bool A310IgnitionSelectedForStart() =>
        _nativeRuntime.A310.Flow4EngineStartStates[0].HasValue
        && Math.Abs(
            _nativeRuntime.A310.Flow4EngineStartStates[0]!.Value
            - A310ControlProfile.IgnitionStartAValue) < 0.1;

    private static bool IsA310IgnitionOff(
        float? nativeSelectorPosition,
        double? standardEngineIgnitionPosition) =>
        nativeSelectorPosition.HasValue
        && Math.Abs(
            nativeSelectorPosition.Value
            - A310ControlProfile.IgnitionOffValue) < 0.1
        || standardEngineIgnitionPosition.HasValue
        && Math.Abs(standardEngineIgnitionPosition.Value) < 0.1;

    private bool A310PacksClosedForStart() =>
        _nativeRuntime.A310.Flow4EngineStartStates[1].HasValue
        && _nativeRuntime.A310.Flow4EngineStartStates[2].HasValue
        && _nativeRuntime.A310.Flow4EngineStartStates[1]!.Value <= 0.5f
        && _nativeRuntime.A310.Flow4EngineStartStates[2]!.Value <= 0.5f;

    private bool A310FuelPumpsOn() =>
        _nativeRuntime.A310.FuelPumpStates.All(value => value.HasValue && value.Value > 0.5f);

    private void ApplyA310EngineStartState(AircraftState state)
    {
        state.A310IgnitionSelectedForStart = A310IgnitionSelectedForStart();
        state.A310PacksClosedForStart = A310PacksClosedForStart();
        state.A310Engine1StarterSelected = _nativeRuntime.A310.Flow4EngineStartStates[3] > 0.5f;
        state.A310Engine2StarterSelected = _nativeRuntime.A310.Flow4EngineStartStates[4] > 0.5f;
        state.A310Engine1FuelLeverOn = _nativeRuntime.A310.Flow4EngineStartStates[5] > 0.5f;
        state.A310Engine2FuelLeverOn = _nativeRuntime.A310.Flow4EngineStartStates[6] > 0.5f;
        state.A310IgnitionOff = IsA310IgnitionOff(
            _nativeRuntime.A310.Flow4EngineStartStates[0],
            state.EngineModeSelectorPosition);
    }

    private void ApplyA310AfterStartState(AircraftState state)
    {
        state.A310RudderTrimCentered = _nativeRuntime.A310.Flow2States[19].HasValue
                                        && Math.Abs(_nativeRuntime.A310.Flow2States[19]!.Value) < 0.05;
        state.A310TaxiLightTaxi = _nativeRuntime.A310.InitialLightStates[2].HasValue
                                   && Math.Abs(_nativeRuntime.A310.InitialLightStates[2]!.Value - 1) < 0.1;
        state.A310AutobrakeMax = A310AutobrakeMaxSelected();
        state.A310WeatherRadarOn = _nativeRuntime.A310.WeatherRadarSystem.HasValue
                                    && Math.Abs(_nativeRuntime.A310.WeatherRadarSystem.Value) < 0.1
                                    && _nativeRuntime.A310.Flow5States[0].HasValue
                                    && Math.Abs(_nativeRuntime.A310.Flow5States[0]!.Value - 2) < 0.1;
    }

    private bool A310AutobrakeMaxSelected() =>
        IsA310AutobrakeMaxSelected(
            _nativeRuntime.A310.Flow2States[18],
            _nativeRuntime.A310.Flow5States[1]);

    private static bool IsA310AutobrakeMaxSelected(
        float? selectorLevel,
        float? maxDecelerationAnnunciator) =>
        selectorLevel.HasValue
        && Math.Abs(selectorLevel.Value - 3) < 0.1
        || maxDecelerationAnnunciator.HasValue
        && maxDecelerationAnnunciator.Value > 0.5f;

    private void TryStartPendingBeforeTakeoffFlow()
    {
        if (!_pendingAutomaticBeforeTakeoffFlow
            || _state == null
            || !_state.BeforeTakeoffHoldEligible
            || IsProcedureActive(_procedureRunner.Status))
        {
            return;
        }

        var definition = ProcedureCatalog.Find(_state, "before-takeoff");
        if (definition == null || _completedProcedureIds.Contains(definition.Id))
        {
            _pendingAutomaticBeforeTakeoffFlow = false;
            return;
        }

        _pendingAutomaticBeforeTakeoffFlow = false;
        AppendDashboardLog(
            "Runway holding point detected after taxi; starting Flow 6.");
        _automation.Enqueue($"procedure start {definition.Id}");
    }

    private void TryStartPendingTakeoffFlow()
    {
        if (!_pendingAutomaticTakeoffFlow
            || _state == null
            || IsProcedureActive(_procedureRunner.Status))
        {
            return;
        }

        var definition = ProcedureCatalog.Find(_state, "takeoff-climb");
        if (definition == null || _completedProcedureIds.Contains(definition.Id))
        {
            _pendingAutomaticTakeoffFlow = false;
            return;
        }

        _pendingAutomaticTakeoffFlow = false;
        AppendDashboardLog(
            "Completed Before Takeoff flow restored; arming Flow 7 before the takeoff roll.");
        _automation.Enqueue($"procedure start {definition.Id}");
    }

    private static bool IsPushbackUnderway(AircraftState state) =>
        GsxDepartureCoordinator.IsPushbackUnderway(
            state.OnGround,
            state.ParkingBrakeSet,
            state.GroundSpeedKnots);

    private static bool IsEngineStartPhaseStarted(AircraftState state) =>
        GsxDepartureCoordinator.EngineStartPhaseStarted(
            state.EngineModeIgnStart,
            state.Engine1StarterActive,
            state.Engine2StarterActive,
            state.Engine1FuelFlowDetected,
            state.Engine2FuelFlowDetected,
            state.Engine1Running,
            state.Engine2Running);

    private bool IsGsxPushbackDirectionResponsePending()
    {
        var gsx = _gsx.Snapshot;
        return (gsx.MenuOpen
                && GsxPromptPolicy.IsPushbackDirectionMenu(gsx.CurrentMenu))
               || gsx.PendingChoice
               || gsx.AwaitingChoiceAcknowledgement;
    }

    private bool TryAutoSelectSayIntentionsPushbackDirection()
    {
        var gsx = _gsx.Snapshot;
        if (!gsx.MenuOpen
            || _state == null
            || !_sayIntentionsRuntime.PushbackTargetHeadingDegrees.HasValue
            || DateTime.UtcNow - _sayIntentionsRuntime.PushbackTargetCapturedUtc
                > TimeSpan.FromMinutes(30))
        {
            return false;
        }

        var targetHeading = _sayIntentionsRuntime.PushbackTargetHeadingDegrees.Value;
        var choice = GsxPushbackDirectionCoordinator.FindChoice(
            gsx.CurrentMenu,
            _state.MagneticHeadingDegrees,
            targetHeading);
        if (!choice.HasValue)
        {
            return false;
        }

        var label = gsx.CurrentMenu.Choices[choice.Value];
        var currentHeading = _state.MagneticHeadingDegrees;
        _sayIntentionsRuntime.ClearPushbackTargetHeading();
        SubmitLiveGsxChoice(choice.Value, label, null);
        CloseGsxChoiceDialog();
        AppendDashboardLog(
            $"Matched SayIntentions 'Face {targetHeading:000}' clearance: sent GSX '{label}'.");
        AppLog.Write(
            $"Auto-selected live GSX pushback choice '{label}' from aircraft heading {currentHeading:000.0} to SayIntentions target {targetHeading:000.0}.");
        return true;
    }

    private bool TryAutoAcceptGsxAttachPushbackTug()
    {
        var gsx = _gsx.Snapshot;
        var departureFlowActive =
            (string.Equals(
                 _procedureRunner.Definition?.Id,
                 "apu-start-pushback",
                 StringComparison.OrdinalIgnoreCase)
             || string.Equals(
                 _procedureRunner.Definition?.Id,
                 "engine-start-sequence",
                 StringComparison.OrdinalIgnoreCase))
            && IsProcedureActive(_procedureRunner.Status)
            || _pendingGsxEngineStartProcedure != null;
        if (!gsx.MenuOpen
            || !_settings.EnableGsxIntegration
            || !_settings.GsxAutomaticallyPrepareDeparture
            || (!gsx.BoardingRequestedThisFlight
                && !gsx.DepartureRequestedThisFlight
                && !gsx.DepartureRequestAccepted
                && !departureFlowActive))
        {
            return false;
        }

        var choice = GsxPromptPolicy.FindAttachPushbackTugConfirmation(
            gsx.CurrentMenu);
        if (!choice.HasValue)
        {
            return false;
        }

        var label = gsx.CurrentMenu.Choices[choice.Value];
        SubmitLiveGsxChoice(choice.Value, label, null);
        CloseGsxChoiceDialog();
        AppendDashboardLog("First Officer instructed GSX to attach the pushback tug.");
        AppLog.Write("Auto-accepted the live GSX pushback-tug attachment prompt.");
        return true;
    }

    private bool TryAutoContinueGsxPushback()
    {
        var gsx = _gsx.Snapshot;
        var departureFlowActive =
            string.Equals(
                _procedureRunner.Definition?.Id,
                "apu-start-pushback",
                StringComparison.OrdinalIgnoreCase)
            && IsProcedureActive(_procedureRunner.Status)
            || _pendingGsxEngineStartProcedure != null;
        if (!gsx.MenuOpen
            || !_settings.EnableGsxIntegration
            || !_settings.GsxAutomaticallyPrepareDeparture
            || !departureFlowActive
            || _state == null
            || !_state.OnGround
            || !_state.ParkingBrakeSet
            || _state.GroundSpeedKnots > 0.5)
        {
            return false;
        }

        var choice = GsxPromptPolicy.FindContinuePushbackAction(
            gsx.CurrentMenu);
        if (!choice.HasValue)
        {
            return false;
        }

        var label = gsx.CurrentMenu.Choices[choice.Value];
        SubmitLiveGsxChoice(choice.Value, label, null);
        CloseGsxChoiceDialog();
        AppendDashboardLog(
            "First Officer instructed GSX to continue pushback after verifying the parking brake is set.");
        AppLog.Write(
            "Auto-selected the live GSX Continue Pushback action after parking-brake readback.");
        return true;
    }

    private void TryStartPendingGsxEngineFlow()
    {
        if (_pendingGsxEngineStartProcedure == null
            || _state == null
            || IsProcedureActive(_procedureRunner.Status))
        {
            return;
        }

        if (IsGsxPushbackDirectionResponsePending())
        {
            return;
        }

        var pushbackUnderway = IsPushbackUnderway(_state);
        var engineStartPhaseStarted = IsEngineStartPhaseStarted(_state);
        if (!pushbackUnderway && !engineStartPhaseStarted)
        {
            return;
        }

        var definition = _pendingGsxEngineStartProcedure;
        _pendingGsxEngineStartProcedure = null;
        _gsx.ClearDepartureRequestAcceptedTime();
        AppendDashboardLog(
            pushbackUnderway
                ? "GSX pushback is underway; starting the engine-start flow."
                : "Engine-start phase already detected after resume; continuing Flow 4 without a new GSX pushback request.");
        StartProcedure(definition);
    }

    private void SetPmdgIrsSelector(bool left, uint position)
    {
        SendPmdgNg3Control(left ? 255u : 256u, position);
        var now = DateTime.UtcNow;
        if (left)
        {
            _pmdgNg3Runtime.RecordLeftIrsCommand(position, now);
            if (_state != null)
            {
                _state.Adirs1SelectorState = position;
            }
        }
        else
        {
            _pmdgNg3Runtime.RecordRightIrsCommand(position, now);
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
        if (_pmdgNg3Runtime.State?.LogoLightOn == on)
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
        _pmdgNg3Runtime.ClearLogoLightCommand();
        AppLog.Write(
            $"Executed PMDG logo light ROTOR_BRAKE command: switch id {switchId} action {actionCode}, {clicks} click(s) toward {(on ? "ON" : "OFF")}.");
        FinishOneShot();
    }

    private void SetPmdgPositionStrobe(uint position)
    {
        var current = _pmdgNg3Runtime.State?.PositionStrobeSelector;
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
        _pmdgNg3Runtime.ClearPositionStrobeCommand();

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

        _pmdgNg3Runtime.RecordEmergencyExitCommand(position, DateTime.UtcNow);
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
            _pmdgNg3Runtime.State?.CenterFuelQuantityPounds > PmdgCenterFuelPumpRequiredThresholdPounds;
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
        if (_pmdgNg3Runtime.State?.ElectricHydraulicPump1On != true)
        {
            SendPmdgNg3Control(168, PmdgMouseLeftSingle);
            clicked.Add("ELEC 1");
        }

        if (_pmdgNg3Runtime.State?.ElectricHydraulicPump2On != true)
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
        var current = _pmdgNg3Runtime.State?.GearLever;
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

        _automation.Schedule(
            delayMs,
            () => TransmitGearEvent(eventId),
            $"PMDG gear fallback {eventId}",
            AircraftVariant.Pmdg737800);
    }

    private void TransmitGearEvent(CopilotEvent eventId)
    {
        if (Connection == null)
        {
            return;
        }

        Connection.TransmitClientEvent(
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

    private void SetAirbusRunwayTurnoffLights(bool on)
    {
        if (_state == null || Connection == null)
        {
            AppendDashboardLog("Runway turnoff lights blocked: aircraft state is unavailable.");
            FinishOneShot(4);
            return;
        }

        if (_state.IsFlyByWireA320Neo)
        {
            var desired = on ? 1 : 0;
            SendMobiFlightCommand(
                $"MF.SimVars.Set.(A:CIRCUIT SWITCH ON:21, Bool) {desired} != if{{ 21 (>K:ELECTRICAL_CIRCUIT_TOGGLE) }} " +
                $"(A:CIRCUIT SWITCH ON:22, Bool) {desired} != if{{ 22 (>K:ELECTRICAL_CIRCUIT_TOGGLE) }}");
            SendMobiFlightCommand("MF.DummyCmd");
        }
        else
        {
            var inputEventHash = _state.IsIniBuildsA321Lr
                ? A321ControlProfile.RunwayTurnoffInputEventHash
                : _state.IsIniBuildsA330
                    ? A330ControlProfile.RunwayTurnoffInputEventHash
                    : A320RunwayTurnoffProfile.InputEventHash;
            Connection.SetInputEvent(inputEventHash, on ? 1.0 : 0.0);
        }

        BeginNativeAction(
            "Runway turnoff lights",
            state => state.RunwayTurnoffLightsOn == on,
            false,
            TimeSpan.FromSeconds(10),
            on ? "ON" : "OFF");
        AppLog.Write(
            $"Executed {_state.Variant} runway turnoff light command: {(on ? "ON" : "OFF")}.");
    }

    private void SetPmdgLandingLights(bool on)
    {
        var commandedPosition = on ? 2f : 0f;
        _pmdgNg3Runtime.RecordLandingLightCommand(commandedPosition, DateTime.UtcNow);
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
        if (Connection == null)
        {
            return;
        }

        Connection.TransmitClientEvent(
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            CopilotEvent.RotorBrake,
            switchId * 100u + actionCode,
            Priority.Highest,
            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
    }

    private void SchedulePmdgRotorBrakeSwitch(uint switchId, uint actionCode, int delayMs)
    {
        _automation.Schedule(
            delayMs,
            () => SendPmdgRotorBrakeSwitch(switchId, actionCode),
            $"PMDG rotor-brake switch {switchId}",
            AircraftVariant.Pmdg737800);
    }

    private void SchedulePmdgNg3Control(uint sdkEventOffset, uint parameter, int delayMs)
    {
        _automation.Schedule(
            delayMs,
            () => SendPmdgNg3Control(sdkEventOffset, parameter),
            $"PMDG NG3 control {sdkEventOffset}",
            AircraftVariant.Pmdg737800);
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
        var current = _pmdgNg3Runtime.State?.TransponderMode;
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
        var takeoffFlaps = _state?.BoeingTakeoffFlaps ?? _pmdgNg3Runtime.State?.TakeoffFlaps ?? 5;
        if (takeoffFlaps <= 0)
        {
            takeoffFlaps = 5;
        }

        SetPmdgFlapsDetent(takeoffFlaps);
    }

    private void SetPmdgLandingFlaps()
    {
        var landingFlaps = _state?.BoeingLandingFlaps ?? _pmdgNg3Runtime.State?.LandingFlaps ?? 30;
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

        _pendingAutomaticTakeoffFlow =
            _settings.AutoChainFlow6To7
            && _completedProcedureIds.Contains("before-takeoff")
            && !_completedProcedureIds.Contains("takeoff-climb");
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

        if (string.Equals(definition.Id, "after-start-taxi", StringComparison.OrdinalIgnoreCase))
        {
            _pmdg777TaxiLightsCommandedThisFlow = false;
            if (_state != null)
            {
                _state.Pmdg777TaxiLightsCommandedThisFlow = false;
            }
        }

        StartProcedure(definition);
    }

    private void ClearCommandedAircraftState()
    {
        _pmdgNg3Runtime.ClearCommandedState();
        _pmdg777Runtime.ClearObservedTests();

        _nativeRuntime.ClearCommandedState();
    }

    private void ResetFlightProgress()
    {
        CancelFuelPumpSequence();
        CancelPendingSayIntentionsAtcRequest();
        _procedureRunner.Cancel();
        ClearCommandedAircraftState();
        _pendingFireTest = null;
        _pendingFlyByWireFireTest = null;
        _apuFireTestCompleted = false;
        _engine1FireTestCompleted = false;
        _engine2FireTestCompleted = false;
        if (_state != null)
        {
            _state.ApuFireTestCompleted = false;
            _state.Engine1FireTestCompleted = false;
            _state.Engine2FireTestCompleted = false;
        }
        _completedProcedureIds.Clear();
        _forwardTaxiObservedThisFlight = false;
        _taxiToRunwayArmed = false;
        _pendingAutomaticBeforeTakeoffFlow = false;
        _pendingAutomaticTakeoffFlow = false;
        _gsx.ResetFlightState();
        _taxiClearanceReceived = false;
        _takeoffClearanceReceived = false;
        _pmdg777TaxiLightsCommandedThisFlow = false;
        _pendingGsxEngineStartProcedure = null;
        _sayIntentionsRuntime.ClearPushbackTargetHeading();
        _procedureSession.ResetProgress(DateTime.UtcNow);
        _simBriefFlightPlan = null;
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
        UpdateSimBriefStatus();
        UpdateDashboard();
        if (_settings.SimBriefAutoImportOnNewFlight
            && (!string.IsNullOrWhiteSpace(_settings.SimBriefPilotId)
                || !string.IsNullOrWhiteSpace(_settings.SimBriefUsername)))
        {
            _ = ImportLatestSimBriefAsync(showReview: true, automatic: true);
        }
        FinishOneShot();
    }

    private void RunPmdgFireTest(uint eventOffset, uint testPosition, string label, int holdMilliseconds)
    {
        SendPmdgNg3Control(eventOffset, testPosition);
        SchedulePmdgNg3Control(eventOffset, 1, holdMilliseconds);
        AppLog.Write($"Executed PMDG {label} test; held for {holdMilliseconds / 1000.0:F1} seconds.");
        FinishOneShot();
    }

    private void RunPmdgOverheatFireTest()
    {
        SendPmdgNg3Control(696, 2);
        SchedulePmdgNg3Control(347, PmdgMouseLeftSingle, 2500);
        SchedulePmdgNg3Control(696, 1, 5000);
        AppLog.Write("Executed PMDG OVHT/FIRE test: held for 5.0 seconds with master FIRE WARN cancellation during the hold.");
        FinishOneShot();
    }

    private void RunAsobo737MaxFireTests()
    {
        if (Connection == null || _state?.IsAsobo737Max8 != true)
        {
            AppendDashboardLog("737 MAX fire tests blocked: Asobo 737 MAX profile is not active.");
            FinishOneShot(3);
            return;
        }

        if (_asobo737MaxFireTestsInProgress)
        {
            return;
        }

        _asobo737MaxFireTestsInProgress = true;
        _apuFireTestCompleted = false;
        _engine1FireTestCompleted = false;
        _engine2FireTestCompleted = false;
        if (_state != null)
        {
            _state.ApuFireTestCompleted = false;
            _state.Engine1FireTestCompleted = false;
            _state.Engine2FireTestCompleted = false;
        }

        AppendDashboardLog("737 MAX fire tests running.");
        AppLog.Write(
            "Executing Asobo 737 MAX fire tests via dedicated fire-test B-events and extinguisher InputEvents.");

        var steps = new Queue<(ulong? Hash, double Position, string? CalculatorCode, string Label, int HoldMs, bool Repeat)>(new (ulong? Hash, double Position, string? CalculatorCode, string Label, int HoldMs, bool Repeat)[]
        {
            (null, 0.0, "(>B:FIRE_CONTROL_OVHT_DET_TEST_OVHT_FIRE)", "OVHT/FIRE detection", 5000, true),
            (Asobo737MaxProcedureFireWarningInputEventHash, 1.0, null, "fire warning bell", 1500, true),
            (Asobo737MaxProcedureFireWarningInputEventHash, 0.0, null, "fire warning bell release", 500, false),
            (null, 0.0, "(>B:FIRE_CONTROL_OVHT_DET_TEST_NEUTRAL)", "fire detection neutral", 500, false),
            (null, 0.0, "(>B:FIRE_CONTROL_OVHT_DET_TEST_FAULT_INOP)", "FAULT/INOP detection", 3000, true),
            (null, 0.0, "(>B:FIRE_CONTROL_OVHT_DET_TEST_NEUTRAL)", "fire detection neutral", 500, false),
            (Asobo737MaxExtinguisherTestInputEventHash, 0.0, null, "extinguisher test 1", 3000, true),
            (Asobo737MaxExtinguisherTestInputEventHash, 1.0, null, "extinguisher test neutral", 500, false),
            (Asobo737MaxExtinguisherTestInputEventHash, 2.0, null, "extinguisher test 2", 3000, true),
            (Asobo737MaxExtinguisherTestInputEventHash, 1.0, null, "extinguisher test neutral", 500, false),
            (null, 0.0, "1 (>B:CARGO_FIRE_TEST_Inc)", "cargo fire test", 5000, true),
            (null, 0.0, "(>B:CARGO_FIRE_TEST_Neutral)", "cargo fire test neutral", 500, false)
        });

        System.Windows.Forms.Timer? timer = null;
        GenerationBoundCockpitAction? guardedAction = null;
        DateTime stepDeadlineUtc = DateTime.MinValue;
        bool stepStarted = false;
        bool stepSent = false;
        (ulong? Hash, double Position, string? CalculatorCode, string Label, int HoldMs, bool Repeat) currentStep = default;
        void ContinueSequence()
        {
            if (guardedAction?.IsCurrent != true
                || Connection == null
                || _state?.IsAsobo737Max8 != true)
            {
                _asobo737MaxFireTestsInProgress = false;
                if (timer != null)
                {
                    _automation.Complete(timer);
                }
                AppendDashboardLog("737 MAX fire tests stopped: aircraft automation is no longer current.");
                FinishOneShot(4);
                return;
            }

            if (!stepStarted)
            {
                if (steps.Count == 0)
                {
                    _apuFireTestCompleted = true;
                    _engine1FireTestCompleted = true;
                    _engine2FireTestCompleted = true;
                    if (_state != null)
                    {
                        _state.ApuFireTestCompleted = true;
                        _state.Engine1FireTestCompleted = true;
                        _state.Engine2FireTestCompleted = true;
                    }

                    _asobo737MaxFireTestsInProgress = false;
                    if (timer != null)
                    {
                        _automation.Complete(timer);
                    }
                    AppendDashboardLog("737 MAX fire tests completed.");
                    FinishOneShot();
                    return;
                }

                currentStep = steps.Dequeue();
                stepDeadlineUtc = DateTime.UtcNow.AddMilliseconds(currentStep.HoldMs);
                stepStarted = true;
                stepSent = false;
                AppLog.Write(
                    currentStep.CalculatorCode != null
                        ? $"Asobo 737 MAX fire test step started: {currentStep.Label}; calculator {currentStep.CalculatorCode}; repeat={currentStep.Repeat}."
                        : $"Asobo 737 MAX fire test step started: {currentStep.Label}; InputEvent {currentStep.Hash} -> {currentStep.Position:0.#}; repeat={currentStep.Repeat}.");
            }

            try
            {
                if (currentStep.Repeat || !stepSent)
                {
                    if (!string.IsNullOrWhiteSpace(currentStep.CalculatorCode))
                    {
                        SendMobiFlightCommand($"MF.SimVars.Set.{currentStep.CalculatorCode}");
                        SendMobiFlightCommand("MF.DummyCmd");
                    }
                    else if (currentStep.Hash.HasValue)
                    {
                        Connection.SetInputEvent(currentStep.Hash.Value, currentStep.Position);
                    }

                    stepSent = true;
                }
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException)
            {
                _asobo737MaxFireTestsInProgress = false;
                if (timer != null)
                {
                    _automation.Complete(timer);
                }
                AppendDashboardLog($"737 MAX fire tests failed: {ex.Message}");
                FinishOneShot(4);
                return;
            }

            if (DateTime.UtcNow >= stepDeadlineUtc)
            {
                stepStarted = false;
            }

            if (timer != null)
            {
                timer.Interval = stepStarted
                    ? currentStep.Repeat ? 150 : Math.Max(1, (int)Math.Min(150, (stepDeadlineUtc - DateTime.UtcNow).TotalMilliseconds))
                    : 1;
                timer.Start();
            }
        }

        timer = new System.Windows.Forms.Timer { Interval = 1 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            ContinueSequence();
        };
        guardedAction = _automation.Track(timer);
        ContinueSequence();
    }

    private void SetAsobo737MaxIrsSelector(bool left, int position)
    {
        if (Connection == null || _state?.IsAsobo737Max8 != true)
        {
            AppendDashboardLog("737 MAX IRS command blocked: Asobo 737 MAX profile is not active.");
            FinishOneShot(3);
            return;
        }

        var selectorName = left ? "left" : "right";
        double ReadState(AircraftState state) =>
            left ? state.Adirs1SelectorState : state.Adirs2SelectorState;

        bool Verify(AircraftState state)
        {
            var reconciledState = _asobo737MaxRuntime.ResolveIrsState(left, DateTime.UtcNow);
            if (reconciledState.HasValue)
            {
                if (left)
                {
                    state.Adirs1SelectorState = reconciledState.Value;
                }
                else
                {
                    state.Adirs2SelectorState = reconciledState.Value;
                }

                return reconciledState.Value >= position;
            }

            return ReadState(state) >= position;
        }

        if (Verify(_state))
        {
            AppendDashboardLog($"737 MAX {selectorName} IRS selector already NAV.");
            FinishOneShot();
            return;
        }

        try
        {
            var navEvent = left
                ? "AFT_OVHD_L_IRS_NAV"
                : "AFT_OVHD_R_IRS_NAV";
            SendMobiFlightCommand($"MF.SimVars.Set.(>B:{navEvent})");
            SendMobiFlightCommand("MF.DummyCmd");
            if (left)
            {
                _asobo737MaxRuntime.RecordLeftIrsCommand(position, DateTime.UtcNow);
            }
            else
            {
                _asobo737MaxRuntime.RecordRightIrsCommand(position, DateTime.UtcNow);
            }

            var readbackHash = left ? _asobo737MaxRuntime.LeftIrsInputEventHash : _asobo737MaxRuntime.RightIrsInputEventHash;
            if (readbackHash.HasValue)
            {
                Connection.GetInputEvent(
                    left ? Request.Asobo737MaxLeftIrsInputEvent : Request.Asobo737MaxRightIrsInputEvent,
                    readbackHash.Value);
            }

            AppLog.Write(
                $"Asobo 737 MAX {selectorName} IRS NAV command sent: {navEvent}.");
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            AppendDashboardLog($"737 MAX {selectorName} IRS command failed: {ex.Message}");
            FinishOneShot(4);
            return;
        }

        BeginNativeAction(
            $"737 MAX {selectorName} IRS selector",
            Verify,
            true,
            TimeSpan.FromSeconds(10));
    }

    private void SetAsobo737MaxPositionLightSteady()
    {
        SetAsobo737MaxLightingInputEvent(
            "position light",
            _asobo737MaxRuntime.PositionLightInputEventHash,
            () => _asobo737MaxRuntime.PositionLightInputState,
            0,
            state => state.NavigationLightsOn,
            state => state.NavigationLightsOn = true);
    }

    private void SetAsobo737MaxLogoLight(bool on)
    {
        SetAsobo737MaxLightingInputEvent(
            "logo light",
            _asobo737MaxRuntime.LogoLightInputEventHash,
            () => _asobo737MaxRuntime.LogoLightInputState,
            on ? 0 : 1,
            state => on ? state.LogoLightsOn : !state.LogoLightsOn,
            state => state.LogoLightsOn = on);
    }

    private void SetAsobo737MaxEmergencyExitLightsArmed()
    {
        if (Connection == null || _state?.IsAsobo737Max8 != true)
        {
            AppendDashboardLog("737 MAX emergency-exit lights command blocked: Asobo 737 MAX profile is not active.");
            FinishOneShot(3);
            return;
        }

        bool Verify(AircraftState state)
        {
            if (_asobo737MaxRuntime.EmergencyExitInputState.HasValue
                && _asobo737MaxRuntime.EmergencyExitCoverInputState.HasValue)
            {
                var armed =
                    Math.Abs(_asobo737MaxRuntime.EmergencyExitInputState.Value - 1) < 0.1
                    && Math.Abs(_asobo737MaxRuntime.EmergencyExitCoverInputState.Value) < 0.1;
                if (armed)
                {
                    state.EmergencyExitSelectorPosition = 1;
                }

                return armed;
            }

            return state.EmergencyExitSelectorPosition.HasValue
                   && Math.Abs(state.EmergencyExitSelectorPosition.Value - 1) < 0.1;
        }

        if (Verify(_state))
        {
            AppendDashboardLog("737 MAX emergency-exit lights already ARMED.");
            FinishOneShot();
            return;
        }

        if (!_asobo737MaxRuntime.EmergencyExitCoverInputEventHash.HasValue)
        {
            AppendDashboardLog("737 MAX emergency-exit lights blocked: cover InputEvent readback is not bound yet.");
            FinishOneShot(3);
            return;
        }

        try
        {
            Connection.SetInputEvent(_asobo737MaxRuntime.EmergencyExitCoverInputEventHash.Value, 0d);
            if (_asobo737MaxRuntime.EmergencyExitInputEventHash.HasValue)
            {
                Connection.SetInputEvent(_asobo737MaxRuntime.EmergencyExitInputEventHash.Value, 1d);
            }

            AppLog.Write("Asobo 737 MAX emergency-exit lights command sent: cover closed, switch armed.");
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            AppendDashboardLog($"737 MAX emergency-exit lights command failed: {ex.Message}");
            FinishOneShot(4);
            return;
        }

        BeginNativeAction(
            "737 MAX emergency-exit lights",
            Verify,
            true,
            TimeSpan.FromSeconds(5),
            "ARMED");
    }

    private void SetAsobo737MaxPassengerSign(
        string name,
        ulong? inputEventHash,
        Func<double?> readDirectInputEventState)
    {
        if (Connection == null || _state?.IsAsobo737Max8 != true)
        {
            AppendDashboardLog($"737 MAX {name} command blocked: Asobo 737 MAX profile is not active.");
            FinishOneShot(3);
            return;
        }

        const double desiredPosition = 1d;
        bool Verify(AircraftState state)
        {
            var direct = readDirectInputEventState();
            return direct.HasValue && Math.Abs(direct.Value - desiredPosition) < 0.1;
        }

        if (Verify(_state))
        {
            AppendDashboardLog($"737 MAX {name} already set.");
            FinishOneShot();
            return;
        }

        if (!inputEventHash.HasValue)
        {
            AppendDashboardLog($"737 MAX {name} blocked: InputEvent readback is not bound yet.");
            FinishOneShot(3);
            return;
        }

        try
        {
            Connection.SetInputEvent(inputEventHash.Value, desiredPosition);
            AppLog.Write($"Asobo 737 MAX {name} InputEvent command sent: {desiredPosition:0.###}.");
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            AppendDashboardLog($"737 MAX {name} command failed: {ex.Message}");
            FinishOneShot(4);
            return;
        }

        BeginNativeAction(
            $"737 MAX {name}",
            Verify,
            true,
            TimeSpan.FromSeconds(5),
            "SET");
    }

    private void SetAsobo737MaxApuSelector(double desiredPosition)
    {
        if (Connection == null || _state?.IsAsobo737Max8 != true)
        {
            AppendDashboardLog("737 MAX APU selector command blocked: Asobo 737 MAX profile is not active.");
            FinishOneShot(3);
            return;
        }

        bool Verify(AircraftState state)
        {
            if (_asobo737MaxRuntime.ApuInputState.HasValue)
            {
                return Math.Abs(_asobo737MaxRuntime.ApuInputState.Value - desiredPosition) < 0.1
                       || (Math.Abs(desiredPosition) < 0.1 && (state.ApuStarterPercent > 0 || state.ApuSpoolingOrAvailable));
            }

            if (Math.Abs(desiredPosition - Asobo737MaxControlProfile.ApuOn) < 0.1)
            {
                return state.ApuMasterSwitchOn;
            }

            if (Math.Abs(desiredPosition - Asobo737MaxControlProfile.ApuOff) < 0.1)
            {
                return !state.ApuMasterSwitchOn;
            }

            return state.ApuStartButtonOn || state.ApuSpoolingOrAvailable;
        }

        if (Verify(_state))
        {
            AppendDashboardLog(
                $"737 MAX APU selector already {Asobo737MaxApuSelectorLabel(desiredPosition)}.");
            FinishOneShot();
            return;
        }

        if (!_asobo737MaxRuntime.ApuInputEventHash.HasValue)
        {
            AppendDashboardLog("737 MAX APU selector blocked: InputEvent readback is not bound yet.");
            FinishOneShot(3);
            return;
        }

        try
        {
            Connection.SetInputEvent(_asobo737MaxRuntime.ApuInputEventHash.Value, desiredPosition);
            AppLog.Write($"Asobo 737 MAX APU selector InputEvent command sent: {desiredPosition:0.###}.");
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            AppendDashboardLog($"737 MAX APU selector command failed: {ex.Message}");
            FinishOneShot(4);
            return;
        }

        BeginNativeAction(
            "737 MAX APU selector",
            Verify,
            true,
            TimeSpan.FromSeconds(8),
            Asobo737MaxApuSelectorLabel(desiredPosition));
    }

    private static string Asobo737MaxApuSelectorLabel(double position) =>
        Math.Abs(position - Asobo737MaxControlProfile.ApuOff) < 0.1
            ? "OFF"
            : Math.Abs(position - Asobo737MaxControlProfile.ApuOn) < 0.1
                ? "ON"
                : "START";

    private void SetAsobo737MaxLightingInputEvent(
        string name,
        ulong? inputEventHash,
        Func<double?> readDirectInputEventState,
        double desiredInputEventValue,
        Func<AircraftState, bool> fallbackVerify,
        Action<AircraftState> applyVerifiedState)
    {
        if (Connection == null || _state?.IsAsobo737Max8 != true)
        {
            AppendDashboardLog($"737 MAX {name} command blocked: Asobo 737 MAX profile is not active.");
            FinishOneShot(3);
            return;
        }

        bool Verify(AircraftState state)
        {
            var directInputEventState = readDirectInputEventState();
            if (directInputEventState.HasValue)
            {
                if (Math.Abs(directInputEventState.Value - desiredInputEventValue) < 0.1)
                {
                    applyVerifiedState(state);
                    return true;
                }

                return false;
            }

            return fallbackVerify(state);
        }

        if (Verify(_state))
        {
            AppendDashboardLog($"737 MAX {name} already set.");
            FinishOneShot();
            return;
        }

        if (!inputEventHash.HasValue)
        {
            AppendDashboardLog($"737 MAX {name} command blocked: InputEvent readback is not bound yet.");
            FinishOneShot(3);
            return;
        }

        try
        {
            Connection.SetInputEvent(inputEventHash.Value, desiredInputEventValue);
            AppLog.Write(
                $"Asobo 737 MAX {name} InputEvent command sent: {desiredInputEventValue:0.###}.");
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            AppendDashboardLog($"737 MAX {name} command failed: {ex.Message}");
            FinishOneShot(4);
            return;
        }

        BeginNativeAction(
            $"737 MAX {name}",
            Verify,
            true,
            TimeSpan.FromSeconds(5));
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
            "after-start-taxi" => _settings.AutoChainFlow5To6,
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

        var step = _procedureRunner.CurrentStep;
        var stepIndex = _procedureRunner.CurrentStepIndex;
        _procedureRunner.ConfirmManualStep(_state);
        if (step != null
            && _procedureRunner.CurrentStepIndex > stepIndex
            && string.Equals(
                step.Id,
                "captain-pushback-clearance",
                StringComparison.OrdinalIgnoreCase)
            && !_gsx.Snapshot.DepartureRequestedThisFlight
            && _settings.GsxAutomaticallyPrepareDeparture)
        {
            _gsx.SetDepartureRequestedThisFlight(
                BeginGsxAction(GsxDepartureAction.PrepareForDeparture));
        }
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
        if (!_gsx.Snapshot.BoardingRequestedThisFlight
            && _settings.GsxAutomaticallyRequestBoarding
            && string.Equals(
                _procedureRunner.Definition?.Id,
                "flight-computer-preflight",
                StringComparison.OrdinalIgnoreCase)
            && _procedureRunner.Status is ProcedureStatus.Running
                or ProcedureStatus.WaitingForManualAction
                or ProcedureStatus.WaitingForVerification)
        {
            _gsx.SetBoardingRequestedThisFlight(
                BeginGsxAction(GsxDepartureAction.Boarding));
        }

        var completedDefinition = _procedureRunner.Status == ProcedureStatus.Completed
            ? _procedureRunner.Definition
            : null;
        var newlyCompleted = _procedureRunner.Status == ProcedureStatus.Completed
            && completedDefinition != null
            && _completedProcedureIds.Add(completedDefinition.Id);
        if (newlyCompleted)
        {
            SpeakProcedureCompletion(completedDefinition!);
        }
        SaveProcedureSession();
        PrintProcedureUpdate();
        PublishEfbState(force: true);

        var nextFlow = completedDefinition == null
            ? null
            : GetAutomaticNextFlow(completedDefinition.Id);
        if (nextFlow != null)
        {
            if (string.Equals(
                    nextFlow.Id,
                    "before-takeoff",
                    StringComparison.OrdinalIgnoreCase)
                && _state?.BeforeTakeoffHoldEligible != true)
            {
                _pendingAutomaticBeforeTakeoffFlow = true;
                AppendDashboardLog(
                    $"{completedDefinition!.Name} complete; {nextFlow.Name} will start after taxi when the aircraft stops at the runway holding point.");
            }
            else
            {
                AppendDashboardLog(
                    $"{completedDefinition!.Name} complete; {nextFlow.Name} will start automatically.");
                _automation.Enqueue($"procedure start {nextFlow.Id}");
            }
        }
    }

    private void OnProcedureStepCompleted(ProcedureStep step)
    {
        TryRequestGsxDeboardingAtGate();

        if (string.Equals(
                step.Id,
                "fo-taxi-clearance",
                StringComparison.OrdinalIgnoreCase))
        {
            // Only movement after taxi clearance is valid evidence that the
            // aircraft taxied toward the runway. Discard pushback and any gate
            // repositioning observed earlier in the flight.
            _taxiToRunwayArmed = true;
            _forwardTaxiObservedThisFlight = false;
        }

        if (_gsx.Snapshot.DepartureRequestedThisFlight
            || !_settings.GsxAutomaticallyPrepareDeparture
            || !string.Equals(
                step.Id,
                "captain-pushback-clearance",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _gsx.SetDepartureRequestedThisFlight(
            BeginGsxAction(GsxDepartureAction.PrepareForDeparture));
    }

    private void TryRequestGsxDeboardingAtGate()
    {
        if (_gsx.Snapshot.DeboardingRequestedThisFlight
            || !_settings.EnableGsxIntegration
            || !_settings.GsxAutomaticallyRequestDeboarding
            || _state == null
            || !string.Equals(
                _procedureRunner.Definition?.Id,
                "parking-shutdown",
                StringComparison.OrdinalIgnoreCase)
            || !_state.OnGround
            || _state.GroundSpeedKnots > 0.5
            || !_state.ParkingBrakeSet
            || !_state.EnginesOff
            || !(_state.ApuAvailable || _state.ExternalPowerOn))
        {
            return;
        }

        _gsx.SetDeboardingRequestedThisFlight(
            BeginGsxAction(GsxDepartureAction.Deboarding));
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

    private void SpeakCurrentProcedureStepAtCommand()
    {
        var step = _procedureRunner.CurrentStep;
        if (step == null
            || !_settings.EnableStandardCallouts
            || (_voiceCalloutQueue == null
                && !_settings.UseSayIntentionsVoiceCallouts))
        {
            return;
        }

        var phrase = ProcedureCalloutCatalog.ForStep(
            step.Id,
            _state,
            _settings.CalloutDetail);
        if (phrase == null || !_calloutsSpokenAtCommand.Add(step.Id))
        {
            return;
        }

        DispatchVoiceCallout(phrase, GetCalloutPriority(step.Id), step.Id);
        AppendDashboardLog($"Voice callout at command: {phrase}");
    }

    private void SpeakProcedureCallout(ProcedureStep step)
    {
        if (!_settings.EnableStandardCallouts
            || (_voiceCalloutQueue == null && !_settings.UseSayIntentionsVoiceCallouts))
        {
            return;
        }
        if (step.Id == "fo-reverse-callout"
            && _state?.ReverseThrustEngaged != true)
        {
            return;
        }
        if (_calloutsSpokenAtCommand.Remove(step.Id))
        {
            return;
        }

        var phrase = ProcedureCalloutCatalog.ForStep(
            step.Id,
            _state,
            _settings.CalloutDetail);
        if (phrase == null)
        {
            return;
        }

        try
        {
            DispatchVoiceCallout(phrase, GetCalloutPriority(step.Id), step.Id);
            AppendDashboardLog($"Voice callout: {phrase}");
        }
        catch (InvalidOperationException ex)
        {
            AppLog.Write($"Voice callout failed: {ex.Message}");
        }
    }

    private void SpeakProcedureCompletion(ProcedureDefinition definition)
    {
        if (!_settings.EnableStandardCallouts
            || (_voiceCalloutQueue == null && !_settings.UseSayIntentionsVoiceCallouts))
        {
            return;
        }

        var phrase = ProcedureCalloutCatalog.ForCompletedProcedure(
            definition.Id,
            _settings.CalloutDetail);
        if (phrase == null)
        {
            return;
        }

        DispatchVoiceCallout(phrase, 60);
        AppendDashboardLog($"Voice callout: {phrase}");
    }

    private void DispatchVoiceCallout(
        string phrase,
        int priority,
        string? stepId = null)
    {
        if (SayIntentionsVoicePolicy.RequiresLowLatencyLocalVoice(stepId)
            && _voiceCalloutQueue != null)
        {
            _voiceCalloutQueue.Enqueue(phrase, priority);
            return;
        }

        var sayIntentionsFlight = _sayIntentionsRuntime.Flight;
        if (_settings.UseSayIntentionsVoiceCallouts && sayIntentionsFlight != null)
        {
            var callout = new SayIntentionsQueuedCallout(
                phrase,
                priority,
                DateTime.UtcNow,
                SayIntentionsVoicePolicy.MaxQueueAge(stepId));
            if (SayIntentionsVoicePolicy.BypassesQueue(stepId))
            {
                _ = SendImmediateSayIntentionsCalloutAsync(
                    sayIntentionsFlight,
                    callout);
                return;
            }
            lock (_sayIntentionsVoiceQueueSync)
            {
                _sayIntentionsVoiceTail = PlaySayIntentionsCalloutAfterAsync(
                    _sayIntentionsVoiceTail,
                    sayIntentionsFlight,
                    callout);
            }
            return;
        }

        _voiceCalloutQueue?.Enqueue(phrase, priority);
    }

    private async Task SendImmediateSayIntentionsCalloutAsync(
        SayIntentionsFlightContext flight,
        SayIntentionsQueuedCallout callout)
    {
        try
        {
            if (await _sayIntentionsClient
                    .SayCopilotCalloutAsync(
                        flight,
                        callout.Phrase,
                        _sayIntentionsCancellation.Token)
                    .ConfigureAwait(false))
            {
                return;
            }
        }
        catch (OperationCanceledException) when (_sayIntentionsCancellation.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or OperationCanceledException
                                   or ArgumentOutOfRangeException
                                   or ObjectDisposedException)
        {
            AppLog.Write(
                "Immediate SayIntentions takeoff callout unavailable; using local voice fallback.");
        }

        _voiceCalloutQueue?.Enqueue(callout.Phrase, callout.Priority);
    }

    private async Task PlaySayIntentionsCalloutAfterAsync(
        Task previous,
        SayIntentionsFlightContext flight,
        SayIntentionsQueuedCallout callout)
    {
        try
        {
            await previous.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            AppLog.Write("A previous SayIntentions callout ended unexpectedly; continuing the voice queue.");
        }

        await SayWithSayIntentionsOrFallbackAsync(flight, callout)
            .ConfigureAwait(false);
    }

    private async Task SayWithSayIntentionsOrFallbackAsync(
        SayIntentionsFlightContext flight,
        SayIntentionsQueuedCallout callout)
    {
        try
        {
            if (!await WaitForSayIntentionsIntercomQuietAsync(callout)
                    .ConfigureAwait(false))
            {
                AppLog.Write(
                    $"Skipped stale SayIntentions callout while the intercom was occupied: {callout.Phrase}");
                return;
            }

            if (await _sayIntentionsClient
                    .SayCopilotCalloutAsync(
                        flight,
                        callout.Phrase,
                        _sayIntentionsCancellation.Token)
                    .ConfigureAwait(false))
            {
                await WaitForSayIntentionsPlaybackAsync()
                    .ConfigureAwait(false);
                return;
            }
        }
        catch (OperationCanceledException) when (_sayIntentionsCancellation.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or OperationCanceledException
                                   or ArgumentOutOfRangeException
                                   or ObjectDisposedException)
        {
            // Never log the exception or request URI: SAPI authenticates in the query string.
            AppLog.Write("SayIntentions voice unavailable; using local voice fallback.");
        }
        _voiceCalloutQueue?.Enqueue(callout.Phrase, callout.Priority);
    }

    private async Task<bool> WaitForSayIntentionsIntercomQuietAsync(
        SayIntentionsQueuedCallout callout)
    {
        DateTime? quietSinceUtc = null;
        while (DateTime.UtcNow - callout.CreatedUtc <= callout.MaxQueueAge)
        {
            _sayIntentionsCancellation.Token.ThrowIfCancellationRequested();
            if (Volatile.Read(ref _sayIntentionsIntercomReceivingMask) == 0)
            {
                quietSinceUtc ??= DateTime.UtcNow;
                if (DateTime.UtcNow - quietSinceUtc.Value >= TimeSpan.FromSeconds(1.5))
                {
                    return true;
                }
            }
            else
            {
                quietSinceUtc = null;
            }

            await Task.Delay(200, _sayIntentionsCancellation.Token)
                .ConfigureAwait(false);
        }

        return false;
    }

    private async Task WaitForSayIntentionsPlaybackAsync()
    {
        var signalWasObserved =
            Volatile.Read(ref _sayIntentionsIntercomSignalObserved) != 0;
        var playbackStartDeadlineUtc = DateTime.UtcNow +
            (signalWasObserved ? TimeSpan.FromSeconds(8) : TimeSpan.FromSeconds(2));
        while (DateTime.UtcNow < playbackStartDeadlineUtc
               && Volatile.Read(ref _sayIntentionsIntercomReceivingMask) == 0)
        {
            await Task.Delay(200, _sayIntentionsCancellation.Token)
                .ConfigureAwait(false);
        }

        if (Volatile.Read(ref _sayIntentionsIntercomReceivingMask) == 0)
        {
            // The API accepted the callout but no playback signal was observed.
            // Keep a small separation so successive requests are never rapid-fired.
            await Task.Delay(250, _sayIntentionsCancellation.Token)
                .ConfigureAwait(false);
            return;
        }

        var playbackEndDeadlineUtc = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        DateTime? quietSinceUtc = null;
        while (DateTime.UtcNow < playbackEndDeadlineUtc)
        {
            _sayIntentionsCancellation.Token.ThrowIfCancellationRequested();
            if (Volatile.Read(ref _sayIntentionsIntercomReceivingMask) == 0)
            {
                quietSinceUtc ??= DateTime.UtcNow;
                if (DateTime.UtcNow - quietSinceUtc.Value >= TimeSpan.FromSeconds(1.5))
                {
                    return;
                }
            }
            else
            {
                quietSinceUtc = null;
            }

            await Task.Delay(200, _sayIntentionsCancellation.Token)
                .ConfigureAwait(false);
        }

        AppLog.Write("SayIntentions intercom remained active; releasing the callout queue after timeout.");
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
        if (Connection == null || _state == null)
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

        Connection.TransmitClientEvent(
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

        if (!_state.IsIniBuildsAirbusFamily || !_mobiFlightSession.AdapterReady)
        {
            Console.Error.WriteLine("NAV & LOGO procedure blocked: iniBuilds adapter is unavailable.");
            FinishOneShot(4);
            return;
        }

        if (_state.IsIniBuildsA310 && _nativeRuntime.A310.Flow5States[2].HasValue)
        {
            _state.GroundSpoilersArmed = _nativeRuntime.A310.Flow5States[2]!.Value > 0.5f;
        }
        else if (_state.IsIniBuildsA330)
        {
            if (_nativeRuntime.A330.NavLogoInputState.HasValue
                && Math.Abs(_nativeRuntime.A330.NavLogoInputState.Value - nativePosition) < 0.1)
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
            Connection!.SetInputEvent(A330NavLogoInputEventHash, (double)nativePosition);
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

        if (!_mobiFlightSession.RuntimeReady)
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
        if (Connection == null || _state == null)
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

        if (!_mobiFlightSession.AdapterReady)
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
        _nativeRuntime.RecordFbwBatteryCommand(batteryNumber, desiredOn);
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
            if (Connection == null)
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

            Connection.SetInputEvent(
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
        if (_state == null || !_mobiFlightSession.RuntimeReady)
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
        if (_state?.IsIniBuildsA310 == true)
        {
            SendA310ControlValue(
                A310ControlProfile.ApuBleedState,
                desiredOn ? 1 : 0,
                $"APU bleed {(desiredOn ? "ON" : "OFF")}");
            FinishOneShot();
            return;
        }

        if (_state?.IsAsobo737Max8 == true)
        {
            SetAsobo737MaxApuBleed(desiredOn);
            return;
        }

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
            if (Connection == null)
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

            Connection.SetInputEvent(
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

    private void SetAsobo737MaxApuBleed(bool desiredOn)
    {
        if (Connection == null || _state?.IsAsobo737Max8 != true)
        {
            AppendDashboardLog("737 MAX APU bleed blocked: Asobo 737 MAX profile is not active.");
            FinishOneShot(4);
            return;
        }

        if (_asobo737MaxRuntime.ApuBleedInputEventHash.HasValue)
        {
            if (_asobo737MaxRuntime.ApuBleedInputState.HasValue
                && Asobo737MaxBinarySwitchIsOn(_asobo737MaxRuntime.ApuBleedInputState.Value) == desiredOn)
            {
                AppendDashboardLog($"737 MAX APU bleed already {desiredOn.ToOnOff()}.");
                FinishOneShot();
                return;
            }

            Connection.SetInputEvent(
                _asobo737MaxRuntime.ApuBleedInputEventHash.Value,
                desiredOn ? 0.0 : 1.0);
            AppLog.Write(
                $"Asobo 737 MAX APU bleed absolute command sent: {(desiredOn ? 0 : 1)} ({desiredOn.ToOnOff()}).");
            BeginNativeAction(
                "737 MAX APU bleed",
                state => state.ApuBleedOn == desiredOn,
                desiredOn,
                TimeSpan.FromSeconds(10));
            return;
        }

        AppendDashboardLog("737 MAX APU bleed blocked: PNEUMATICS_APU_BLEED readback is not ready yet.");
        FinishOneShot(4);
    }

    private void SetAsobo737MaxSingleInputEvent(
        string name,
        ulong inputEventHash,
        double desiredValue,
        Func<AircraftState, bool> verify,
        TimeSpan? timeout = null,
        bool forceCommand = false)
    {
        if (Connection == null || _state?.IsAsobo737Max8 != true)
        {
            AppendDashboardLog($"737 MAX {name} blocked: Asobo 737 MAX profile is not active.");
            FinishOneShot(4);
            return;
        }

        if (!forceCommand && verify(_state))
        {
            AppendDashboardLog($"737 MAX {name} already set.");
            FinishOneShot();
            return;
        }

        try
        {
            Connection.SetInputEvent(inputEventHash, desiredValue);
            AppLog.Write($"Asobo 737 MAX {name} InputEvent command sent: {desiredValue:0.###}.");
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            AppendDashboardLog($"737 MAX {name} command failed: {ex.Message}");
            FinishOneShot(4);
            return;
        }

        BeginNativeAction(
            $"737 MAX {name}",
            verify,
            true,
            timeout ?? TimeSpan.FromSeconds(10));
    }

    private void ForceSetAsobo737MaxSingleInputEventCommand(
        string name,
        ulong inputEventHash,
        double commandValue,
        Func<AircraftState, bool> verify,
        TimeSpan? timeout = null)
    {
        if (Connection == null || _state?.IsAsobo737Max8 != true)
        {
            AppendDashboardLog($"737 MAX {name} blocked: Asobo 737 MAX profile is not active.");
            FinishOneShot(4);
            return;
        }

        try
        {
            Connection.SetInputEvent(inputEventHash, commandValue);
            AppLog.Write(
                $"Asobo 737 MAX {name} forced single InputEvent command sent: {commandValue:0.###}.");
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            AppendDashboardLog($"737 MAX {name} command failed: {ex.Message}");
            FinishOneShot(4);
            return;
        }

        BeginNativeAction(
            $"737 MAX {name}",
            verify,
            true,
            timeout ?? TimeSpan.FromSeconds(10));
    }

    private void ScheduleInputEvent(ulong inputEventHash, double value, int delayMs)
    {
        _automation.Schedule(
            delayMs,
            () =>
            {
                try
                {
                    Connection!.SetInputEvent(inputEventHash, value);
                }
                catch (Exception ex) when (ex is COMException or InvalidOperationException)
                {
                    AppLog.Write($"Scheduled InputEvent {inputEventHash}={value:0.###} failed: {ex.Message}");
                }
            },
            $"InputEvent {inputEventHash}={value:0.###}",
            _state?.Variant);
    }

    private void ScheduleSystemEvent(
        CopilotEvent eventId,
        uint data0,
        uint data1,
        int delayMs,
        string label)
    {
        _automation.Schedule(
            delayMs,
            () =>
            {
                try
                {
                    TransmitSystemEvent(eventId, data0, data1);
                    AppLog.Write($"{label} system event sent.");
                }
                catch (Exception ex) when (ex is COMException or InvalidOperationException)
                {
                    AppLog.Write($"Scheduled {label} system event failed: {ex.Message}");
                }
            },
            $"{label} system event",
            _state?.Variant);
    }

    private void SetAsobo737MaxDualInputEvent(
        string name,
        IReadOnlyList<ulong> inputEventHashes,
        double desiredValue,
        Func<AircraftState, bool> verify,
        TimeSpan? timeout = null,
        bool forceCommand = false)
    {
        if (Connection == null || _state?.IsAsobo737Max8 != true)
        {
            AppendDashboardLog($"737 MAX {name} blocked: Asobo 737 MAX profile is not active.");
            FinishOneShot(4);
            return;
        }

        if (!forceCommand && verify(_state))
        {
            AppendDashboardLog($"737 MAX {name} already set.");
            FinishOneShot();
            return;
        }

        try
        {
            foreach (var inputEventHash in inputEventHashes)
            {
                Connection.SetInputEvent(inputEventHash, desiredValue);
            }

            AppLog.Write($"Asobo 737 MAX {name} InputEvent command sent: {desiredValue:0.###} on {inputEventHashes.Count} switch(es).");
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            AppendDashboardLog($"737 MAX {name} command failed: {ex.Message}");
            FinishOneShot(4);
            return;
        }

        BeginNativeAction(
            $"737 MAX {name}",
            verify,
            true,
            timeout ?? TimeSpan.FromSeconds(10));
    }

    private void SetAsobo737MaxIsolationValve(bool open)
    {
        var desiredValue = open
            ? Asobo737MaxControlProfile.IsolationValveOpen
            : Asobo737MaxControlProfile.IsolationValveAuto;
        SetAsobo737MaxSingleInputEvent(
            open ? "isolation valve OPEN" : "isolation valve AUTO",
            Asobo737MaxIsolationValveInputEventHash,
            desiredValue,
            state => open ? state.IsolationValveOpen : state.IsolationValveAuto);
    }

    private void ForceSetAsobo737MaxIsolationValve(bool open)
    {
        var desiredValue = open
            ? Asobo737MaxControlProfile.IsolationValveOpen
            : Asobo737MaxControlProfile.IsolationValveAuto;
        ForceSetAsobo737MaxSingleInputEventCommand(
            open ? "isolation valve OPEN" : "isolation valve AUTO",
            Asobo737MaxIsolationValveInputEventHash,
            desiredValue,
            state => open ? state.IsolationValveOpen : state.IsolationValveAuto);
    }

    private void SetAsobo737MaxPacks(bool auto)
    {
        var desiredValue = auto
            ? Asobo737MaxControlProfile.PackAuto
            : Asobo737MaxControlProfile.PackOff;
        SetAsobo737MaxDualInputEvent(
            auto ? "packs AUTO" : "packs OFF",
            new[] { Asobo737MaxLeftPackInputEventHash, Asobo737MaxRightPackInputEventHash },
            desiredValue,
            state => auto ? state.PacksAuto : state.PacksOffForEngineStart);
    }

    private void ForceSetAsobo737MaxPacks(bool auto)
    {
        var desiredValue = auto
            ? Asobo737MaxControlProfile.PackAuto
            : Asobo737MaxControlProfile.PackOff;
        ForceSetAsobo737MaxSingleInputEventCommand(
            auto ? "left pack AUTO" : "left pack OFF",
            Asobo737MaxLeftPackInputEventHash,
            desiredValue,
            state => auto ? state.PacksAuto : state.PacksOffForEngineStart);
        ScheduleInputEvent(Asobo737MaxRightPackInputEventHash, desiredValue, 350);
        AppLog.Write(
            $"Asobo 737 MAX right pack forced single InputEvent command scheduled: {desiredValue:0.###}.");
    }

    private void SetAsobo737MaxEngineBleeds(bool on)
    {
        SetAsobo737MaxDualInputEvent(
            on ? "engine bleed switches ON" : "engine bleed switches OFF",
            Asobo737MaxEngineBleedInputEventHashes,
            on
                ? Asobo737MaxControlProfile.EngineBleedOn
                : Asobo737MaxControlProfile.EngineBleedOff,
            state => state.BoeingEngineBleedsOn == on);
    }

    private void SetAsobo737MaxEngineGenerators(bool on)
    {
        SetAsobo737MaxDualInputEvent(
            on ? "engine generators ON" : "engine generators OFF",
            Asobo737MaxEngineGeneratorInputEventHashes,
            on ? 1.0 : 0.0,
            state => state.EngineGeneratorsOn == on,
            TimeSpan.FromSeconds(12));
    }

    private void ForceSetAsobo737MaxApuGenerator(bool desiredOn)
    {
        if (_state?.IsAsobo737Max8 != true || Connection == null)
        {
            AppendDashboardLog("Blocked Asobo 737 MAX APU generator command: a different aircraft profile is active.");
            FinishOneShot(3);
            return;
        }

        if (!Asobo737MaxApuGeneratorsReady())
        {
            AppendDashboardLog("737 MAX APU generators blocked: generator InputEvent readbacks are not ready yet.");
            FinishOneShot(4);
            return;
        }

        foreach (var hash in _asobo737MaxRuntime.ApuGeneratorInputEventHashes)
        {
            if (hash.HasValue)
            {
                Connection.SetInputEvent(hash.Value, desiredOn ? 1.0 : 0.0);
            }
        }

        AppLog.Write(
            $"Asobo 737 MAX APU generator forced single command: {(desiredOn ? "ON" : "OFF")}.");
        BeginNativeAction(
            "737 MAX APU generators",
            state => desiredOn
                ? state.ApuGeneratorPowerEstablished
                : !state.ApuGeneratorPowerEstablished,
            desiredOn,
            TimeSpan.FromSeconds(12));
    }

    private void SetAsobo737MaxElectricHydraulicPumps(bool on)
    {
        SetAsobo737MaxDualInputEvent(
            on ? "electric hydraulic pumps ON" : "electric hydraulic pumps OFF",
            Asobo737MaxElectricHydraulicPumpInputEventHashes,
            on
                ? Asobo737MaxControlProfile.ElectricHydraulicPumpOn
                : Asobo737MaxControlProfile.ElectricHydraulicPumpOff,
            state => state.BoeingElectricHydraulicPumpsOn == on);
    }

    private void SetAsobo737MaxTaxiLight(bool on)
    {
        SetAsobo737MaxSingleInputEvent(
            on ? "taxi light AUTO" : "taxi light OFF",
            Asobo737MaxTaxiLightInputEventHash,
            on
                ? Asobo737MaxControlProfile.TaxiLightAuto
                : Asobo737MaxControlProfile.TaxiLightOff,
            state => state.NoseLightSelectorPosition.HasValue
                     && Math.Abs(state.NoseLightSelectorPosition.Value - (on ? 1 : 2)) < 0.1);
    }

    private void SetAsobo737MaxAutothrottleArmed()
    {
        SetAsobo737MaxSingleInputEvent(
            "autothrottle ARM",
            Asobo737MaxAutothrottleInputEventHash,
            Asobo737MaxControlProfile.AutothrottleArmed,
            state => state.BoeingAutothrottleArmed);
    }

    private void PulseAsobo737MaxMcpButton(string name, ulong inputEventHash)
    {
        if (Connection == null || _state?.IsAsobo737Max8 != true)
        {
            AppendDashboardLog($"737 MAX {name} blocked: Asobo 737 MAX profile is not active.");
            FinishOneShot(4);
            return;
        }

        try
        {
            Connection.SetInputEvent(inputEventHash, 1.0);
            Connection.SetInputEvent(inputEventHash, 0.0);
            AppLog.Write($"Asobo 737 MAX {name} MCP button pulse sent.");
            FinishOneShot();
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            AppendDashboardLog($"737 MAX {name} command failed: {ex.Message}");
            FinishOneShot(4);
        }
    }

    private void SetAsobo737MaxTransponderTaRa()
    {
        SetAsobo737MaxSingleInputEvent(
            "transponder TA/RA",
            Asobo737MaxTransponderOperatingModeInputEventHash,
            Asobo737MaxControlProfile.TransponderTaRa,
            state => state.BoeingTransponderOperatingMode.HasValue
                     && Asobo737MaxControlProfile.IsTransponderTaRa(
                         state.BoeingTransponderOperatingMode.Value));
    }

    private void SetAsobo737MaxTransponderAuto()
    {
        SetAsobo737MaxSingleInputEvent(
            "transponder AUTO",
            Asobo737MaxTransponderModeInputEventHash,
            Asobo737MaxControlProfile.TransponderAuto,
            state => state.TransponderModeSelectorPosition.HasValue
                     && Asobo737MaxControlProfile.IsTransponderAuto(
                         state.TransponderModeSelectorPosition.Value));
    }

    private void SetAsobo737MaxRunwayTurnoffLights(bool on)
    {
        SetAsobo737MaxDualInputEvent(
            on ? "runway turnoff lights ON" : "runway turnoff lights OFF",
            Asobo737MaxRunwayTurnoffInputEventHashes,
            on
                ? Asobo737MaxControlProfile.RunwayTurnoffLightOn
                : Asobo737MaxControlProfile.RunwayTurnoffLightOff,
            state => state.RunwayTurnoffLightsOn == on);
    }

    private void SetAsobo737MaxLandingLights(bool on)
    {
        SetAsobo737MaxDualInputEvent(
            on ? "landing lights ON" : "landing lights OFF",
            Asobo737MaxLandingLightInputEventHashes,
            on
                ? Asobo737MaxControlProfile.LandingLightOn
                : Asobo737MaxControlProfile.LandingLightOff,
            state => state.LeftLandingLightSelectorPosition.HasValue
                     && state.RightLandingLightSelectorPosition.HasValue
                     && Math.Abs(state.LeftLandingLightSelectorPosition.Value - (on ? 0 : 1)) < 0.1
                     && Math.Abs(state.RightLandingLightSelectorPosition.Value - (on ? 0 : 1)) < 0.1);
    }

    private void SetAsobo737MaxAntiCollision(bool on)
    {
        ForceSetAsobo737MaxSingleInputEventCommand(
            on ? "anti-collision light ON" : "anti-collision light OFF",
            Asobo737MaxAntiCollisionInputEventHash,
            on
                ? Asobo737MaxControlProfile.AntiCollisionOn
                : Asobo737MaxControlProfile.AntiCollisionOff,
            state => state.BeaconOn == on);
    }

    private void SetAsobo737MaxGroundPowerOff()
    {
        if (Connection == null || _state?.IsAsobo737Max8 != true)
        {
            AppendDashboardLog("737 MAX ground power OFF blocked: Asobo 737 MAX profile is not active.");
            FinishOneShot(4);
            return;
        }

        if (!_state.ApuAvailable || !_state.ApuGeneratorSwitchOn)
        {
            AppendDashboardLog("737 MAX ground power OFF blocked: APU available and APU generators ON are required.");
            FinishOneShot(4);
            return;
        }

        try
        {
            Connection.SetInputEvent(
                Asobo737MaxExternalPowerInputEventHash,
                Asobo737MaxControlProfile.ExternalPowerOff);
            ScheduleInputEvent(
                Asobo737MaxExternalPowerInputEventHash,
                Asobo737MaxControlProfile.ExternalPowerNeutral,
                350);
            AppLog.Write(
                "Asobo 737 MAX native ground power OFF command sent; neutral release scheduled.");
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            AppendDashboardLog($"737 MAX ground power OFF command failed: {ex.Message}");
            FinishOneShot(4);
            return;
        }

        BeginNativeAction(
            "737 MAX ground power",
            state => !state.ExternalPowerOn,
            false,
            TimeSpan.FromSeconds(8),
            "OFF");
    }

    private void SetAsobo737MaxPositionStrobe(double position)
    {
        if (!_asobo737MaxRuntime.PositionLightInputEventHash.HasValue)
        {
            AppendDashboardLog("737 MAX position/strobe selector blocked: InputEvent readback is not bound yet.");
            FinishOneShot(4);
            return;
        }

        SetAsobo737MaxSingleInputEvent(
            $"position/strobe selector {position:0}",
            _asobo737MaxRuntime.PositionLightInputEventHash.Value,
            position,
            state => state.StrobeSelectorPosition.HasValue
                     && Math.Abs(state.StrobeSelectorPosition.Value - position) < 0.1);
    }

    private void SetAsobo737MaxTakeoffFlaps()
    {
        var takeoffFlaps = _state?.BoeingTakeoffFlaps ?? 5;
        if (takeoffFlaps <= 0)
        {
            takeoffFlaps = 5;
        }

        SetAsobo737MaxFlaps(takeoffFlaps);
    }

    private void SetAsobo737MaxLandingFlaps()
    {
        var landingFlaps = _state?.BoeingLandingFlaps ?? 30;
        if (landingFlaps <= 0)
        {
            landingFlaps = 30;
        }

        SetAsobo737MaxFlaps(landingFlaps);
    }

    private void SetAsobo737MaxFlaps(int flaps)
    {
        var detentIndex = flaps switch
        {
            <= 0 => 0,
            1 => 1,
            2 => 2,
            5 => 3,
            10 => 4,
            15 => 5,
            25 => 6,
            30 => 7,
            40 => 8,
            _ => 0
        };
        SetAsobo737MaxSingleInputEvent(
            flaps <= 0 ? "flaps CLEAN" : $"flaps {flaps}",
            Asobo737MaxFlapsInputEventHash,
            detentIndex / 8.0,
            state => state.BoeingFlapsAtSetting(flaps),
            TimeSpan.FromSeconds(15));
    }

    private void SetAsobo737MaxAutobrake(int desiredLevel, string label)
    {
        SetAsobo737MaxSingleInputEvent(
            $"autobrake {label}",
            Asobo737MaxAutobrakeInputEventHash,
            desiredLevel,
            state => state.AutobrakeLevel.HasValue
                     && Math.Abs(state.AutobrakeLevel.Value - desiredLevel) < 0.1);
    }

    private void SetAsobo737MaxGear(bool up)
    {
        if (_state == null || !_state.IsAsobo737Max8)
        {
            AppendDashboardLog("737 MAX landing gear blocked: Asobo 737 MAX profile is not active.");
            FinishOneShot(4);
            return;
        }

        if (up && (_state.OnGround || _state.VerticalSpeedFeetPerMinute <= 100))
        {
            AppendDashboardLog("737 MAX landing gear UP blocked: positive airborne climb is required.");
            FinishOneShot(3);
            return;
        }

        if (up ? _state.GearHandleUp : _state.GearHandleDown)
        {
            AppendDashboardLog($"737 MAX landing gear already {(up ? "UP" : "DOWN")}.");
            FinishOneShot();
            return;
        }

        SendMobiFlightCommand(up
            ? "MF.SimVars.Set.(>B:LANDING_GEARS_UP)"
            : "MF.SimVars.Set.(>B:LANDING_GEARS_DOWN)");
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            "737 MAX landing gear",
            state => up ? state.GearHandleUp : state.GearHandleDown,
            true,
            TimeSpan.FromSeconds(up ? 12 : 15),
            up ? "UP" : "DOWN");
    }

    private void SetApuGenerator(bool desiredOn)
        => PulseInputEvent(
            "APU generator",
            3205083420795941787UL,
            desiredOn,
            state => state.ApuGeneratorSwitchOn == desiredOn);

    private void SetAsobo737MaxApuGenerator(bool desiredOn)
    {
        if (_state?.IsAsobo737Max8 != true)
        {
            AppendDashboardLog("Blocked Asobo 737 MAX APU generator command: a different aircraft profile is active.");
            FinishOneShot(3);
            return;
        }

        if (Asobo737MaxApuGeneratorsReady())
        {
            if (desiredOn == Asobo737MaxApuGeneratorsOn()
                && (!desiredOn || _state.ApuGeneratorPowerEstablished))
            {
                AppendDashboardLog($"737 MAX APU generators already {desiredOn.ToOnOff()}.");
                FinishOneShot();
                return;
            }

            foreach (var hash in _asobo737MaxRuntime.ApuGeneratorInputEventHashes)
            {
                if (hash.HasValue)
                {
                    Connection!.SetInputEvent(hash.Value, desiredOn ? 1.0 : 0.0);
                }
            }

            BeginNativeAction(
                "737 MAX APU generators",
                state => desiredOn
                    ? state.ApuGeneratorPowerEstablished
                    : !state.ApuGeneratorPowerEstablished,
                desiredOn,
                TimeSpan.FromSeconds(12));
            return;
        }

        PulseInputEvent(
            "737 MAX APU generator",
            3205083420795941787UL,
            desiredOn,
            state => desiredOn
                ? state.ApuGeneratorPowerEstablished
                : !state.ApuGeneratorPowerEstablished,
            TimeSpan.FromSeconds(12));
    }

    private void SetFuelPumps(bool desiredOn)
    {
        if (_state?.IsAsobo737Max8 == true)
        {
            SetAsobo737MaxFuelPumps(desiredOn);
            return;
        }
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

        _pendingFuelPumpSequence = new PendingFuelPumpSequence(
            toggles, desiredOn, _automation.CurrentGeneration, _state!.Variant);
        _fuelPumpSequenceTimer?.Dispose();
        _fuelPumpSequenceTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _fuelPumpSequenceTimer.Tick += (_, _) => ExecuteNextFuelPumpToggle();
        ExecuteNextFuelPumpToggle();
    }

    private void SetAsobo737MaxFuelPumps(bool desiredOn)
    {
        if (_state?.IsAsobo737Max8 != true)
        {
            AppendDashboardLog("737 MAX fuel pumps blocked: the loaded aircraft is not the Asobo 737 MAX.");
            FinishOneShot(3);
            return;
        }
        if (Connection == null)
        {
            AppendDashboardLog("737 MAX fuel pumps blocked: SimConnect is unavailable.");
            FinishOneShot(3);
            return;
        }
        if (!Asobo737MaxFuelPumpInputEventsReady())
        {
            AppendDashboardLog("737 MAX fuel pumps blocked: fuel pump InputEvent readbacks are not bound yet.");
            FinishOneShot(3);
            return;
        }

        var alreadyDesired = desiredOn
            ? Asobo737MaxFuelPumpsConfigured()
            : _asobo737MaxRuntime.FuelPumpInputStates.All(state => state.HasValue && !Asobo737MaxFuelPumpIsOn(state.Value));
        if (alreadyDesired)
        {
            AppendDashboardLog($"737 MAX fuel pumps already {(desiredOn ? "ON" : "OFF")}.");
            FinishOneShot();
            return;
        }

        var toggles = new Queue<FuelPumpToggle>();
        for (var index = 0; index < _asobo737MaxRuntime.FuelPumpInputStates.Count; index++)
        {
                var currentOn = Asobo737MaxFuelPumpIsOn(_asobo737MaxRuntime.FuelPumpInputStates[index]!.Value);
                if (currentOn == desiredOn)
                {
                    continue;
                }

            toggles.Enqueue(
                new FuelPumpToggle(
                    index + 1,
                    _asobo737MaxRuntime.FuelPumpInputEventHashes[index]!.Value,
                    desiredOn ? 0.0 : 1.0));
        }

        _pendingFuelPumpSequence = new PendingFuelPumpSequence(
            toggles, desiredOn, _automation.CurrentGeneration, _state!.Variant);
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

        _pendingFuelPumpSequence = new PendingFuelPumpSequence(
            toggles, desiredOn, _automation.CurrentGeneration, _state!.Variant);
        _fuelPumpSequenceTimer?.Dispose();
        _fuelPumpSequenceTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _fuelPumpSequenceTimer.Tick += (_, _) => ExecuteNextFuelPumpToggle();
        ExecuteNextFuelPumpToggle();
    }

    private void SetA330FuelPumps(bool desiredOn)
    {
        if (Connection == null || _state == null)
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

        _pendingFuelPumpSequence = new PendingFuelPumpSequence(
            toggles, desiredOn, _automation.CurrentGeneration, _state!.Variant);
        _fuelPumpSequenceTimer?.Dispose();
        _fuelPumpSequenceTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _fuelPumpSequenceTimer.Tick += (_, _) => ExecuteNextFuelPumpToggle();
        ExecuteNextFuelPumpToggle();
    }

    private void SetFlyByWireFuelPumps(bool desiredOn)
    {
        if (Connection == null || _state == null)
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

        _pendingFuelPumpSequence = new PendingFuelPumpSequence(
            toggles, desiredOn, _automation.CurrentGeneration, _state!.Variant);
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
        if (_disposingOrDisposed
            || Connection == null
            || _state == null
            || !_automation.IsCurrent(_pendingFuelPumpSequence.Generation)
            || _state.Variant != _pendingFuelPumpSequence.ExpectedVariant)
        {
            AppLog.Write(
                $"Discarded stale fuel-pump sequence from generation {_pendingFuelPumpSequence.Generation}; current generation is {_automation.CurrentGeneration}.");
            _pendingFuelPumpSequence = null;
            StopFuelPumpSequenceTimer();
            FinishOneShot(4);
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
            Connection!.SetInputEvent(
                toggle.InputEventHash.Value,
                toggle.InputEventValue ?? (_pendingFuelPumpSequence.DesiredOn ? 1.0 : 0.0));
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
        _nativeRuntime.A330.FuelPumpInputStates.All(state => state.HasValue);

    private bool A330FuelPumpsConfigured() =>
        A330FuelPumpInputEventsReady()
        && _nativeRuntime.A330.FuelPumpInputStates.All(state => state!.Value >= 0.5);

    private bool Asobo737MaxFuelPumpInputEventsReady() =>
        _asobo737MaxRuntime.FuelPumpInputEventHashes.All(hash => hash.HasValue)
        && _asobo737MaxRuntime.FuelPumpInputStates.All(state => state.HasValue);

    private bool Asobo737MaxApuGeneratorsReady() =>
        _asobo737MaxRuntime.ApuGeneratorInputEventHashes.All(hash => hash.HasValue)
        && _asobo737MaxRuntime.ApuGeneratorInputStates.All(state => state.HasValue);

    private bool Asobo737MaxApuGeneratorsOn() =>
        Asobo737MaxApuGeneratorsReady()
        && _asobo737MaxRuntime.ApuGeneratorInputStates.All(state => state!.Value >= 0.5);

    private bool Asobo737MaxFuelPumpsConfigured() =>
        Asobo737MaxFuelPumpInputEventsReady()
        && _asobo737MaxRuntime.FuelPumpInputStates.All(state => Asobo737MaxFuelPumpIsOn(state!.Value));

    private static bool Asobo737MaxFuelPumpIsOn(double value) =>
        Math.Abs(value) < 0.1;

    private static bool Asobo737MaxBinarySwitchIsOn(double value) =>
        Math.Abs(value) < 0.1;

    private static bool Asobo737MaxBinarySwitchIsOnNormal(double value) =>
        value >= 0.5;

    private static double Asobo737MaxPackPosition(double value) =>
        Asobo737MaxControlProfile.NormalizePackPosition(value);

    private static double Asobo737MaxIsolationValvePosition(double value) =>
        Asobo737MaxControlProfile.NormalizeIsolationValvePosition(value);

    private static double Asobo737MaxFlapsHandleIndex(double value) =>
        Math.Round(value * 8, MidpointRounding.AwayFromZero);

    private bool A330SignInputEventsReady() =>
        _nativeRuntime.A330.SignInputStates.All(state => state.HasValue);

    private double? ResolveA330AutobrakeLevel()
        => _nativeRuntime.A330AutobrakeLevel;

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
        Connection!.SetInputEvent(inputEventHash, 1.0);
        _automation.Schedule(
            500,
            () => Connection!.SetInputEvent(inputEventHash, 0.0),
            $"InputEvent pulse release {inputEventHash}",
            _state?.Variant);
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
            Connection!.SetInputEvent(
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
            Connection!.SetInputEvent(inputEventHash, (double)position);
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
        if (Connection == null || _state == null)
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
            Connection.SetInputEvent(inputEventHash, (double)position);
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
        _nativeRuntime.RecordFbwAdirsCommand(selector, position, commandedUtc);
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
            if (Connection == null || !_nativeRuntime.A330.CrewOxygenInputState.HasValue)
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

            Connection.SetInputEvent(
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
        if (Connection == null)
        {
            AppendDashboardLog("Crew oxygen blocked: simulator connection is unavailable.");
            FinishOneShot(3);
            return;
        }

        var plan = FbwA320CrewOxygenAdapter.CreatePlan(
            _state,
            desiredOn,
            _nativeRuntime.Fbw.CrewOxygenTyped,
            _nativeRuntime.Fbw.CrewOxygen);
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
            Connection.SetInputEvent(plan.InputEventHash, plan.RawState);
        }
        catch (COMException ex)
        {
            AppLog.Write($"FBW crew oxygen SetInputEvent failed; falling back to calculator commands: {ex.Message}");
        }

        foreach (var command in plan.MobiFlightCommands)
        {
            SendMobiFlightCommand(command);
        }
        _nativeRuntime.RecordFbwCrewOxygenCommand(desiredOn, DateTime.UtcNow);
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
            Connection!.SetInputEvent(A330StrobeInputEventHash, (double)desiredPosition);
            SendMobiFlightCommand($"MF.SimVars.Set.(>B:AIRLINER_STROBE_TOGGLE_{desiredPosition})");
            SendMobiFlightCommand("MF.DummyCmd");
            AppLog.Write(
                $"A330 strobe command sent: {FormatStrobePosition(desiredPosition)}.");
        }
        else
        {
            Connection!.SetInputEvent(8986586253276960537UL, (double)desiredPosition);
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

        if (!_mobiFlightSession.RuntimeReady)
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

        _automation.Schedule(
            5000,
            () =>
            {
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

                AppendDashboardLog($"{name} completed and released safely.");
                FinishOneShot();
            },
            $"A330 {name} release",
            AircraftVariant.IniBuildsA330);
    }

    private void StartFlyByWireFireTest(FireTestSystem system)
    {
        if (Connection == null || _state == null)
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

        var expectedVariant = _state.Variant;
        _automation.Schedule(
            5000,
            () =>
            {
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
                AppendDashboardLog($"{name} completed and released safely.");
                FinishOneShot();
            },
            $"FBW {name} release",
            expectedVariant);
    }

    private void VerifyPendingFireTest()
    {
        if (_pendingFireTest == null || _state == null || Connection == null)
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

        Connection!.SetInputEvent(inputEventHash, (double)desiredPosition);
        BeginNativeAction(
            FormatSignSelectorName(selector),
            Verify,
            desiredPosition != 2,
            TimeSpan.FromSeconds(10));
    }

    private void SetA321SignSelector(SignSelector selector, int desiredPosition)
    {
        if (Connection == null || _state == null)
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

        Connection.SetInputEvent(
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
        if (Connection == null || _state == null)
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

        Connection.SetInputEvent(
            A330SignInputEventHashes[index],
            A330ControlProfile.ToPhysicalSignPosition(desiredPosition));
        BeginNativeAction(
            FormatSignSelectorName(selector),
            Verify,
            desiredPosition != 2,
            TimeSpan.FromSeconds(10));
    }

    private void SetFlyByWireSignSelector(SignSelector selector, int desiredPosition)
    {
        if (Connection == null || _state == null)
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
            Connection!.SetInputEvent(
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
        if (_state == null || !_mobiFlightSession.RuntimeReady)
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
            if (_state == null || !_mobiFlightSession.RuntimeReady)
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
            _nativeRuntime.RecordFbwTcasModeCommand(desiredPosition, DateTime.UtcNow);
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
            if (Connection == null || !_nativeRuntime.A330.TcasTrafficInputState.HasValue)
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
            if (_state == null || !_mobiFlightSession.RuntimeReady)
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
            _nativeRuntime.RecordFbwTcasAltitudeCommand(desiredOn, DateTime.UtcNow);
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
            if (Connection == null || !_nativeRuntime.A330.TcasAltitudeInputState.HasValue)
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

            Connection.SetInputEvent(
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
        if (Connection == null || _state == null)
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
        if (GearUpCommandVerified(_state))
        {
            AppendDashboardLog("Landing gear already UP.");
            FinishOneShot();
            return;
        }

        if (_state.IsIniBuildsA310)
        {
            // The A310 exposes 0=UP, 1=transit and 2=DOWN. Its cockpit
            // ignores the generic iniBuilds LANDING_GEAR Gear_Inc event.
            SendMobiFlightCommand(
                $"MF.SimVars.Set.0 (>L:{A310ControlProfile.GearHandleState}) " +
                "0 (>K:GEAR_SET)");
        }
        else
        {
            SendMobiFlightCommand(_state.IsFlyByWireAirbus
                ? "MF.SimVars.Set.(>K:GEAR_UP)"
                : "MF.SimVars.Set.(>B:LANDING_GEAR_Gear_Inc) " +
                  "'INI.GEAR_UP' (>F:KeyEvent)");
        }
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            "Landing gear",
            GearUpCommandVerified,
            true,
            TimeSpan.FromSeconds(12));
    }

    private void SetGearDown()
    {
        if (Connection == null || _state == null)
        {
            AppendDashboardLog("Landing gear DOWN blocked: simulator state is unavailable.");
            FinishOneShot(3);
            return;
        }
        if (GearDownCommandVerified(_state))
        {
            AppendDashboardLog("Landing gear already DOWN.");
            FinishOneShot();
            return;
        }

        if (_state.IsIniBuildsA310)
        {
            SendMobiFlightCommand(
                $"MF.SimVars.Set.2 (>L:{A310ControlProfile.GearHandleState}) " +
                "1 (>K:GEAR_SET)");
        }
        else
        {
            SendMobiFlightCommand(_state.IsFlyByWireAirbus
                ? "MF.SimVars.Set.(>K:GEAR_DOWN)"
                : "MF.SimVars.Set.(>B:LANDING_GEAR_Gear_Dec) " +
                  "'INI.GEAR_DOWN' (>F:KeyEvent)");
        }
        SendMobiFlightCommand("MF.DummyCmd");
        BeginNativeAction(
            "Landing gear",
            GearDownCommandVerified,
            true,
            TimeSpan.FromSeconds(15));
    }

    private static bool GearUpCommandVerified(AircraftState state) =>
        state.IsIniBuildsA310 || state.IsFlyByWireAirbus
            ? state.GearHandleUp
            : state.GearUpVerified;

    private static bool GearDownCommandVerified(AircraftState state) =>
        state.IsIniBuildsA310 || state.IsFlyByWireAirbus
            ? state.GearHandleDown
            : state.GearDownVerified;

    private void SetGroundSpoilersDisarmed()
    {
        if (Connection == null || _state == null || !_state.IsSupportedA320)
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
            _nativeRuntime.RecordFbwSpoilersCommand(false, DateTime.UtcNow);
        }
        else if (_state.IsIniBuildsA330)
        {
            SendMobiFlightCommand("MF.SimVars.Set.0 (>K:SPOILERS_ARM_SET)");
            _nativeRuntime.RecordA330SpoilersCommand(false);
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
        if (Connection == null || _state == null)
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
            if (Connection == null
                || !_mobiFlightSession.AdapterReady
                || !_mobiFlightSession.RuntimeReady
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
            _nativeRuntime.RecordFbwWeatherRadarPwsCommand(desiredPosition, DateTime.UtcNow);
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
            if (Connection == null || !_nativeRuntime.A330.WeatherRadarPwsInputState.HasValue)
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

        Connection!.SetInputEvent(14794713865952973521UL, (double)nativePosition);
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
            if (Connection == null || !_mobiFlightSession.AdapterReady)
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
            _nativeRuntime.RecordFbwNoseLightCommand(desiredPosition, DateTime.UtcNow);
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
            if (Connection == null || !_nativeRuntime.A330.NoseLightInputState.HasValue)
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
            if (Connection == null || !_mobiFlightSession.AdapterReady)
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
            _nativeRuntime.RecordFbwLandingLightCommand(desiredPosition, DateTime.UtcNow);
            SendMobiFlightCommand("MF.DummyCmd");
            BeginNativeAction(
                "Landing lights",
                VerifyFbw,
                desiredPosition == 0,
                TimeSpan.FromSeconds(10),
                FormatLandingLightPosition(desiredPosition));
            return;
        }

        if (_state?.IsIniBuildsA330 == true)
        {
            if (Connection == null || !_nativeRuntime.A330.LandingLightInputState.HasValue)
            {
                AppendDashboardLog("Landing light blocked: A330 switch readback is unavailable.");
                FinishOneShot(4);
                return;
            }

            var desiredOn = desiredPosition == 0;
            bool VerifyA330(AircraftState state) =>
                state.LeftLandingLightSelectorPosition.HasValue
                && Math.Abs(
                    state.LeftLandingLightSelectorPosition.Value
                    - (desiredOn ? 0 : 1)) < 0.1;
            if (VerifyA330(_state))
            {
                AppendDashboardLog($"A330 landing light already {(desiredOn ? "ON" : "OFF")}.");
                FinishOneShot();
                return;
            }

            Connection.SetInputEvent(
                A330LandingLightInputEventHash,
                desiredOn ? 1.0 : 0.0);
            BeginNativeAction(
                "A330 landing light",
                VerifyA330,
                desiredOn,
                TimeSpan.FromSeconds(10),
                desiredOn ? "ON" : "OFF");
            AppLog.Write($"A330 landing light command sent: {(desiredOn ? "ON" : "OFF")}.");
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
            || Connection == null
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
        Connection!.TransmitClientEvent_EX1(
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
        if (Connection == null
            || _state == null
            || !_state.IsSupportedA320
            || !_state.OnGround
            || !_mobiFlightSession.AdapterReady
            || !_mobiFlightSession.RuntimeReady)
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
            if (Connection == null || !_mobiFlightSession.RuntimeReady)
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
            _nativeRuntime.RecordFbwSpoilersCommand(true, DateTime.UtcNow);
        }
        else if (_state.IsIniBuildsA330)
        {
            SendMobiFlightCommand("MF.SimVars.Set.1 (>K:SPOILERS_ARM_SET)");
            _nativeRuntime.RecordA330SpoilersCommand(true);
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
        if (Connection == null || _state == null || !_state.IsSupportedA320)
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
        if (Connection == null || _state == null || !_state.IsSupportedA320)
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
            StartA330FlapRetractionSequence(
                A330ControlProfile.FlapRetractionStepCount(_state.FlapsHandleIndex));
            return;
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

    private void StartA330FlapRetractionSequence(int remainingSteps)
    {
        var timer = new System.Windows.Forms.Timer
        {
            Interval = A330ControlProfile.FlapStepIntervalMilliseconds
        };
        var guardedAction = _automation.Track(timer);

        void CompleteSequence()
        {
            _automation.Complete(timer);
            BeginNativeAction(
                "Flaps CLEAN",
                state => state.FlapsAtDetent(0),
                true,
                TimeSpan.FromSeconds(30));
        }

        void SendNextStep()
        {
            if (!guardedAction.IsCurrent
                || Connection == null
                || _state?.IsIniBuildsA330 != true)
            {
                _automation.Complete(timer);
                return;
            }
            SendMobiFlightCommand(A330ControlProfile.FlapsRetractOneDetentCommand);
            SendMobiFlightCommand("MF.DummyCmd");
            remainingSteps--;
            if (remainingSteps <= 0)
            {
                CompleteSequence();
            }
        }

        timer.Tick += (_, _) => SendNextStep();
        SendNextStep();
        if (remainingSteps > 0)
        {
            timer.Start();
        }
    }

    private void SetAutobrake(int desiredLevel, string label)
    {
        if (Connection == null
            || _state == null
            || !_state.IsSupportedA320
            || !_mobiFlightSession.AdapterReady
            || !_mobiFlightSession.RuntimeReady
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
            _nativeRuntime.RecordFbwAutobrakeCommand(desiredLevel, DateTime.UtcNow);
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
        _automation.Schedule(
            500,
            () =>
            {
                SendMobiFlightCommand($"MF.SimVars.Set.0 (>L:{commandLVar})");
                SendMobiFlightCommand("MF.DummyCmd");
            },
            $"native pulse release {commandLVar}",
            _state?.Variant);
    }

    private bool ValidateNativeInputAction(
        string name,
        bool requireCompleteNativeState = true,
        bool requireStationary = true)
    {
        if (Connection == null
            || _state == null
            || !_mobiFlightSession.AdapterReady
            || !_mobiFlightSession.RuntimeReady
            || (requireCompleteNativeState && !_nativeRuntime.AirbusNativeStateReady))
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
        if (Connection == null || _state == null)
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
            var message = $"External-power procedure blocked: {blockedReason}";
            Console.Error.WriteLine(message);
            AppendDashboardLog(message);
            AppLog.Write(message);
            FinishOneShot(3);
            return;
        }
        if (_state.ExternalPowerOn == desiredOn)
        {
            Console.WriteLine($"External power is already {(desiredOn ? "ON" : "OFF")}.");
            FinishOneShot();
            return;
        }

        if (_state.IsIniBuildsA310)
        {
            if (!_mobiFlightSession.RuntimeReady)
            {
                AppendDashboardLog(
                    "A310 external-power command blocked: the MobiFlight WASM bridge is not ready.");
                FinishOneShot(4);
                return;
            }

            // The A310 does not reliably respond to SET_EXTERNAL_POWER. Its overhead
            // pushbutton uses the simulator's toggle event, so only pulse it after the
            // native readback above confirms that the requested state differs.
            SendMobiFlightCommand("MF.SimVars.Set.1 (>K:TOGGLE_EXTERNAL_POWER)");
            SendMobiFlightCommand("MF.DummyCmd");
            BeginNativeAction(
                "A310 external power",
                state => state.ExternalPowerOn == desiredOn,
                desiredOn,
                TimeSpan.FromSeconds(10));
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
        Connection!.TransmitClientEvent_EX1(
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
        if (!state.IsIniBuildsAirbusFamily
            && !state.IsIniBuildsA310
            && !state.IsAsobo737Max8)
        {
            return "the loaded aircraft is not supported for external-power automation.";
        }

        if (desiredOn && (!state.OnGround || state.GroundSpeedKnots > 0.5))
        {
            return "aircraft must be stationary on the ground before connecting power.";
        }

        if (desiredOn && !state.EnginesOff)
        {
            return "engines must be off before connecting external power.";
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
            $"  FBW BAT 1 AUTO untyped/typed/commanded: {FormatOptionalBool(_nativeRuntime.Fbw.Battery1Auto)}/{FormatOptionalBool(_nativeRuntime.Fbw.Battery1AutoTyped)}/{FormatOptionalBool(_nativeRuntime.Fbw.CommandedBattery1Auto)}",
            $"  FBW BAT 2 AUTO untyped/typed/commanded: {FormatOptionalBool(_nativeRuntime.Fbw.Battery2Auto)}/{FormatOptionalBool(_nativeRuntime.Fbw.Battery2AutoTyped)}/{FormatOptionalBool(_nativeRuntime.Fbw.CommandedBattery2Auto)}",
            $"  FBW BAT potential 1/2: {FormatOptionalFloat(_nativeRuntime.Fbw.Battery1Potential, "F1")}/{FormatOptionalFloat(_nativeRuntime.Fbw.Battery2Potential, "F1")} V",
            $"  App EXT PWR available/on: {_state.ExternalPowerAvailable.ToYesNo()}/{_state.ExternalPowerOn.ToOnOff()}",
            $"  FBW EXT PWR available untyped/typed: {FormatOptionalBool(_nativeRuntime.Fbw.ExternalPowerAvailable)}/{FormatOptionalBool(_nativeRuntime.Fbw.ExternalPowerAvailableTyped)}",
            $"  FBW EXT PWR ON untyped/typed: {FormatOptionalBool(_nativeRuntime.Fbw.ExternalPowerOn)}/{FormatOptionalBool(_nativeRuntime.Fbw.ExternalPowerOnTyped)}",
            $"  FBW A380 EXT PWR available 1/2/3/4: {FormatOptionalBool(_nativeRuntime.Fbw.A380ExternalPower1AvailableTyped)}/{FormatOptionalBool(_nativeRuntime.Fbw.A380ExternalPower2AvailableTyped)}/{FormatOptionalBool(_nativeRuntime.Fbw.A380ExternalPower3AvailableTyped)}/{FormatOptionalBool(_nativeRuntime.Fbw.A380ExternalPower4AvailableTyped)}",
            $"  FBW A380 EXT PWR ON 1/2/3/4: {FormatOptionalBool(_nativeRuntime.Fbw.A380ExternalPower1OnTyped)}/{FormatOptionalBool(_nativeRuntime.Fbw.A380ExternalPower2OnTyped)}/{FormatOptionalBool(_nativeRuntime.Fbw.A380ExternalPower3OnTyped)}/{FormatOptionalBool(_nativeRuntime.Fbw.A380ExternalPower4OnTyped)}",
            $"  FBW A380 direct EXT PWR available 1/2/3/4: {_state.FbwA380ExternalPower1Available.ToYesNo()}/{_state.FbwA380ExternalPower2Available.ToYesNo()}/{_state.FbwA380ExternalPower3Available.ToYesNo()}/{_state.FbwA380ExternalPower4Available.ToYesNo()}",
            $"  FBW A380 direct EXT PWR ON 1/2/3/4: {_state.FbwA380ExternalPower1On.ToOnOff()}/{_state.FbwA380ExternalPower2On.ToOnOff()}/{_state.FbwA380ExternalPower3On.ToOnOff()}/{_state.FbwA380ExternalPower4On.ToOnOff()}",
            $"  FBW A380 AC buses powered 1/2/3/4: {_state.FbwA380AcBus1Powered.ToYesNo()}/{_state.FbwA380AcBus2Powered.ToYesNo()}/{_state.FbwA380AcBus3Powered.ToYesNo()}/{_state.FbwA380AcBus4Powered.ToYesNo()}",
            $"  Generic EXT PWR unindexed available/on: {_state.ExternalPowerAvailableUnindexed.ToYesNo()}/{_state.ExternalPowerOnUnindexed.ToOnOff()}",
            $"  App ADIRS 1/2/3 selector: {_state.Adirs1SelectorState:F0}/{_state.Adirs2SelectorState:F0}/{_state.Adirs3SelectorState:F0}",
            $"  FBW ADIRS 1 untyped/typed/commanded: {FormatOptionalFloat(_nativeRuntime.Fbw.Adirs1Selector, "F0")}/{FormatOptionalFloat(_nativeRuntime.Fbw.Adirs1SelectorTyped, "F0")}/{FormatOptionalFloat(_nativeRuntime.Fbw.CommandedAdirs1Selector, "F0")}",
            $"  FBW ADIRS 2 untyped/typed/commanded: {FormatOptionalFloat(_nativeRuntime.Fbw.Adirs2Selector, "F0")}/{FormatOptionalFloat(_nativeRuntime.Fbw.Adirs2SelectorTyped, "F0")}/{FormatOptionalFloat(_nativeRuntime.Fbw.CommandedAdirs2Selector, "F0")}",
            $"  FBW ADIRS 3 untyped/typed/commanded: {FormatOptionalFloat(_nativeRuntime.Fbw.Adirs3Selector, "F0")}/{FormatOptionalFloat(_nativeRuntime.Fbw.Adirs3SelectorTyped, "F0")}/{FormatOptionalFloat(_nativeRuntime.Fbw.CommandedAdirs3Selector, "F0")}",
            $"  FBW ADIRS ON BAT: {FormatOptionalBool(_nativeRuntime.Fbw.AdirsOnBattery)}",
            $"  FBW crew oxygen untyped/typed/commanded: {FormatOptionalBool(_nativeRuntime.Fbw.CrewOxygen)}/{FormatOptionalBool(_nativeRuntime.Fbw.CrewOxygenTyped)}/{FormatOptionalBool(_nativeRuntime.Fbw.CommandedCrewOxygen)}",
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

    private void PrintCapabilities()
    {
        var capabilities = _state?.IsIniBuildsA310 == true
            ? A310ControlProfile.Capabilities
            : A320Capabilities.All;
        foreach (var capability in capabilities)
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
        Width = 1220;
        Height = 900;
        MinimumSize = new System.Drawing.Size(1040, 760);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = System.Drawing.Color.FromArgb(242, 245, 248);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 8
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 154));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 206));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
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
        _simBriefBadgeLabel = NewStatusBadge("SIMBRIEF NOT SET", System.Drawing.Color.DimGray);
        _versionBadgeLabel = NewStatusBadge($"v{GetApplicationVersion()}", System.Drawing.Color.FromArgb(40, 68, 106));
        topStatusBar.Controls.Add(_simBadgeLabel);
        topStatusBar.Controls.Add(_aircraftBadgeLabel);
        topStatusBar.Controls.Add(_adapterBadgeLabel);
        topStatusBar.Controls.Add(_flowBadgeLabel);
        topStatusBar.Controls.Add(_simBriefBadgeLabel);
        _sayIntentionsBadgeLabel = NewStatusBadge("SAYINTENTIONS CHECKING", System.Drawing.Color.DarkGoldenrod);
        topStatusBar.Controls.Add(_sayIntentionsBadgeLabel);
        _gsxBadgeLabel = NewStatusBadge("GSX CHECKING", System.Drawing.Color.DarkGoldenrod);
        topStatusBar.Controls.Add(_gsxBadgeLabel);
        topStatusBar.Controls.Add(_versionBadgeLabel);
        root.Controls.Add(topStatusBar);
        UpdateSimBriefStatus();

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
            ColumnCount = 3,
            Padding = new Padding(12, 10, 12, 10),
            BackColor = System.Drawing.Color.White,
            Margin = new Padding(0, 0, 12, 0)
        };
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        statusShell.Controls.Add(statusPanel, 0, 0);
        statusShell.Controls.Add(BuildAircraftCard(), 1, 0);

        var overviewTitle = new Label
        {
            Text = "Flight overview",
            AutoSize = true,
            Font = new System.Drawing.Font(
                Font.FontFamily,
                11,
                System.Drawing.FontStyle.Bold),
            ForeColor = System.Drawing.Color.FromArgb(40, 68, 106),
            Margin = new Padding(0, 0, 0, 8)
        };
        statusPanel.Controls.Add(overviewTitle, 0, 0);
        statusPanel.SetColumnSpan(overviewTitle, 2);
        statusPanel.RowCount = 1;
        statusPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _phaseLabel = AddDashboardRow(statusPanel, "Operational phase", "Unknown");
        _simBriefStatusLabel = AddDashboardRow(
            statusPanel,
            "Flight plan",
            SimBriefStatusText());
        _recommendationLabel = AddDashboardRow(
            statusPanel,
            "Next flow",
            "Waiting for aircraft state...");
        _telemetryLabel = AddDashboardRow(
            statusPanel,
            "Aircraft telemetry",
            "Waiting for aircraft state...");
        var integrationsButton = new Button
        {
            Text = "Integrations",
            Width = 112,
            Height = 34,
            Margin = new Padding(4, 2, 0, 2),
            FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.White,
            ForeColor = System.Drawing.Color.FromArgb(40, 68, 106),
            UseVisualStyleBackColor = false
        };
        integrationsButton.FlatAppearance.BorderColor =
            System.Drawing.Color.FromArgb(190, 198, 208);
        integrationsButton.Click += (_, _) => ShowIntegrationsDialog();
        statusPanel.Controls.Add(integrationsButton, 2, 1);
        statusPanel.SetRowSpan(integrationsButton, 4);

        // These details remain available to badges, diagnostics, and integration
        // dialogs without repeating them in the main flight overview.
        _connectionLabel = NewDashboardLabel("Connecting...");
        _aircraftLabel = NewDashboardLabel("Waiting for state...");
        _electricalLabel = NewDashboardLabel("Waiting for state...");
        _adapterLabel = NewDashboardLabel("Connecting...");
        _sayIntentionsStatusLabel = NewDashboardLabel(
            "Client not detected - optional integration inactive.");
        _gsxStatusLabel = NewDashboardLabel(GsxStatusText());
        _versionLabel = NewDashboardLabel(
            $"{GetApplicationVersion()} - checking GitHub releases...");
        UpdateGsxStatus();

        var settingsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10, 7, 10, 7),
            BackColor = System.Drawing.Color.White,
            Margin = new Padding(0, 0, 0, 14)
        };
        var flightSetupPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        var preferencesPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 2, 0, 0)
        };
        flightSetupPanel.Controls.Add(new Label
        {
            Text = "Flight setup",
            AutoSize = false,
            Width = 105,
            Height = 26,
            Font = new System.Drawing.Font(Font.FontFamily, 9, System.Drawing.FontStyle.Bold),
            ForeColor = System.Drawing.Color.FromArgb(40, 68, 106),
            Margin = new Padding(0, 7, 8, 0)
        });
        preferencesPanel.Controls.Add(new Label
        {
            Text = "Preferences",
            AutoSize = false,
            Width = 105,
            Height = 26,
            Font = new System.Drawing.Font(Font.FontFamily, 9, System.Drawing.FontStyle.Bold),
            ForeColor = System.Drawing.Color.FromArgb(40, 68, 106),
            Margin = new Padding(0, 7, 8, 0)
        });
        preferencesPanel.Controls.Add(new Label
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
        preferencesPanel.Controls.Add(_automationPolicyBox);

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
            if (_calloutDetailBox != null)
            {
                _calloutDetailBox.Enabled = _voiceCalloutsBox.Checked;
            }
        };
        preferencesPanel.Controls.Add(_voiceCalloutsBox);

        preferencesPanel.Controls.Add(new Label
        {
            Text = "Detail:",
            AutoSize = true,
            Margin = new Padding(12, 7, 4, 0)
        });
        _calloutDetailBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 92,
            Enabled = _settings.EnableStandardCallouts
        };
        _calloutDetailBox.Items.AddRange(
            Enum.GetValues(typeof(CalloutDetail)).Cast<object>().ToArray());
        _calloutDetailBox.SelectedItem = _settings.CalloutDetail;
        _calloutDetailBox.SelectedIndexChanged += (_, _) =>
        {
            _settings.CalloutDetail = (CalloutDetail)_calloutDetailBox.SelectedItem;
            SettingsStore.Save(_settings);
        };
        preferencesPanel.Controls.Add(_calloutDetailBox);

        _sayIntentionsVoiceBox = new CheckBox
        {
            Text = "SayIntentions voices",
            AutoSize = true,
            Checked = _settings.UseSayIntentionsVoiceCallouts,
            Margin = new Padding(12, 5, 0, 0)
        };
        _sayIntentionsVoiceBox.CheckedChanged += (_, _) =>
        {
            _settings.UseSayIntentionsVoiceCallouts = _sayIntentionsVoiceBox.Checked;
            SettingsStore.Save(_settings);
        };
        preferencesPanel.Controls.Add(_sayIntentionsVoiceBox);

        var featureSettingsButton = new Button
        {
            Text = "Flight settings...",
            AutoSize = true,
            Margin = new Padding(8, 2, 8, 0)
        };
        featureSettingsButton.Click += (_, _) => ShowFeatureSettingsDialog();
        var payloadOverviewButton = new Button
        {
            Text = "Payload...",
            AutoSize = true,
            Margin = new Padding(0, 2, 8, 0)
        };
        payloadOverviewButton.Click += (_, _) => ShowSimBriefPayloadOverview();

        flightSetupPanel.Controls.Add(new Label
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
        flightSetupPanel.Controls.Add(_transitionAltitudeBox);
        flightSetupPanel.Controls.Add(new Label
        {
            Text = "ft",
            AutoSize = true,
            Margin = new Padding(2, 7, 0, 0)
        });

        flightSetupPanel.Controls.Add(new Label
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
        flightSetupPanel.Controls.Add(_takeoffV1Box);
        flightSetupPanel.Controls.Add(new Label
        {
            Text = "kt",
            AutoSize = true,
            Margin = new Padding(2, 7, 0, 0)
        });

        flightSetupPanel.Controls.Add(new Label
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
            if (_takeoffV2Box != null
                && _takeoffV2Box.Value < _takeoffRotateBox.Value)
            {
                _takeoffV2Box.Value = Math.Min(
                    _takeoffV2Box.Maximum,
                    _takeoffRotateBox.Value);
            }
            SettingsStore.Save(_settings);
            if (_state != null)
            {
                _state.TakeoffRotateSpeedKnots =
                    _settings.TakeoffRotateSpeedKnots;
            }
        };
        flightSetupPanel.Controls.Add(_takeoffRotateBox);
        flightSetupPanel.Controls.Add(new Label
        {
            Text = "kt",
            AutoSize = true,
            Margin = new Padding(2, 7, 0, 0)
        });

        flightSetupPanel.Controls.Add(new Label
        {
            Text = "V2:",
            AutoSize = true,
            Margin = new Padding(10, 7, 4, 0)
        });
        _takeoffV2Box = new NumericUpDown
        {
            Minimum = 80,
            Maximum = 220,
            Increment = 1,
            Value = Math.Max(
                _settings.TakeoffRotateSpeedKnots,
                Math.Min(220, _settings.TakeoffV2SpeedKnots)),
            Width = 64
        };
        _takeoffV2Box.ValueChanged += (_, _) =>
        {
            if (_takeoffRotateBox != null
                && _takeoffV2Box.Value < _takeoffRotateBox.Value)
            {
                _takeoffV2Box.Value = Math.Min(
                    _takeoffV2Box.Maximum,
                    _takeoffRotateBox.Value);
                return;
            }
            _settings.TakeoffV2SpeedKnots = (int)_takeoffV2Box.Value;
            SettingsStore.Save(_settings);
            if (_state != null)
            {
                _state.TakeoffV2SpeedKnots =
                    _settings.TakeoffV2SpeedKnots;
            }
        };
        flightSetupPanel.Controls.Add(_takeoffV2Box);
        flightSetupPanel.Controls.Add(new Label
        {
            Text = "kt",
            AutoSize = true,
            Margin = new Padding(2, 7, 0, 0)
        });
        flightSetupPanel.Controls.Add(featureSettingsButton);
        flightSetupPanel.Controls.Add(payloadOverviewButton);
        settingsPanel.Controls.Add(flightSetupPanel, 0, 0);
        settingsPanel.Controls.Add(preferencesPanel, 0, 1);
        root.Controls.Add(settingsPanel);

        var timelineGroup = new GroupBox
        {
            Text = "Flight progress",
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
            DisplayMember = nameof(ProcedureListItem.DisplayName),
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 31,
            BorderStyle = BorderStyle.None,
            BackColor = System.Drawing.Color.White
        };
        _flowList.DrawItem += DrawFlowItem;
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
            if (IsProcedureActive(_procedureRunner.Status)
                || _pendingGsxEngineStartProcedure != null)
            {
                return;
            }

            if (_flowList.SelectedItem is ProcedureListItem item)
            {
                _automation.Enqueue($"procedure start {item.Definition.Id}");
            }
        };
        startPanel.Controls.Add(_startSelectedFlowButton, 0, 0);
        timelineLayout.Controls.Add(startPanel, 1, 0);
        timelineGroup.Controls.Add(timelineLayout);
        root.Controls.Add(timelineGroup);

        var activeArea = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        activeArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
        activeArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        root.Controls.Add(activeArea);

        var procedureGroup = new GroupBox
        {
            Text = "Current action",
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            Margin = new Padding(0, 0, 8, 0),
            BackColor = System.Drawing.Color.White
        };
        activeArea.Controls.Add(procedureGroup, 0, 0);

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
        procedureLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        procedureLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        procedureGroup.Controls.Add(procedureLayout);

        _procedureLabel = NewDashboardLabel("None");
        _stepLabel = NewDashboardLabel("No active step");
        _stepLabel.Font = new System.Drawing.Font(
            Font.FontFamily,
            12,
            System.Drawing.FontStyle.Bold);
        _stepLabel.ForeColor = System.Drawing.Color.FromArgb(31, 41, 55);
        _statusBadgeLabel = NewDashboardLabel("Idle");
        _statusBadgeLabel.Font = new System.Drawing.Font(
            SystemFonts.DefaultFont,
            System.Drawing.FontStyle.Bold);
        _waitingForLabel = NewDashboardLabel("Waiting for: none");
        _waitingForLabel.MaximumSize = new System.Drawing.Size(760, 0);
        _stepProgressLabel = NewDashboardLabel("0 of 0 steps complete");
        _stepProgressLabel.ForeColor = System.Drawing.Color.DimGray;
        _procedureProgress = new ProgressBar { Dock = DockStyle.Top, Height = 16 };
        procedureLayout.Controls.Add(_statusBadgeLabel);
        procedureLayout.Controls.Add(_procedureLabel);
        procedureLayout.Controls.Add(_stepLabel);
        procedureLayout.Controls.Add(_waitingForLabel);
        procedureLayout.Controls.Add(_stepProgressLabel);
        procedureLayout.Controls.Add(_procedureProgress);

        var procedureButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 6, 0, 0)
        };
        _startFirstFlowButton = NewProcedureButton(
            "Start first flow",
            "procedure start power-up-initial-setup",
            132,
            System.Drawing.Color.FromArgb(39, 130, 87),
            System.Drawing.Color.FromArgb(22, 101, 52),
            System.Drawing.Color.FromArgb(34, 148, 96),
            emphasize: true);
        procedureButtons.Controls.Add(_startFirstFlowButton);
        _confirmCompletedButton = NewProcedureButton(
            "Confirm completed",
            "procedure confirm",
            150,
            System.Drawing.Color.FromArgb(39, 130, 87),
            System.Drawing.Color.FromArgb(22, 101, 52),
            System.Drawing.Color.FromArgb(34, 148, 96),
            emphasize: true,
            bindCommand: false);
        _confirmCompletedButton.Click += async (_, _) => await HandleConfirmButtonAsync();
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
                _automation.Enqueue("procedure reset");
            }
        };
        procedureButtons.Controls.Add(resetProgressButton);
        procedureGroup.Controls.Add(procedureButtons);

        var gsxGroup = new GroupBox
        {
            Text = "GSX ground services",
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            Margin = new Padding(8, 0, 0, 0),
            BackColor = System.Drawing.Color.White
        };
        activeArea.Controls.Add(gsxGroup, 1, 0);
        var gsxLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5
        };
        gsxLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        gsxLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        gsxLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        gsxLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        gsxLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        gsxGroup.Controls.Add(gsxLayout);
        _gsxLiveSummaryLabel = NewDashboardLabel("Checking GSX status...");
        _gsxLiveSummaryLabel.Font = new System.Drawing.Font(
            Font.FontFamily,
            10,
            System.Drawing.FontStyle.Bold);
        _gsxLiveSummaryLabel.MaximumSize = new System.Drawing.Size(360, 0);
        _gsxPassengerLabel = NewDashboardLabel("Passenger progress unavailable");
        _gsxPassengerLabel.ForeColor = System.Drawing.Color.DimGray;
        _gsxPassengerProgress = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 16,
            Minimum = 0,
            Maximum = 100
        };
        _gsxLiveActionLabel = NewDashboardLabel("No action required");
        _gsxLiveActionLabel.MaximumSize = new System.Drawing.Size(360, 0);
        _gsxLiveActionLabel.Padding = new Padding(8);
        _gsxLiveActionLabel.BackColor = System.Drawing.Color.FromArgb(236, 253, 245);
        _gsxLiveActionLabel.ForeColor = System.Drawing.Color.FromArgb(6, 95, 70);
        _manageGsxButton = new Button
        {
            Text = "Open GSX details",
            AutoSize = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            FlatStyle = FlatStyle.Flat,
            ForeColor = System.Drawing.Color.FromArgb(40, 68, 106),
            BackColor = System.Drawing.Color.White,
            UseVisualStyleBackColor = false
        };
        _manageGsxButton.Click += (_, _) => ShowGsxDialog(this);
        gsxLayout.Controls.Add(_gsxLiveSummaryLabel);
        gsxLayout.Controls.Add(_gsxPassengerLabel);
        gsxLayout.Controls.Add(_gsxPassengerProgress);
        gsxLayout.Controls.Add(_gsxLiveActionLabel);
        gsxLayout.Controls.Add(_manageGsxButton);

        var logGroup = new GroupBox
        {
            Text = "Activity log",
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            Visible = false
        };
        _eventLog = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            Font = new System.Drawing.Font("Consolas", 9)
        };
        logGroup.Controls.Add(_eventLog);
        var activityShell = new Panel { Dock = DockStyle.Fill };
        var toggleActivityButton = new Button
        {
            Text = "Show activity log",
            Dock = DockStyle.Top,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            BackColor = System.Drawing.Color.White,
            ForeColor = System.Drawing.Color.FromArgb(55, 65, 81),
            UseVisualStyleBackColor = false
        };
        toggleActivityButton.FlatAppearance.BorderColor =
            System.Drawing.Color.FromArgb(209, 213, 219);
        toggleActivityButton.Click += (_, _) =>
        {
            logGroup.Visible = !logGroup.Visible;
            root.RowStyles[6].Height = logGroup.Visible ? 170 : 42;
            toggleActivityButton.Text = logGroup.Visible
                ? "Hide activity log"
                : "Show activity log";
        };
        activityShell.Controls.Add(logGroup);
        activityShell.Controls.Add(toggleActivityButton);
        toggleActivityButton.BringToFront();
        logGroup.Padding = new Padding(8, 38, 8, 8);
        root.Controls.Add(activityShell);

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
        var debugJumpButton = new Button
        {
            Text = "Debug jump to flow",
            AutoSize = true
        };
        debugJumpButton.Click += (_, _) => ShowDebugJumpDialog();
        actions.Controls.Add(debugJumpButton);
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

    private void StartSayIntentionsMonitoring()
    {
        if (_sayIntentionsTimer != null)
        {
            return;
        }

        _sayIntentionsTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _sayIntentionsTimer.Tick += async (_, _) => await RefreshSayIntentionsStatusAsync();
        _sayIntentionsTimer.Start();
    }

    private async Task RefreshSayIntentionsStatusAsync()
    {
        if (_sayIntentionsRefreshInProgress || _sayIntentionsCancellation.IsCancellationRequested)
        {
            return;
        }

        _sayIntentionsRefreshInProgress = true;
        try
        {
            var result = await _sayIntentionsClient
                .DiscoverAsync(_sayIntentionsCancellation.Token);
            _sayIntentionsRuntime.SetFlight(result.Context);
            if (_pendingSayIntentionsAtcStepId != null
                && _pendingSayIntentionsAtcStartedUtc.HasValue
                && DateTime.UtcNow - _pendingSayIntentionsAtcStartedUtc.Value
                > TimeSpan.FromMinutes(3))
            {
                AppendDashboardLog(
                    "SayIntentions did not create a verifiable ATC exchange within 3 minutes. Check its active flight, then press Confirm to retry.");
                CancelPendingSayIntentionsAtcRequest();
            }
            // Discovery controls the badge. Update it before optional mode and
            // history calls so a slow or temporarily failing SAPI endpoint
            // cannot leave an online client displayed as offline.
            UpdateSayIntentionsStatus(result);
            if (result.Context != null)
            {
                if (_settings.UseSayIntentionsCopilotCommunications)
                {
                    // Establish the configured communications owner as soon as
                    // the SayIntentions session is discovered. Native callback
                    // actions can otherwise race the desktop client while it is
                    // still applying SIAI_COPILOT.
                    await EnsureSayIntentionsCopilotModeAsync(
                        result.Context,
                        _sayIntentionsCancellation.Token);
                }
                await MirrorSayIntentionsCommunicationsAsync(
                    result.Context,
                    _sayIntentionsCancellation.Token);
            }
            else
            {
                _sayIntentionsRuntime.ResetDiscoverySession();
                // A short local-client or SAPI outage must not detach a valid
                // ATC checkpoint from its procedure. Explicit cancel/reset and
                // procedure transitions own pending-request cancellation.
            }
        }
        catch (OperationCanceledException) when (_sayIntentionsCancellation.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        finally
        {
            _sayIntentionsRefreshInProgress = false;
        }
    }

    private void UpdateSayIntentionsStatus(SayIntentionsDiscoveryResult result)
    {
        if (_sayIntentionsStatusLabel == null)
        {
            return;
        }

        switch (result.State)
        {
            case SayIntentionsConnectionState.Connected:
                if (_sayIntentionsTimer != null
                    && _pendingSayIntentionsAtcStepId == null)
                {
                    _sayIntentionsTimer.Interval = 10000;
                }
                var flight = result.Context!;
                var callsign = string.IsNullOrWhiteSpace(flight.Callsign)
                    ? "active flight"
                    : flight.Callsign;
                var gate = string.IsNullOrWhiteSpace(flight.AssignedGate)
                    ? ""
                    : $" | gate {flight.AssignedGate}";
                var comms = _settings.UseSayIntentionsCopilotCommunications
                    ? _pendingSayIntentionsAtcStepId != null
                        ? " | ATC response pending"
                        : " | checkpoint control ready"
                    : " | comms Pilot";
                _sayIntentionsStatusLabel.Text =
                    $"Connected - {callsign} | {flight.RouteLabel}{gate}{comms}";
                _sayIntentionsStatusLabel.ForeColor = System.Drawing.Color.DarkGreen;
                SetStatusBadge(
                    _sayIntentionsBadgeLabel,
                    "SAYINTENTIONS CONNECTED",
                    System.Drawing.Color.SeaGreen);
                break;

            case SayIntentionsConnectionState.NoActiveFlight:
                if (_sayIntentionsTimer != null)
                {
                    _sayIntentionsTimer.Interval = 5000;
                }
                _sayIntentionsStatusLabel.Text =
                    "Client detected - start a SayIntentions flight to activate integration.";
                _sayIntentionsStatusLabel.ForeColor = System.Drawing.Color.DarkGoldenrod;
                SetStatusBadge(
                    _sayIntentionsBadgeLabel,
                    "SAYINTENTIONS READY",
                    System.Drawing.Color.DarkGoldenrod);
                break;

            default:
                if (_sayIntentionsTimer != null)
                {
                    _sayIntentionsTimer.Interval = 5000;
                }
                _sayIntentionsStatusLabel.Text =
                    "Client not detected - optional integration inactive.";
                _sayIntentionsStatusLabel.ForeColor = System.Drawing.Color.DimGray;
                SetStatusBadge(
                    _sayIntentionsBadgeLabel,
                    "SAYINTENTIONS OFFLINE",
                    System.Drawing.Color.DimGray);
                break;
        }
    }

    private async Task<bool> EnsureSayIntentionsCopilotModeAsync(
        SayIntentionsFlightContext flight,
        CancellationToken cancellationToken,
        bool force = false,
        bool? desiredOverride = null)
    {
        var desired = desiredOverride
                      ?? _settings.UseSayIntentionsCopilotCommunications;
        if (!force && _sayIntentionsRuntime.IsCopilotModeCurrent(flight.SessionKey, desired))
        {
            return true;
        }

        await _sayIntentionsCommsModeGate.WaitAsync(cancellationToken);
        try
        {
            if (!force && _sayIntentionsRuntime.IsCopilotModeCurrent(flight.SessionKey, desired))
            {
                return true;
            }

            var accepted = await _sayIntentionsClient
                .SetCopilotCommunicationsAsync(
                    flight,
                    desired,
                    cancellationToken);
            if (!accepted)
            {
                AppLog.Write(
                    "SayIntentions rejected the requested communications-mode change.");
                return false;
            }

            _sayIntentionsRuntime.RecordCopilotModeApplied(flight.SessionKey, desired);
            AppLog.Write(
                desired
                    ? "SayIntentions communications assigned to its First Officer."
                    : "SayIntentions communications returned to the pilot.");
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or InvalidOperationException
                                   or ArgumentException)
        {
            AppLog.Write(
                $"SayIntentions communications-mode update failed: {ex.Message}");
            return false;
        }
        finally
        {
            _sayIntentionsCommsModeGate.Release();
        }
    }

    private async Task MirrorSayIntentionsCommunicationsAsync(
        SayIntentionsFlightContext flight,
        CancellationToken cancellationToken)
    {
        try
        {
            var communications = await _sayIntentionsClient
                .GetCommunicationsAsync(flight, cancellationToken);
            CaptureSayIntentionsArrivalStand(flight, communications);
            if (_sayIntentionsRuntime.IsNewCommunicationSession(flight.SessionKey))
            {
                _sayIntentionsRuntime.BeginCommunicationSession(flight.SessionKey, communications);
                CaptureRecentSayIntentionsPushbackDirection(communications);
                CaptureRecentSayIntentionsApproach(communications);
                await TryCompleteCurrentSayIntentionsAtcStepFromHistoryAsync(
                    flight,
                    communications,
                    cancellationToken);
                return;
            }

            foreach (var communication in communications
                         .OrderBy(item => item.Id))
            {
                var change = _sayIntentionsRuntime.ObserveCommunication(communication);
                if (!change.HasChanges)
                {
                    continue;
                }
                if (!communication.Channel.StartsWith(
                        "COM",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var station = string.IsNullOrWhiteSpace(communication.Station)
                    ? "SayIntentions ATC"
                    : communication.Station;
                var frequency = string.IsNullOrWhiteSpace(communication.Frequency)
                    ? ""
                    : $" {communication.Frequency}";
                // SayIntentions names these fields from the ATC station's
                // perspective: outgoing_message is ATC -> aircraft, while
                // incoming_message is aircraft -> ATC.
                if (change.IncomingChanged)
                {
                    var speaker = communication.IsCopilot ? "F/O" : "Pilot";
                    AppendDashboardLog(
                        $"{speaker} -> ATC [{station}{frequency}]: "
                        + communication.IncomingMessage.Trim());
                }
                if (change.OutgoingChanged)
                {
                    AppendDashboardLog(
                        $"ATC -> F/O [{station}{frequency}]: "
                        + communication.OutgoingMessage.Trim());
                    CaptureSayIntentionsPushbackDirection(communication);
                    CaptureSayIntentionsApproach(communication);
                }

                if (change.OutgoingChanged || change.IncomingChanged)
                {
                    await TryCompletePendingSayIntentionsAtcStepAsync(
                        flight,
                        communication,
                        cancellationToken);
                }
            }

            await TryCompleteCurrentSayIntentionsAtcStepFromHistoryAsync(
                flight,
                communications,
                cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or InvalidOperationException
                                   or ArgumentException)
        {
            AppLog.Write(
                $"SayIntentions communication-history refresh failed: {ex.Message}");
        }
    }

    private void InitializePmdg777Sdk(SimConnect sender)
    {
        if (_pmdg777SdkInitialized)
        {
            return;
        }

        try
        {
            SimConnectRegistrationService.RegisterPmdg777(sender);
            _pmdg777SdkInitialized = true;
            AppLog.Write("PMDG 777X SDK data and control connections initialized.");
        }
        catch (Exception ex)
        {
            AppLog.Write($"PMDG 777X SDK initialization failed: {ex.Message}");
        }
    }

    private void SetPmdg777BatteryOn()
    {
        if (Connection == null
            || _state?.IsPmdg777300Er != true
            || !_pmdg777SdkInitialized
            || !_pmdg777Runtime.DataReady)
        {
            _procedureRunner.Fail("PMDG 777 battery command blocked: verified 777X data or the SDK control mapping is not ready.");
            AppLog.Write("PMDG 777 battery command blocked: published 777X data or SDK control mapping not ready.");
            return;
        }

        if (_state.Pmdg777BatteryOn)
        {
            AppLog.Write("PMDG 777 battery already ON; no command sent.");
            return;
        }

        if (_pmdg777Runtime.ControlState.Event != 0)
        {
            _procedureRunner.Fail($"PMDG 777 battery command blocked: SDK control event {_pmdg777Runtime.ControlState.Event} is pending.");
            return;
        }

        var command = new Pmdg777Control
        {
            Event = Pmdg777ControlProfile.BatterySwitchEvent,
            Parameter = 1
        };
        Connection.SetClientData(
            ClientDataArea.Pmdg777Control,
            ClientDataDefinition.Pmdg777Control,
            SIMCONNECT_CLIENT_DATA_SET_FLAG.DEFAULT,
            0,
            command);
        _pmdg777Runtime.SetPendingControl(command);
        AppLog.Write("PMDG 777 FO action sent: BATTERY switch ON; awaiting independent 777X data readback.");
    }

    private void SetPmdg777PrimaryExternalPowerOn()
    {
        if (_state?.Pmdg777PrimaryExternalPowerOn == true)
        {
            return;
        }
        if (_state?.Pmdg777PrimaryExternalPowerAvailable != true)
        {
            AppLog.Write("PMDG 777 primary external power command deferred: waiting for AVAIL.");
            return;
        }

        SendPmdg777Control(
            Pmdg777ControlProfile.PrimaryExternalPowerSwitchEvent,
            Pmdg777ControlProfile.MouseLeftSingle,
            "PRIMARY EXTERNAL POWER switch PUSH");
    }

    private void SetPmdg777SecondaryExternalPowerOn()
    {
        if (_state?.Pmdg777SecondaryExternalPowerOn == true
            || _state?.Pmdg777SecondaryExternalPowerAvailable != true)
        {
            return;
        }

        SendPmdg777Control(
            Pmdg777ControlProfile.SecondaryExternalPowerSwitchEvent,
            Pmdg777ControlProfile.MouseLeftSingle,
            "SECONDARY EXTERNAL POWER switch PUSH");
    }

    private void SetPmdg777AdiruOn()
    {
        if (_state?.Pmdg777AdiruOn == true)
        {
            return;
        }

        var offDuration = _pmdg777Runtime.AdiruOffSinceUtc.HasValue
            ? DateTime.UtcNow - _pmdg777Runtime.AdiruOffSinceUtc.Value
            : TimeSpan.Zero;
        var remaining = TimeSpan.FromSeconds(30) - offDuration;
        if (remaining > TimeSpan.Zero)
        {
            if (_pmdg777AdiruOnTimer != null)
            {
                _automation.Complete(_pmdg777AdiruOnTimer);
            }
            _pmdg777AdiruOnTimer = _automation.Schedule(
                Math.Max(100, (int)Math.Ceiling(remaining.TotalMilliseconds)),
                () =>
                {
                    _pmdg777AdiruOnTimer = null;
                    SendPmdg777Control(
                        Pmdg777ControlProfile.AdiruSwitchEvent,
                        1,
                        "ADIRU switch ON after 30 seconds OFF");
                },
                "PMDG 777 ADIRU ON",
                AircraftVariant.Pmdg777300Er);
            AppLog.Write($"PMDG 777 ADIRU remains OFF for the SOP interval; ON command scheduled in {remaining.TotalSeconds:0.0} seconds.");
            return;
        }

        SendPmdg777Control(
            Pmdg777ControlProfile.AdiruSwitchEvent,
            1,
            "ADIRU switch ON after 30 seconds OFF");
    }

    private void SetPmdg777IfePassengerSeatsOn()
    {
        if (_state?.Pmdg777IfePassengerSeatsOn == true)
        {
            return;
        }

        SendPmdg777Control(
            Pmdg777ControlProfile.IfePassengerSeatsSwitchEvent,
            1,
            "IFE/PASSENGER SEATS power switch ON");
    }

    private void SetPmdg777CabinUtilityOn()
    {
        if (_state?.Pmdg777CabinUtilityOn == true)
        {
            return;
        }

        SendPmdg777Control(
            Pmdg777ControlProfile.CabinUtilitySwitchEvent,
            1,
            "CABIN/UTILITY power switch ON");
    }

    private void SetPmdg777EmergencyLightsArmed()
    {
        if (_state?.Pmdg777EmergencyLightsArmed == true)
        {
            return;
        }

        SendPmdg777Control(
            Pmdg777ControlProfile.EmergencyLightsSwitchEvent,
            1,
            "EMERGENCY LIGHTS selector ARMED");
    }

    private void SetPmdg777EmergencyLightsGuardClosed()
    {
        if (_state?.Pmdg777EmergencyLightsGuardClosed == true)
        {
            return;
        }

        SendPmdg777Control(
            Pmdg777ControlProfile.EmergencyLightsGuardEvent,
            Pmdg777ControlProfile.MouseLeftSingle,
            "EMERGENCY LIGHTS guard CLOSED");
    }

    private void SetPmdg777NavigationLightOn()
    {
        if (_state?.Pmdg777NavigationLightOn == true)
        {
            return;
        }

        SendPmdg777Control(
            Pmdg777ControlProfile.NavigationLightSwitchEvent,
            1,
            "NAVIGATION light switch ON");
    }

    private void SetPmdg777SwitchOn(bool alreadySet, uint eventId, string label)
    {
        if (alreadySet)
        {
            return;
        }

        SendPmdg777Control(eventId, 1, label);
    }

    private void SetPmdg777GuardClosed(bool alreadyClosed, uint eventId, string label)
    {
        if (alreadyClosed)
        {
            return;
        }

        SendPmdg777Control(eventId, Pmdg777ControlProfile.MouseLeftSingle, label);
    }

    private void ConfigurePmdg777EngineFuelFirePreflight()
    {
        QueuePmdg777Controls(
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 84, 0, "APU fire handle IN"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 85, 0, "forward cargo fire ARM OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 86, 0, "aft cargo fire ARM OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 90, 1, "left EEC NORM"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 92, 1, "right EEC NORM"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 94, 1, "left START/IGNITION NORM"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 95, 1, "right START/IGNITION NORM"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 96, 1, "AUTOSTART ON"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 97, 0, "left fuel-jettison nozzle OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 99, 0, "right fuel-jettison nozzle OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 102, 0, "fuel-jettison ARM OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 103, 0, "left forward fuel pump OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 104, 0, "right forward fuel pump OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 105, 0, "left aft fuel pump OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 106, 0, "right aft fuel pump OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 107, 0, "forward crossfeed OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 108, 0, "aft crossfeed OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 109, 0, "left center fuel pump OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 110, 0, "right center fuel pump OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 101, 1, "fuel-to-remain selector IN"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 1011, 0, "fuel-to-remain selector pushed IN"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 111, 1, "wing anti-ice AUTO"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 112, 1, "left engine anti-ice AUTO"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 113, 1, "right engine anti-ice AUTO"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 651, 0, "left engine fire handle IN"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 652, 0, "right engine fire handle IN"));
    }

    private void ConfigurePmdg777ElectricalHydraulicPreflight()
    {
        var controls = new List<(uint EventId, uint Parameter, string Label)>
        {
            (Pmdg777ControlProfile.IfePassengerSeatsSwitchEvent, 1, "IFE/PASSENGER SEATS power ON"),
            (Pmdg777ControlProfile.CabinUtilitySwitchEvent, 1, "CABIN/UTILITY power ON"),
            (Pmdg777ControlProfile.EmergencyLightsSwitchEvent, 1, "EMERGENCY LIGHTS selector ARMED"),
            (Pmdg777ControlProfile.NavigationLightSwitchEvent, 1, "NAVIGATION light ON"),
            (Pmdg777ControlProfile.ThrustAsymmetryCompensationEvent, 1, "thrust-asymmetry compensation AUTO"),
            (Pmdg777ControlProfile.PrimaryFlightComputersEvent, 1, "PRIMARY FLIGHT COMPUTERS AUTO"),
            (Pmdg777ControlProfile.ApuGeneratorSwitchEvent, 1, "APU GENERATOR switch ON"),
            (Pmdg777ControlProfile.EngineGeneratorOneSwitchEvent, 1, "left GENERATOR switch ON"),
            (Pmdg777ControlProfile.EngineGeneratorTwoSwitchEvent, 1, "right GENERATOR switch ON"),
            (Pmdg777ControlProfile.BackupGeneratorOneSwitchEvent, 1, "left BACKUP GENERATOR switch ON"),
            (Pmdg777ControlProfile.BackupGeneratorTwoSwitchEvent, 1, "right BACKUP GENERATOR switch ON"),
            (Pmdg777ControlProfile.LeftSideWindowHeatEvent, 1, "left side WINDOW HEAT ON"),
            (Pmdg777ControlProfile.LeftForwardWindowHeatEvent, 1, "left forward WINDOW HEAT ON"),
            (Pmdg777ControlProfile.RightForwardWindowHeatEvent, 1, "right forward WINDOW HEAT ON"),
            (Pmdg777ControlProfile.RightSideWindowHeatEvent, 1, "right side WINDOW HEAT ON"),
            (Pmdg777ControlProfile.LeftEnginePrimaryHydraulicPumpEvent, 1, "left engine PRIMARY hydraulic pump ON"),
            (Pmdg777ControlProfile.RightEnginePrimaryHydraulicPumpEvent, 1, "right engine PRIMARY hydraulic pump ON")
        };
        if (_state?.Pmdg777PrimaryFlightComputersGuardClosed != true)
        {
            controls.Add((Pmdg777ControlProfile.PrimaryFlightComputersGuardEvent, Pmdg777ControlProfile.MouseLeftSingle, "PRIMARY FLIGHT COMPUTERS guard CLOSED"));
        }
        if (_state?.Pmdg777EmergencyLightsGuardClosed != true)
        {
            controls.Add((Pmdg777ControlProfile.EmergencyLightsGuardEvent, Pmdg777ControlProfile.MouseLeftSingle, "EMERGENCY LIGHTS guard CLOSED"));
        }
        if (_state?.Pmdg777PassengerOxygenGuardClosed != true)
        {
            controls.Add((Pmdg777ControlProfile.PassengerOxygenGuardEvent, Pmdg777ControlProfile.MouseLeftSingle, "PASSENGER OXYGEN guard CLOSED"));
        }
        QueuePmdg777Controls(controls.ToArray());
    }

    private void ConfigurePmdg777ExteriorLightsPreflight()
    {
        QueuePmdg777Controls(
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 22, 0, "left landing light OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 23, 0, "nose landing light OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 24, 0, "right landing light OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 114, 0, "beacon OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 115, 1, "navigation light ON"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 119, 0, "left runway-turnoff light OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 120, 0, "right runway-turnoff light OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 121, 0, "taxi light OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 122, 0, "strobe light OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 29, 1, "no-smoking selector AUTO"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 30, 0, "seat-belt selector OFF"));
    }

    private void DisconnectPmdg777ExternalPower()
    {
        var controls = new List<(uint EventId, uint Parameter, string Label)>();
        if (_state?.Pmdg777PrimaryExternalPowerOn == true)
        {
            controls.Add((Pmdg777ControlProfile.PrimaryExternalPowerSwitchEvent,
                Pmdg777ControlProfile.MouseLeftSingle,
                "primary external power OFF"));
        }
        if (_state?.Pmdg777SecondaryExternalPowerOn == true)
        {
            controls.Add((Pmdg777ControlProfile.SecondaryExternalPowerSwitchEvent,
                Pmdg777ControlProfile.MouseLeftSingle,
                "secondary external power OFF"));
        }

        if (controls.Count > 0)
        {
            QueuePmdg777Controls(controls.ToArray());
        }
    }

    private void ConfigurePmdg777HydraulicsBeforeStart()
    {
        QueuePmdg777Controls(
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 38, 1, "right electric demand pump AUTO"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 40, 1, "center 1 electric primary pump ON"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 41, 1, "center 2 electric primary pump ON"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 35, 1, "left electric demand pump AUTO"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 36, 1, "center 1 air demand pump AUTO"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 37, 1, "center 2 air demand pump AUTO"));
    }

    private void ConfigurePmdg777FuelPumpsBeforeStart()
    {
        var centerPosition = _state?.Pmdg777CenterFuelPumpsRequired == true ? 1u : 0u;
        QueuePmdg777Controls(
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 103, 1, "left forward fuel pump ON"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 104, 1, "right forward fuel pump ON"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 105, 1, "left aft fuel pump ON"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 106, 1, "right aft fuel pump ON"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 109, centerPosition, $"left center fuel pump {(centerPosition == 1 ? "ON" : "OFF")}"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 110, centerPosition, $"right center fuel pump {(centerPosition == 1 ? "ON" : "OFF")}"));
    }

    private void SetPmdg777TakeoffFlaps()
    {
        var setting = _state?.Pmdg777FmcTakeoffFlaps ?? 0;
        var detentEvent = setting switch
        {
            1 => Pmdg777ControlProfile.FlapsOneEvent,
            5 => Pmdg777ControlProfile.FlapsFiveEvent,
            15 => Pmdg777ControlProfile.FlapsFifteenEvent,
            20 => Pmdg777ControlProfile.FlapsTwentyEvent,
            25 => Pmdg777ControlProfile.FlapsTwentyFiveEvent,
            30 => Pmdg777ControlProfile.FlapsThirtyEvent,
            _ => uint.MaxValue
        };
        if (detentEvent == uint.MaxValue)
        {
            _procedureRunner.Fail(
                "PMDG TAKEOFF REF does not contain a supported takeoff flap setting.");
            FinishOneShot(3);
            return;
        }

        QueuePmdg777Controls(
            (detentEvent,
                Pmdg777ControlProfile.FlapsPresetParameter,
                $"flaps {setting}"));
    }

    private void SetPmdg777LandingFlaps()
    {
        var setting = _state?.Pmdg777FmcLandingFlaps ?? 0;
        var detentEvent = setting switch
        {
            20 => Pmdg777ControlProfile.FlapsTwentyEvent,
            25 => Pmdg777ControlProfile.FlapsTwentyFiveEvent,
            30 => Pmdg777ControlProfile.FlapsThirtyEvent,
            _ => uint.MaxValue
        };
        var targetLever = setting switch
        {
            20 => 4,
            25 => 5,
            30 => 6,
            _ => -1
        };
        if (detentEvent == uint.MaxValue)
        {
            _procedureRunner.Fail(
                "PMDG APPROACH REF does not contain a supported landing flap setting.");
            FinishOneShot(3);
            return;
        }

        if (Pmdg777ControlProfile.ApproachFlapCommandWouldRetract(
                _state?.Pmdg777FlapsLever ?? 0,
                targetLever))
        {
            AppLog.Write(
                $"PMDG 777 landing flaps {setting} command inhibited: current flap lever is already beyond the requested detent.");
            return;
        }

        QueuePmdg777Controls(
            (detentEvent,
                Pmdg777ControlProfile.FlapsPresetParameter,
                $"landing flaps {setting}"));
    }

    private void SetPmdg777ApproachFlaps(
        int targetLever,
        uint detentEvent,
        string label)
    {
        var currentLever = _state?.Pmdg777FlapsLever ?? 0;
        if (Pmdg777ControlProfile.ApproachFlapCommandWouldRetract(
                currentLever,
                targetLever))
        {
            AppLog.Write(
                $"PMDG 777 {label} command inhibited: current flap lever {currentLever} is already beyond target {targetLever}.");
            return;
        }

        QueuePmdg777Controls(
            (detentEvent,
                Pmdg777ControlProfile.FlapsPresetParameter,
                label));
    }

    private void SetPmdg777Gear(bool down)
    {
        if (Connection == null
            || _state?.IsPmdg777300Er != true
            || !_pmdg777Runtime.DataReady)
        {
            _procedureRunner.Fail(
                $"PMDG 777 landing gear {(down ? "DOWN" : "UP")} blocked: verified 777X data is not ready.");
            return;
        }

        if (down ? _state.Pmdg777GearLeverDown : _state.Pmdg777GearLeverUp)
        {
            return;
        }

        var action = down
            ? Pmdg777ControlProfile.RotorBrakeWheelDownAction
            : Pmdg777ControlProfile.RotorBrakeWheelUpAction;
        SendPmdgRotorBrakeSwitch(
            Pmdg777ControlProfile.GearLeverRotorBrakeSwitchId,
            action);
        SchedulePmdgRotorBrakeSwitch(
            Pmdg777ControlProfile.GearLeverRotorBrakeSwitchId,
            action,
            500);
        SchedulePmdgRotorBrakeSwitch(
            Pmdg777ControlProfile.GearLeverRotorBrakeSwitchId,
            action,
            1000);
        AppLog.Write(
            $"PMDG 777 FO action sent through exact gear-lever ROTOR_BRAKE path: landing gear {(down ? "DOWN" : "UP")}; awaiting independent 777X data readback.");
    }

    private void SetPmdg777SpeedbrakeArmed()
    {
        if (Connection == null
            || _state?.IsPmdg777300Er != true
            || !_pmdg777Runtime.DataReady)
        {
            _procedureRunner.Fail(
                "PMDG 777 speedbrake ARMED blocked: verified 777X data is not ready.");
            return;
        }

        if (_state.Pmdg777SpeedbrakeArmed)
        {
            return;
        }

        SendPmdgRotorBrakeSwitch(
            Pmdg777ControlProfile.SpeedbrakeArmRotorBrakeSwitchId,
            Pmdg777ControlProfile.RotorBrakeLeftSingleAction);
        SchedulePmdgRotorBrakeSwitch(
            Pmdg777ControlProfile.SpeedbrakeArmRotorBrakeSwitchId,
            Pmdg777ControlProfile.RotorBrakeLeftSingleAction,
            500);
        SchedulePmdgRotorBrakeSwitch(
            Pmdg777ControlProfile.SpeedbrakeArmRotorBrakeSwitchId,
            Pmdg777ControlProfile.RotorBrakeLeftSingleAction,
            1000);
        AppLog.Write(
            "PMDG 777 FO action sent through exact speedbrake-ARM ROTOR_BRAKE path; awaiting independent 777X data readback.");
    }

    private void ConfigurePmdg777ShutdownPumps()
    {
        QueuePmdg777Controls(
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 103, 0, "left forward fuel pump OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 104, 0, "right forward fuel pump OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 105, 0, "left aft fuel pump OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 106, 0, "right aft fuel pump OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 109, 0, "left center fuel pump OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 110, 0, "right center fuel pump OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 40, 0, "center 1 electric primary pump OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 41, 0, "center 2 electric primary pump OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 35, 0, "left electric demand pump OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 38, 0, "right electric demand pump OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 36, 0, "center 1 air demand pump OFF"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 37, 0, "center 2 air demand pump OFF"));
    }

    private void ConfigurePmdg777AirPanelPreflight()
    {
        QueuePmdg777Controls(
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 129, 1, "left engine bleed AUTO"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 130, 1, "right engine bleed AUTO"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 131, 1, "APU bleed AUTO"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 132, 1, "left isolation valve AUTO"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 133, 1, "center isolation valve AUTO"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 134, 1, "right isolation valve AUTO"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 135, 1, "left pack AUTO"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 136, 1, "right pack AUTO"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 137, 1, "left trim air ON"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 138, 1, "right trim air ON"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 142, 1, "upper recirculation fan ON"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 143, 1, "lower recirculation fan ON"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 144, 1, "equipment cooling AUTO"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 139, 30, "flight-deck temperature AUTO midpoint"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 140, 30, "cabin temperature midpoint"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 1261, 0, "landing-altitude selector IN"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 127, 1, "forward outflow valve AUTO"),
            (Pmdg777ControlProfile.ThirdPartyEventIdMinimum + 128, 1, "aft outflow valve AUTO"));
    }

    private void QueuePmdg777Controls(params (uint EventId, uint Parameter, string Label)[] controls)
    {
        if (_pmdg777ControlQueue.Count > 0)
        {
            _procedureRunner.Fail("PMDG 777 control sequence blocked: another FO sequence is still active.");
            return;
        }

        foreach (var control in controls)
        {
            _pmdg777ControlQueue.Enqueue(control);
        }

        if (_pmdg777ControlQueueTimer != null)
        {
            _automation.Complete(_pmdg777ControlQueueTimer);
        }
        _pmdg777ControlQueueTimer = new System.Windows.Forms.Timer
        {
            Interval = Pmdg777ControlProfile.HumanControlIntervalMilliseconds
        };
        _pmdg777ControlQueueTimer.Tick += (_, _) => SendNextPmdg777QueuedControl();
        _pmdg777ControlQueueAction = _automation.Track(_pmdg777ControlQueueTimer);
        _pmdg777ControlQueueTimer.Start();
        SendNextPmdg777QueuedControl();
    }

    private void SendNextPmdg777QueuedControl()
    {
        if (_pmdg777ControlQueueAction?.IsCurrent != true
            || Connection == null
            || _state?.Variant != AircraftVariant.Pmdg777300Er)
        {
            _pmdg777ControlQueue.Clear();
            if (_pmdg777ControlQueueTimer != null)
            {
                _automation.Complete(_pmdg777ControlQueueTimer);
                _pmdg777ControlQueueTimer = null;
            }
            _pmdg777ControlQueueAction = null;
            FinishOneShot(4);
            return;
        }
        if (_pmdg777Runtime.ControlState.Event != 0)
        {
            return;
        }

        if (_pmdg777ControlQueue.Count == 0)
        {
            if (_pmdg777ControlQueueTimer != null)
            {
                _automation.Complete(_pmdg777ControlQueueTimer);
            }
            _pmdg777ControlQueueTimer = null;
            _pmdg777ControlQueueAction = null;
            FinishOneShot();
            return;
        }

        var control = _pmdg777ControlQueue.Dequeue();
        SendPmdg777Control(control.EventId, control.Parameter, control.Label);
    }

    private void SetPmdg777TransponderStandby()
    {
        if (_state?.Pmdg777TransponderStandby == true)
        {
            return;
        }

        SendPmdg777Control(
            Pmdg777ControlProfile.TransponderModeSelectorEvent,
            0,
            "transponder mode selector STBY");
    }

    private void SetPmdg777FirstOfficerFlightDirectorOn()
    {
        if (_state?.Pmdg777FirstOfficerFlightDirectorOn == true)
        {
            return;
        }

        SendPmdg777Control(
            Pmdg777ControlProfile.FirstOfficerFlightDirectorSwitchEvent,
            1,
            "FIRST OFFICER FLIGHT DIRECTOR switch ON");
    }

    private void SendPmdg777Control(uint eventId, uint parameter, string label)
    {
        if (Connection == null
            || _state?.IsPmdg777300Er != true
            || !_pmdg777SdkInitialized
            || !_pmdg777Runtime.DataReady)
        {
            _procedureRunner.Fail($"PMDG 777 {label} blocked: verified SDK data or control mapping is not ready.");
            return;
        }
        if (_pmdg777Runtime.ControlState.Event != 0)
        {
            _procedureRunner.Fail($"PMDG 777 {label} blocked: SDK control event {_pmdg777Runtime.ControlState.Event} is still pending.");
            return;
        }

        var command = new Pmdg777Control { Event = eventId, Parameter = parameter };
        Connection.SetClientData(
            ClientDataArea.Pmdg777Control,
            ClientDataDefinition.Pmdg777Control,
            SIMCONNECT_CLIENT_DATA_SET_FLAG.DEFAULT,
            0,
            command);
        _pmdg777Runtime.SetPendingControl(command);
        AppLog.Write($"PMDG 777 FO action sent: {label}; awaiting independent 777X data readback.");
    }

    private void SetA310BatteriesAuto()
    {
        if (Connection == null || _state?.IsIniBuildsA310 != true)
        {
            AppendDashboardLog("A310 battery command blocked: A310 aircraft state is unavailable.");
            FinishOneShot(3);
            return;
        }
        if (!_state.OnGround || !_state.EnginesOff || !_mobiFlightSession.AdapterReady)
        {
            AppendDashboardLog("A310 battery command blocked: requires the A310 on the ground, engines off, with its adapter connected.");
            FinishOneShot(4);
            return;
        }

        SendA310BatteryAutoCommand(1);
        ScheduleA310BatteryAutoCommand(2, 900);
        ScheduleA310BatteryAutoCommand(3, 1800);
        AppLog.Write(
            "A310 BAT 1/2/3 AUTO sequence started with 0.9-second spacing; awaiting three independent native readbacks.");
        AppendDashboardLog(
            "A310 BAT 1, BAT 2 and BAT 3 AUTO command sent; awaiting readback.");
        FinishOneShot();
    }

    private void SendA310BatteryAutoCommand(int batteryNumber)
    {
        SendMobiFlightCommand(
            $"MF.SimVars.Set.{A310ControlProfile.BatteryAutoCalculatorCode(batteryNumber)}");
        SendMobiFlightCommand("MF.DummyCmd");
        AppLog.Write($"A310 BAT {batteryNumber} AUTO command sent.");
    }

    private void ScheduleA310BatteryAutoCommand(int batteryNumber, int delayMs)
    {
        _automation.Schedule(
            delayMs,
            () => SendA310BatteryAutoCommand(batteryNumber),
            $"A310 battery {batteryNumber} AUTO",
            AircraftVariant.IniBuildsA310);
    }

    private void SetA310WipersAndWeatherRadarOff()
    {
        if (Connection == null || _state?.IsIniBuildsA310 != true || !_mobiFlightSession.AdapterReady)
        {
            AppendDashboardLog("A310 wiper/radar command blocked: A310 adapter is unavailable.");
            FinishOneShot(3);
            return;
        }

        SendA310ControlValue(A310ControlProfile.CaptainWiperState, 0, "captain wiper OFF");
        ScheduleA310ControlValue(
            A310ControlProfile.FirstOfficerWiperState,
            0,
            "first-officer wiper OFF",
            900);
        ScheduleA310ControlValue(
            A310ControlProfile.WeatherRadarSystemState,
            1,
            "weather radar system OFF",
            1800);
        AppendDashboardLog("A310 wipers and weather radar OFF sequence sent; awaiting native readback.");
        FinishOneShot();
    }

    private void RunA310ApuFireTest()
    {
        _nativeRuntime.ResetA310ApuFireObservation();
        SendA310ControlValue(A310ControlProfile.ApuFireTestState, 1, "APU SQUIB test pressed");
        ScheduleA310ControlValue(A310ControlProfile.ApuFireTestState, 0, "APU SQUIB test released", 1800);
        ScheduleA310ControlValue(A310ControlProfile.ApuLoopTestSwitchState, 1, "APU LOOP A test selected", 2800);
        ScheduleA310ControlValue(A310ControlProfile.ApuLoopTestSwitchState, 0, "APU LOOP A test released", 4600);
        ScheduleA310ControlValue(A310ControlProfile.ApuLoopTestSwitchState, -1, "APU LOOP B test selected", 5600);
        ScheduleA310ControlValue(A310ControlProfile.ApuLoopTestSwitchState, 0, "APU LOOP B test released", 7400);
        AppendDashboardLog("A310 APU SQUIB and LOOP A/B test sequence started; awaiting live test readback.");
        FinishOneShot();
    }

    private void SetA310IrsNav()
    {
        SendA310ControlValue(A310ControlProfile.Irs1State, 1, "IRS 1 NAV");
        ScheduleA310ControlValue(A310ControlProfile.Irs2State, 1, "IRS 2 NAV", 900);
        ScheduleA310ControlValue(A310ControlProfile.Irs3State, 1, "IRS 3 NAV", 1800);
        AppendDashboardLog("A310 IRS 1, 2 and 3 NAV sequence sent; awaiting native readback.");
        FinishOneShot();
    }

    private void RunA310AnnunciatorTest()
    {
        _nativeRuntime.ResetA310AnnunciatorObservation();
        SendA310ControlValue(A310ControlProfile.AnnunciatorLightTestState, 1, "annunciator light test pressed");
        ScheduleA310ControlValue(A310ControlProfile.AnnunciatorLightTestState, 0, "annunciator light test released", 2200);
        AppendDashboardLog("A310 annunciator light test started; awaiting live test readback.");
        FinishOneShot();
    }

    private void SetA310InitialExteriorLights()
    {
        var settings = new (string State, int Value, string Label)[]
        {
            (A310ControlProfile.TaxiLightState, 2, "nose light OFF"),
            (A310ControlProfile.LeftLandingLightState, 2, "left landing light RETRACT"),
            (A310ControlProfile.RightLandingLightState, 2, "right landing light RETRACT"),
            (A310ControlProfile.WingLightState, 0, "wing light OFF"),
            (A310ControlProfile.BeaconLightState, 0, "beacon OFF"),
            (A310ControlProfile.LeftRunwayTurnoffLightState, 0, "left runway-turnoff light OFF"),
            (A310ControlProfile.RightRunwayTurnoffLightState, 0, "right runway-turnoff light OFF"),
            (A310ControlProfile.NavLogoLightState, 1, "NAV/LOGO 1")
        };

        for (var index = 0; index < settings.Length; index++)
        {
            var setting = settings[index];
            if (index == 0)
            {
                SendA310ControlValue(setting.State, setting.Value, setting.Label);
            }
            else
            {
                ScheduleA310ControlValue(
                    setting.State,
                    setting.Value,
                    setting.Label,
                    index * 700);
            }
        }

        // The A310's AUTO strobe detent is represented by the shared iniBuilds
        // AUTO flag while the actual strobe circuit remains off on the ground.
        ScheduleA310CalculatorCode(
            "1 (>L:STROBE_0_AUTO) 0 (>K:STROBES_SET)",
            "strobe selector AUTO",
            settings.Length * 700);
        AppendDashboardLog("A310 initial exterior-light flow started with human-paced selector movement.");
        FinishOneShot();
    }

    private void SetA310PreflightSigns()
    {
        RunA310Sequence(
            new[]
            {
                (A310ControlProfile.NoSmokingState, 1, "no-smoking selector AUTO"),
                (A310ControlProfile.SeatbeltsState, 1, "seat-belt signs ON")
            },
            900,
            "A310 preflight signs sequence sent; awaiting native readback.");
    }

    private void SetA310AutoflightComputers()
    {
        RunA310Sequence(
            new[]
            {
                (A310ControlProfile.AtsMaster1State, 1, "ATS 1 ON"),
                (A310ControlProfile.AtsMaster2State, 1, "ATS 2 ON"),
                (A310ControlProfile.PitchTrim1State, 1, "pitch-trim computer 1 ON"),
                (A310ControlProfile.PitchTrim2State, 1, "pitch-trim computer 2 ON"),
                (A310ControlProfile.YawDamper1State, 1, "yaw damper 1 ON"),
                (A310ControlProfile.YawDamper2State, 1, "yaw damper 2 ON")
            },
            700,
            "A310 ATS, pitch-trim and yaw-damper sequence sent; awaiting native readback.");
    }

    private void SetA310PreflightHeat()
    {
        RunA310Sequence(
            new[]
            {
                (A310ControlProfile.WindowHeat1State, 1, "window heat 1 ON"),
                (A310ControlProfile.WindowHeat2State, 1, "window heat 2 ON"),
                (A310ControlProfile.WindowHeat3State, 1, "window heat 3 ON"),
                (A310ControlProfile.WindowHeat4State, 1, "window heat 4 ON"),
                (A310ControlProfile.ProbeHeatCaptainState, 1, "captain probe heat ON"),
                (A310ControlProfile.ProbeHeatFirstOfficerState, 1, "first-officer probe heat ON"),
                (A310ControlProfile.ProbeHeatStandbyState, 1, "standby probe heat ON")
            },
            650,
            "A310 window and probe heat sequence sent; awaiting native readback.");
    }

    private void RunA310CargoSmokeTest()
    {
        _nativeRuntime.ResetA310CargoSmokeObservation();
        SendA310ControlValue(A310ControlProfile.CargoSmokeTestState, 1, "cargo-smoke loop test pressed");
        ScheduleA310ControlValue(A310ControlProfile.CargoSmokeTestState, 0, "cargo-smoke loop test released", 2200);
        AppendDashboardLog("A310 cargo-smoke test started; awaiting live test readback.");
        FinishOneShot();
    }

    private void RunA310EgpwsTest()
    {
        _nativeRuntime.ResetA310EgpwsObservation();
        SendA310ControlValue(A310ControlProfile.EgpwsTestState, 1, "EGPWS test pressed");
        ScheduleA310ControlValue(A310ControlProfile.EgpwsTestState, 0, "EGPWS test released", 3000);
        AppendDashboardLog("A310 EGPWS test started; awaiting live test readback.");
        FinishOneShot();
    }

    private void SetA310PreflightPedestal()
    {
        RunA310Sequence(
            new[]
            {
                (A310ControlProfile.AutobrakeState, 0, "autobrake deselected"),
                (A310ControlProfile.TcasPedestalModeState, 0, "TCAS preflight/standby"),
                (A310ControlProfile.WeatherRadarSystemState, 1, "weather-radar system OFF"),
                (A310ControlProfile.RudderTrimResetState, 1, "rudder-trim reset pressed"),
                (A310ControlProfile.RudderTrimResetState, 0, "rudder-trim reset released")
            },
            800,
            "A310 preflight pedestal configuration sent; awaiting native readback.");
    }

    private void SetA310FuelPumpsOn()
    {
        RunA310Sequence(
            A310ControlProfile.FuelPumpStates
                .Select((stateName, index) =>
                    (stateName, 1, $"tank fuel pump {index + 1} ON"))
                .ToArray(),
            650,
            "A310 tank fuel pumps are being selected ON sequentially; awaiting native readback.");
    }

    private void SetA310ApuOff()
    {
        // MSFS 2024's A310 cockpit model may accept the LVar write without
        // actuating the guarded pushbutton. Mirror the proven startup path by
        // sending the corresponding native InputEvents as well.
        ScheduleInputEvent(A310ApuBleedInputEventHash, 0.0, 50);
        ScheduleInputEvent(A310ApuMasterInputEventHash, 0.0, 1050);
        RunA310Sequence(
            new[]
            {
                (A310ControlProfile.ApuBleedState, 0, "APU bleed OFF"),
                (A310ControlProfile.ApuMasterState, 0, "APU master OFF")
            },
            1000,
            "A310 APU bleed and master shutdown sent; awaiting native readback.");
    }

    private void ArmA310Speedbrake()
    {
        SendMobiFlightCommand(
            "MF.SimVars.Set.1 (>K:SPOILERS_ARM_SET) " +
            "1 (>L:A310_SPOILERS_ARMED, Bool)");
        SendMobiFlightCommand("MF.DummyCmd");
        AppendDashboardLog("A310 speedbrake ARM command sent; awaiting simulator readback.");
        FinishOneShot();
    }

    private void ResetA310RudderTrim()
    {
        SendA310ControlValue(A310ControlProfile.RudderTrimResetState, 1, "rudder-trim reset pressed");
        ScheduleA310ControlValue(A310ControlProfile.RudderTrimResetState, 0, "rudder-trim reset released", 700);
        AppendDashboardLog("A310 rudder trim reset; awaiting zero readback.");
        FinishOneShot();
    }

    private void SetA310TakeoffFlaps15Zero()
    {
        // 15/0 is the first extension position of the A310's combined
        // slat/flap selector. Use the simulator's explicit first-detent event
        // so this cannot advance an already-extended selector another step.
        SendMobiFlightCommand("MF.SimVars.Set.(>K:FLAPS_1)");
        SendMobiFlightCommand("MF.DummyCmd");
        AppendDashboardLog(
            "A310 slat/flap selector commanded to 15/0; awaiting handle readback.");
        FinishOneShot();
    }

    private void SetA310AutobrakeMax()
    {
        SendA310ControlValue(A310ControlProfile.AutobrakeMaxCommandState, 1, "autobrake MAX pressed");
        ScheduleA310ControlValue(A310ControlProfile.AutobrakeMaxCommandState, 0, "autobrake MAX released", 700);
        AppendDashboardLog("A310 autobrake MAX selected; awaiting annunciator readback.");
        FinishOneShot();
    }

    private void SetA310TransponderAndWeatherRadar()
    {
        RunA310Sequence(
            new[]
            {
                (A310ControlProfile.TcasPedestalModeState, 1, "transponder XPDR"),
                (A310ControlProfile.WeatherRadarSystemState, 0, "weather-radar system 1"),
                (A310ControlProfile.WeatherRadarModeState, 2, "weather-radar mode WX")
            },
            700,
            "A310 transponder and weather radar taxi configuration sent; awaiting native readback.");
    }

    private void SetA310TakeoffExteriorLights()
    {
        RunA310Sequence(
            new[]
            {
                (A310ControlProfile.NavLogoLightState, 1, "NAV/LOGO 1"),
                (A310ControlProfile.BeaconLightState, 1, "beacon ON"),
                (A310ControlProfile.TaxiLightState, 0, "nose light T.O."),
                (A310ControlProfile.LeftLandingLightState, 0, "left landing light ON"),
                (A310ControlProfile.RightLandingLightState, 0, "right landing light ON"),
                (A310ControlProfile.WingLightState, 0, "wing light as required/OFF"),
                (A310ControlProfile.LeftRunwayTurnoffLightState, 1, "left runway-turnoff light ON"),
                (A310ControlProfile.RightRunwayTurnoffLightState, 1, "right runway-turnoff light ON")
            },
            650,
            "A310 takeoff exterior-light sequence sent; awaiting native selector readback.");
        ScheduleA310CalculatorCode(
            "0 (>L:STROBE_0_AUTO) 1 (>K:STROBES_SET)",
            "strobe selector ON",
            8 * 650);
    }

    private void DisarmA310Speedbrake()
    {
        SendMobiFlightCommand(
            "MF.SimVars.Set.0 (>K:SPOILERS_ARM_SET) " +
            "0 (>L:A310_SPOILERS_ARMED, Bool)");
        SendMobiFlightCommand("MF.DummyCmd");
        AppendDashboardLog("A310 speedbrake DISARM command sent; awaiting native readback.");
        FinishOneShot();
    }

    private void SetA310ClimbLights()
    {
        RunA310Sequence(
            new[]
            {
                (A310ControlProfile.TaxiLightState, 2, "nose light OFF"),
                (A310ControlProfile.LeftRunwayTurnoffLightState, 0, "left runway-turnoff light OFF"),
                (A310ControlProfile.RightRunwayTurnoffLightState, 0, "right runway-turnoff light OFF")
            },
            650,
            "A310 climb-light sequence sent; landing lights remain ON until 10,000 feet.");
    }

    private void SetA310AltimetersStandard()
    {
        RunA310Sequence(
            new[]
            {
                (A310ControlProfile.CaptainAltimeterStandardState, 1, "captain altimeter STD"),
                (A310ControlProfile.FirstOfficerAltimeterStandardState, 1, "first-officer altimeter STD"),
                (A310ControlProfile.StandbyAltimeterStandardState, 1, "standby altimeter STD")
            },
            650,
            "A310 altimeters selected STANDARD at transition altitude; awaiting simulator readback.");
    }

    private void SetA310LandingLightsRetracted()
    {
        RunA310Sequence(
            new[]
            {
                (A310ControlProfile.LeftLandingLightState, 2, "left landing light RETRACT/OFF"),
                (A310ControlProfile.RightLandingLightState, 2, "right landing light RETRACT/OFF")
            },
            900,
            "A310 landing lights commanded RETRACT/OFF above 10,000 feet.");
    }

    private void SetA310ApproachLights()
    {
        RunA310Sequence(
            new[]
            {
                (A310ControlProfile.SeatbeltsState, 1, "seat-belt signs ON"),
                (A310ControlProfile.TaxiLightState, 0, "nose light T.O."),
                (A310ControlProfile.LeftLandingLightState, 0, "left landing light ON"),
                (A310ControlProfile.RightLandingLightState, 0, "right landing light ON"),
                (A310ControlProfile.LeftRunwayTurnoffLightState, 1, "left runway-turnoff light ON"),
                (A310ControlProfile.RightRunwayTurnoffLightState, 1, "right runway-turnoff light ON")
            },
            650,
            "A310 approach signs and exterior-light sequence sent; awaiting native readback.");
    }

    private void SetA310SelectedAirspeed(int targetKnots)
    {
        if (Connection == null
            || _state?.IsIniBuildsA310 != true
            || _state.OnGround)
        {
            AppendDashboardLog(
                "A310 selected-speed command blocked: airborne A310 state is unavailable.");
            FinishOneShot(4);
            return;
        }

        if (_state.AutopilotSelectedAirspeedKnots is > 0
            && _state.AutopilotSelectedAirspeedKnots <= targetKnots + 1)
        {
            AppendDashboardLog(
                $"A310 selected speed already {_state.AutopilotSelectedAirspeedKnots:F0} kt; keeping the lower target.");
            FinishOneShot();
            return;
        }

        TransmitSystemEvent(
            CopilotEvent.SetAutopilotAirspeed,
            (uint)targetKnots,
            0);
        AppendDashboardLog(
            $"A310 selected speed commanded to {targetKnots} kt; awaiting independent autopilot target readback.");
        BeginNativeAction(
            $"A310 selected speed {targetKnots} kt",
            state => Math.Abs(
                         state.AutopilotSelectedAirspeedKnots
                         - targetKnots) <= 1,
            true,
            TimeSpan.FromSeconds(8));
    }

    private void SetA310FlapsDetent(int detent, string label)
    {
        if (_state?.IsIniBuildsA310 != true)
        {
            AppendDashboardLog("A310 slat/flap command blocked: simulator state is unavailable.");
            FinishOneShot(4);
            return;
        }

        var eventName = detent switch
        {
            0 => "FLAPS_UP",
            1 => "FLAPS_1",
            2 => "FLAPS_2",
            3 => "FLAPS_3",
            4 => "FLAPS_4",
            _ => throw new ArgumentOutOfRangeException(nameof(detent))
        };
        SendMobiFlightCommand($"MF.SimVars.Set.(>K:{eventName})");
        SendMobiFlightCommand("MF.DummyCmd");
        AppendDashboardLog($"A310 slat/flap selector commanded to {label}; awaiting handle readback.");
        FinishOneShot();
    }

    private void SetA310AfterLandingLights()
    {
        RunA310Sequence(
            new[]
            {
                (A310ControlProfile.TaxiLightState, 1, "nose light TAXI"),
                (A310ControlProfile.LeftLandingLightState, 2, "left landing light RETRACT/OFF"),
                (A310ControlProfile.RightLandingLightState, 2, "right landing light RETRACT/OFF"),
                (A310ControlProfile.WingLightState, 0, "wing light OFF"),
                (A310ControlProfile.LeftRunwayTurnoffLightState, 0, "left runway-turnoff light OFF"),
                (A310ControlProfile.RightRunwayTurnoffLightState, 0, "right runway-turnoff light OFF")
            },
            650,
            "A310 after-landing exterior-light sequence sent; awaiting native readback.");
        ScheduleA310CalculatorCode(
            "1 (>L:STROBE_0_AUTO) 0 (>K:STROBES_SET)",
            "strobe selector AUTO",
            6 * 650);
    }

    private void SetA310TransponderRadarStandby()
    {
        RunA310Sequence(
            new[]
            {
                (A310ControlProfile.TcasPedestalModeState, 0, "TCAS STBY"),
                (A310ControlProfile.WeatherRadarSystemState, 1, "weather-radar system OFF"),
                (A310ControlProfile.WeatherRadarModeState, 0, "weather-radar mode OFF")
            },
            650,
            "A310 transponder and weather radar selected STBY/OFF; awaiting native readback.");
    }

    private void SetA310FuelPumpsForParking()
    {
        var retainApuPump = _state?.ApuMasterSwitchOn == true || _state?.ApuAvailable == true;
        RunA310Sequence(
            A310ControlProfile.FuelPumpStates
                .Select((stateName, index) =>
                    (stateName, index == 3 && retainApuPump ? 1 : 0,
                        $"tank fuel pump {index + 1} {(index == 3 && retainApuPump ? "ON for APU" : "OFF")}"))
                .ToArray(),
            500,
            retainApuPump
                ? "A310 fuel pumps selected OFF sequentially; left inner tank pump 2 retained for APU operation."
                : "A310 fuel pumps selected OFF sequentially; awaiting native readback.");
    }

    private void SetA310ProbeHeatOff()
    {
        RunA310Sequence(
            new[]
            {
                (A310ControlProfile.ProbeHeatCaptainState, 0, "captain probe heat OFF"),
                (A310ControlProfile.ProbeHeatFirstOfficerState, 0, "first-officer probe heat OFF"),
                (A310ControlProfile.ProbeHeatStandbyState, 0, "standby probe heat OFF")
            },
            650,
            "A310 probe heat selected OFF sequentially; awaiting native readback.");
    }

    private void SetA310IrsOff()
    {
        RunA310Sequence(
            new[]
            {
                (A310ControlProfile.Irs1State, 0, "IRS 1 OFF"),
                (A310ControlProfile.Irs2State, 0, "IRS 2 OFF"),
                (A310ControlProfile.Irs3State, 0, "IRS 3 OFF")
            },
            900,
            "A310 IRS selectors moving OFF sequentially; awaiting native readback.");
    }

    private void SetA310ExteriorLightsOff()
    {
        RunA310Sequence(
            new[]
            {
                (A310ControlProfile.BeaconLightState, 0, "beacon OFF"),
                (A310ControlProfile.TaxiLightState, 2, "nose light OFF"),
                (A310ControlProfile.LeftLandingLightState, 2, "left landing light RETRACT/OFF"),
                (A310ControlProfile.RightLandingLightState, 2, "right landing light RETRACT/OFF"),
                (A310ControlProfile.WingLightState, 0, "wing light OFF"),
                (A310ControlProfile.LeftRunwayTurnoffLightState, 0, "left runway-turnoff light OFF"),
                (A310ControlProfile.RightRunwayTurnoffLightState, 0, "right runway-turnoff light OFF")
            },
            600,
            "A310 exterior lights selected OFF sequentially; awaiting native readback.");
        ScheduleA310CalculatorCode(
            "1 (>L:STROBE_0_AUTO) 0 (>K:STROBES_SET)",
            "strobe selector AUTO/OFF",
            7 * 600);
    }

    private void SetA310BatteriesOff()
    {
        RunA310Sequence(
            new[]
            {
                (A310ControlProfile.Battery1State, 0, "BAT 1 OFF"),
                (A310ControlProfile.Battery2State, 0, "BAT 2 OFF"),
                (A310ControlProfile.Battery3State, 0, "BAT 3 OFF")
            },
            900,
            "A310 batteries selected OFF sequentially; awaiting native readback.");
    }

    private void StartA310Apu()
    {
        if (Connection == null || _state?.IsIniBuildsA310 != true)
        {
            AppendDashboardLog("A310 APU start blocked: simulator state is unavailable.");
            FinishOneShot(4);
            return;
        }
        if (_state.ApuAvailable)
        {
            AppendDashboardLog("A310 APU already available.");
            FinishOneShot();
            return;
        }

        SendA310ControlValue(
            A310ControlProfile.LeftInnerTankPump2State,
            1,
            "left INNER TK PUMP 2 ON");
        ScheduleA310ControlValue(A310ControlProfile.ApuMasterState, 1, "APU MASTER ON", 1200);
        ScheduleInputEvent(A310ApuMasterInputEventHash, 1.0, 1200);
        ScheduleA310ControlValue(A310ControlProfile.ApuStartButtonState, 1, "APU START ON", 4500);
        ScheduleInputEvent(A310ApuStartInputEventHash, 1.0, 4500);
        ScheduleSystemEvent(CopilotEvent.StartApu, 0, 0, 4700, "A310 APU starter");
        AppendDashboardLog(
            "A310 left INNER TK PUMP 2, MASTER and START sequence sent; waiting for APU AVAILABLE.");
        FinishOneShot();
    }

    private void SetA310ApuPowerAndBleed()
    {
        if (_state?.IsIniBuildsA310 != true || !_mobiFlightSession.RuntimeReady)
        {
            AppendDashboardLog("A310 APU power/bleed blocked: native runtime state is unavailable.");
            FinishOneShot(4);
            return;
        }

        RunA310Sequence(
            new[]
            {
                (A310ControlProfile.ApuGeneratorState, 1, "APU generator ON"),
                (A310ControlProfile.ApuBleedState, 1, "APU bleed ON")
            },
            900,
            "A310 APU generator and bleed controls selected; awaiting native readback.");
    }

    private void SetA310IgnitionOff()
    {
        RunA310Sequence(
            new[]
            {
                (
                    A310ControlProfile.IgnitionSelectorState,
                    A310ControlProfile.IgnitionCrankValue,
                    "ignition selector through CRANK"),
                (
                    A310ControlProfile.IgnitionSelectorState,
                    A310ControlProfile.IgnitionOffValue,
                    "ignition selector OFF")
            },
            650,
            "A310 ignition selector rotating from START A through CRANK to OFF; awaiting native readback.");
    }

    private void RunA310Sequence(
        IReadOnlyList<(string State, int Value, string Label)> settings,
        int spacingMs,
        string dashboardMessage)
    {
        for (var index = 0; index < settings.Count; index++)
        {
            var setting = settings[index];
            if (index == 0)
            {
                SendA310ControlValue(setting.State, setting.Value, setting.Label);
            }
            else
            {
                ScheduleA310ControlValue(
                    setting.State,
                    setting.Value,
                    setting.Label,
                    index * spacingMs);
            }
        }
        AppendDashboardLog(dashboardMessage);
        FinishOneShot();
    }

    private void ScheduleA310CalculatorCode(string calculatorCode, string label, int delayMs)
    {
        _automation.Schedule(
            delayMs,
            () =>
            {
                SendMobiFlightCommand($"MF.SimVars.Set.{calculatorCode}");
                SendMobiFlightCommand("MF.DummyCmd");
                AppLog.Write($"A310 {label} command sent.");
            },
            $"A310 {label}",
            AircraftVariant.IniBuildsA310);
    }

    private void SendA310ControlValue(string stateName, int value, string label)
    {
        SendMobiFlightCommand(
            $"MF.SimVars.Set.{A310ControlProfile.SetCalculatorCode(stateName, value)}");
        SendMobiFlightCommand("MF.DummyCmd");
        AppLog.Write($"A310 {label} command sent.");
    }

    private void ScheduleA310ControlValue(
        string stateName,
        int value,
        string label,
        int delayMs)
    {
        _automation.Schedule(
            delayMs,
            () => SendA310ControlValue(stateName, value, label),
            $"A310 {label}",
            AircraftVariant.IniBuildsA310);
    }

    private void CaptureSayIntentionsArrivalStand(
        SayIntentionsFlightContext flight,
        IReadOnlyList<SayIntentionsCommunication> communications)
    {
        // SayIntentions is authoritative for the assigned arrival stand.
        // Capture its assignment even while GSX is unavailable or has not
        // yet delegated Remote Control; the pending stand can be applied as
        // soon as a compatible GSX position menu appears.
        var assignedStand = communications
            .Where(item => item.Channel.StartsWith(
                "COM",
                StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Id)
            .Select(item => GsxArrivalGateCoordinator.ExtractAssignedStand(
                item.OutgoingMessage))
            .FirstOrDefault(value => value != null);

        if (assignedStand == null
            && !string.IsNullOrWhiteSpace(flight.DestinationIcao)
            && string.Equals(
                flight.CurrentAirport,
                flight.DestinationIcao,
                StringComparison.OrdinalIgnoreCase))
        {
            assignedStand = GsxArrivalGateCoordinator.NormalizeStand(
                flight.AssignedGate);
        }

        var gsx = _gsx.Snapshot;
        if (assignedStand == null
            || string.Equals(
                assignedStand,
                gsx.SelectedArrivalStand,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                assignedStand,
                gsx.PendingArrivalStand,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _gsx.SetArrivalStandPending(assignedStand);
        AppendDashboardLog(
            $"SayIntentions assigned Gate {assignedStand}; waiting to synchronize the GSX destination.");
        AppLog.Write(
            $"Captured SayIntentions arrival stand {assignedStand} for optional GSX synchronization.");
        TryAutoSelectGsxArrivalStand();
    }

    private void CaptureRecentSayIntentionsPushbackDirection(
        IReadOnlyList<SayIntentionsCommunication> communications)
    {
        var clearance = communications
            .Where(item => SayIntentionsAtcResponseClassifier.IsRecent(
                item.TimestampUtc,
                DateTimeOffset.UtcNow,
                TimeSpan.FromMinutes(30)))
            .Where(item => SayIntentionsAtcResponseClassifier.IsClearanceResponse(
                "captain-pushback-clearance",
                item.OutgoingMessage,
                item.IncomingMessage))
            .OrderByDescending(item => item.Id)
            .FirstOrDefault();
        if (clearance != null)
        {
            CaptureSayIntentionsPushbackDirection(clearance);
        }
    }

    private void CaptureRecentSayIntentionsApproach(
        IReadOnlyList<SayIntentionsCommunication> communications)
    {
        var assignment = communications
            .Where(item => item.Channel.StartsWith(
                "COM",
                StringComparison.OrdinalIgnoreCase))
            .Where(item => SayIntentionsAtcResponseClassifier.IsRecent(
                item.TimestampUtc,
                DateTimeOffset.UtcNow,
                TimeSpan.FromHours(2)))
            .OrderByDescending(item => item.Id)
            .Select(item => SayIntentionsApproachAssignment.TryParse(
                    item.OutgoingMessage,
                    out var parsed)
                ? parsed
                : (SayIntentionsApproachAssignment?)null)
            .FirstOrDefault(item => item.HasValue);
        if (assignment.HasValue)
        {
            CaptureSayIntentionsApproach(assignment.Value);
        }
    }

    private void CaptureSayIntentionsApproach(
        SayIntentionsCommunication communication)
    {
        if (SayIntentionsApproachAssignment.TryParse(
                communication.OutgoingMessage,
                out var assignment))
        {
            CaptureSayIntentionsApproach(assignment);
        }
    }

    private void CaptureSayIntentionsApproach(
        SayIntentionsApproachAssignment assignment)
    {
        if (!_sayIntentionsRuntime.RecordApproachAssignment(assignment))
        {
            return;
        }

        AppendDashboardLog(
            $"SayIntentions approach captured: {(assignment.IsIls ? "ILS " : "")}runway {assignment.Runway}.");
        AppLog.Write(
            $"Captured SayIntentions approach assignment: {(assignment.IsIls ? "ILS " : "")}runway {assignment.Runway}.");
    }

    private void CaptureSayIntentionsPushbackDirection(
        SayIntentionsCommunication communication)
    {
        if (!SayIntentionsAtcResponseClassifier.IsClearanceResponse(
                "captain-pushback-clearance",
                communication.OutgoingMessage,
                communication.IncomingMessage)
            || !GsxPushbackDirectionCoordinator.TryParseTargetHeading(
                communication.OutgoingMessage,
                out var heading))
        {
            return;
        }

        _sayIntentionsRuntime.RecordPushbackTargetHeading(heading, DateTime.UtcNow);
        AppLog.Write(
            $"Captured SayIntentions pushback target heading {heading:000} degrees magnetic.");
    }

    private bool CanCoordinateArrivalStandWithGsx()
    {
        var gsx = _gsx.Snapshot;
        var flowId = _procedureRunner.Definition?.Id;
        var arrivalFlowActive = string.Equals(
                                    flowId,
                                    "after-landing-taxi",
                                    StringComparison.OrdinalIgnoreCase)
                                || string.Equals(
                                    flowId,
                                    "parking-shutdown",
                                    StringComparison.OrdinalIgnoreCase);
        return GsxArrivalGateCoordinator.IsBridgeAvailable(
            _settings.EnableGsxIntegration
            && _gsxInstallation != null
            && gsx.CouatlStarted,
            gsx.OwnsRemoteControl,
            _sayIntentionsRuntime.Flight != null,
            _state?.OnGround == true,
            arrivalFlowActive);
    }

    private bool TryAutoSelectGsxArrivalStand()
    {
        var gsx = _gsx.Snapshot;
        if (!CanCoordinateArrivalStandWithGsx()
            || !gsx.MenuOpen
            || gsx.CurrentMenu.IsEmpty
            || gsx.PendingArrivalStand == null)
        {
            return false;
        }

        var selection = GsxArrivalGateCoordinator.FindSelection(
            gsx.CurrentMenu,
            gsx.PendingArrivalStand);
        if (selection == null)
        {
            return false;
        }

        var target = gsx.PendingArrivalStand;
        var label = gsx.CurrentMenu.Choices[selection.ChoiceIndex];
        CloseGsxChoiceDialog();
        _gsx.SendAutomatedMenuChoice(selection.ChoiceIndex);

        if (selection.CompletesSelection)
        {
            _gsx.CompleteArrivalStandSelection(target);
            AppendDashboardLog(
                $"GSX destination synchronized to assigned Gate {target}.");
            AppLog.Write(
                $"GSX arrival stand selection completed: {label}.");
        }
        else
        {
            AppendDashboardLog(
                $"Advancing the GSX position menu toward assigned Gate {target}.");
            AppLog.Write(
                $"GSX arrival stand selection advanced through: {label}.");
        }

        return true;
    }

    private async Task<bool> TryCompleteCurrentSayIntentionsAtcStepFromHistoryAsync(
        SayIntentionsFlightContext flight,
        IReadOnlyList<SayIntentionsCommunication> communications,
        CancellationToken cancellationToken)
    {
        var currentStepId = _procedureRunner.CurrentStep?.Id;
        if (!IsSayIntentionsAtcStep(currentStepId)
            || _procedureRunner.Status is not (ProcedureStatus.WaitingForManualAction
                or ProcedureStatus.WaitingForVerification))
        {
            return false;
        }

        var baseline = _pendingSayIntentionsAtcStepId == currentStepId
            ? _pendingSayIntentionsAtcBaselineId
            : 0;
        var clearance = SayIntentionsAtcResponseClassifier.FindRecentClearance(
            currentStepId!,
            communications,
            baseline,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(30));
        if (clearance == null)
        {
            return false;
        }

        if (_pendingSayIntentionsAtcStepId == null)
        {
            _pendingSayIntentionsAtcStepId = currentStepId;
            _pendingSayIntentionsAtcBaselineId = 0;
            _pendingSayIntentionsAtcStartedUtc = DateTime.UtcNow;
            AppendDashboardLog(
                "SayIntentions already obtained the required ATC clearance; no duplicate request will be sent.");
        }

        await TryCompletePendingSayIntentionsAtcStepAsync(
            flight,
            clearance,
            cancellationToken,
            clearanceAlreadyClassified: true);
        return true;
    }

    private async Task TryCompletePendingSayIntentionsAtcStepAsync(
        SayIntentionsFlightContext flight,
        SayIntentionsCommunication communication,
        CancellationToken cancellationToken,
        bool clearanceAlreadyClassified = false)
    {
        var pendingStepId = _pendingSayIntentionsAtcStepId;
        if (pendingStepId == null
            || communication.Id <= _pendingSayIntentionsAtcBaselineId
            || !clearanceAlreadyClassified
            && !SayIntentionsAtcResponseClassifier.IsClearanceResponse(
                pendingStepId,
                communication.OutgoingMessage,
                communication.IncomingMessage))
        {
            return;
        }

        if (_procedureRunner.CurrentStep?.Id != pendingStepId
            || _procedureRunner.Status is not (ProcedureStatus.WaitingForManualAction
                or ProcedureStatus.WaitingForVerification))
        {
            _pendingSayIntentionsAtcStepId = null;
            _pendingSayIntentionsAtcBaselineId = 0;
            _pendingSayIntentionsAtcStartedUtc = null;
            return;
        }

        AppendDashboardLog(
            SayIntentionsAtcResponseClassifier.VerificationMessage(pendingStepId));
        _pendingSayIntentionsAtcStepId = null;
        _pendingSayIntentionsAtcBaselineId = 0;
        _pendingSayIntentionsAtcStartedUtc = null;
        if (_sayIntentionsTimer != null)
        {
            _sayIntentionsTimer.Interval = 10000;
        }

        if (_procedureRunner.CurrentStep?.Kind == ProcedureStepKind.AutomaticAction)
        {
            if (pendingStepId == "fo-taxi-clearance")
            {
                _taxiClearanceReceived = true;
                if (_state != null)
                {
                    _state.TaxiClearanceReceived = true;
                }
            }
            else if (pendingStepId == "fo-takeoff-clearance")
            {
                _takeoffClearanceReceived = true;
                if (_state != null)
                {
                    _state.TakeoffClearanceReceived = true;
                }
            }
            if (_state != null)
            {
                _procedureRunner.Update(_state);
            }
        }
        else
        {
            _automation.Enqueue("procedure confirm");
        }
    }

    private void CancelPendingSayIntentionsAtcRequest()
    {
        if (_pendingSayIntentionsAtcStepId == null)
        {
            return;
        }

        _pendingSayIntentionsAtcStepId = null;
        _pendingSayIntentionsAtcBaselineId = 0;
        _pendingSayIntentionsAtcStartedUtc = null;
        if (_sayIntentionsTimer != null)
        {
            _sayIntentionsTimer.Interval = 10000;
        }

    }

    private string GsxStatusText()
    {
        if (!_settings.EnableGsxIntegration)
        {
            return "Disabled - flights continue without GSX coordination.";
        }

        if (_gsxInstallation == null)
        {
            return "Not installed - optional integration inactive.";
        }

        var gsx = _gsx.Snapshot;
        return gsx.CouatlStarted
            ? gsx.RemoteControlActive && !gsx.OwnsRemoteControl
                ? "Attention - GSX Remote Control is already owned by another add-on."
                : gsx.OwnsRemoteControl
                    ? "Active - coordinating the current GSX departure operation."
                    : "Ready - Couatl connected; passive monitoring active."
            : "Installed - waiting for the Couatl engine.";
    }

    private string GsxConfigurationSummary() =>
        $"Boarding {(_settings.GsxAutomaticallyRequestBoarding ? "ON" : "OFF")} | "
        + $"Departure preparation {(_settings.GsxAutomaticallyPrepareDeparture ? "ON" : "OFF")} | "
        + $"Deboarding {(_settings.GsxAutomaticallyRequestDeboarding ? "ON" : "OFF")}";

    private void UpdateGsxStatus()
    {
        var gsx = _gsx.Snapshot;
        if (_gsxInstallation != null && _gsxFileReader == null)
        {
            _gsxFileReader = new GsxFileReader(_gsxInstallation);
        }

        if (_gsxStatusLabel != null)
        {
            _gsxStatusLabel.Text = GsxStatusText();
            _gsxStatusLabel.ForeColor = !_settings.EnableGsxIntegration
                || _gsxInstallation == null
                    ? System.Drawing.Color.DimGray
                    : gsx.CouatlStarted
                        ? System.Drawing.Color.DarkGreen
                        : System.Drawing.Color.DarkGoldenrod;
        }

        var liveState = GsxLiveStatusFormatter.Format(
            _gsx.StatusSnapshot(DateTime.UtcNow),
            gsx.CurrentMenu,
            _settings.EnableGsxIntegration,
            _gsxInstallation != null,
            gsx.CouatlStarted);
        if (_gsxLiveSummaryLabel != null)
        {
            _gsxLiveSummaryLabel.Text = liveState.SummaryText;
            _gsxLiveSummaryLabel.ForeColor = liveState.HasActionRequired
                ? System.Drawing.Color.FromArgb(180, 83, 9)
                : System.Drawing.Color.FromArgb(31, 41, 55);
        }
        if (_gsxPassengerLabel != null)
        {
            _gsxPassengerLabel.Text = liveState.PassengerProgressText
                ?? "Passenger progress unavailable";
        }
        if (_gsxPassengerProgress != null)
        {
            _gsxPassengerProgress.Value = liveState.PassengerPercent ?? 0;
        }
        if (_gsxLiveActionLabel != null)
        {
            _gsxLiveActionLabel.Text = liveState.HasActionRequired
                ? $"Action required: {liveState.ActionRequiredText}"
                : "No action required";
            _gsxLiveActionLabel.BackColor = liveState.HasActionRequired
                ? System.Drawing.Color.FromArgb(255, 247, 237)
                : System.Drawing.Color.FromArgb(236, 253, 245);
            _gsxLiveActionLabel.ForeColor = liveState.HasActionRequired
                ? System.Drawing.Color.FromArgb(154, 52, 18)
                : System.Drawing.Color.FromArgb(6, 95, 70);
        }
        if (_manageGsxButton != null)
        {
            var hasSelectablePrompt = gsx.MenuOpen
                                      && !gsx.CurrentMenu.IsEmpty
                                      && !GsxPromptPolicy.IsRootServicesMenu(
                                          gsx.CurrentMenu);
            _manageGsxButton.Text = hasSelectablePrompt
                ? "Answer GSX prompt..."
                : "Open GSX details";
            _manageGsxButton.BackColor = hasSelectablePrompt
                ? System.Drawing.Color.FromArgb(255, 247, 237)
                : System.Drawing.Color.White;
            _manageGsxButton.ForeColor = hasSelectablePrompt
                ? System.Drawing.Color.FromArgb(154, 52, 18)
                : System.Drawing.Color.FromArgb(40, 68, 106);
        }

        var badgeText = !_settings.EnableGsxIntegration
            ? "GSX DISABLED"
            : _gsxInstallation == null
                ? "GSX NOT INSTALLED"
                : gsx.CouatlStarted
                    ? gsx.RemoteControlActive && !gsx.OwnsRemoteControl
                        ? "GSX ATTENTION"
                        : gsx.OwnsRemoteControl
                            ? "GSX ACTIVE"
                            : "GSX READY"
                    : "GSX OFFLINE";
        var badgeColor = !_settings.EnableGsxIntegration || _gsxInstallation == null
            ? System.Drawing.Color.DimGray
            : gsx.CouatlStarted
                ? gsx.RemoteControlActive && !gsx.OwnsRemoteControl
                    ? System.Drawing.Color.DarkOrange
                    : System.Drawing.Color.SeaGreen
                : System.Drawing.Color.DarkGoldenrod;
        SetStatusBadge(_gsxBadgeLabel, badgeText, badgeColor);
    }

    private void ShowGsxDialog(IWin32Window? owner = null)
    {
        using var dialog = new Form
        {
            Text = "GSX departure integration",
            Width = 720,
            Height = 640,
            MinimumSize = new System.Drawing.Size(620, 500),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable,
            MaximizeBox = true,
            MinimizeBox = false
        };
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        dialog.Controls.Add(root);
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };
        root.Controls.Add(layout, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = "GSX departure coordination",
            AutoSize = true,
            Font = new System.Drawing.Font(Font.FontFamily, 14, System.Drawing.FontStyle.Bold)
        });
        layout.Controls.Add(new Label
        {
            Text = "GSX keeps control of passenger, fuel, cargo, catering, door and timing settings. The First Officer only coordinates boarding, departure and arrival deboarding milestones.",
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(620, 0),
            ForeColor = System.Drawing.Color.DimGray,
            Margin = new Padding(0, 6, 0, 12)
        });

        var enabled = new CheckBox
        {
            Text = "Enable GSX integration",
            Checked = _settings.EnableGsxIntegration,
            AutoSize = true
        };
        var boarding = new CheckBox
        {
            Text = "Automatically request GSX boarding when Flow 2 starts",
            Checked = _settings.GsxAutomaticallyRequestBoarding,
            AutoSize = true
        };
        var departure = new CheckBox
        {
            Text = "Automatically prepare for pushback after clearance in Flow 3",
            Checked = _settings.GsxAutomaticallyPrepareDeparture,
            AutoSize = true
        };
        var deboarding = new CheckBox
        {
            Text = "Automatically request GSX deboarding after parking at the gate",
            Checked = _settings.GsxAutomaticallyRequestDeboarding,
            AutoSize = true
        };
        layout.Controls.Add(enabled);
        layout.Controls.Add(boarding);
        layout.Controls.Add(departure);
        layout.Controls.Add(deboarding);
        layout.Controls.Add(new Label
        {
            Text = "These options are independent. Disable boarding/deboarding for cargo, positioning, or other flights without passengers while keeping GSX pushback coordination enabled.",
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(620, 0),
            ForeColor = System.Drawing.Color.DimGray,
            Margin = new Padding(20, 4, 0, 4)
        });
        void UpdateGsxOptionAvailability()
        {
            boarding.Enabled = enabled.Checked;
            departure.Enabled = enabled.Checked;
            deboarding.Enabled = enabled.Checked;
        }
        enabled.CheckedChanged += (_, _) => UpdateGsxOptionAvailability();
        UpdateGsxOptionAvailability();

        var status = new Label
        {
            Text = GsxStatusText(),
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(620, 0),
            Margin = new Padding(0, 14, 0, 4)
        };
        layout.Controls.Add(status);

        var observed = new TextBox
        {
            Width = 620,
            Height = 180,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Text = "Passive protocol proof: no GSX service command has been sent."
        };
        layout.Controls.Add(observed);

        var promptControls = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 10, 0, 0),
            Visible = false
        };
        var promptLabel = new Label
        {
            AutoSize = true,
            Font = new System.Drawing.Font(
                Font.FontFamily,
                Font.Size,
                System.Drawing.FontStyle.Bold),
            Margin = new Padding(0, 7, 8, 0)
        };
        var promptChoices = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 300
        };
        var sendPromptChoice = new Button
        {
            Text = "Send response to GSX",
            AutoSize = true,
            BackColor = System.Drawing.Color.FromArgb(39, 130, 87),
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false
        };
        promptControls.Controls.Add(promptLabel);
        promptControls.Controls.Add(promptChoices);
        promptControls.Controls.Add(sendPromptChoice);
        layout.Controls.Add(promptControls);

        var testControls = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 10, 0, 0)
        };
        var requestBoarding = new Button
        {
            Text = "Request boarding now",
            AutoSize = true,
            Enabled = _settings.EnableGsxIntegration && _gsx.Snapshot.CouatlStarted
        };
        var prepareDeparture = new Button
        {
            Text = "Prepare departure now",
            AutoSize = true,
            Enabled = _settings.EnableGsxIntegration && _gsx.Snapshot.CouatlStarted
        };
        var requestDeboarding = new Button
        {
            Text = "Request deboarding now",
            AutoSize = true,
            Enabled = _settings.EnableGsxIntegration && _gsx.Snapshot.CouatlStarted
        };
        var recoverControl = new Button
        {
            Text = "Recover VFO GSX control",
            AutoSize = true,
            Visible = _gsx.Snapshot.RemoteControlActive
                      && !_gsx.Snapshot.OwnsRemoteControl,
            Enabled = _settings.EnableGsxIntegration && _gsx.Snapshot.CouatlStarted
        };
        var openGsxMenu = new Button
        {
            Text = "Open current GSX menu",
            AutoSize = true,
            Enabled = _settings.EnableGsxIntegration && _gsx.Snapshot.CouatlStarted
        };
        requestBoarding.Click += (_, _) => BeginGsxAction(GsxDepartureAction.Boarding);
        prepareDeparture.Click += (_, _) => BeginGsxAction(GsxDepartureAction.PrepareForDeparture);
        requestDeboarding.Click += (_, _) => BeginGsxAction(GsxDepartureAction.Deboarding);
        openGsxMenu.Click += (_, _) =>
        {
            var gsx = _gsx.Snapshot;
            if (!gsx.OwnsRemoteControl)
            {
                if (gsx.RemoteControlActive)
                {
                    AppendDashboardLog(
                        "GSX Remote Control is owned by another add-on; the menu was not opened.");
                    return;
                }

                _gsx.ClaimRemoteControl(DateTime.UtcNow);
            }
            _gsx.OpenMenu();
            AppendDashboardLog("Opening the current GSX menu for a response.");
        };
        recoverControl.Click += (_, _) =>
        {
            var result = MessageBox.Show(
                dialog,
                "GSX reports that Remote Control is active, but its protocol does not identify the owner. "
                + "Only continue if no other GSX remote-control add-on or EFB is currently active.\n\n"
                + "Recover control for MSFS 2024 Virtual First Officer?",
                "Recover GSX control",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
            {
                return;
            }

            RecoverGsxRemoteControl();
            recoverControl.Visible = false;
            status.Text = GsxStatusText();
        };
        testControls.Controls.Add(requestBoarding);
        testControls.Controls.Add(prepareDeparture);
        testControls.Controls.Add(requestDeboarding);
        testControls.Controls.Add(openGsxMenu);
        testControls.Controls.Add(recoverControl);
        layout.Controls.Add(testControls);

        void RefreshObservedState()
        {
            var gsx = _gsx.Snapshot;
            status.Text = GsxStatusText();
            recoverControl.Visible = gsx.RemoteControlActive
                                     && !gsx.OwnsRemoteControl;
            recoverControl.Enabled = _settings.EnableGsxIntegration
                                     && gsx.CouatlStarted;
            var tooltip = _gsxFileReader?.ReadTooltip() ?? Array.Empty<string>();
            var fileMenu = _gsxFileReader?.ReadMenu()
                           ?? new GsxMenuSnapshot(string.Empty, Array.Empty<string>());
            var menu = gsx.MenuOpen && !gsx.CurrentMenu.IsEmpty
                ? gsx.CurrentMenu
                : fileMenu;
            var selectable = gsx.MenuOpen
                             && !menu.IsEmpty
                             && !GsxPromptPolicy.IsRootServicesMenu(menu);
            promptControls.Visible = selectable;
            if (selectable)
            {
                promptLabel.Text = $"Response required: {menu.Title}";
                promptChoices.BeginUpdate();
                promptChoices.Items.Clear();
                foreach (var choice in menu.Choices)
                {
                    promptChoices.Items.Add(choice);
                }
                promptChoices.EndUpdate();
                if (promptChoices.Items.Count > 0)
                {
                    promptChoices.SelectedIndex = 0;
                }
            }
            var lines = new List<string>
            {
                GsxStatusText(),
                "",
                menu.IsEmpty ? "No live GSX menu is currently open." : $"Menu: {menu.Title}"
            };
            lines.AddRange(menu.Choices.Select((choice, index) => $"{index + 1}. {choice}"));
            if (tooltip.Count > 0)
            {
                lines.Add("");
                lines.Add("Latest GSX status:");
                lines.AddRange(tooltip);
            }
            observed.Lines = lines.ToArray();
        }

        sendPromptChoice.Click += (_, _) =>
        {
            var gsx = _gsx.Snapshot;
            if (!gsx.MenuOpen
                || gsx.CurrentMenu.IsEmpty
                || GsxPromptPolicy.IsRootServicesMenu(gsx.CurrentMenu)
                || promptChoices.SelectedIndex < 0
                || promptChoices.SelectedIndex >= gsx.CurrentMenu.Choices.Count)
            {
                RefreshObservedState();
                return;
            }

            var choiceIndex = promptChoices.SelectedIndex;
            var choiceLabel = gsx.CurrentMenu.Choices[choiceIndex];
            SendGsxMenuChoice(choiceIndex, choiceLabel);
            CloseGsxChoiceDialog();
            RefreshObservedState();
        };

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 12, 0, 0)
        };
        var save = new Button { Text = "Save", AutoSize = true };
        var refresh = new Button { Text = "Refresh status", AutoSize = true };
        var close = new Button { Text = "Close", AutoSize = true };
        save.Click += (_, _) =>
        {
            _settings.EnableGsxIntegration = enabled.Checked;
            _settings.GsxAutomaticallyRequestBoarding = boarding.Checked;
            _settings.GsxAutomaticallyPrepareDeparture = departure.Checked;
            _settings.GsxAutomaticallyRequestDeboarding = deboarding.Checked;
            SettingsStore.Save(_settings);
            if (!_settings.EnableGsxIntegration)
            {
                ReleaseGsxRemoteControl();
            }
            UpdateGsxStatus();
            var gsx = _gsx.Snapshot;
            requestBoarding.Enabled = _settings.EnableGsxIntegration && gsx.CouatlStarted;
            prepareDeparture.Enabled = _settings.EnableGsxIntegration && gsx.CouatlStarted;
            requestDeboarding.Enabled = _settings.EnableGsxIntegration && gsx.CouatlStarted;
            openGsxMenu.Enabled = _settings.EnableGsxIntegration && gsx.CouatlStarted;
            recoverControl.Enabled = _settings.EnableGsxIntegration && gsx.CouatlStarted;
            RefreshObservedState();
        };
        refresh.Click += (_, _) => RefreshObservedState();
        close.Click += (_, _) => dialog.Close();
        buttons.Controls.Add(save);
        buttons.Controls.Add(refresh);
        buttons.Controls.Add(close);
        buttons.Dock = DockStyle.Fill;
        root.Controls.Add(buttons, 0, 1);

        RefreshObservedState();
        dialog.ShowDialog(owner ?? this);
    }

    private void ShowIntegrationsDialog()
    {
        using var dialog = new Form
        {
            Text = "Manage integrations",
            Width = 680,
            Height = 680,
            MinimumSize = new System.Drawing.Size(620, 600),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable,
            MaximizeBox = true,
            MinimizeBox = false
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        dialog.Controls.Add(layout);

        var header = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8)
        };
        header.Controls.Add(new Label
        {
            Text = "Integrations",
            AutoSize = true,
            Font = new System.Drawing.Font(
                Font.FontFamily,
                14,
                System.Drawing.FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        });
        var intro = new Label
        {
            Text = "Configure and review optional services used by the First Officer.",
            AutoSize = true,
            ForeColor = System.Drawing.Color.DimGray,
            Margin = new Padding(0)
        };
        header.Controls.Add(intro);
        layout.Controls.Add(header, 0, 0);

        var cards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0, 8, 0, 8)
        };
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 33.334F));
        layout.Controls.Add(cards, 0, 1);

        var simBriefCard = new GroupBox
        {
            Text = "SimBrief",
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 7)
        };
        var simBriefCardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        simBriefCardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        simBriefCardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var simBriefStatus = NewDashboardLabel(SimBriefStatusText());
        simBriefStatus.MaximumSize = new System.Drawing.Size(470, 0);
        var manageSimBrief = new Button { Text = "Manage SimBrief", AutoSize = true };
        simBriefCardLayout.Controls.Add(simBriefStatus, 0, 0);
        simBriefCardLayout.Controls.Add(manageSimBrief, 1, 0);
        simBriefCard.Controls.Add(simBriefCardLayout);
        cards.Controls.Add(simBriefCard, 0, 0);

        var sayIntentionsCard = new GroupBox
        {
            Text = "SayIntentions",
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Margin = new Padding(0, 7, 0, 0)
        };
        var sayIntentionsCardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        sayIntentionsCardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sayIntentionsCardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var sayIntentionsStatus = NewDashboardLabel(
            _sayIntentionsStatusLabel?.Text
            ?? "Client not detected - optional integration inactive.");
        sayIntentionsStatus.MaximumSize = new System.Drawing.Size(470, 0);
        var manageSayIntentions = new Button
        {
            Text = "Manage SayIntentions",
            AutoSize = true
        };
        sayIntentionsCardLayout.Controls.Add(sayIntentionsStatus, 0, 0);
        sayIntentionsCardLayout.Controls.Add(manageSayIntentions, 1, 0);
        sayIntentionsCard.Controls.Add(sayIntentionsCardLayout);
        cards.Controls.Add(sayIntentionsCard, 0, 1);

        var gsxCard = new GroupBox
        {
            Text = "GSX Pro",
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Margin = new Padding(0, 7, 0, 0)
        };
        var gsxCardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        gsxCardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        gsxCardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var gsxStatus = NewDashboardLabel(
            GsxStatusText() + Environment.NewLine + GsxConfigurationSummary());
        gsxStatus.MaximumSize = new System.Drawing.Size(470, 0);
        var manageGsx = new Button { Text = "Manage GSX", AutoSize = true };
        gsxCardLayout.Controls.Add(gsxStatus, 0, 0);
        gsxCardLayout.Controls.Add(manageGsx, 1, 0);
        gsxCard.Controls.Add(gsxCardLayout);
        cards.Controls.Add(gsxCard, 0, 2);

        void RefreshStatuses()
        {
            simBriefStatus.Text = SimBriefStatusText();
            simBriefStatus.ForeColor = _simBriefFlightPlan != null
                ? System.Drawing.Color.DarkGreen
                : SimBriefConfigured
                    ? System.Drawing.Color.DarkGoldenrod
                    : System.Drawing.Color.DimGray;
            sayIntentionsStatus.Text = _sayIntentionsStatusLabel?.Text
                ?? "Client not detected - optional integration inactive.";
            sayIntentionsStatus.ForeColor = _sayIntentionsStatusLabel?.ForeColor
                ?? System.Drawing.Color.DimGray;
            gsxStatus.Text =
                GsxStatusText() + Environment.NewLine + GsxConfigurationSummary();
            gsxStatus.ForeColor = _gsxStatusLabel?.ForeColor
                ?? System.Drawing.Color.DimGray;
        }

        manageSimBrief.Click += (_, _) =>
        {
            ShowSimBriefDialog(dialog);
            RefreshStatuses();
        };
        manageSayIntentions.Click += (_, _) =>
        {
            ShowSayIntentionsDialog(dialog);
            RefreshStatuses();
        };
        manageGsx.Click += (_, _) =>
        {
            ShowGsxDialog(dialog);
            RefreshStatuses();
        };
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 8, 0, 0)
        };
        var closeButton = new Button { Text = "Close", AutoSize = true };
        closeButton.Click += (_, _) => dialog.Close();
        var refreshButton = new Button { Text = "Refresh status", AutoSize = true };
        refreshButton.Click += async (_, _) =>
        {
            refreshButton.Enabled = false;
            await RefreshSayIntentionsStatusAsync();
            RefreshStatuses();
            refreshButton.Enabled = true;
        };
        footer.Controls.Add(closeButton);
        footer.Controls.Add(refreshButton);
        layout.Controls.Add(footer, 0, 2);

        RefreshStatuses();
        dialog.ShowDialog(this);
    }

    private void ShowSayIntentionsDialog(IWin32Window? owner = null)
    {
        using var dialogCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _sayIntentionsCancellation.Token);
        using var dialog = new Form
        {
            Text = "SayIntentions companion",
            Width = 760,
            Height = 600,
            MinimumSize = new System.Drawing.Size(640, 460),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable,
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
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        dialog.Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            Text = "SayIntentions owns radio tuning and the ongoing ATC workflow. "
                   + "Credentials are discovered locally and are never saved by this app.",
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(700, 0),
            Margin = new Padding(0, 0, 0, 10)
        }, 0, 0);
        var copilotCommsBox = new CheckBox
        {
            Text = "Let the SayIntentions First Officer manage ATC communications and radio tuning",
            Checked = _settings.UseSayIntentionsCopilotCommunications,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        copilotCommsBox.CheckedChanged += async (_, _) =>
        {
            _settings.UseSayIntentionsCopilotCommunications = copilotCommsBox.Checked;
            SettingsStore.Save(_settings);
            if (_sayIntentionsRuntime.Flight != null && !copilotCommsBox.Checked)
            {
                await EnsureSayIntentionsCopilotModeAsync(
                    _sayIntentionsRuntime.Flight,
                    dialogCancellation.Token,
                    force: true,
                    desiredOverride: false);
                UpdateSayIntentionsStatus(
                    SayIntentionsDiscoveryResult.Connected(_sayIntentionsRuntime.Flight));
            }
        };
        layout.Controls.Add(copilotCommsBox, 0, 1);
        var details = new RichTextBox
        {
            Dock = DockStyle.Fill,
            MinimumSize = new System.Drawing.Size(0, 220),
            ReadOnly = true,
            BackColor = System.Drawing.Color.White,
            Font = new System.Drawing.Font("Consolas", 9),
            Text = "Checking SayIntentions..."
        };
        layout.Controls.Add(details, 0, 2);
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 10, 0, 0)
        };
        var closeButton = new Button { Text = "Close", AutoSize = true };
        closeButton.Click += (_, _) => dialog.Close();
        var refreshButton = new Button { Text = "Refresh", AutoSize = true };
        refreshButton.Click += async (_, _) =>
            await LoadSayIntentionsOperationalDataAsync(
                details,
                refreshButton,
                dialogCancellation.Token);
        var testVoiceButton = new Button
        {
            Text = "Test First Officer voice",
            AutoSize = true
        };
        testVoiceButton.Click += async (_, _) =>
            await TestSayIntentionsVoiceAsync(
                testVoiceButton,
                dialogCancellation.Token);
        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(refreshButton);
        buttons.Controls.Add(testVoiceButton);
        layout.Controls.Add(buttons, 0, 3);
        dialog.FormClosed += (_, _) => dialogCancellation.Cancel();
        dialog.Shown += async (_, _) =>
            await LoadSayIntentionsOperationalDataAsync(
                details,
                refreshButton,
                dialogCancellation.Token);
        dialog.ShowDialog(owner ?? this);
    }

    private async Task TestSayIntentionsVoiceAsync(
        Button testButton,
        CancellationToken cancellationToken)
    {
        testButton.Enabled = false;
        try
        {
            var discovery = await _sayIntentionsClient.DiscoverAsync(cancellationToken);
            if (discovery.Context == null)
            {
                MessageBox.Show(
                    this,
                    "Start an active SayIntentions flight before testing the First Officer voice.",
                    "SayIntentions unavailable",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var spoken = await _sayIntentionsClient.SayCopilotCalloutAsync(
                discovery.Context,
                "Good morning Captain, SayIntentions voice is connected",
                cancellationToken);
            if (!spoken)
            {
                throw new HttpRequestException("SayIntentions rejected the voice test.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Dialog or application closed.
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or InvalidOperationException
                                   or ArgumentOutOfRangeException)
        {
            MessageBox.Show(
                this,
                "The SayIntentions voice test was not accepted. The app will continue using its local voice fallback.",
                "Voice test unavailable",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            if (!testButton.IsDisposed)
            {
                testButton.Enabled = true;
            }
        }
    }

    private async Task LoadSayIntentionsOperationalDataAsync(
        RichTextBox details,
        Button refreshButton,
        CancellationToken cancellationToken)
    {
        refreshButton.Enabled = false;
        details.Text = "Checking SayIntentions...";
        try
        {
            var discovery = await _sayIntentionsClient
                .DiscoverAsync(cancellationToken);
            _sayIntentionsRuntime.SetFlight(discovery.Context);
            if (discovery.Context == null)
            {
                UpdateSayIntentionsStatus(discovery);
                details.Text = discovery.State == SayIntentionsConnectionState.NoActiveFlight
                    ? "SayIntentions is running, but no active flight is available."
                    : "The SayIntentions Windows client is not currently detected.";
                return;
            }

            var flight = discovery.Context;
            UpdateSayIntentionsStatus(discovery);
            var lines = new List<string>
            {
                $"Callsign: {ValueOrUnknown(flight.Callsign)}",
                $"Route:    {flight.RouteLabel}",
                $"Airport:  {ValueOrUnknown(flight.CurrentAirport)}",
                $"Gate:     {ValueOrUnknown(flight.AssignedGate)}",
                $"Comms:    {(_pendingSayIntentionsAtcStepId != null ? "Copilot - awaiting ATC" : "checkpoint control ready")}",
                ""
            };

            try
            {
                var weather = await _sayIntentionsClient
                    .GetWeatherAsync(flight, cancellationToken);
                lines.Add("WEATHER AND ATIS");
                foreach (var airport in weather.Airports)
                {
                    var wind = airport.WindDirection.HasValue && airport.WindSpeed.HasValue
                        ? $"{airport.WindDirection:000}/{airport.WindSpeed}"
                        : "unknown";
                    lines.Add($"{airport.Airport} | runway {ValueOrUnknown(airport.ActiveRunway)} | wind {wind}");
                    if (!string.IsNullOrWhiteSpace(airport.Atis)) lines.Add($"ATIS:  {airport.Atis}");
                    if (!string.IsNullOrWhiteSpace(airport.Metar)) lines.Add($"METAR: {airport.Metar}");
                    if (!string.IsNullOrWhiteSpace(airport.Taf)) lines.Add($"TAF:   {airport.Taf}");
                    lines.Add("");
                }

                if (weather.Frequencies.Count > 0)
                {
                    lines.Add("FREQUENCIES");
                    lines.AddRange(weather.Frequencies.Select(frequency =>
                        $"{frequency.Airport} {frequency.Type,-10} {frequency.Frequency,-8} {frequency.Callsign}"));
                    lines.Add("");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException
                                       or InvalidOperationException
                                       or ArgumentException)
            {
                lines.Add("Weather/ATIS is currently unavailable.");
                lines.Add("");
            }

            try
            {
                var parking = await _sayIntentionsClient
                    .GetParkingAsync(flight, cancellationToken);
                if (parking != null && !string.IsNullOrWhiteSpace(parking.Name))
                {
                    lines.Add($"ASSIGNED PARKING: {parking.Name}");
                    lines.Add("");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException
                                       or InvalidOperationException
                                       or ArgumentException)
            {
                lines.Add("Assigned parking is currently unavailable.");
                lines.Add("");
            }

            try
            {
                var communications = await _sayIntentionsClient
                    .GetCommunicationsAsync(flight, cancellationToken);
                lines.Add("RECENT COMMUNICATIONS");
                foreach (var communication in communications
                             .Skip(Math.Max(0, communications.Count - 10)))
                {
                    lines.Add($"[{communication.TimestampUtc}] {communication.Station} {communication.Channel}".Trim());
                    if (!string.IsNullOrWhiteSpace(communication.OutgoingMessage))
                        lines.Add($"YOU: {communication.OutgoingMessage}");
                    if (!string.IsNullOrWhiteSpace(communication.IncomingMessage))
                        lines.Add($"ATC: {communication.IncomingMessage}");
                    lines.Add("");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException
                                       or InvalidOperationException
                                       or ArgumentException)
            {
                lines.Add("Communications history is currently unavailable.");
            }

            details.Lines = lines.ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        finally
        {
            if (!refreshButton.IsDisposed)
            {
                refreshButton.Enabled = true;
            }
        }
    }

    private static string ValueOrUnknown(string value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value;

    private void BeginAutomatedSayIntentionsAtcStep()
    {
        var step = _procedureRunner.CurrentStep;
        if (step == null || !IsSayIntentionsAtcStep(step.Id))
        {
            return;
        }

        if (_state?.SayIntentionsAtcActive != true)
        {
            _procedureRunner.Update(_state!);
            return;
        }

        _ = HandleConfirmButtonAsync();
    }

    private async Task HandleConfirmButtonAsync()
    {
        var step = _procedureRunner.CurrentStep;
        if (IsPushbackClearanceBlockedByGsx(step))
        {
            AppendDashboardLog(
                "Pushback/start clearance request blocked until GSX boarding is complete.");
            UpdateProcedureActionButtons();
            PublishEfbState(force: true);
            return;
        }
        var flight = _sayIntentionsRuntime.Flight;
        if (step == null
            || !IsSayIntentionsAtcStep(step.Id)
            || flight == null
            || !_settings.UseSayIntentionsCopilotCommunications
            || _procedureRunner.Status is not (ProcedureStatus.WaitingForManualAction
                or ProcedureStatus.WaitingForVerification))
        {
            _automation.Enqueue("procedure confirm");
            return;
        }

        if (_sayIntentionsHandoffInProgress)
        {
            return;
        }

        if (_pendingSayIntentionsAtcStepId != null)
        {
            AppendDashboardLog("Still waiting for the SayIntentions ATC response.");
            return;
        }

        _sayIntentionsHandoffInProgress = true;
        UpdateProcedureActionButtons();
        try
        {
            var communications = await _sayIntentionsClient.GetCommunicationsAsync(
                flight,
                _sayIntentionsCancellation.Token);
            if (await TryCompleteCurrentSayIntentionsAtcStepFromHistoryAsync(
                    flight,
                    communications,
                    _sayIntentionsCancellation.Token))
            {
                return;
            }
            _sayIntentionsRuntime.EstablishCommunicationBaseline(flight.SessionKey, communications);
            _pendingSayIntentionsAtcBaselineId = _sayIntentionsRuntime.LastCommunicationId;

            var copilotModeWasReady = _sayIntentionsRuntime.IsCopilotModeCurrent(
                flight.SessionKey,
                true);
            var accepted = await EnsureSayIntentionsCopilotModeAsync(
                flight,
                _sayIntentionsCancellation.Token,
                desiredOverride: true);
            if (!accepted)
            {
                AppendDashboardLog(
                    "SayIntentions did not accept the Copilot communications handoff. Check the integration, then press Confirm again.");
                return;
            }

            if (!copilotModeWasReady)
            {
                // setVar confirms API acceptance, but SayIntentions exposes no
                // readable acknowledgement that its desktop client has applied
                // the mode. Allow one bounded client synchronization interval
                // only when this checkpoint actually changed communications.
                await Task.Delay(
                    TimeSpan.FromSeconds(2),
                    _sayIntentionsCancellation.Token);
            }

            _pendingSayIntentionsAtcStepId = step.Id;
            _pendingSayIntentionsAtcStartedUtc = DateTime.UtcNow;
            var instructionAccepted =
                SayIntentionsCopilotActionMap.TryGetActionName(
                    step.Id,
                    out var actionName)
                    ? await _sayIntentionsClient.TriggerCopilotActionAsync(
                        flight,
                        actionName,
                        _sayIntentionsCancellation.Token)
                    : await _sayIntentionsClient.AskCopilotAsync(
                        flight,
                        step.Id == "fo-taxi-clearance"
                            ? "Request our taxi clearance."
                            : "Report ready for departure and request takeoff clearance.",
                        _sayIntentionsCancellation.Token);
            if (!instructionAccepted)
            {
                _pendingSayIntentionsAtcStepId = null;
                _pendingSayIntentionsAtcBaselineId = 0;
                _pendingSayIntentionsAtcStartedUtc = null;
                AppendDashboardLog(
                    "SayIntentions did not accept the First Officer instruction. Check the integration, then press Confirm again.");
                return;
            }

            AppendDashboardLog(
                "SayIntentions First Officer is handling the ATC request. Waiting for the matching clearance before completing this step.");
            if (_sayIntentionsTimer != null)
            {
                _sayIntentionsTimer.Interval = 2000;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or InvalidOperationException
                                   or ArgumentException)
        {
            AppLog.Write($"SayIntentions Copilot instruction failed: {ex.Message}");
            _pendingSayIntentionsAtcStepId = null;
            _pendingSayIntentionsAtcBaselineId = 0;
            _pendingSayIntentionsAtcStartedUtc = null;
            AppendDashboardLog(
                "SayIntentions could not start the First Officer communication. Check the integration, then press Confirm again.");
        }
        catch (OperationCanceledException) when (_sayIntentionsCancellation.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        finally
        {
            _sayIntentionsHandoffInProgress = false;
            UpdateProcedureActionButtons();
        }
    }

    private static bool IsSayIntentionsAtcStep(string? stepId) =>
        stepId is "captain-ifr-clearance"
            or "captain-pushback-clearance"
            or "fo-taxi-clearance"
            or "fo-takeoff-clearance";

    private GsxLiveState GetGsxLiveState()
    {
        var runtime = _gsx.Snapshot;
        var gsx = GsxLiveStatusFormatter.Format(
            _gsx.StatusSnapshot(DateTime.UtcNow),
            runtime.MenuOpen ? runtime.CurrentMenu : null,
            _settings.EnableGsxIntegration,
            _gsxInstallation != null,
            runtime.CouatlStarted);
        if (gsx.BoardingComplete)
        {
            _gsx.SetBoardingCompletedThisFlight(true);
        }
        return gsx;
    }

    private bool IsPushbackClearanceBlockedByGsx(ProcedureStep? step)
    {
        if (!string.Equals(
                step?.Id,
                "captain-pushback-clearance",
                StringComparison.OrdinalIgnoreCase)
            || !_settings.EnableGsxIntegration
            || _gsxInstallation == null)
        {
            return false;
        }

        var gsx = GetGsxLiveState();
        var runtime = _gsx.Snapshot;
        return GsxDepartureCoordinator.ShouldBlockPushbackClearance(
            runtime.CouatlStarted,
            runtime.BoardingRequestedThisFlight,
            gsx.BoardingInProgress,
            runtime.BoardingCompletedThisFlight);
    }

    private bool SimBriefConfigured =>
        !string.IsNullOrWhiteSpace(_settings.SimBriefPilotId)
        || !string.IsNullOrWhiteSpace(_settings.SimBriefUsername);

    private string SimBriefStatusText() => _simBriefFlightPlan != null
        ? $"Active flight {_simBriefFlightPlan.RouteLabel} - use Manage SimBrief to review or refresh."
        : SimBriefConfigured
            ? "Configured and ready for on-demand import."
            : "Not configured - use Manage SimBrief to add a Pilot ID or username.";

    private void UpdateSimBriefStatus(string? temporaryStatus = null)
    {
        if (_simBriefStatusLabel != null)
        {
            _simBriefStatusLabel.Text = temporaryStatus ?? SimBriefStatusText();
        }

        if (temporaryStatus != null
            && temporaryStatus.StartsWith("Importing", StringComparison.OrdinalIgnoreCase))
        {
            SetStatusBadge(
                _simBriefBadgeLabel,
                "SIMBRIEF IMPORTING",
                System.Drawing.Color.FromArgb(40, 95, 150));
            return;
        }

        if (temporaryStatus != null)
        {
            SetStatusBadge(
                _simBriefBadgeLabel,
                "SIMBRIEF UNAVAILABLE",
                System.Drawing.Color.FromArgb(150, 48, 48));
            return;
        }

        if (_simBriefFlightPlan != null)
        {
            SetStatusBadge(
                _simBriefBadgeLabel,
                $"SIMBRIEF {_simBriefFlightPlan.RouteLabel.ToUpperInvariant()}",
                System.Drawing.Color.FromArgb(39, 130, 87));
            return;
        }

        SetStatusBadge(
            _simBriefBadgeLabel,
            SimBriefConfigured ? "SIMBRIEF READY" : "SIMBRIEF NOT SET",
            SimBriefConfigured
                ? System.Drawing.Color.FromArgb(172, 113, 37)
                : System.Drawing.Color.DimGray);
    }

    private void ShowSimBriefDialog(IWin32Window? owner = null)
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
        var briefingButton = new Button
        {
            Text = "Review flight briefing",
            AutoSize = true,
            Enabled = _simBriefFlightPlan != null
        };
        var closeButton = new Button { Text = "Close", AutoSize = true };
        Action saveSettings = () =>
        {
            _settings.SimBriefPilotId = pilotIdBox.Text.Trim();
            _settings.SimBriefUsername = usernameBox.Text.Trim();
            _settings.SimBriefAutoImportOnNewFlight = autoImportBox.Checked;
            SettingsStore.Save(_settings);
            UpdateSimBriefStatus();
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
            briefingButton.Enabled = _simBriefFlightPlan != null;
            importButton.Text = "Import latest flight";
            importButton.Enabled = true;
        };
        briefingButton.Click += (_, _) => ShowSimBriefBriefing();
        closeButton.Click += (_, _) => dialog.Close();
        buttons.Controls.Add(importButton);
        buttons.Controls.Add(briefingButton);
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(closeButton);
        layout.Controls.Add(buttons, 0, 5);
        layout.SetColumnSpan(buttons, 2);
        dialog.AcceptButton = importButton;
        dialog.CancelButton = closeButton;
        dialog.ShowDialog(owner ?? this);
    }

    private void ShowSimBriefBriefing()
    {
        var plan = _simBriefFlightPlan;
        if (plan == null)
        {
            MessageBox.Show(this, "Import and activate a SimBrief flight first.", "Flight briefing", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var plannedFuel = SimBriefOperationalContext.BlockFuelKilograms(plan);
        var actualFuel = _state?.ActualFuelKilograms ?? 0;
        var fuelLine = plannedFuel.HasValue
            ? $"Block fuel {plannedFuel.Value:N0} kg | aircraft {actualFuel:N0} kg | {_state?.SimBriefFuelStatus ?? "live fuel unavailable"}"
            : "Block fuel unavailable in the imported OFP";
        var takeoffLine =
            $"Runway {plan.OriginRunway ?? "--"} | flaps {plan.TakeoffFlaps ?? "--"} | " +
            $"V1 {plan.TakeoffV1Knots?.ToString() ?? "--"} | VR {plan.TakeoffVrKnots?.ToString() ?? "--"} | V2 {plan.TakeoffV2Knots?.ToString() ?? "--"}";
        var cruiseLine =
            $"Cruise {(plan.CruiseAltitudeFeet.HasValue ? $"FL{plan.CruiseAltitudeFeet.Value / 100:000}" : "--")} | " +
            $"cost index {plan.CostIndex?.ToString() ?? "--"} | transition altitude {plan.TransitionAltitudeFeet?.ToString("N0") ?? "--"} ft";
        var departureNavigation = SimBriefNavigationSummary.Departure(plan);
        var arrivalNavigation = SimBriefNavigationSummary.Arrival(plan);
        var airacLine = SimBriefNavigationSummary.Airac(plan);
        var navlogLine = SimBriefNavigationSummary.Navlog(plan);
        var comparison = _state?.SimBriefTakeoffStatus ?? "Cockpit comparison available after aircraft connection";

        MessageBox.Show(this,
            $"{plan.RouteLabel}  {plan.FlightNumber}\nAircraft {plan.AircraftIcao} {plan.AircraftRegistration}\n\n" +
            $"NAVIGATION\n{airacLine}\n{departureNavigation}\n{arrivalNavigation}\n{navlogLine}\n\n" +
            $"TAKEOFF\n{takeoffLine}\n{comparison}\n\n" +
            $"FUEL\n{fuelLine}\n\n" +
            $"CRUISE\n{cruiseLine}\n\n" +
            $"ALTERNATE\n{(string.IsNullOrWhiteSpace(plan.AlternateIcao) ? "--" : plan.AlternateIcao)}\n\n" +
            $"ROUTE\n{SimBriefNavigationSummary.PreferredRoute(plan)}",
            "SimBrief operational briefing",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
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
        UpdateSimBriefStatus("Importing latest OFP...");
        try
        {
            var plan = await new SimBriefClient().FetchLatestAsync(
                _settings.SimBriefPilotId,
                _settings.SimBriefUsername);
            SimBriefCacheStore.Save(plan);
            AppendDashboardLog($"SimBrief imported: {plan.RouteLabel} {plan.FlightNumber}".Trim());
            if (showReview)
                ReviewAndApplySimBrief(plan);
            else
                ActivateSimBriefFlightPlan(plan);
            UpdateSimBriefStatus();
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
            UpdateSimBriefStatus("Unavailable ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â existing settings kept");
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
            SimBriefOperationalContext.ExpectedAircraftIcaos(
                _state?.Variant ?? AircraftVariant.Unsupported),
            DateTime.UtcNow);
        var changes = new List<string>();
        if (plan.TransitionAltitudeFeet is >= 1000 and <= 20000)
            changes.Add($"Transition altitude: {plan.TransitionAltitudeFeet:N0} ft");
        if (plan.TakeoffV1Knots is >= 80 and <= 219)
            changes.Add($"V1: {plan.TakeoffV1Knots} kt");
        if (plan.TakeoffVrKnots is >= 80 and <= 220)
            changes.Add($"VR: {plan.TakeoffVrKnots} kt");
        if (plan.TakeoffV2Knots is >= 80 and <= 220)
            changes.Add($"V2: {plan.TakeoffV2Knots} kt");

        var message = SimBriefSummary(plan)
            + (warnings.Count > 0 ? "\n\nWarnings:\n- " + string.Join("\n- ", warnings) : "")
            + (changes.Count > 0
                ? "\n\nApply these reviewed values to the app?\n- " + string.Join("\n- ", changes)
                : "\n\nThis OFP contains no supported takeoff values to apply. It will remain available as a flight summary.");
        if (changes.Count == 0)
        {
            MessageBox.Show(this, message, "SimBrief flight imported", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ActivateSimBriefFlightPlan(plan);
            return;
        }
        if (MessageBox.Show(this, message, "Review SimBrief import", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        ActivateSimBriefFlightPlan(plan);
        AppendDashboardLog("Reviewed SimBrief flight activated for this flight only.");
    }

    private void ActivateSimBriefFlightPlan(ImportedFlightPlan plan)
    {
        _simBriefFlightPlan = plan;
        _procedureSession.ActiveFlightPlan = plan;
        _procedureSession.SavedUtc = DateTime.UtcNow;
        ProcedureSessionStore.Save(_procedureSession);

        if (SimBriefOperationalContext.ApplyTakeoffSettings(plan, _settings))
        {
            SettingsStore.Save(_settings);
            SyncTakeoffSettingControls();
            AppendDashboardLog("Reviewed SimBrief takeoff settings applied.");
        }
    }

    private void SyncTakeoffSettingControls()
    {
        if (_transitionAltitudeBox != null)
        {
            _transitionAltitudeBox.Value = Math.Max(
                _transitionAltitudeBox.Minimum,
                Math.Min(_transitionAltitudeBox.Maximum, _settings.TransitionAltitudeFeet));
        }
        if (_takeoffV1Box != null)
        {
            _takeoffV1Box.Value = Math.Max(
                _takeoffV1Box.Minimum,
                Math.Min(_takeoffV1Box.Maximum, _settings.TakeoffV1SpeedKnots));
        }
        if (_takeoffRotateBox != null)
        {
            _takeoffRotateBox.Value = Math.Max(
                Math.Max(_takeoffRotateBox.Minimum, _settings.TakeoffV1SpeedKnots),
                Math.Min(_takeoffRotateBox.Maximum, _settings.TakeoffRotateSpeedKnots));
        }
        if (_takeoffV2Box != null)
        {
            _takeoffV2Box.Value = Math.Max(
                Math.Max(_takeoffV2Box.Minimum, _settings.TakeoffRotateSpeedKnots),
                Math.Min(_takeoffV2Box.Maximum, _settings.TakeoffV2SpeedKnots));
        }
        if (_state != null)
        {
            _state.TransitionAltitudeFeet = _settings.TransitionAltitudeFeet;
            _state.TakeoffV1SpeedKnots = _settings.TakeoffV1SpeedKnots;
            _state.TakeoffRotateSpeedKnots = _settings.TakeoffRotateSpeedKnots;
            _state.TakeoffV2SpeedKnots = _settings.TakeoffV2SpeedKnots;
        }
    }

    private static string SimBriefSummary(ImportedFlightPlan? plan)
    {
        if (plan == null) return "No SimBrief flight has been imported yet.";
        var generated = plan.GeneratedUtc.HasValue
            ? $"generated {plan.GeneratedUtc.Value.ToLocalTime():g}"
            : "generation time unavailable";
        var cruise = plan.CruiseAltitudeFeet.HasValue ? $"FL{plan.CruiseAltitudeFeet.Value / 100:000}" : "cruise n/a";
        var runways = $"{(string.IsNullOrWhiteSpace(plan.OriginRunway) ? "--" : plan.OriginRunway)} -> {(string.IsNullOrWhiteSpace(plan.DestinationRunway) ? "--" : plan.DestinationRunway)}";
        return $"{plan.RouteLabel}  {plan.FlightNumber}\nAircraft {plan.AircraftIcao} {plan.AircraftRegistration} | {cruise} | runways {runways}\n{SimBriefNavigationSummary.Airac(plan)} | {generated}";
    }

    private void ShowSimBriefPayloadOverview()
    {
        var plan = _simBriefFlightPlan;
        using var dialog = new Form
        {
            Text = "SimBrief payload overview",
            Width = 760,
            Height = 720,
            MinimumSize = new System.Drawing.Size(620, 480),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable,
            MaximizeBox = true,
            MinimizeBox = false
        };
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        dialog.Controls.Add(root);

        var heading = new Label
        {
            Text = plan == null
                ? "No active SimBrief flight"
                : $"{plan.RouteLabel}  {plan.FlightNumber}   |   {plan.AircraftIcao} {plan.AircraftRegistration}".Trim(),
            AutoSize = true,
            Font = new System.Drawing.Font(Font.FontFamily, 12, System.Drawing.FontStyle.Bold),
            ForeColor = System.Drawing.Color.FromArgb(30, 69, 110),
            Margin = new Padding(0, 0, 0, 12)
        };
        root.Controls.Add(heading, 0, 0);

        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BorderStyle = BorderStyle.FixedSingle
        };
        root.Controls.Add(scroll, 0, 1);
        var values = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(12)
        };
        values.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        values.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        scroll.Controls.Add(values);

        void AddSection(string text)
        {
            var row = values.RowCount++;
            values.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var label = new Label
            {
                Text = text,
                AutoSize = true,
                Font = new System.Drawing.Font(Font.FontFamily, 10, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(40, 68, 106),
                Margin = new Padding(0, row == 0 ? 0 : 14, 0, 5)
            };
            values.Controls.Add(label, 0, row);
            values.SetColumnSpan(label, 2);
        }

        void AddValue(string name, string value)
        {
            var row = values.RowCount++;
            values.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            values.Controls.Add(new Label
            {
                Text = name,
                AutoSize = true,
                Font = new System.Drawing.Font(Font.FontFamily, 9, System.Drawing.FontStyle.Bold),
                Margin = new Padding(0, 4, 10, 4)
            }, 0, row);
            values.Controls.Add(new Label
            {
                Text = value,
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 4)
            }, 1, row);
        }

        if (plan == null)
        {
            AddValue(
                "Payload data",
                "Import and activate a SimBrief flight from Manage integrations first.");
        }
        else
        {
            AddSection("Passengers and cargo");
            AddValue("Passengers", SimBriefPayloadSummary.FormatCount(plan.PassengerCount));
            if (plan.MaximumPassengerCount.HasValue)
            {
                AddValue("Aircraft passenger capacity", SimBriefPayloadSummary.FormatCount(plan.MaximumPassengerCount));
            }
            AddValue("Weight per passenger", SimBriefPayloadSummary.FormatWeightPerUnit(plan.PassengerWeight, plan.Units));
            AddValue("Passenger mass", SimBriefPayloadSummary.FormatWeight(SimBriefPayloadSummary.PassengerMass(plan), plan.Units));
            AddValue("Checked bags", SimBriefPayloadSummary.FormatCount(plan.BaggageCount));
            AddValue("Weight per bag", SimBriefPayloadSummary.FormatWeightPerUnit(plan.BaggageWeight, plan.Units));
            AddValue("Baggage mass", SimBriefPayloadSummary.FormatWeight(SimBriefPayloadSummary.BaggageMass(plan), plan.Units));
            AddValue("Freight", SimBriefPayloadSummary.FormatWeight(plan.FreightWeight, plan.Units));
            AddValue("Total cargo", SimBriefPayloadSummary.FormatWeight(plan.CargoWeight, plan.Units));
            AddValue("Total payload", SimBriefPayloadSummary.FormatWeight(plan.PayloadWeight, plan.Units));

            AddSection("Fuel");
            AddValue("Block fuel", SimBriefPayloadSummary.FormatWeight(plan.BlockFuel, plan.Units));
            AddValue("Taxi fuel", SimBriefPayloadSummary.FormatWeight(plan.TaxiFuel, plan.Units));
            AddValue("Takeoff fuel", SimBriefPayloadSummary.FormatWeight(plan.TakeoffFuel, plan.Units));
            AddValue("Planned landing fuel", SimBriefPayloadSummary.FormatWeight(plan.LandingFuel, plan.Units));

            AddSection("Aircraft weights");
            AddValue("Operating empty weight", SimBriefPayloadSummary.FormatWeight(plan.OperatingEmptyWeight, plan.Units));
            AddValue("Zero fuel weight", SimBriefPayloadSummary.FormatWeight(plan.ZeroFuelWeight, plan.Units));
            AddValue("Ramp weight", SimBriefPayloadSummary.FormatWeight(plan.RampWeight, plan.Units));
            AddValue("Takeoff weight", SimBriefPayloadSummary.FormatWeight(plan.TakeoffWeight, plan.Units));
            AddValue("Landing weight", SimBriefPayloadSummary.FormatWeight(plan.LandingWeight, plan.Units));
        }

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 10, 0, 0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(new Label
        {
            Text = "Dispatch totals only. Loading stations and CG remain aircraft-specific because SimBrief does not provide their distribution.",
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(560, 0),
            ForeColor = System.Drawing.Color.DimGray,
            Margin = new Padding(0, 5, 12, 0)
        }, 0, 0);
        var close = new Button
        {
            Text = "Close",
            AutoSize = true,
            DialogResult = DialogResult.OK
        };
        footer.Controls.Add(close, 1, 0);
        root.Controls.Add(footer, 0, 2);
        dialog.AcceptButton = close;
        dialog.CancelButton = close;
        dialog.ShowDialog(this);
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
            _automation.Enqueue($"debug jump {item.Definition.Id}");
        }
    }

    private void ShowFeatureSettingsDialog()
    {
        using var dialog = new Form
        {
            Text = "Flight settings",
            Width = 590,
            Height = 720,
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

        void AddSectionHeader(string text)
        {
            var row = layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var header = new Label
            {
                Text = text,
                AutoSize = true,
                Font = new System.Drawing.Font(Font.FontFamily, 10, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(30, 69, 110),
                Margin = new Padding(0, 6, 0, 6)
            };
            layout.Controls.Add(header, 0, row);
            layout.SetColumnSpan(header, 3);
        }

        _settings.AircraftApproachOverrides ??= new List<AircraftApproachOverride>();
        var aircraftProfile = AircraftApproachProfiles.Resolve(_state?.Title);
        var savedOverride = _settings.AircraftApproachOverrides.LastOrDefault(item =>
            string.Equals(item.ProfileKey, aircraftProfile.Key, StringComparison.OrdinalIgnoreCase));
        var standardSchedule = aircraftProfile.StandardSchedule.Clone();
        var displayedSchedule = (savedOverride?.Schedule ?? standardSchedule).Clone();

        AddSectionHeader("Approach profile");
        var profileRow = layout.RowCount++;
        var profileLabel = new Label
        {
            Text = $"Loaded aircraft profile: {aircraftProfile.DisplayName}\n" +
                   (savedOverride == null
                       ? "Using the built-in standard baseline."
                       : "Using a saved airline-specific override."),
            AutoSize = true,
            Font = new System.Drawing.Font(Font.FontFamily, 9, System.Drawing.FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        };
        layout.Controls.Add(profileLabel, 0, profileRow);
        layout.SetColumnSpan(profileLabel, 3);

        var noteRow = layout.RowCount++;
        var profileNote = new Label
        {
            Text = "The baseline changes automatically with the detected aircraft. Airline, runway, weather and ATC procedures can differ; enable an override to save your own SOP for this aircraft only.",
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(530, 0),
            Margin = new Padding(0, 0, 0, 10)
        };
        layout.Controls.Add(profileNote, 0, noteRow);
        layout.SetColumnSpan(profileNote, 3);

        var overrideRow = layout.RowCount++;
        var useOverride = new CheckBox
        {
            Text = $"Use airline-specific override for {aircraftProfile.DisplayName}",
            Checked = savedOverride != null,
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 10)
        };
        layout.Controls.Add(useOverride, 0, overrideRow);
        layout.SetColumnSpan(useOverride, 3);

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
            displayedSchedule.Flaps1DistanceNm, 3, 30, "NM");
        var flapAltitude = AddNumber(
            "Flaps 1 maximum indicated altitude",
            displayedSchedule.Flaps1AltitudeFeet, 1000, 20000, "ft");
        var flapSpeed = AddNumber(
            "Flaps 1 max command speed",
            displayedSchedule.Flaps1SpeedKnots, 100, 250, "kt");
        var flap2Distance = AddNumber(
            "Flaps 2 target distance",
            displayedSchedule.Flaps2DistanceNm, 2, 25, "NM");
        var flap2Altitude = AddNumber(
            "Flaps 2 maximum radio altitude",
            displayedSchedule.Flaps2AltitudeAglFeet, 1000, 8000, "ft");
        var flap2Speed = AddNumber(
            "Flaps 2 max command speed",
            displayedSchedule.Flaps2SpeedKnots, 100, 230, "kt");
        var gearDistance = AddNumber(
            "Gear-down target distance",
            displayedSchedule.GearDistanceNm, 2, 20, "NM");
        var gearAltitude = AddNumber(
            "Gear-down maximum radio altitude",
            displayedSchedule.GearAltitudeAglFeet, 500, 5000, "ft");
        var gearSpeed = AddNumber(
            "Gear-down target speed",
            displayedSchedule.GearSpeedKnots, 100, 250, "kt");
        var landingDistance = AddNumber(
            "Landing configuration target distance",
            displayedSchedule.LandingConfigDistanceNm, 1, 15, "NM");
        var landingAltitude = AddNumber(
            "Landing configuration maximum radio altitude",
            displayedSchedule.LandingConfigAltitudeAglFeet, 300, 3000, "ft");
        var landingSpeed = AddNumber(
            "Landing config max command speed",
            displayedSchedule.LandingConfigSpeedKnots, 100, 220, "kt");
        var flapsFullSpeed = AddNumber(
            "Flaps FULL max command speed",
            displayedSchedule.FlapsFullSpeedKnots, 100, 220, "kt");

        var scheduleBoxes = new[]
        {
            flapDistance, flapAltitude, flapSpeed,
            flap2Distance, flap2Altitude, flap2Speed,
            gearDistance, gearAltitude, gearSpeed,
            landingDistance, landingAltitude, landingSpeed, flapsFullSpeed
        };
        void UpdateScheduleEditing()
        {
            foreach (var box in scheduleBoxes) box.Enabled = useOverride.Checked;
            profileLabel.Text = $"Loaded aircraft profile: {aircraftProfile.DisplayName}\n" +
                (useOverride.Checked
                    ? "Airline-specific override enabled."
                    : "Using the built-in standard baseline.");
        }
        useOverride.CheckedChanged += (_, _) => UpdateScheduleEditing();
        UpdateScheduleEditing();

        AddSectionHeader("Flow chaining");
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
        var flow5Chain = AddCheck(
            "Automatically start Flow 6 after Flow 5",
            _settings.AutoChainFlow5To6);
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
        var defaults = new Button { Text = "Use aircraft standard" };
        defaults.Click += (_, _) =>
        {
            useOverride.Checked = false;
            flapDistance.Value = standardSchedule.Flaps1DistanceNm;
            flapAltitude.Value = standardSchedule.Flaps1AltitudeFeet;
            flapSpeed.Value = standardSchedule.Flaps1SpeedKnots;
            flap2Distance.Value = standardSchedule.Flaps2DistanceNm;
            flap2Altitude.Value = standardSchedule.Flaps2AltitudeAglFeet;
            flap2Speed.Value = standardSchedule.Flaps2SpeedKnots;
            gearDistance.Value = standardSchedule.GearDistanceNm;
            gearAltitude.Value = standardSchedule.GearAltitudeAglFeet;
            gearSpeed.Value = standardSchedule.GearSpeedKnots;
            landingDistance.Value = standardSchedule.LandingConfigDistanceNm;
            landingAltitude.Value = standardSchedule.LandingConfigAltitudeAglFeet;
            landingSpeed.Value = standardSchedule.LandingConfigSpeedKnots;
            flapsFullSpeed.Value = standardSchedule.FlapsFullSpeedKnots;
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

        _settings.AircraftApproachOverrides.RemoveAll(item =>
            string.Equals(item.ProfileKey, aircraftProfile.Key, StringComparison.OrdinalIgnoreCase));
        if (useOverride.Checked)
        {
            _settings.AircraftApproachOverrides.Add(new AircraftApproachOverride
            {
                ProfileKey = aircraftProfile.Key,
                Schedule = new ApproachScheduleSettings
                {
                    Flaps1DistanceNm = (int)flapDistance.Value,
                    Flaps1AltitudeFeet = (int)flapAltitude.Value,
                    Flaps1SpeedKnots = (int)flapSpeed.Value,
                    Flaps2DistanceNm = (int)flap2Distance.Value,
                    Flaps2AltitudeAglFeet = (int)flap2Altitude.Value,
                    Flaps2SpeedKnots = (int)flap2Speed.Value,
                    GearDistanceNm = (int)gearDistance.Value,
                    GearAltitudeAglFeet = (int)gearAltitude.Value,
                    GearSpeedKnots = (int)gearSpeed.Value,
                    LandingConfigDistanceNm = (int)landingDistance.Value,
                    LandingConfigAltitudeAglFeet = (int)landingAltitude.Value,
                    LandingConfigSpeedKnots = (int)landingSpeed.Value,
                    FlapsFullSpeedKnots = (int)flapsFullSpeed.Value
                }
            });
        }
        _settings.AutoChainEarlierFlows = earlierChains.Checked;
        _settings.AutoChainFlow5To6 = flow5Chain.Checked;
        _settings.AutoChainFlow6To7 = flow6Chain.Checked;
        _settings.AutoChainFlow10To11 = flow10Chain.Checked;
        _settings.AutoChainFlow11To12 = flow11Chain.Checked;
        SettingsStore.Save(_settings);
        ApplyApproachSettingsToState();
        AppendDashboardLog(
            useOverride.Checked
                ? $"Airline-specific approach override saved for {aircraftProfile.DisplayName}."
                : $"Aircraft-standard approach profile restored for {aircraftProfile.DisplayName}.");
    }

    private void ApplyApproachSettingsToState()
    {
        if (_state == null)
        {
            return;
        }
        var schedule = AircraftApproachProfiles.EffectiveSchedule(
            _state.Title,
            _settings.AircraftApproachOverrides);
        _state.ApproachFlaps1DistanceNm = schedule.Flaps1DistanceNm;
        _state.ApproachFlaps1AltitudeFeet = schedule.Flaps1AltitudeFeet;
        _state.ApproachFlaps1SpeedKnots = schedule.Flaps1SpeedKnots;
        _state.ApproachFlaps2DistanceNm = schedule.Flaps2DistanceNm;
        _state.ApproachFlaps2AltitudeAglFeet = schedule.Flaps2AltitudeAglFeet;
        _state.ApproachFlaps2SpeedKnots = schedule.Flaps2SpeedKnots;
        _state.ApproachGearDistanceNm = schedule.GearDistanceNm;
        _state.ApproachGearAltitudeAglFeet = schedule.GearAltitudeAglFeet;
        _state.ApproachGearSpeedKnots = schedule.GearSpeedKnots;
        _state.ApproachLandingConfigDistanceNm =
            schedule.LandingConfigDistanceNm;
        _state.ApproachLandingConfigAltitudeAglFeet =
            schedule.LandingConfigAltitudeAglFeet;
        _state.ApproachLandingConfigSpeedKnots =
            schedule.LandingConfigSpeedKnots;
        _state.ApproachFlapsFullSpeedKnots = schedule.FlapsFullSpeedKnots;
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
        button.Click += (_, _) => _automation.Enqueue(command);
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
        bool emphasize,
        bool bindCommand = true)
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
        if (bindCommand)
        {
            button.Click += (_, _) => _automation.Enqueue(command);
        }
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
            Connection != null ? "MSFS CONNECTED" : "MSFS DISCONNECTED",
            Connection != null
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
                    : _state.IsIniBuildsA310
                    ? "iniBuilds A310-300"
                    : _state.IsFlyByWireA320Neo
                    ? "FBW A32NX"
                    : _state.IsPmdg777300Er
                    ? "PMDG 777-300ER INTEGRATION"
                    : _state.IsPmdg737800
                    ? "PMDG 737-800"
                    : _state.IsAsobo737Max8
                    ? "737 MAX EXPERIMENTAL"
                    : "AIRCRAFT UNSUPPORTED",
            _state.IsSupportedAircraft
                ? System.Drawing.Color.FromArgb(39, 130, 87)
                : System.Drawing.Color.FromArgb(172, 113, 37));
        SetStatusBadge(
            _adapterBadgeLabel,
            _state.IsPmdg777300Er
                ? _pmdg777Runtime.DataReady
                    ? "777 SDK OK"
                    : "777 SDK WAITING"
            : _state.IsPmdg737800
                ? _pmdgNg3Runtime.IsReady
                    ? "PMDG SDK OK"
                    : "PMDG SDK WAITING"
                : _state.IsAsobo737Max8
                    ? "MSFS/ASOBO OK"
                : _mobiFlightSession.AdapterReady ? "MOBIFLIGHT OK" : "ADAPTER OFFLINE",
            _state.IsPmdg777300Er
                ? _pmdg777Runtime.DataReady
                    ? System.Drawing.Color.FromArgb(39, 130, 87)
                    : System.Drawing.Color.FromArgb(172, 113, 37)
            : _state.IsPmdg737800
                ? _pmdgNg3Runtime.IsReady
                    ? System.Drawing.Color.FromArgb(39, 130, 87)
                    : System.Drawing.Color.FromArgb(172, 113, 37)
                : _state.IsAsobo737Max8
                    ? System.Drawing.Color.FromArgb(39, 130, 87)
                : _mobiFlightSession.AdapterReady
                ? System.Drawing.Color.FromArgb(39, 130, 87)
                : System.Drawing.Color.FromArgb(150, 48, 48));
        _electricalLabel!.Text =
            $"BAT 1 {_state.Battery1On.ToOnOff()} | BAT 2 {_state.Battery2On.ToOnOff()} | " +
            $"{(_state.IsIniBuildsA330 ? $"APU BAT {_state.ApuBatteryOn.ToOnOff()} | " : "")}" +
            $"{FormatExternalPowerSummary(_state)} | " +
            $"Beacon {_state.BeaconOn.ToOnOff()} | NAV&LOGO " +
            $"{(_state.NavLogoSelectorPosition.HasValue ? FormatNavLogoPosition((int)Math.Round(_state.NavLogoSelectorPosition.Value)) : "UNKNOWN")} | " +
            $"APU {_state.ApuMasterSwitchOn.ToOnOff()}/{_state.ApuRpmPercent:F0}%";
        _adapterLabel!.Text = _state.IsPmdg777300Er
            ? _pmdg777Runtime.DataReady
                ? "PMDG 777X SDK connected; Flow 1 BATTERY ON action and PMDG switch readback active."
                : "PMDG 777X SDK waiting - enable [SDK] EnableDataBroadcast=1 in 777_Options.ini and restart MSFS."
        : _state.IsPmdg737800
            ? _pmdgNg3Runtime.IsReady
                ? "PMDG NG3 SDK data connected"
                : "PMDG NG3 SDK waiting - enable [SDK] EnableDataBroadcast=1 in 737_Options.ini"
            : _state.IsAsobo737Max8
                ? "EXPERIMENTAL Asobo 737 MAX profile; incomplete live validation and no unattended Flow 7 use."
            : _state.IsIniBuildsA310
                ? "A310-300 gate-to-gate profile active."
            : _mobiFlightSession.AdapterReady
                ? "MobiFlight connected"
                : "MobiFlight not connected - aircraft controls unavailable";
        _adapterLabel.ForeColor = _state.IsPmdg777300Er
            ? _pmdg777Runtime.DataReady
                ? System.Drawing.Color.DarkGreen
                : System.Drawing.Color.DarkOrange
        : _state.IsPmdg737800
            ? _pmdgNg3Runtime.IsReady
                ? System.Drawing.Color.DarkGreen
                : System.Drawing.Color.DarkOrange
            : _state.IsAsobo737Max8
                ? System.Drawing.Color.DarkGreen
            : _mobiFlightSession.AdapterReady
            ? System.Drawing.Color.DarkGreen
            : System.Drawing.Color.DarkRed;
        _telemetryLabel!.Text = FormatCurrentStepTelemetry(_state);
        _telemetryLabel.ForeColor = _state.TelemetryIssues.Count == 0
            ? System.Drawing.Color.DarkSlateBlue
            : System.Drawing.Color.DarkRed;

        var gsxEngineStartPending = _pendingGsxEngineStartProcedure != null;
        var taxiToHoldingPoint = IsTaxiToHoldingPointTransition(_state);
        var definition = _pendingGsxEngineStartProcedure ?? _procedureRunner.Definition;
        var currentStep = gsxEngineStartPending ? null : _procedureRunner.CurrentStep;
        SetStatusBadge(
            _flowBadgeLabel,
            gsxEngineStartPending
                ? $"WAITING FOR GSX - {definition!.Name.Split('.')[0]}"
                : taxiToHoldingPoint
                ? "READY TO TAXI - FLOW 6 AT HOLD"
                : definition == null
                ? "FLOW IDLE"
                : $"{FormatProcedureStatus(_procedureRunner.Status).ToUpperInvariant()} - {definition.Name.Split('.')[0]}",
            gsxEngineStartPending
                ? System.Drawing.Color.FromArgb(190, 126, 37)
                : _procedureRunner.Status switch
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
            gsxEngineStartPending
                ? $"{definition!.Name} - Waiting for GSX pushback"
                : definition == null
                ? "None"
                : $"{definition.Name} - {_procedureRunner.Status} - {definition.AutomationSummary}";
        _stepLabel!.Text =
            gsxEngineStartPending
                ? "Current step: GSX pushback preparation"
                : taxiToHoldingPoint
                ? "Next action: Taxi to the runway holding point"
                : currentStep == null
                ? "No active step"
                : $"Current step: {currentStep.Label} " +
                  $"({FormatCrewRole(currentStep.AssignedRole)})";
        _waitingForLabel!.Text = gsxEngineStartPending
            ? FormatGsxPushbackWaitingReason()
            : taxiToHoldingPoint
                ? TaxiToHoldingPointGuidance
            : FormatWaitingReason(currentStep, _state, _procedureRunner.Status);
        _waitingForLabel.ForeColor = _procedureRunner.Status == ProcedureStatus.Failed
            ? System.Drawing.Color.DarkRed
            : gsxEngineStartPending
                ? System.Drawing.Color.DarkOrange
                : System.Drawing.Color.DimGray;
        _procedureProgress!.Maximum = Math.Max(1, definition?.Steps.Count ?? 1);
        _procedureProgress.Value = Math.Min(
            _procedureProgress.Maximum,
            gsxEngineStartPending ? 0 : _procedureRunner.CompletedStepCount);
        if (_stepProgressLabel != null)
        {
            var totalSteps = definition?.Steps.Count ?? 0;
            var completedSteps = gsxEngineStartPending
                ? 0
                : Math.Min(totalSteps, _procedureRunner.CompletedStepCount);
            var percent = totalSteps == 0
                ? 0
                : (int)Math.Round(completedSteps * 100d / totalSteps);
            _stepProgressLabel.Text = totalSteps == 0
                ? "No flow active"
                : $"{completedSteps} of {totalSteps} steps complete ({percent}%)";
        }

        var recommendation = FlowRecommendationEngine.Recommend(
            _state,
            _completedProcedureIds);
        _recommendationLabel!.Text =
            recommendation.Procedure == null
                ? recommendation.Reason
                : $"{recommendation.Procedure.Name} - {recommendation.Reason}";
        _recommendationLabel.ForeColor = recommendation.Overdue
            ? System.Drawing.Color.DarkRed
            : System.Drawing.Color.DarkBlue;
        RefreshFlowList(recommendation.Procedure?.Id, definition?.Id);
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

        if (probe.Contains("737") || probe.Contains("B738"))
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

        if (_pendingGsxEngineStartProcedure != null)
        {
            _statusBadgeLabel.Text = "Status: Waiting for GSX";
            _statusBadgeLabel.ForeColor = System.Drawing.Color.DarkOrange;
            return;
        }

        if (IsTaxiToHoldingPointTransition(_state))
        {
            _statusBadgeLabel.Text = "Status: Ready to taxi";
            _statusBadgeLabel.ForeColor = System.Drawing.Color.DarkBlue;
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
        var waitingForGsx = _pendingGsxEngineStartProcedure != null;
        var waitingForBoarding = IsPushbackClearanceBlockedByGsx(
            _procedureRunner.CurrentStep);
        var taxiToHoldingPoint = IsTaxiToHoldingPointTransition(_state);

        if (_startFirstFlowButton != null)
        {
            _startFirstFlowButton.Text = taxiToHoldingPoint
                ? "Taxi to holding point"
                : "Start first flow";
            _startFirstFlowButton.Enabled = !waitingForGsx && !taxiToHoldingPoint;
            _startFirstFlowButton.BackColor = waitingForGsx
                ? System.Drawing.Color.FromArgb(107, 114, 128)
                : taxiToHoldingPoint
                    ? System.Drawing.Color.FromArgb(30, 64, 175)
                : System.Drawing.Color.FromArgb(39, 130, 87);
        }

        if (_startSelectedFlowButton != null)
        {
            _startSelectedFlowButton.Text = waitingForGsx
                ? "Waiting for GSX"
                : status == ProcedureStatus.Paused
                ? "Flow paused"
                : active
                    ? "Flow running"
                    : "Start selected flow";
            _startSelectedFlowButton.BackColor = waitingForGsx
                ? System.Drawing.Color.FromArgb(190, 126, 37)
                : status switch
            {
                ProcedureStatus.Paused => System.Drawing.Color.FromArgb(151, 110, 35),
                ProcedureStatus.Running => System.Drawing.Color.FromArgb(30, 64, 175),
                ProcedureStatus.WaitingForVerification => System.Drawing.Color.FromArgb(29, 78, 216),
                ProcedureStatus.WaitingForManualAction => System.Drawing.Color.FromArgb(190, 126, 37),
                _ => System.Drawing.Color.FromArgb(39, 130, 87)
            };
            _startSelectedFlowButton.ForeColor = System.Drawing.Color.White;
            _startSelectedFlowButton.Enabled = !waitingForGsx;
            _startSelectedFlowButton.FlatAppearance.BorderSize = active || waitingForGsx ? 2 : 0;
            _startSelectedFlowButton.FlatAppearance.BorderColor =
                active || waitingForGsx
                    ? System.Drawing.Color.FromArgb(15, 23, 42)
                    : _startSelectedFlowButton.BackColor;
        }

        if (_confirmCompletedButton != null)
        {
            var waitingForPilot = status == ProcedureStatus.WaitingForManualAction;
            var waitingForAtc = _pendingSayIntentionsAtcStepId != null;
            _confirmCompletedButton.BackColor = waitingForGsx
                ? System.Drawing.Color.FromArgb(107, 114, 128)
                : waitingForPilot
                ? System.Drawing.Color.FromArgb(194, 65, 12)
                : System.Drawing.Color.FromArgb(39, 130, 87);
            _confirmCompletedButton.FlatAppearance.MouseDownBackColor = waitingForGsx
                ? System.Drawing.Color.FromArgb(107, 114, 128)
                : waitingForPilot
                ? System.Drawing.Color.FromArgb(146, 64, 14)
                : System.Drawing.Color.FromArgb(22, 101, 52);
            _confirmCompletedButton.FlatAppearance.MouseOverBackColor = waitingForGsx
                ? System.Drawing.Color.FromArgb(107, 114, 128)
                : waitingForPilot
                ? System.Drawing.Color.FromArgb(245, 158, 11)
                : System.Drawing.Color.FromArgb(34, 148, 96);
            _confirmCompletedButton.Enabled = !waitingForGsx
                                              && !taxiToHoldingPoint
                                              && !waitingForBoarding
                                              && !_sayIntentionsHandoffInProgress
                                              && !waitingForAtc;
            _confirmCompletedButton.Text = taxiToHoldingPoint
                ? "Flow 6 starts at hold"
                : waitingForGsx
                ? "Waiting for GSX..."
                : waitingForBoarding
                ? "Waiting for boarding..."
                : _sayIntentionsHandoffInProgress
                ? "Handing ATC to F/O..."
                : waitingForAtc
                    ? "Waiting for ATC..."
                : waitingForPilot
                    ? "Confirm now"
                    : "Confirm completed";
        }
    }

    private string FormatGsxPushbackWaitingReason()
    {
        var gsx = _gsx.Snapshot;
        var tooltip = _gsx.StatusSnapshot(DateTime.UtcNow);
        var status = tooltip.Count > 0
            ? string.Join(" | ", tooltip)
            : "GSX is preparing the tug.";
        var appearsStalled = gsx.DepartureRequestAcceptedUtc.HasValue
                             && DateTime.UtcNow - gsx.DepartureRequestAcceptedUtc.Value
                             >= TimeSpan.FromMinutes(2);
        return appearsStalled
            ? $"Waiting for GSX: {status} GSX has not progressed for over two minutes; check the GSX toolbar/status."
            : $"Waiting for GSX: {status} Flow 4 will start automatically when pushback movement begins.";
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

    private string FormatWaitingReason(
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
            if (IsPushbackClearanceBlockedByGsx(step))
            {
                return "Waiting for: GSX boarding completion before requesting pushback and engine-start clearance.";
            }
            if (_sayIntentionsRuntime.Flight != null && IsSayIntentionsAtcStep(step.Id))
            {
                return step.Id switch
                {
                    "captain-ifr-clearance" =>
                        "Waiting for: IFR clearance. Press Confirm to instruct the SayIntentions First Officer to request clearance.",
                    "captain-pushback-clearance" =>
                        "Waiting for: pushback/engine-start clearance. Press Confirm to instruct the SayIntentions First Officer to make the request.",
                    "fo-taxi-clearance" =>
                        "Waiting for: taxi clearance. Press Confirm to instruct the SayIntentions First Officer to make the request.",
                    _ =>
                        "Waiting for: takeoff clearance. While holding short, press Confirm to instruct the SayIntentions First Officer to report ready for departure."
                };
            }
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
                state.IsIniBuildsA330
                    ? $"Flaps 1 gate: distance <= {state.ApproachFlaps1DistanceNm} NM; altitude <= {state.ApproachFlaps1AltitudeFeet:N0} ft only without distance data."
                    : state.IsFlyByWireA320Neo
                        ? $"Flaps 1 gate: distance <= {state.ApproachFlaps1DistanceNm} NM and altitude <= {state.ApproachFlaps1AltitudeFeet:N0} ft; altitude is the fallback without distance data."
                    : $"Flaps 1 gate: distance <= {state.ApproachFlaps1DistanceNm} NM or altitude <= {state.ApproachFlaps1AltitudeFeet:N0} ft.",
            "flaps-one-speed" =>
                $"Flaps CONFIG 1 speed safe: IAS {state.IndicatedAirspeedKnots:F0} kt <= {state.EffectiveApproachFlaps1SpeedKnots} kt.",
            "flaps-two-point" =>
                state.IsIniBuildsA330
                    ? $"Flaps 2 gate: distance <= {state.ApproachFlaps2DistanceNm} NM; radio altitude <= {state.ApproachFlaps2AltitudeAglFeet:N0} ft only without distance data."
                    : state.IsFlyByWireA320Neo
                        ? $"Flaps 2 gate: distance <= {state.ApproachFlaps2DistanceNm} NM and radio altitude <= {state.ApproachFlaps2AltitudeAglFeet:N0} ft; radio altitude is the fallback without distance data."
                    : $"Flaps 2 gate: distance <= {state.ApproachFlaps2DistanceNm} NM or radio altitude <= {state.ApproachFlaps2AltitudeAglFeet:N0} ft.",
            "flaps-two-speed" =>
                $"Flaps CONFIG 2 speed safe: IAS {state.IndicatedAirspeedKnots:F0} kt <= {state.EffectiveApproachFlaps2SpeedKnots} kt.",
            "gear-down-point" =>
                state.IsIniBuildsA330
                    ? $"gear gate: distance <= {state.ApproachGearDistanceNm} NM; radio altitude <= {state.ApproachGearAltitudeAglFeet:N0} ft only without distance data."
                    : state.IsFlyByWireA320Neo
                        ? $"gear gate: distance <= {state.ApproachGearDistanceNm} NM and radio altitude <= {state.ApproachGearAltitudeAglFeet:N0} ft; radio altitude is the fallback without distance data."
                    : $"gear gate: distance <= {state.ApproachGearDistanceNm} NM or radio altitude <= {state.ApproachGearAltitudeAglFeet:N0} ft.",
            "landing-config-point" =>
                state.IsIniBuildsA330
                    ? $"landing-config gate: distance <= {state.ApproachLandingConfigDistanceNm} NM; radio altitude <= {state.ApproachLandingConfigAltitudeAglFeet:N0} ft only without distance data."
                    : state.IsFlyByWireA320Neo
                        ? $"landing-config gate: distance <= {state.ApproachLandingConfigDistanceNm} NM and radio altitude <= {state.ApproachLandingConfigAltitudeAglFeet:N0} ft; radio altitude is the fallback without distance data."
                    : $"landing-config gate: distance <= {state.ApproachLandingConfigDistanceNm} NM or radio altitude <= {state.ApproachLandingConfigAltitudeAglFeet:N0} ft.",
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

        var distanceAvailable =
            state.ApproachDistanceToTouchdownNm.HasValue
            && state.ApproachDistanceToTouchdownNm.Value > 0;
        var distanceReached =
            distanceAvailable
            && state.ApproachDistanceToTouchdownNm.GetValueOrDefault() <= distanceGate;
        var gateReady = state.IsFlyByWireA320Neo
            ? fallbackReached && (!distanceAvailable || distanceReached)
            : state.IsIniBuildsA330
                ? distanceAvailable ? distanceReached : fallbackReached
                : distanceReached || fallbackReached;
        var distanceText = state.ApproachDistanceToTouchdownNm.HasValue
            ? $"{state.ApproachDistanceToTouchdownNm.Value:F1} NM {state.ApproachDistanceSource}"
            : "n/a";
        var blockers = new List<string>();
        if (!gateReady)
        {
            if (!distanceReached && distanceAvailable)
            {
                blockers.Add($"distance not reached ({distanceText})");
            }
            if (!fallbackReached
                && (state.IsFlyByWireA320Neo || !distanceAvailable))
            {
                blockers.Add($"vertical gate not reached ({fallbackLabel})");
            }
            if (blockers.Count == 0)
            {
                blockers.Add($"distance/fallback not reached ({distanceText}, {fallbackLabel})");
            }
        }

        description =
            "Approach gate status: " +
            $"IAS {state.IndicatedAirspeedKnots:F0} kt (speed reference {speedGate} kt), " +
            $"ALT {state.IndicatedAltitudeFeet:F0} ft, " +
            $"AGL {state.AltitudeAboveGroundFeet:F0} ft, " +
            $"DIST {distanceText} <= {distanceGate} NM, " +
            $"vertical {fallbackLabel} {(fallbackReached ? "met" : distanceAvailable && state.IsIniBuildsA330 ? "ignored while distance is available" : "not met")}; " +
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
        var simBrief = string.IsNullOrWhiteSpace(state.SimBriefDestinationIcao)
            ? ""
            : $" | OFP {state.SimBriefOriginIcao}-{state.SimBriefDestinationIcao} FL{state.PlannedCruiseAltitudeFeet / 100:000}";
        return stepId switch
        {
            "fo-v1" => $"{flight} | target V1 {state.TakeoffV1SpeedKnots} kt",
            "fo-rotate" => $"{flight} | target VR {state.TakeoffRotateSpeedKnots} kt",
            "fmc-perf" => $"{flight} | {state.SimBriefTakeoffStatus}",
            "cruise-established" => $"{flight}{simBrief}",
            "captain-fmc-arrival" or "captain-briefing" => $"{flight}{distance}{simBrief}",
            "approach-config-start" =>
                $"{flight} | trigger <={state.ApproachFlaps1AltitudeFeet:N0} ft indicated or distance gate",
            "flaps-one-speed" =>
                $"{flight} | wait IAS <={state.EffectiveApproachFlaps1SpeedKnots} kt for CONFIG 1",
            "flaps-five-gate" =>
                $"{flight}{distance} | trigger <={state.ApproachFlaps2DistanceNm} NM; fallback <={state.ApproachFlaps2AltitudeAglFeet:N0} ft AGL only without distance",
            "flaps-five-speed" =>
                $"{flight} | wait IAS <={state.EffectiveApproachFlaps2SpeedKnots} kt for Flaps 5",
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

    private void RefreshFlowList(string? recommendedId, string? activeId)
    {
        if (_flowList == null)
        {
            return;
        }

        var procedures = ProcedureCatalog.ForAircraft(_state);
        var refreshRequired = _flowList.Items.Count != procedures.Count;
        for (var index = 0; !refreshRequired && index < procedures.Count; index++)
        {
            var procedure = procedures[index];
            refreshRequired = !(_flowList.Items[index] is ProcedureListItem existing)
                || !string.Equals(existing.Definition.Id, procedure.Id, StringComparison.OrdinalIgnoreCase)
                || existing.Completed != _completedProcedureIds.Contains(procedure.Id)
                || existing.Recommended != (procedure.Id == recommendedId)
                || existing.Active != (procedure.Id == activeId);
        }
        if (!refreshRequired)
        {
            return;
        }

        var selectedIndex = _flowList.SelectedIndex;
        var topIndex = _flowList.Items.Count > 0 ? _flowList.TopIndex : 0;
        _flowList.BeginUpdate();
        for (var index = 0; index < procedures.Count; index++)
        {
            var procedure = procedures[index];
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
        while (_flowList.Items.Count > procedures.Count)
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

    private void OnEfbCommBusEvent(
        SimConnect sender,
        SIMCONNECT_RECV_COMM_BUS data)
    {
        if (!_efbTransport.TryAcceptCommandChunk(
                (EfbCommBusEvent)data.uEventID,
                data.dwEntryNumber,
                data.dwOutOf,
                data.rgData,
                out var payload))
        {
            return;
        }

        AppLog.Write(
            $"Received MSFS EFB CommBus command payload ({payload.Length} chars).");
        HandleEfbCommand(payload);
    }

    private void HandleEfbCommand(string payload)
    {
        if (!EfbCompanionProtocol.TryParseCommand(
                payload,
                out var command,
                out var parseError))
        {
            SendEfbCommandResult(string.Empty, false, parseError);
            AppLog.Write($"Rejected malformed EFB command: {parseError}");
            return;
        }

        if (command.Action == "request_state")
        {
            // A bounded acknowledgement lets the EFB distinguish a busy state
            // renderer from a disconnected desktop. Rate limiting also keeps
            // older EFB builds from recreating their acknowledgement loop.
            var now = DateTime.UtcNow;
            if (!_efbTransport.CanAcknowledgeStateRequest(now))
            {
                return;
            }

            SendEfbCommandResult(
                command.RequestId,
                true,
                "State refresh acknowledged.");
            PublishEfbState(force: true);
            return;
        }

        if (_state == null)
        {
            SendEfbCommandResult(
                command.RequestId,
                false,
                "Aircraft telemetry is not available yet.");
            return;
        }

        var status = _procedureRunner.Status;
        var active = IsProcedureActive(status)
                     || _pendingGsxEngineStartProcedure != null;
        switch (command.Action)
        {
            case "start_next_flow":
            {
                if (active)
                {
                    SendEfbCommandResult(
                        command.RequestId,
                        false,
                        "Another flow is already active.");
                    return;
                }

                var recommendation = FlowRecommendationEngine.Recommend(
                    _state,
                    _completedProcedureIds).Procedure;
                if (recommendation == null
                    || _completedProcedureIds.Contains(recommendation.Id))
                {
                    SendEfbCommandResult(
                        command.RequestId,
                        false,
                        "No next flow is available for the current aircraft.");
                    return;
                }

                if (!CanStartProcedureNow(
                        recommendation,
                        _state,
                        out var startReason))
                {
                    SendEfbCommandResult(
                        command.RequestId,
                        false,
                        startReason);
                    return;
                }

                _automation.Enqueue($"procedure start {recommendation.Id}");
                SendEfbCommandResult(
                    command.RequestId,
                    true,
                    $"Starting {recommendation.Name}.");
                break;
            }
            case "start_flow":
            {
                if (active)
                {
                    SendEfbCommandResult(
                        command.RequestId,
                        false,
                        "Another flow is already active.");
                    return;
                }

                var definition = ProcedureCatalog.ForAircraft(_state)
                    .FirstOrDefault(
                        item => string.Equals(
                            item.Id,
                            command.FlowId,
                            StringComparison.OrdinalIgnoreCase));
                if (definition == null)
                {
                    SendEfbCommandResult(
                        command.RequestId,
                        false,
                        "That flow is not available for the current aircraft.");
                    return;
                }

                var recommendation = FlowRecommendationEngine.Recommend(
                    _state,
                    _completedProcedureIds).Procedure;
                if (recommendation == null
                    || !string.Equals(
                        definition.Id,
                        recommendation.Id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    SendEfbCommandResult(
                        command.RequestId,
                        false,
                        recommendation == null
                            ? "No flow is currently eligible to start."
                            : $"Complete {recommendation.Name} before starting {definition.Name}.");
                    return;
                }

                if (!CanStartProcedureNow(
                        definition,
                        _state,
                        out var startReason))
                {
                    SendEfbCommandResult(
                        command.RequestId,
                        false,
                        startReason);
                    return;
                }

                _automation.Enqueue($"procedure start {definition.Id}");
                SendEfbCommandResult(
                    command.RequestId,
                    true,
                    $"Starting {definition.Name}.");
                break;
            }
            case "gsx_open_menu":
            {
                var gsx = _gsx.Snapshot;
                if (!_settings.EnableGsxIntegration
                    || _gsxInstallation == null
                    || !gsx.CouatlStarted)
                {
                    SendEfbCommandResult(
                        command.RequestId,
                        false,
                        "GSX is not currently available.");
                    return;
                }

                if (gsx.RemoteControlActive && !gsx.OwnsRemoteControl)
                {
                    SendEfbCommandResult(
                        command.RequestId,
                        false,
                        "GSX remote control is currently owned by another add-on.");
                    return;
                }

                if (!gsx.OwnsRemoteControl)
                {
                    _gsx.ClaimRemoteControl(DateTime.UtcNow);
                    UpdateGsxStatus();
                }

                _gsx.OpenMenu();
                SendEfbCommandResult(
                    command.RequestId,
                    true,
                    "Opening the current GSX menu.");
                AppLog.Write("EFB requested the current GSX menu.");
                break;
            }
            case "gsx_menu_choice":
            {
                var choiceIndex = command.ChoiceIndex ?? -1;
                var gsx = _gsx.Snapshot;
                if (!_settings.EnableGsxIntegration
                    || !gsx.MenuOpen
                    || gsx.CurrentMenu.IsEmpty
                    || GsxPromptPolicy.IsRootServicesMenu(gsx.CurrentMenu))
                {
                    SendEfbCommandResult(
                        command.RequestId,
                        false,
                        "GSX is not waiting for a selectable response.");
                    return;
                }

                if (!GsxPromptPolicy.CanSubmitRemoteChoice(
                        gsx.MenuOpen,
                        gsx.CurrentMenu,
                        choiceIndex))
                {
                    SendEfbCommandResult(
                        command.RequestId,
                        false,
                        "That GSX menu choice is no longer available.");
                    return;
                }

                var choiceLabel = gsx.CurrentMenu.Choices[choiceIndex];
                RequestGsxMenuChoice(
                    choiceIndex,
                    choiceLabel,
                    command.RequestId);
                CloseGsxChoiceDialog();
                break;
            }
            case "confirm":
                if (status != ProcedureStatus.WaitingForManualAction)
                {
                    SendEfbCommandResult(
                        command.RequestId,
                        false,
                        "No pilot action is waiting for confirmation.");
                    return;
                }
                if (_sayIntentionsHandoffInProgress
                    || _pendingSayIntentionsAtcStepId != null)
                {
                    SendEfbCommandResult(
                        command.RequestId,
                        false,
                        "The First Officer is already waiting for the ATC response.");
                    return;
                }

                _ = HandleConfirmButtonAsync();
                SendEfbCommandResult(
                    command.RequestId,
                    true,
                    "Confirmation requested.");
                break;
            case "pause":
                if (status is not ProcedureStatus.Running
                    and not ProcedureStatus.WaitingForManualAction
                    and not ProcedureStatus.WaitingForVerification)
                {
                    SendEfbCommandResult(
                        command.RequestId,
                        false,
                        "The current flow cannot be paused.");
                    return;
                }
                _automation.Enqueue("procedure pause");
                SendEfbCommandResult(command.RequestId, true, "Pausing flow.");
                break;
            case "resume":
                if (status != ProcedureStatus.Paused && status != ProcedureStatus.Failed)
                {
                    SendEfbCommandResult(
                        command.RequestId,
                        false,
                        "The current flow is not paused or failed.");
                    return;
                }
                _automation.Enqueue("procedure resume");
                SendEfbCommandResult(command.RequestId, true, "Resuming flow.");
                break;
            case "cancel":
                if (!active)
                {
                    SendEfbCommandResult(
                        command.RequestId,
                        false,
                        "No flow is active.");
                    return;
                }
                _automation.Enqueue("procedure cancel");
                SendEfbCommandResult(command.RequestId, true, "Cancelling flow.");
                break;
        }

        AppLog.Write(
            $"Accepted MSFS EFB command '{command.Action}' "
            + $"({command.RequestId}).");
    }

    private void SendEfbCommandResult(
        string requestId,
        bool accepted,
        string message)
    {
        if (Connection == null)
        {
            return;
        }

        _efbTransport.SendEnvelope(
            Connection,
            _efbTransport.CreateCommandResultEnvelope(
                requestId,
                accepted,
                message,
                DateTime.UtcNow),
            AppLog.Write);
    }

    private void PublishEfbState(bool force = false)
    {
        if (Connection == null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (!_efbTransport.ShouldPublishState(now, force))
        {
            return;
        }

        var envelope = BuildEfbStateEnvelope(EfbCompanionProtocol.Version);
        _efbTransport.SendEnvelope(Connection, envelope, AppLog.Write);
    }

    private Dictionary<string, object?> BuildEfbStateEnvelope(int protocolVersion)
    {
        var state = _state;
        var gsx = GetGsxLiveState();
        var gsxRuntime = _gsx.Snapshot;
        var definition =
            _pendingGsxEngineStartProcedure ?? _procedureRunner.Definition;
        var currentStep = _pendingGsxEngineStartProcedure == null
            ? _procedureRunner.CurrentStep
            : null;
        var recommendation = state == null
            ? null
            : FlowRecommendationEngine.Recommend(
                state,
                _completedProcedureIds).Procedure;
        var totalSteps = definition?.Steps.Count ?? 0;
        var completedSteps = _pendingGsxEngineStartProcedure != null
            ? 0
            : Math.Min(totalSteps, _procedureRunner.CompletedStepCount);
        var taxiToHoldingPoint = IsTaxiToHoldingPointTransition(state);
        var waitingFor = state == null
            ? "Waiting for aircraft telemetry."
            : _pendingGsxEngineStartProcedure != null
                ? FormatGsxPushbackWaitingReason()
                : taxiToHoldingPoint
                    ? TaxiToHoldingPointGuidance
                : FormatWaitingReason(
                    currentStep,
                    state,
                    _procedureRunner.Status);
        var statusText = _pendingGsxEngineStartProcedure != null
            ? "Waiting for GSX"
            : taxiToHoldingPoint
                ? "Ready to taxi"
            : FormatProcedureStatus(_procedureRunner.Status);
        var procedureActive =
            IsProcedureActive(_procedureRunner.Status)
            || _pendingGsxEngineStartProcedure != null;

        var flows = state == null
            ? Array.Empty<object>()
            : ProcedureCatalog.ForAircraft(state)
                .Select(
                    flow => (object)new Dictionary<string, object?>
                    {
                        ["id"] = flow.Id,
                        ["name"] = flow.Name,
                        ["automationSummary"] = flow.AutomationSummary,
                        ["state"] = string.Equals(
                                        flow.Id,
                                        definition?.Id,
                                        StringComparison.OrdinalIgnoreCase)
                            ? "current"
                            : _completedProcedureIds.Contains(flow.Id)
                                ? "done"
                                : string.Equals(
                                    flow.Id,
                                    recommendation?.Id,
                                    StringComparison.OrdinalIgnoreCase)
                                    ? "next"
                                    : "upcoming"
                    })
                .ToArray();

        return new Dictionary<string, object?>
        {
            ["protocolVersion"] = protocolVersion,
            ["kind"] = "state",
            ["sentUtc"] = DateTime.UtcNow.ToString("O"),
            ["companionVersion"] = GetApplicationVersion(),
            ["connected"] = Connection != null,
            ["aircraftReady"] = state != null,
            ["aircraft"] = new Dictionary<string, object?>
            {
                ["title"] = state?.Title ?? "Waiting for aircraft",
                ["family"] = state?.AircraftFamilyLabel ?? "Unknown",
                ["supported"] = state?.IsSupportedAircraft == true,
                ["warning"] = state?.IsAsobo737Max8 == true
                    ? "Development beta: MAX Flow 7 is not cleared for unattended use."
                    : null,
                ["phase"] = state == null
                    ? "Unknown"
                    : OperationalPhaseDetector.Detect(state).ToString()
            },
            ["telemetry"] = new Dictionary<string, object?>
            {
                ["aglFeet"] = state?.AltitudeAboveGroundFeet ?? 0,
                ["altitudeFeet"] = state?.IndicatedAltitudeFeet ?? 0,
                ["airspeedKnots"] = state?.IndicatedAirspeedKnots ?? 0,
                ["verticalSpeedFpm"] = state?.VerticalSpeedFeetPerMinute ?? 0
            },
            ["flow"] = new Dictionary<string, object?>
            {
                ["id"] = definition?.Id,
                ["name"] = definition?.Name ?? "No active flow",
                ["status"] = statusText,
                ["currentStepId"] = currentStep?.Id,
                ["currentStep"] = taxiToHoldingPoint
                    ? "Taxi to the runway holding point"
                    : currentStep?.Label ?? "No active step",
                ["assignedRole"] = currentStep == null
                    ? string.Empty
                    : FormatCrewRole(currentStep.AssignedRole),
                ["completedSteps"] = completedSteps,
                ["totalSteps"] = totalSteps,
                ["waitingFor"] = waitingFor,
                ["guidance"] = taxiToHoldingPoint
                    ? TaxiToHoldingPointGuidance
                    : "Continue the gate-to-gate sequence",
                ["transition"] = taxiToHoldingPoint
                    ? "taxi-to-hold"
                    : null,
                ["canStart"] =
                    state != null
                    && !procedureActive
                    && recommendation != null
                    && CanStartProcedureNow(
                        recommendation,
                        state,
                        out _),
                ["canConfirm"] =
                    _procedureRunner.Status
                        == ProcedureStatus.WaitingForManualAction
                    && !IsPushbackClearanceBlockedByGsx(currentStep)
                    && !_sayIntentionsHandoffInProgress
                    && _pendingSayIntentionsAtcStepId == null,
                ["canPause"] = _procedureRunner.Status
                    is ProcedureStatus.Running
                    or ProcedureStatus.WaitingForManualAction
                    or ProcedureStatus.WaitingForVerification,
                ["canResume"] =
                    _procedureRunner.Status == ProcedureStatus.Paused
                    || _procedureRunner.Status == ProcedureStatus.Failed,
                ["canCancel"] = procedureActive
            },
            ["flows"] = flows,
            ["gsx"] = new Dictionary<string, object?>
            {
                ["summary"] = gsx.SummaryText,
                ["passengerOperation"] = gsx.PassengerOperationText,
                ["passengerProgress"] = gsx.PassengerProgressText,
                ["passengerPercent"] = gsx.PassengerPercent ?? 0,
                ["actionRequired"] = gsx.ActionRequiredText,
                ["hasActionRequired"] = gsx.HasActionRequired,
                ["activeServices"] = gsx.ActiveServices.ToArray(),
                ["promptTitle"] =
                    gsxRuntime.MenuOpen
                    && !gsxRuntime.CurrentMenu.IsEmpty
                    && !GsxPromptPolicy.IsRootServicesMenu(
                        gsxRuntime.CurrentMenu)
                        ? gsxRuntime.CurrentMenu.Title
                        : null,
                ["choices"] =
                    gsxRuntime.MenuOpen
                    && !gsxRuntime.CurrentMenu.IsEmpty
                    && !GsxPromptPolicy.IsRootServicesMenu(
                        gsxRuntime.CurrentMenu)
                        ? gsxRuntime.CurrentMenu.Choices.ToArray()
                        : Array.Empty<string>(),
                ["canOpenMenu"] =
                    _settings.EnableGsxIntegration
                    && _gsxInstallation != null
                    && gsxRuntime.CouatlStarted
                    && (!gsxRuntime.RemoteControlActive
                        || gsxRuntime.OwnsRemoteControl)
            }
        };
    }

    private void DrawFlowItem(object? sender, DrawItemEventArgs e)
    {
        if (_flowList == null
            || e.Index < 0
            || e.Index >= _flowList.Items.Count
            || !(_flowList.Items[e.Index] is ProcedureListItem item))
        {
            return;
        }

        var selected = (e.State & DrawItemState.Selected) != 0;
        var background = selected
            ? System.Drawing.Color.FromArgb(232, 240, 249)
            : item.Active
                ? System.Drawing.Color.FromArgb(239, 246, 255)
                : System.Drawing.Color.White;
        using (var backgroundBrush = new System.Drawing.SolidBrush(background))
        {
            e.Graphics.FillRectangle(backgroundBrush, e.Bounds);
        }

        var markerColor = item.Completed
            ? System.Drawing.Color.FromArgb(39, 130, 87)
            : item.Active
                ? System.Drawing.Color.FromArgb(37, 99, 235)
                : item.Recommended
                    ? System.Drawing.Color.FromArgb(217, 119, 6)
                    : System.Drawing.Color.FromArgb(209, 213, 219);
        using (var markerBrush = new System.Drawing.SolidBrush(markerColor))
        {
            e.Graphics.FillEllipse(
                markerBrush,
                e.Bounds.Left + 8,
                e.Bounds.Top + 8,
                14,
                14);
        }

        var markerText = item.Completed ? "ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦ÃƒÂ¢Ã¢â€šÂ¬Ã…â€œÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã¢â‚¬Å“" : (e.Index + 1).ToString();
        using (var markerFont = new System.Drawing.Font(
                   Font.FontFamily,
                   7,
                   System.Drawing.FontStyle.Bold))
        {
            TextRenderer.DrawText(
                e.Graphics,
                markerText,
                markerFont,
                new System.Drawing.Rectangle(e.Bounds.Left + 7, e.Bounds.Top + 7, 16, 16),
                item.Completed
                    ? System.Drawing.Color.White
                    : System.Drawing.Color.FromArgb(55, 65, 81),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        var statusText = item.Active
            ? "CURRENT"
            : item.Recommended
                ? "NEXT"
                : item.Completed
                    ? "DONE"
                    : "UPCOMING";
        using (var nameFont = new System.Drawing.Font(
                   Font.FontFamily,
                   9,
                   item.Active
                       ? System.Drawing.FontStyle.Bold
                       : System.Drawing.FontStyle.Regular))
        {
            TextRenderer.DrawText(
                e.Graphics,
                item.Definition.Name,
                nameFont,
                new System.Drawing.Rectangle(
                    e.Bounds.Left + 30,
                    e.Bounds.Top + 3,
                    e.Bounds.Width - 190,
                    24),
                System.Drawing.Color.FromArgb(31, 41, 55),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        using (var statusFont = new System.Drawing.Font(
                   Font.FontFamily,
                   8,
                   System.Drawing.FontStyle.Bold))
        {
            TextRenderer.DrawText(
                e.Graphics,
                statusText,
                statusFont,
                new System.Drawing.Rectangle(e.Bounds.Right - 150, e.Bounds.Top + 3, 65, 24),
                markerColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }
        using (var detailFont = new System.Drawing.Font(Font.FontFamily, 8))
        {
            TextRenderer.DrawText(
                e.Graphics,
                item.Definition.AutomationSummary,
                detailFont,
                new System.Drawing.Rectangle(e.Bounds.Right - 82, e.Bounds.Top + 3, 78, 24),
                System.Drawing.Color.DimGray,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        using var separator =
            new System.Drawing.Pen(System.Drawing.Color.FromArgb(229, 231, 235));
        e.Graphics.DrawLine(
            separator,
            e.Bounds.Left + 30,
            e.Bounds.Bottom - 1,
            e.Bounds.Right,
            e.Bounds.Bottom - 1);
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
            || _asobo737MaxFireTestsInProgress
            || _pendingFuelPumpSequence != null
            || _automation.HasPendingActions)
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

    private static void HandleSimConnectException(SIMCONNECT_RECV_EXCEPTION data)
    {
        var exception = (SIMCONNECT_EXCEPTION)data.dwException;
        if (exception == SIMCONNECT_EXCEPTION.ALREADY_CREATED)
        {
            return;
        }

        var detail =
            $"SimConnect exception: {exception} "
            + $"(send ID {data.dwSendID}, index {data.dwIndex}).";
        Console.Error.WriteLine(detail);
        AppLog.Write(detail);
    }

    private void HandleSimConnectDisconnected()
    {
        Console.WriteLine("MSFS closed the SimConnect session.");
        AppLog.Write("MSFS closed the SimConnect session.");
        _commandTimer?.Stop();
        InvalidateAircraftAutomation(
            AutomationInvalidationReason.SimConnectDisconnected);
        _gsx.OnSimConnectDisconnected();
        CloseGsxChoiceDialog();
        UpdateGsxStatus();
        _mobiFlightSession.ResetConnectionState();
        _pmdg777SdkInitialized = false;
        _pmdg777Runtime.ResetConnectionState();
        _pmdg777AdiruOnTimer?.Stop();
        _pmdg777AdiruOnTimer?.Dispose();
        _pmdg777AdiruOnTimer = null;
        _efbTransport.ResetConnectionState();
        ResetMobiFlightRuntimeAfterDisconnect();
        if (_connectionLabel != null)
        {
            _connectionLabel.Text = "Disconnected; waiting for MSFS...";
            _connectionLabel.ForeColor = System.Drawing.Color.DarkRed;
        }
    }

    private void ResetMobiFlightRuntimeAfterDisconnect(bool aircraftChanged = false)
    {
        // The WASM module survives a simulator connection restart, but this
        // SimConnect client's data definitions do not. Force the complete
        // runtime client and ordered SimVar table to be recreated after every
        // reconnect instead of accepting values left from the previous
        // session as current aircraft readback.
        _mobiFlightSession.ResetRuntimeState();

        if (aircraftChanged)
        {
            _nativeRuntime.ResetAircraftState();
        }
        else
        {
            _nativeRuntime.ResetConnectionState();
        }
        if (aircraftChanged)
        {
            _asobo737MaxRuntime.ResetAircraftState();
        }
        else
        {
            _asobo737MaxRuntime.ResetConnectionState();
        }
        _asobo737MaxFireTestsInProgress = false;

        AppLog.Write(
            "MobiFlight runtime state cleared; full native readback registration required after reconnect.");
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposingOrDisposed)
        {
            return;
        }

        _disposingOrDisposed = true;
        if (disposing)
        {
            _pendingFuelPumpSequence = null;
            StopFuelPumpSequenceTimer();
            if (_pendingFireTest != null)
            {
                if (Connection != null)
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
            _asobo737MaxFireTestsInProgress = false;
            _commandTimer?.Stop();
            _commandTimer?.Dispose();
            _commandTimer = null;
            _pmdg777AdiruOnTimer?.Dispose();
            _sayIntentionsTimer?.Stop();
            _sayIntentionsTimer?.Dispose();
            _sayIntentionsTimer = null;
            if (_sayIntentionsRuntime.CopilotModeApplied && _sayIntentionsRuntime.Flight != null)
            {
                try
                {
                    using var restoreTimeout = new CancellationTokenSource(
                        TimeSpan.FromSeconds(2));
                    _sayIntentionsClient
                        .SetCopilotCommunicationsAsync(
                            _sayIntentionsRuntime.Flight,
                            false,
                            restoreTimeout.Token)
                        .GetAwaiter()
                        .GetResult();
                    AppLog.Write(
                        "SayIntentions communications returned to the pilot during shutdown.");
                }
                catch (Exception ex) when (ex is HttpRequestException
                                           or OperationCanceledException
                                           or InvalidOperationException)
                {
                    AppLog.Write(
                        $"Could not restore SayIntentions pilot communications during shutdown: {ex.Message}");
                }
            }
            _sayIntentionsCancellation.Cancel();
            ReleaseGsxRemoteControl();
            _automation.Dispose();
            StopReplay();
            _aircraftIdentityLookupCancellation?.Cancel();
            _aircraftIdentityLookupCancellation?.Dispose();
            _aircraftIdentityLookupCancellation = null;
            SetAircraftThumbnail(null);
            _flightTelemetryStore.Dispose();
            _voiceCalloutQueue?.Dispose();
            _voiceCalloutQueue = null;
            _sayIntentionsClient.Dispose();
            _sayIntentionsCommsModeGate.Dispose();
            _sayIntentionsCancellation.Dispose();
            _simConnectSession.Dispose();
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
            bool desiredOn,
            long generation,
            AircraftVariant expectedVariant)
        {
            Toggles = toggles;
            DesiredOn = desiredOn;
            Generation = generation;
            ExpectedVariant = expectedVariant;
        }

        public Queue<FuelPumpToggle> Toggles { get; }
        public bool DesiredOn { get; }
        public long Generation { get; }
        public AircraftVariant ExpectedVariant { get; }
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

        public FuelPumpToggle(
            int number,
            ulong inputEventHash,
            double inputEventValue)
            : this(number, inputEventHash)
        {
            InputEventValue = inputEventValue;
        }

        public int Number { get; }
        public string CalculatorCode { get; }
        public ulong? InputEventHash { get; }
        public double? InputEventValue { get; }
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


