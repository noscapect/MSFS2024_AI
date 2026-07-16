using Microsoft.FlightSimulator.SimConnect;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Msfs2024Ai.SimConnectProbe;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "reflect-clientdata", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var method in typeof(SimConnect)
                         .GetMethods()
                         .Where(method => method.Name.IndexOf("ClientData", StringComparison.OrdinalIgnoreCase) >= 0)
                         .OrderBy(method => method.Name))
            {
                Console.WriteLine(method);
            }

            foreach (var name in Enum.GetNames(typeof(SIMCONNECT_CLIENT_DATA_PERIOD)))
            {
                Console.WriteLine($"PERIOD {name}");
            }

            foreach (var name in Enum.GetNames(typeof(SIMCONNECT_CLIENT_DATA_REQUEST_FLAG)))
            {
                Console.WriteLine($"REQUEST_FLAG {name}");
            }

            foreach (var name in Enum.GetNames(typeof(SIMCONNECT_CLIENT_DATA_SET_FLAG)))
            {
                Console.WriteLine($"SET_FLAG {name}");
            }
            return;
        }

        Console.WriteLine("MSFS 2024 SimConnect probe");
        Console.WriteLine("Connecting to the running simulator...");

        var powerUp = args.Any(arg => string.Equals(arg, "power-up", StringComparison.OrdinalIgnoreCase));
        var monitor = args.Any(arg => string.Equals(arg, "monitor-batteries", StringComparison.OrdinalIgnoreCase));
        var monitorA330Batteries = args.Any(arg => string.Equals(arg, "monitor-a330-batteries", StringComparison.OrdinalIgnoreCase));
        var inspectBeforeStart = args.Any(
            arg => string.Equals(arg, "inspect-before-start", StringComparison.OrdinalIgnoreCase));
        var monitorBeforeStart = args.Any(
            arg => string.Equals(arg, "monitor-before-start", StringComparison.OrdinalIgnoreCase));
        var listLVars = args.Any(
            arg => string.Equals(arg, "list-lvars", StringComparison.OrdinalIgnoreCase));
        var bridgeCommand = args
            .SkipWhile(arg => !string.Equals(arg, "bridge", StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .FirstOrDefault();
        var actionName = args
            .SkipWhile(arg => !string.Equals(arg, "execute-action", StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .FirstOrDefault();
        var inputEventFilter = args
            .SkipWhile(arg => !string.Equals(arg, "input-filter", StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .FirstOrDefault();
        var monitorInputHashText = args
            .SkipWhile(arg => !string.Equals(arg, "monitor-input", StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .FirstOrDefault();
        ulong? monitorInputHash = null;
        if (ulong.TryParse(monitorInputHashText, out var parsedMonitorInputHash))
        {
            monitorInputHash = parsedMonitorInputHash;
        }
        var monitorInputName = args
            .SkipWhile(arg => !string.Equals(arg, "monitor-input-name", StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .FirstOrDefault();
        using var window = new SimConnectWindow(
            powerUp,
            monitor,
            monitorA330Batteries,
            inspectBeforeStart || monitorBeforeStart,
            monitorBeforeStart,
            listLVars,
            bridgeCommand,
            actionName,
            inputEventFilter,
            monitorInputHash,
            monitorInputName);

        var timeout = new System.Windows.Forms.Timer
        {
            Interval = monitor || monitorA330Batteries || monitorBeforeStart || monitorInputHash.HasValue ? 900_000 : 15_000
        };
        timeout.Tick += (_, _) =>
        {
            timeout.Stop();
            if (!window.Connected)
            {
                Console.Error.WriteLine("Timed out waiting for SimConnect.");
                Environment.ExitCode = 2;
            }

            Application.ExitThread();
        };
        timeout.Start();

        window.Connect();
        Application.Run();
    }
}

internal sealed class SimConnectWindow : Form
{
    private const int WmUserSimConnect = 0x0402;
    private SimConnect? _simConnect;
    private bool _aircraftReceived;
    private bool _inputEventsReceived;
    private int _inputValuesReceived;
    private int _aircraftSnapshotsReceived;
    private AircraftData _latestAircraft;
    private const string A330BatteryProbeClientName = "MSFS2024_AI_A330BatteryProbe";
    private readonly bool _powerUpRequested;
    private readonly bool _monitorRequested;
    private readonly bool _monitorA330BatteriesRequested;
    private readonly bool _inspectBeforeStartRequested;
    private readonly bool _monitorBeforeStartRequested;
    private readonly bool _listLVarsRequested;
    private readonly string? _bridgeCommand;
    private readonly string? _actionName;
    private readonly string? _inputEventFilter;
    private readonly ulong? _monitorInputHash;
    private readonly string? _monitorInputName;
    private bool _powerUpIssued;
    private bool _bridgeResponseReceived;
    private readonly List<string> _bridgeResponseChunks = new List<string>();
    private System.Windows.Forms.Timer? _verificationTimer;
    private StreamWriter? _genericInputMonitorWriter;
    private System.Windows.Forms.Timer? _genericInputPollingTimer;
    private string? _lastGenericInputValue;
    private StreamWriter? _a330BatteryMonitorWriter;
    private System.Windows.Forms.Timer? _a330BatteryPollingTimer;
    private bool _a330BatteryRuntimeInitialized;
    private readonly Dictionary<string, float> _a330BatteryLVars = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private string _a330ApuBatteryInputEventValue = string.Empty;
    private DateTime _lastA330BatteryConsoleWriteUtc = DateTime.MinValue;

    [DllImport("SimConnect.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int SimConnect_SetInputEvent(
        IntPtr simConnectHandle,
        ulong hash,
        uint valueSize,
        ref double value);

    [DllImport("SimConnect.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int SimConnect_CallCommBusEvent(
        IntPtr simConnectHandle,
        string eventName,
        SIMCONNECT_COMM_BUS_BROADCAST_TO broadcastTo,
        uint bufferSize,
        byte[] data);

    [DllImport("SimConnect.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int SimConnect_ExecuteAction(
        IntPtr simConnectHandle,
        uint requestId,
        string actionId,
        uint parameterSize,
        ref double parameterValues);
    private readonly List<string> _inputEventLines = new List<string>();
    private readonly List<string> _beforeStartInspectionLines = new List<string>();
    private readonly Dictionary<Request, double> _latestInputValues = new Dictionary<Request, double>();
    private static readonly Dictionary<ulong, string> KeyInputEvents = new Dictionary<ulong, string>
    {
        [18319405923321542877UL] = "AIRLINER_ELEC_BAT1",
        [4119307791518802792UL] = "AIRLINER_ELEC_BAT2",
        [12398139814701135370UL] = "AIRLINER_ELEC_EXT",
        [10580266766214260807UL] = "INSTRUMENT_QNH_CPT_PUSH",
        [3529555828385965624UL] = "INSTRUMENT_QNH_FO_PUSH"
    };
    private static readonly Dictionary<ulong, string> BeforeStartInputEvents =
        new Dictionary<ulong, string>
        {
            [3205083420795941787UL] = "AIRLINER_ELEC_APU_GEN",
            [4080745756015573070UL] = "AIRLINER_APU_MASTER",
            [9344724743939237602UL] = "AIRLINER_APU_START",
            [3010931937409781580UL] = "AIRLINER_APU_BLEED",
            [5157929863266406690UL] = "AIRLINER_ADIRS_1",
            [9260957592121887383UL] = "AIRLINER_ADIRS_2",
            [14012218200692620292UL] = "AIRLINER_ADIRS_3",
            [1174871640386391352UL] = "AIRLINER_OXY_CREW",
            [12887035727064807174UL] = "AIRLINER_LT_SIGN_SEATBELTS",
            [17160241956476466648UL] = "AIRLINER_FUEL_ENG1_L1",
            [2969085048935345773UL] = "AIRLINER_FUEL_ENG1_L2",
            [10520269035956244858UL] = "AIRLINER_FUEL_CTR_1",
            [6264123145813805775UL] = "AIRLINER_FUEL_CTR_2",
            [3693509800080360825UL] = "AIRLINER_FUEL_ENG2_R1",
            [17604810245581348556UL] = "AIRLINER_FUEL_ENG2_R2",
            [1712305263919831311UL] = "AIRLINER_SPOILER_LEVER"
        };
    private readonly Dictionary<Request, string> _inspectionRequests =
        new Dictionary<Request, string>();
    private readonly HashSet<ulong> _inspectionParamsReceived = new HashSet<ulong>();
    private int _inspectionValuesReceived;
    private bool _inspectionArtifactWritten;
    private StreamWriter? _beforeStartMonitorWriter;
    private System.Windows.Forms.Timer? _beforeStartPollingTimer;
    private readonly Dictionary<string, string> _lastPolledBeforeStartValues =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _lVarNames = new List<string>();
    private bool _mobiFlightReady;
    private bool _lVarListStarted;
    private bool _lVarListComplete;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct MobiFlightMessage
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
        public byte[] Data;

        public MobiFlightMessage(string value)
        {
            Data = new byte[1024];
            var bytes = Encoding.ASCII.GetBytes(value);
            Array.Copy(bytes, Data, Math.Min(bytes.Length, Data.Length - 1));
        }

        public override string ToString()
        {
            var end = Array.IndexOf(Data, (byte)0);
            if (end < 0)
            {
                end = Data.Length;
            }
            return Encoding.ASCII.GetString(Data, 0, end);
        }
    }

    public bool Connected { get; private set; }

    private enum Definition
    {
        Aircraft
    }

    private enum CopilotEvent
    {
        Battery1Set,
        Battery2Set,
        ExternalPowerSet
    }

    private enum Priority
    {
        Highest = 1
    }

    private enum CommBusEvent
    {
        BridgeResponse
    }

    private enum Request
    {
        Aircraft,
        InputEvents,
        Battery1,
        Battery2,
        ExternalPower,
        QnhCaptain,
        QnhFirstOfficer,
        A330ApuBatteryInputEvent,
        GenericInputMonitor = 800,
        MobiFlightResponse = 900,
        MobiFlightRuntimeResponse = 901,
        A330BatteryBat1LVar = 910,
        A330BatteryBat2LVar = 911,
        A330BatteryApuBatLVar = 912
    }

    private enum ClientDataArea
    {
        MobiFlightCommand = 900,
        MobiFlightResponse = 901,
        MobiFlightRuntimeLVars = 910,
        MobiFlightRuntimeCommand = 911,
        MobiFlightRuntimeResponse = 912
    }

    private enum ClientDataDefinition
    {
        MobiFlightMessage = 900,
        MobiFlightRuntimeMessage = 901,
        A330BatteryBat1LVar = 910,
        A330BatteryBat2LVar = 911,
        A330BatteryApuBatLVar = 912
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    private struct AircraftData
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Title;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string AtcModel;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string AtcType;

        public double OnGround;
        public double GroundVelocity;
        public double EnginesCombustion;
        public double Battery1;
        public double Battery2;
        public double Battery3;
        public double Battery1Voltage;
        public double Battery2Voltage;
        public double Battery3Voltage;
        public double ExternalPowerAvailable;
        public double ExternalPowerOn;
        public double FlapsHandleIndex;
        public double FlapsEffectiveHandleIndex;
        public double FlapsHandlePercent;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct MobiFlightFloat
    {
        public float Value;
    }

    public SimConnectWindow(
        bool powerUpRequested,
        bool monitorRequested,
        bool monitorA330BatteriesRequested,
        bool inspectBeforeStartRequested,
        bool monitorBeforeStartRequested,
        bool listLVarsRequested,
        string? bridgeCommand,
        string? actionName,
        string? inputEventFilter,
        ulong? monitorInputHash,
        string? monitorInputName)
    {
        _powerUpRequested = powerUpRequested;
        _monitorRequested = monitorRequested;
        _monitorA330BatteriesRequested = monitorA330BatteriesRequested;
        _inspectBeforeStartRequested = inspectBeforeStartRequested;
        _monitorBeforeStartRequested = monitorBeforeStartRequested;
        _listLVarsRequested = listLVarsRequested;
        _bridgeCommand = bridgeCommand;
        _actionName = actionName;
        _inputEventFilter = inputEventFilter;
        _monitorInputHash = monitorInputHash;
        _monitorInputName = monitorInputName;
        Text = "MSFS 2024 AI SimConnect Probe";
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Opacity = 0;
    }

    public void Connect()
    {
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
            _simConnect.OnRecvEnumerateInputEvents += OnEnumerateInputEvents;
            _simConnect.OnRecvEnumerateInputEventParams += OnEnumerateInputEventParams;
            _simConnect.OnRecvGetInputEvent += OnGetInputEvent;
            _simConnect.OnRecvSubscribeInputEvent += OnSubscribedInputEvent;
            _simConnect.OnRecvCommBus += OnCommBus;
            _simConnect.OnRecvClientData += OnClientData;
        }
        catch (COMException ex)
        {
            Console.Error.WriteLine($"Could not open SimConnect: {ex.Message} (0x{ex.ErrorCode:X8})");
            Environment.ExitCode = 1;
            Application.ExitThread();
        }
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
        Connected = true;
        Console.WriteLine("CONNECTED");
        Console.WriteLine($"Application: {data.szApplicationName}");
        Console.WriteLine($"SimConnect API: {data.dwSimConnectVersionMajor}.{data.dwSimConnectVersionMinor}");
        Console.WriteLine($"Simulator build: {data.dwApplicationVersionMajor}.{data.dwApplicationVersionMinor}.{data.dwApplicationBuildMajor}.{data.dwApplicationBuildMinor}");
        foreach (var field in typeof(SimConnect).GetFields(
                     System.Reflection.BindingFlags.Instance
                     | System.Reflection.BindingFlags.NonPublic
                     | System.Reflection.BindingFlags.Public))
        {
            if (field.FieldType == typeof(IntPtr) || field.Name.IndexOf("handle", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine($"SimConnect internal field: {field.FieldType.FullName} {field.Name}");
            }
        }
        foreach (var method in typeof(SimConnect).GetMethods()
                     .Where(item => item.Name.IndexOf("TransmitClientEvent", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            Console.WriteLine($"SimConnect transmit method: {method}");
        }
        foreach (var method in typeof(SimConnect).GetMethods()
                     .Where(item => item.Name.IndexOf("CommBus", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            Console.WriteLine($"SimConnect CommBus method: {method}");
        }
        foreach (var eventInfo in typeof(SimConnect).GetEvents()
                     .Where(item => item.Name.IndexOf("CommBus", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            Console.WriteLine($"SimConnect CommBus event: {eventInfo.Name} => {eventInfo.EventHandlerType}");
            var invoke = eventInfo.EventHandlerType?.GetMethod("Invoke");
            if (invoke != null)
            {
                foreach (var parameter in invoke.GetParameters())
                {
                    Console.WriteLine($"  parameter: {parameter.ParameterType.FullName}");
                    foreach (var field in parameter.ParameterType.GetFields())
                    {
                        Console.WriteLine($"    field: {field.FieldType.FullName} {field.Name}");
                    }
                }
            }
        }

        sender.AddToDataDefinition(Definition.Aircraft, "TITLE", null, SIMCONNECT_DATATYPE.STRING256, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.MapClientEventToSimEvent(CopilotEvent.Battery1Set, "MASTER_BATTERY_SET");
        sender.MapClientEventToSimEvent(CopilotEvent.Battery2Set, "MASTER_BATTERY_SET");
        sender.MapClientEventToSimEvent(CopilotEvent.ExternalPowerSet, "SET_EXTERNAL_POWER");
        sender.AddToDataDefinition(Definition.Aircraft, "ATC MODEL", null, SIMCONNECT_DATATYPE.STRING256, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.Aircraft, "ATC TYPE", null, SIMCONNECT_DATATYPE.STRING256, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.Aircraft, "SIM ON GROUND", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.Aircraft, "GROUND VELOCITY", "Knots", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.Aircraft, "GENERAL ENG COMBUSTION:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.Aircraft, "ELECTRICAL MASTER BATTERY:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.Aircraft, "ELECTRICAL MASTER BATTERY:2", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.Aircraft, "ELECTRICAL MASTER BATTERY:3", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.Aircraft, "ELECTRICAL BATTERY VOLTAGE:1", "Volts", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.Aircraft, "ELECTRICAL BATTERY VOLTAGE:2", "Volts", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.Aircraft, "ELECTRICAL BATTERY VOLTAGE:3", "Volts", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.Aircraft, "EXTERNAL POWER AVAILABLE:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.Aircraft, "EXTERNAL POWER ON:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.Aircraft, "FLAPS HANDLE INDEX", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.Aircraft, "FLAPS EFFECTIVE HANDLE INDEX", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.Aircraft, "FLAPS HANDLE PERCENT", "Percent", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.RegisterDataDefineStruct<AircraftData>(Definition.Aircraft);
        sender.RequestDataOnSimObject(
            Request.Aircraft,
            Definition.Aircraft,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.ONCE,
            SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
            0,
            0,
            0);
        sender.EnumerateInputEvents(Request.InputEvents);
        sender.GetInputEvent(Request.Battery1, 18319405923321542877UL);
        sender.GetInputEvent(Request.Battery2, 4119307791518802792UL);
        sender.GetInputEvent(Request.ExternalPower, 12398139814701135370UL);
        sender.GetInputEvent(Request.QnhCaptain, 10580266766214260807UL);
        sender.GetInputEvent(Request.QnhFirstOfficer, 3529555828385965624UL);
        sender.EnumerateInputEventParams(10580266766214260807UL);
        sender.EnumerateInputEventParams(3529555828385965624UL);
        foreach (var hash in KeyInputEvents.Keys)
        {
            sender.EnumerateInputEventParams(hash);
        }
        if (_inspectBeforeStartRequested)
        {
            var index = 0;
            foreach (var inputEvent in BeforeStartInputEvents)
            {
                var request = (Request)(1000 + index++);
                _inspectionRequests[request] = inputEvent.Value;
                sender.GetInputEvent(request, inputEvent.Key);
                sender.EnumerateInputEventParams(inputEvent.Key);
            }
            Console.WriteLine(
                $"BEFORE START INSPECTION: requesting {BeforeStartInputEvents.Count} read-only Input Events.");
        }
        if (_monitorBeforeStartRequested)
        {
            var outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "artifacts");
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, "a320neo-v2-before-start-monitor.tsv");
            _beforeStartMonitorWriter = new StreamWriter(outputPath, false, Encoding.UTF8)
            {
                AutoFlush = true
            };
            _beforeStartMonitorWriter.WriteLine("TimestampUtc\tSource\tName\tHash\tType\tValue");
            foreach (var hash in BeforeStartInputEvents.Keys)
            {
                sender.SubscribeInputEvent(hash);
            }
            _beforeStartPollingTimer = new System.Windows.Forms.Timer { Interval = 250 };
            _beforeStartPollingTimer.Tick += (_, _) =>
            {
                foreach (var inputEvent in BeforeStartInputEvents)
                {
                    var request = _inspectionRequests.First(item => item.Value == inputEvent.Value).Key;
                    sender.GetInputEvent(request, inputEvent.Key);
                }
            };
            _beforeStartPollingTimer.Start();
            Console.WriteLine(
                "BEFORE START MONITOR READY: operate the APU, ADIRS, seat-belt, and fuel-pump controls manually.");
            Console.WriteLine("Subscriptions active; 250 ms read-only polling fallback active.");
            Console.WriteLine($"Monitor capture: {outputPath}");
        }
        if (_monitorInputHash.HasValue)
        {
            var outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "artifacts");
            Directory.CreateDirectory(outputDirectory);
            var safeName = string.IsNullOrWhiteSpace(_monitorInputName)
                ? _monitorInputHash.Value.ToString()
                : new string(_monitorInputName
                    .Where(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-')
                    .ToArray());
            var outputPath = Path.Combine(outputDirectory, $"input-monitor-{safeName}.tsv");
            _genericInputMonitorWriter = new StreamWriter(outputPath, false, Encoding.UTF8)
            {
                AutoFlush = true
            };
            _genericInputMonitorWriter.WriteLine("TimestampUtc\tSource\tName\tHash\tType\tValue");
            sender.SubscribeInputEvent(_monitorInputHash.Value);
            sender.GetInputEvent(Request.GenericInputMonitor, _monitorInputHash.Value);
            _genericInputPollingTimer = new System.Windows.Forms.Timer { Interval = 250 };
            _genericInputPollingTimer.Tick += (_, _) =>
            {
                sender.GetInputEvent(Request.GenericInputMonitor, _monitorInputHash.Value);
            };
            _genericInputPollingTimer.Start();
            Console.WriteLine(
                $"INPUT MONITOR READY: {FindInputEventName(_monitorInputHash.Value)} " +
                $"({_monitorInputHash.Value}).");
            Console.WriteLine("Subscription active; 250 ms read-only polling fallback active.");
            Console.WriteLine($"Monitor capture: {outputPath}");
        }
        if (_listLVarsRequested)
        {
            InitializeMobiFlight(sender);
        }
        if (_monitorRequested)
        {
            sender.SubscribeInputEvent(18319405923321542877UL);
            sender.SubscribeInputEvent(4119307791518802792UL);
            sender.SubscribeInputEvent(12398139814701135370UL);
            Console.WriteLine("MONITOR READY: click BAT 1 and BAT 2 in the cockpit.");
        }
        if (_monitorA330BatteriesRequested)
        {
            StartA330BatteryMonitor(sender);
        }
        if (!string.IsNullOrWhiteSpace(_bridgeCommand))
        {
            sender.SubscribeToCommBusEvent(CommBusEvent.BridgeResponse, "MSFS2024_AI_A320_RESPONSE");
            var wasmTarget = (SIMCONNECT_COMM_BUS_BROADCAST_TO)Enum.Parse(
                typeof(SIMCONNECT_COMM_BUS_BROADCAST_TO),
                "ALL",
                true);
            sender.CallCommBusEvent(
                "MSFS2024_AI_A320_COMMAND",
                wasmTarget,
                _bridgeCommand);
            Console.WriteLine($"BRIDGE COMMAND SENT: {_bridgeCommand}");
        }
        if (!string.IsNullOrWhiteSpace(_actionName))
        {
            var handleField = typeof(SimConnect).GetField(
                "hSimConnect",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var handle = handleField == null ? IntPtr.Zero : (IntPtr)handleField.GetValue(sender);
            var actionValue = 1.0;
            var result = SimConnect_ExecuteAction(
                handle,
                900,
                _actionName!,
                sizeof(double),
                ref actionValue);
            Console.WriteLine($"ACTION SENT: {_actionName} (0x{result:X8})");
        }
    }

    private void OnAircraftData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if ((Request)data.dwRequestID != Request.Aircraft || data.dwData.Length == 0)
        {
            return;
        }

        var aircraft = (AircraftData)data.dwData[0];
        Console.WriteLine($"Aircraft title: {aircraft.Title}");
        Console.WriteLine($"ATC model/type: {aircraft.AtcModel} / {aircraft.AtcType}");
        Console.WriteLine($"On ground: {aircraft.OnGround != 0}; ground speed: {aircraft.GroundVelocity:F1} kt");
        Console.WriteLine($"Engine 1 combustion: {aircraft.EnginesCombustion != 0}");
        Console.WriteLine($"Batteries 1/2/3: {aircraft.Battery1 != 0}/{aircraft.Battery2 != 0}/{aircraft.Battery3 != 0}");
        Console.WriteLine($"Battery volts 1/2/3: {aircraft.Battery1Voltage:F1}/{aircraft.Battery2Voltage:F1}/{aircraft.Battery3Voltage:F1}");
        Console.WriteLine($"External power available/on: {aircraft.ExternalPowerAvailable != 0}/{aircraft.ExternalPowerOn != 0}");
        Console.WriteLine($"Flaps handle index/effective/percent: {aircraft.FlapsHandleIndex:F2}/{aircraft.FlapsEffectiveHandleIndex:F2}/{aircraft.FlapsHandlePercent:F2}");
        _latestAircraft = aircraft;
        _aircraftSnapshotsReceived++;
        _aircraftReceived = true;
        if (_powerUpRequested && !_powerUpIssued)
        {
            if (!string.Equals(aircraft.Title, "A320neo V2", StringComparison.OrdinalIgnoreCase)
                || aircraft.OnGround == 0
                || aircraft.EnginesCombustion != 0
                || aircraft.ExternalPowerAvailable == 0)
            {
                Console.Error.WriteLine("POWER-UP BLOCKED: requires A320neo V2, on ground, engines off, and external power available.");
                Environment.ExitCode = 3;
                Application.ExitThread();
                return;
            }
        }
        WriteA330BatterySnapshot("SimConnect");

        TryIssuePowerUp(sender);
        ExitWhenComplete();
    }

    private void OnEnumerateInputEvents(SimConnect sender, SIMCONNECT_RECV_ENUMERATE_INPUT_EVENTS data)
    {
        foreach (var descriptor in data.rgData)
        {
            var descriptorType = descriptor.GetType();
            var values = descriptorType
                .GetFields()
                .Select(field => $"{field.Name}={FormatValue(field.GetValue(descriptor))}")
                .Concat(descriptorType
                    .GetProperties()
                    .Where(property => property.GetIndexParameters().Length == 0)
                    .Select(property => $"{property.Name}={FormatValue(property.GetValue(descriptor, null))}"));

            _inputEventLines.Add(string.Join("\t", values));
        }

        if (!_inputEventsReceived && data.dwEntryNumber + 1 >= data.dwOutOf)
        {
            var outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "artifacts");
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, "a320neo-v2-input-events.tsv");
            File.WriteAllLines(outputPath, _inputEventLines);

            Console.WriteLine($"Input Events discovered: {_inputEventLines.Count}");
            Console.WriteLine($"Input Event catalog: {outputPath}");
            if (!string.IsNullOrWhiteSpace(_inputEventFilter))
            {
                var matches = _inputEventLines
                    .Where(item => item.IndexOf(
                        _inputEventFilter,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                Console.WriteLine(
                    $"Input Event filter '{_inputEventFilter}' matched {matches.Count} event(s):");
                foreach (var line in matches.Take(100))
                {
                    Console.WriteLine($"  {line}");
                }
            }
            foreach (var line in _inputEventLines
                         .Where(item => item.IndexOf("BAT", StringComparison.OrdinalIgnoreCase) >= 0
                                        || item.IndexOf("EXT", StringComparison.OrdinalIgnoreCase) >= 0
                                        || item.IndexOf("APU", StringComparison.OrdinalIgnoreCase) >= 0)
                         .Take(30))
            {
                Console.WriteLine($"  {line}");
            }

            _inputEventsReceived = true;
            ExitWhenComplete();
        }
    }

    private void OnEnumerateInputEventParams(
        SimConnect sender,
        SIMCONNECT_RECV_ENUMERATE_INPUT_EVENT_PARAMS data)
    {
        var name = FindInputEventName(data.Hash);
        Console.WriteLine($"Input Event parameters: {name} => {data.Value}");
        if (_inspectBeforeStartRequested && BeforeStartInputEvents.ContainsKey(data.Hash))
        {
            _inspectionParamsReceived.Add(data.Hash);
            _beforeStartInspectionLines.Add(
                $"{EscapeTsv(name)}\t{data.Hash}\tparameters\t{EscapeTsv(data.Value)}");
            WriteInspectionArtifactWhenComplete();
        }
    }

    private void OnGetInputEvent(SimConnect sender, SIMCONNECT_RECV_GET_INPUT_EVENT data)
    {
        var request = (Request)data.dwRequestID;
        if (_inspectionRequests.TryGetValue(request, out var inspectionName))
        {
            var value = FormatValue(data.Value);
            var isChangedPoll = !_lastPolledBeforeStartValues.TryGetValue(
                                    inspectionName,
                                    out var previousValue)
                                || !string.Equals(previousValue, value, StringComparison.Ordinal);
            _lastPolledBeforeStartValues[inspectionName] = value;
            if (!_monitorBeforeStartRequested || isChangedPoll)
            {
                Console.WriteLine(
                    $"{(_monitorBeforeStartRequested ? "POLL EVENT" : "Before Start value")}: " +
                    $"{inspectionName} ({data.eType}) => {value}");
            }
            if (_beforeStartMonitorWriter != null && isChangedPoll)
            {
                _beforeStartMonitorWriter.WriteLine(
                    $"{DateTime.UtcNow:O}\tpoll\t{EscapeTsv(inspectionName)}\t" +
                    $"{FindInputEventHash(inspectionName)}\t{data.eType}\t{EscapeTsv(value)}");
            }
            _beforeStartInspectionLines.Add(
                $"{EscapeTsv(inspectionName)}\t{FindInputEventHash(inspectionName)}\t" +
                $"value:{data.eType}\t{EscapeTsv(value)}");
            _inspectionValuesReceived++;
            WriteInspectionArtifactWhenComplete();
            ExitWhenComplete();
            return;
        }
        if (request == Request.GenericInputMonitor && _monitorInputHash.HasValue)
        {
            var name = FindInputEventName(_monitorInputHash.Value);
            var value = FormatValue(data.Value);
            var isChangedPoll = !string.Equals(
                _lastGenericInputValue,
                value,
                StringComparison.Ordinal);
            _lastGenericInputValue = value;
            if (isChangedPoll)
            {
                Console.WriteLine($"POLL EVENT: {name} ({data.eType}) => {value}");
                _genericInputMonitorWriter?.WriteLine(
                    $"{DateTime.UtcNow:O}\tpoll\t{EscapeTsv(name)}\t" +
                    $"{_monitorInputHash.Value}\t{data.eType}\t{EscapeTsv(value)}");
            }
            return;
        }
        if (request == Request.A330ApuBatteryInputEvent)
        {
            _a330ApuBatteryInputEventValue = FormatValue(data.Value);
            WriteA330BatterySnapshot("InputEvent:APU_BAT");
            return;
        }

        Console.WriteLine(
            $"Input Event value: {request} ({data.eType}) => {FormatValue(data.Value)}");
        if (data.Value.Length > 0)
        {
            _latestInputValues[request] = Convert.ToDouble(data.Value[0]);
        }
        _inputValuesReceived++;
        TryIssuePowerUp(sender);
        ExitWhenComplete();
    }

    private void OnSubscribedInputEvent(
        SimConnect sender,
        SIMCONNECT_RECV_SUBSCRIBE_INPUT_EVENT data)
    {
        var name = FindInputEventName(data.Hash);
        var value = FormatValue(data.Value);
        Console.WriteLine($"MONITOR EVENT: {name} ({data.eType}) => {value}");
        if (_monitorInputHash.HasValue && data.Hash == _monitorInputHash.Value)
        {
            _genericInputMonitorWriter?.WriteLine(
                $"{DateTime.UtcNow:O}\tsubscription\t{EscapeTsv(name)}\t" +
                $"{data.Hash}\t{data.eType}\t{EscapeTsv(value)}");
        }
        if (_beforeStartMonitorWriter != null && BeforeStartInputEvents.ContainsKey(data.Hash))
        {
            _beforeStartMonitorWriter.WriteLine(
                $"{DateTime.UtcNow:O}\tsubscription\t{EscapeTsv(name)}\t" +
                $"{data.Hash}\t{data.eType}\t{EscapeTsv(value)}");
        }
    }

    private string FindInputEventName(ulong hash)
    {
        if (KeyInputEvents.TryGetValue(hash, out var keyName))
        {
            return keyName;
        }
        if (_monitorInputHash.HasValue && hash == _monitorInputHash.Value)
        {
            return string.IsNullOrWhiteSpace(_monitorInputName)
                ? hash.ToString()
                : _monitorInputName!;
        }

        return BeforeStartInputEvents.TryGetValue(hash, out var beforeStartName)
            ? beforeStartName
            : hash.ToString();
    }

    private static ulong FindInputEventHash(string name) =>
        BeforeStartInputEvents.First(item => item.Value == name).Key;

    private static string EscapeTsv(string? value) =>
        (value ?? string.Empty).Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");

    private void WriteInspectionArtifactWhenComplete()
    {
        if (_inspectionArtifactWritten
            || _inspectionValuesReceived < BeforeStartInputEvents.Count
            || _inspectionParamsReceived.Count < BeforeStartInputEvents.Count)
        {
            return;
        }

        var outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "artifacts");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, "a320neo-v2-before-start-input-events.tsv");
        var lines = new[] { "Name\tHash\tObservation\tValue" }
            .Concat(_beforeStartInspectionLines.OrderBy(line => line, StringComparer.OrdinalIgnoreCase));
        File.WriteAllLines(outputPath, lines);
        _inspectionArtifactWritten = true;
        Console.WriteLine($"Before Start Input Event inspection: {outputPath}");
    }

    private void OnCommBus(SimConnect sender, SIMCONNECT_RECV_COMM_BUS data)
    {
        if ((CommBusEvent)data.uEventID != CommBusEvent.BridgeResponse)
        {
            return;
        }

        _bridgeResponseChunks.Add(data.rgData);
        if (data.dwEntryNumber + 1 >= data.dwOutOf)
        {
            Console.WriteLine($"BRIDGE RESPONSE: {string.Concat(_bridgeResponseChunks)}");
            _bridgeResponseReceived = true;
            ExitWhenComplete();
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
        SendMobiFlightCommand(sender, "MF.DummyCmd");
        SendMobiFlightCommand(sender, "MF.Ping");
        SendMobiFlightCommand(sender, "MF.DummyCmd");
        Console.WriteLine("MOBIFLIGHT: requesting aircraft LVar inventory.");
    }

    private void StartA330BatteryMonitor(SimConnect sender)
    {
        var outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "artifacts");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, "a330-battery-monitor.tsv");
        _a330BatteryMonitorWriter = new StreamWriter(outputPath, false, Encoding.UTF8)
        {
            AutoFlush = true
        };
        _a330BatteryMonitorWriter.WriteLine(
            "TimestampUtc\tSource\tBat1Sim\tBat2Sim\tBat3Sim\tBat1V\tBat2V\tBat3V\tBat1LVar\tBat2LVar\tApuBatLVar\tApuBatInputEvent");

        InitializeMobiFlight(sender);
        SendMobiFlightCommand(sender, $"MF.Clients.Add.{A330BatteryProbeClientName}");

        _a330BatteryPollingTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _a330BatteryPollingTimer.Tick += (_, _) =>
        {
            sender.RequestDataOnSimObject(
                Request.Aircraft,
                Definition.Aircraft,
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.ONCE,
                SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                0,
                0,
                0);
            sender.GetInputEvent(Request.A330ApuBatteryInputEvent, 14438692519264741429UL);
            WriteA330BatterySnapshot("Poll");
        };
        _a330BatteryPollingTimer.Start();

        Console.WriteLine("A330 BATTERY MONITOR READY.");
        Console.WriteLine("Please press BAT 1 ON, BAT 2 ON, APU BAT ON, then optionally reverse them.");
        Console.WriteLine($"Monitor capture: {outputPath}");
    }

    private void InitializeA330BatteryRuntime(SimConnect sender)
    {
        if (_a330BatteryRuntimeInitialized)
        {
            return;
        }

        sender.MapClientDataNameToID($"{A330BatteryProbeClientName}.LVars", ClientDataArea.MobiFlightRuntimeLVars);
        sender.CreateClientData(
            ClientDataArea.MobiFlightRuntimeLVars,
            4096,
            SIMCONNECT_CREATE_CLIENT_DATA_FLAG.DEFAULT);
        sender.MapClientDataNameToID($"{A330BatteryProbeClientName}.Command", ClientDataArea.MobiFlightRuntimeCommand);
        sender.CreateClientData(
            ClientDataArea.MobiFlightRuntimeCommand,
            1024,
            SIMCONNECT_CREATE_CLIENT_DATA_FLAG.DEFAULT);
        sender.MapClientDataNameToID($"{A330BatteryProbeClientName}.Response", ClientDataArea.MobiFlightRuntimeResponse);
        sender.CreateClientData(
            ClientDataArea.MobiFlightRuntimeResponse,
            1024,
            SIMCONNECT_CREATE_CLIENT_DATA_FLAG.DEFAULT);

        sender.AddToClientDataDefinition(ClientDataDefinition.MobiFlightRuntimeMessage, 0, 1024, 0, 0);
        sender.RegisterStruct<SIMCONNECT_RECV_CLIENT_DATA, MobiFlightMessage>(ClientDataDefinition.MobiFlightRuntimeMessage);
        sender.RequestClientData(
            ClientDataArea.MobiFlightRuntimeResponse,
            Request.MobiFlightRuntimeResponse,
            ClientDataDefinition.MobiFlightRuntimeMessage,
            SIMCONNECT_CLIENT_DATA_PERIOD.ON_SET,
            SIMCONNECT_CLIENT_DATA_REQUEST_FLAG.CHANGED,
            0,
            0,
            0);

        RegisterA330BatteryLVar(sender, ClientDataDefinition.A330BatteryBat1LVar, Request.A330BatteryBat1LVar, 0);
        RegisterA330BatteryLVar(sender, ClientDataDefinition.A330BatteryBat2LVar, Request.A330BatteryBat2LVar, sizeof(float));
        RegisterA330BatteryLVar(sender, ClientDataDefinition.A330BatteryApuBatLVar, Request.A330BatteryApuBatLVar, 2 * sizeof(float));

        SendMobiFlightRuntimeCommand(sender, "MF.SimVars.Add.(L:INI_OVHD_ELEC_BAT_1_PB_IS_AUTO_SWITCH)");
        SendMobiFlightRuntimeCommand(sender, "MF.SimVars.Add.(L:INI_OVHD_ELEC_BAT_2_PB_IS_AUTO_SWITCH)");
        SendMobiFlightRuntimeCommand(sender, "MF.SimVars.Add.(L:INI_OVHD_ELEC_BAT_3_PB_IS_AUTO_SWITCH)");
        SendMobiFlightRuntimeCommand(sender, "MF.SimVars.Add.(L:AIRLINER_ELEC_APU_BAT_Position)");

        _a330BatteryRuntimeInitialized = true;
        Console.WriteLine("A330 BATTERY MONITOR: MobiFlight LVar runtime connected.");
    }

    private static void RegisterA330BatteryLVar(
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

    private static void SendMobiFlightRuntimeCommand(SimConnect sender, string command)
    {
        sender.SetClientData(
            ClientDataArea.MobiFlightRuntimeCommand,
            ClientDataDefinition.MobiFlightRuntimeMessage,
            SIMCONNECT_CLIENT_DATA_SET_FLAG.DEFAULT,
            0,
            new MobiFlightMessage(command));
    }

    private void WriteA330BatterySnapshot(string source)
    {
        if (!_monitorA330BatteriesRequested || _a330BatteryMonitorWriter == null || !_aircraftReceived)
        {
            return;
        }

        _a330BatteryLVars.TryGetValue("BAT1", out var bat1LVar);
        _a330BatteryLVars.TryGetValue("BAT2", out var bat2LVar);
        _a330BatteryLVars.TryGetValue("APU_BAT", out var apuBatLVar);

        _a330BatteryMonitorWriter.WriteLine(
            $"{DateTime.UtcNow:O}\t{source}\t{_latestAircraft.Battery1:0.###}\t{_latestAircraft.Battery2:0.###}\t{_latestAircraft.Battery3:0.###}\t" +
            $"{_latestAircraft.Battery1Voltage:0.###}\t{_latestAircraft.Battery2Voltage:0.###}\t{_latestAircraft.Battery3Voltage:0.###}\t" +
            $"{bat1LVar:0.###}\t{bat2LVar:0.###}\t{apuBatLVar:0.###}\t{EscapeTsv(_a330ApuBatteryInputEventValue)}");

        if ((DateTime.UtcNow - _lastA330BatteryConsoleWriteUtc).TotalSeconds >= 1)
        {
            _lastA330BatteryConsoleWriteUtc = DateTime.UtcNow;
            Console.WriteLine(
                $"A330 BAT probe | Sim BAT 1/2/3={_latestAircraft.Battery1:0}/{_latestAircraft.Battery2:0}/{_latestAircraft.Battery3:0} " +
                $"V={_latestAircraft.Battery1Voltage:0.0}/{_latestAircraft.Battery2Voltage:0.0}/{_latestAircraft.Battery3Voltage:0.0} " +
                $"LVars BAT1/BAT2/APU={bat1LVar:0.###}/{bat2LVar:0.###}/{apuBatLVar:0.###} " +
                $"InputEvent APU={_a330ApuBatteryInputEventValue}");
        }
    }

    private static void SendMobiFlightCommand(SimConnect sender, string command)
    {
        sender.SetClientData(
            ClientDataArea.MobiFlightCommand,
            ClientDataDefinition.MobiFlightMessage,
            SIMCONNECT_CLIENT_DATA_SET_FLAG.DEFAULT,
            0,
            new MobiFlightMessage(command));
    }

    private void OnClientData(SimConnect sender, SIMCONNECT_RECV_CLIENT_DATA data)
    {
        var request = (Request)data.dwRequestID;
        if (request is Request.A330BatteryBat1LVar or Request.A330BatteryBat2LVar or Request.A330BatteryApuBatLVar)
        {
            var value = ((MobiFlightFloat)data.dwData[0]).Value;
            var name = request == Request.A330BatteryBat1LVar
                ? "BAT1"
                : request == Request.A330BatteryBat2LVar
                    ? "BAT2"
                    : "APU_BAT";
            _a330BatteryLVars[name] = value;
            WriteA330BatterySnapshot($"LVar:{name}");
            return;
        }

        if ((request != Request.MobiFlightResponse && request != Request.MobiFlightRuntimeResponse) || data.dwData.Length == 0)
        {
            return;
        }

        var response = ((MobiFlightMessage)data.dwData[0]).ToString();
        if (request == Request.MobiFlightResponse && string.Equals(response, "MF.Pong", StringComparison.OrdinalIgnoreCase))
        {
            _mobiFlightReady = true;
            SendMobiFlightCommand(sender, "MF.DummyCmd");
            SendMobiFlightCommand(sender, "MF.LVars.List");
            return;
        }

        if (request == Request.MobiFlightResponse
            && _monitorA330BatteriesRequested
            && response.IndexOf(A330BatteryProbeClientName, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            InitializeA330BatteryRuntime(sender);
            return;
        }

        if (request == Request.MobiFlightResponse && string.Equals(response, "MF.LVars.List.Start", StringComparison.OrdinalIgnoreCase))
        {
            _lVarListStarted = true;
            _lVarNames.Clear();
            return;
        }

        if (request == Request.MobiFlightResponse && string.Equals(response, "MF.LVars.List.End", StringComparison.OrdinalIgnoreCase))
        {
            WriteLVarInventory();
            _lVarListComplete = true;
            ExitWhenComplete();
            return;
        }

        if (request == Request.MobiFlightResponse && _lVarListStarted && !string.IsNullOrWhiteSpace(response))
        {
            _lVarNames.Add(response.Trim());
        }
    }

    private void WriteLVarInventory()
    {
        var outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "artifacts");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, "a320neo-v2-lvars.txt");
        var names = _lVarNames
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        File.WriteAllLines(outputPath, names);
        Console.WriteLine($"MOBIFLIGHT: discovered {names.Length} LVars.");
        Console.WriteLine($"LVar inventory: {outputPath}");
        foreach (var name in names.Where(
                     name => name.IndexOf("APU", StringComparison.OrdinalIgnoreCase) >= 0
                             || name.IndexOf("FUEL", StringComparison.OrdinalIgnoreCase) >= 0
                             || name.IndexOf("PUMP", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            Console.WriteLine($"  {name}");
        }
    }

    private void TryIssuePowerUp(SimConnect sender)
    {
        if (!_powerUpRequested || _powerUpIssued || !_aircraftReceived || _inputValuesReceived < KeyInputEvents.Count)
        {
            return;
        }

        Console.WriteLine("POWER-UP: setting BAT 1, BAT 2, and EXT PWR on...");
        sender.TransmitClientEvent_EX1(
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            CopilotEvent.Battery1Set,
            Priority.Highest,
            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY,
            0,
            1,
            0,
            0,
            0);
        sender.TransmitClientEvent_EX1(
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            CopilotEvent.Battery2Set,
            Priority.Highest,
            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY,
            1,
            1,
            0,
            0,
            0);
        sender.TransmitClientEvent_EX1(
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            CopilotEvent.ExternalPowerSet,
            Priority.Highest,
            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY,
            1,
            1,
            0,
            0,
            0);
        _powerUpIssued = true;

        _verificationTimer = new System.Windows.Forms.Timer { Interval = 1500 };
        _verificationTimer.Tick += (_, _) =>
        {
            _verificationTimer.Stop();
            sender.GetInputEvent(Request.Battery1, 18319405923321542877UL);
            sender.GetInputEvent(Request.Battery2, 4119307791518802792UL);
            sender.GetInputEvent(Request.ExternalPower, 12398139814701135370UL);
            sender.RequestDataOnSimObject(
                Request.Aircraft,
                Definition.Aircraft,
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.ONCE,
                SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                0,
                0,
                0);
        };
        _verificationTimer.Start();
    }

    private static string FormatValue(object? value)
    {
        if (value is Array array)
        {
            return string.Join(",", array.Cast<object>().Select(FormatValue));
        }

        return value?.ToString() ?? string.Empty;
    }

    private void ExitWhenComplete()
    {
        if (_monitorRequested
            || _monitorBeforeStartRequested
            || _monitorInputHash.HasValue)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_bridgeCommand) && !_bridgeResponseReceived)
        {
            return;
        }
        if (_inspectBeforeStartRequested && !_inspectionArtifactWritten)
        {
            return;
        }
        if (_listLVarsRequested && (!_mobiFlightReady || !_lVarListComplete))
        {
            return;
        }

        var requiredValueCount = _powerUpRequested ? KeyInputEvents.Count * 2 : KeyInputEvents.Count;
        var requiredSnapshotCount = _powerUpRequested ? 2 : 1;
        if (_aircraftReceived
            && _inputEventsReceived
            && _inputValuesReceived >= requiredValueCount
            && _aircraftSnapshotsReceived >= requiredSnapshotCount)
        {
            if (_powerUpRequested)
            {
                var controlsOn = _latestAircraft.Battery1 != 0
                                 && _latestAircraft.Battery2 != 0
                                 && _latestAircraft.ExternalPowerOn != 0;
                if (!controlsOn)
                {
                    Console.Error.WriteLine("POWER-UP VERIFICATION FAILED: one or more controls did not report ON.");
                    Environment.ExitCode = 4;
                }
                else
                {
                Console.WriteLine("POWER-UP VERIFIED");
                }
            }
            Application.ExitThread();
        }
    }

    private static void OnException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
    {
        Console.Error.WriteLine($"SimConnect exception: {(SIMCONNECT_EXCEPTION)data.dwException}");
    }

    private static void OnQuit(SimConnect sender, SIMCONNECT_RECV data)
    {
        Console.WriteLine("Simulator closed the SimConnect session.");
        Application.ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _verificationTimer?.Dispose();
            _beforeStartPollingTimer?.Dispose();
            _beforeStartMonitorWriter?.Dispose();
            _genericInputPollingTimer?.Dispose();
            _genericInputMonitorWriter?.Dispose();
            _simConnect?.Dispose();
        }

        base.Dispose(disposing);
    }
}

