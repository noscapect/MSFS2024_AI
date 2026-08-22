using Msfs2024Ai.Copilot.AircraftAdapters.Pmdg777;
using System.Runtime.InteropServices;

namespace Msfs2024Ai.Copilot;

internal static class SimConnectContractConstants
{
    public const uint PmdgNg3DataId = 0x4E473331;
    public const uint PmdgNg3DataDefinition = 0x4E473332;
    public const uint PmdgNg3ControlId = 0x4E473333;
    public const uint PmdgNg3ControlDefinition = 0x4E473334;
    public const int PmdgNg3DataSize = 914;
}

internal enum Definition
{
    AircraftState,
    FlightCalloutState,
    GsxRemoteControl = 400,
    GsxMenuOpen = 401,
    GsxMenuChoice = 402
}

internal enum Request
{
    AircraftState,
    FlightCalloutState = 500,
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
    NativeEngineModeSelector = 209,
    NativeA320RunwayTurnoffSelector = 210,
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
    A330LandingLightInputEvent = 239,
    Asobo737MaxEnumerateInputEvents = 240,
    Asobo737MaxBatteryInputEvent = 241,
    Asobo737MaxBatteryCoverInputEvent = 242,
    Asobo737MaxLeftIrsInputEvent = 243,
    Asobo737MaxRightIrsInputEvent = 244,
    Asobo737MaxPositionLightInputEvent = 245,
    Asobo737MaxLogoLightInputEvent = 246,
    Asobo737MaxEmergencyExitInputEvent = 247,
    Asobo737MaxEmergencyExitCoverInputEvent = 248,
    Asobo737MaxFuelPump1InputEvent = 249,
    Asobo737MaxFuelPump2InputEvent = 250,
    Asobo737MaxFuelPump3InputEvent = 251,
    Asobo737MaxFuelPump4InputEvent = 252,
    Asobo737MaxFuelPump5InputEvent = 253,
    Asobo737MaxFuelPump6InputEvent = 254,
    Asobo737MaxSeatbeltsInputEvent = 255,
    Asobo737MaxNoSmokingInputEvent = 256,
    Asobo737MaxApuInputEvent = 257,
    Asobo737MaxApuGenerator1InputEvent = 258,
    Asobo737MaxApuGenerator2InputEvent = 259,
    Asobo737MaxApuBleedInputEvent = 260,
    Asobo737MaxIsolationValveInputEvent = 261,
    Asobo737MaxLeftPackInputEvent = 262,
    Asobo737MaxRightPackInputEvent = 263,
    Asobo737MaxEngineBleed1InputEvent = 264,
    Asobo737MaxEngineBleed2InputEvent = 265,
    Asobo737MaxEngineGenerator1InputEvent = 266,
    Asobo737MaxEngineGenerator2InputEvent = 267,
    Asobo737MaxElectricHydraulicPump1InputEvent = 268,
    Asobo737MaxElectricHydraulicPump2InputEvent = 269,
    Asobo737MaxTaxiLightInputEvent = 270,
    Asobo737MaxRunwayTurnoffLeftInputEvent = 271,
    Asobo737MaxRunwayTurnoffRightInputEvent = 272,
    Asobo737MaxLandingLightLeftInputEvent = 273,
    Asobo737MaxLandingLightRightInputEvent = 274,
    Asobo737MaxAntiCollisionInputEvent = 275,
    Asobo737MaxFlapsInputEvent = 276,
    Asobo737MaxAutobrakeInputEvent = 277,
    Asobo737MaxAutothrottleInputEvent = 278,
    Asobo737MaxTransponderOperatingModeInputEvent = 279,
    Asobo737MaxTransponderModeInputEvent = 280,
    A310Battery1Auto = 281,
    A310Battery2Auto = 282,
    A310Battery3Auto = 283,
    A310HydraulicEngine1 = 284,
    A310HydraulicEngine1A = 285,
    A310HydraulicEngine2 = 286,
    A310HydraulicEngine2B = 287,
    A310HydraulicElectric = 288,
    A310CaptainWiper = 289,
    A310FirstOfficerWiper = 290,
    A310WeatherRadarSystem = 291,
    A310Irs1 = 292,
    A310Irs2 = 293,
    A310Irs3 = 294,
    A310OxygenSupply = 295,
    A310ApuFireTest = 296,
    A310ApuLoopTest = 297,
    A310AnnunciatorTest = 298,
    A310NavLogoLight = 302,
    A310BeaconLight = 303,
    A310TaxiLight = 304,
    A310LeftLandingLight = 305,
    A310RightLandingLight = 306,
    A310WingLight = 307,
    A310LeftRunwayTurnoffLight = 308,
    A310RightRunwayTurnoffLight = 309,
    A310Flow2Seatbelts = 310,
    A310Flow2NoSmoking = 311,
    A310Flow2Ats1 = 312,
    A310Flow2Ats2 = 313,
    A310Flow2PitchTrim1 = 314,
    A310Flow2PitchTrim2 = 315,
    A310Flow2YawDamper1 = 316,
    A310Flow2YawDamper2 = 317,
    A310Flow2WindowHeat1 = 318,
    A310Flow2WindowHeat2 = 319,
    A310Flow2WindowHeat3 = 320,
    A310Flow2WindowHeat4 = 321,
    A310Flow2ProbeHeatCaptain = 322,
    A310Flow2ProbeHeatFirstOfficer = 323,
    A310Flow2ProbeHeatStandby = 324,
    A310Flow2EmergencyExit = 325,
    A310Flow2CargoSmokeTest = 326,
    A310Flow2EgpwsTest = 327,
    A310Flow2Autobrake = 328,
    A310Flow2RudderTrim = 329,
    A310Flow2TcasMode = 330,
    A310Flow2CargoSmokeForward = 331,
    A310Flow2CargoSmokeAft = 332,
    A310Flow2CargoSmokeBulk = 333,
    A310Flow3ApuMaster = 334,
    A310Flow3ApuStart = 335,
    A310Flow3ApuAvailable = 336,
    A310Flow3ApuBleed = 337,
    A310Flow3ApuGenerator = 338,
    A310EnumerateInputEvents = 339,
    A310ApuMasterInputEvent = 340,
    A310ApuStartInputEvent = 341,
    A310ApuGeneratorInputEvent = 342,
    A310ApuBleedInputEvent = 343,
    A310Flow4Ignition = 344,
    A310Flow4Pack1 = 345,
    A310Flow4Pack2 = 346,
    A310Flow4Engine1Starter = 347,
    A310Flow4Engine2Starter = 348,
    A310Flow4Engine1FuelLever = 349,
    A310Flow4Engine2FuelLever = 350,
    A310FuelPump1 = 351,
    A310FuelPump2 = 352,
    A310FuelPump3 = 353,
    A310FuelPump4 = 354,
    A310FuelPump5 = 355,
    A310FuelPump6 = 356,
    A310FuelPump7 = 357,
    A310FuelPump8 = 358,
    A310FuelPump9 = 359,
    A310FuelPump10 = 360,
    A310FuelPump11 = 361,
    A310FuelPump12 = 362,
    A310Flow5WeatherRadarMode = 363,
    A310Flow5AutobrakeMax = 364,
    A310Flow5SpoilersArmed = 365,
    A310GearHandleStatus = 366,
    A310CaptainAltimeterStandard = 367,
    A310FirstOfficerAltimeterStandard = 368,
    A310StandbyAltimeterStandard = 369,
    PmdgNg3Data = 300,
    PmdgNg3Control = 301,
    Pmdg777Data = Pmdg777ControlProfile.DataRequestId,
    Pmdg777Control = Pmdg777ControlProfile.ControlRequestId
}

internal enum ClientDataArea
{
    MobiFlightCommand = 100,
    MobiFlightResponse = 101,
    MobiFlightRuntimeLVars = 110,
    MobiFlightRuntimeCommand = 111,
    MobiFlightRuntimeResponse = 112,
    PmdgNg3Data = unchecked((int)SimConnectContractConstants.PmdgNg3DataId),
    PmdgNg3Control = unchecked((int)SimConnectContractConstants.PmdgNg3ControlId),
    Pmdg777Data = unchecked((int)Pmdg777ControlProfile.DataId),
    Pmdg777Control = unchecked((int)Pmdg777ControlProfile.ControlId)
}

internal enum ClientDataDefinition
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
    NativeEngineModeSelector = 209,
    NativeA320RunwayTurnoffSelector = 210,
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
    A310Battery1Auto = 211,
    A310Battery2Auto = 212,
    A310Battery3Auto = 213,
    A310HydraulicEngine1 = 214,
    A310HydraulicEngine1A = 215,
    A310HydraulicEngine2 = 216,
    A310HydraulicEngine2B = 217,
    A310HydraulicElectric = 218,
    A310CaptainWiper = 219,
    A310FirstOfficerWiper = 220,
    A310WeatherRadarSystem = 221,
    A310Irs1 = 222,
    A310Irs2 = 223,
    A310Irs3 = 224,
    A310OxygenSupply = 225,
    A310ApuFireTest = 226,
    A310ApuLoopTest = 227,
    A310AnnunciatorTest = 228,
    A310NavLogoLight = 229,
    A310BeaconLight = 230,
    A310TaxiLight = 231,
    A310LeftLandingLight = 232,
    A310RightLandingLight = 233,
    A310WingLight = 234,
    A310LeftRunwayTurnoffLight = 235,
    A310RightRunwayTurnoffLight = 236,
    A310Flow2Seatbelts = 237,
    A310Flow2NoSmoking = 238,
    A310Flow2Ats1 = 239,
    A310Flow2Ats2 = 240,
    A310Flow2PitchTrim1 = 241,
    A310Flow2PitchTrim2 = 242,
    A310Flow2YawDamper1 = 243,
    A310Flow2YawDamper2 = 244,
    A310Flow2WindowHeat1 = 245,
    A310Flow2WindowHeat2 = 246,
    A310Flow2WindowHeat3 = 247,
    A310Flow2WindowHeat4 = 248,
    A310Flow2ProbeHeatCaptain = 249,
    A310Flow2ProbeHeatFirstOfficer = 250,
    A310Flow2ProbeHeatStandby = 251,
    A310Flow2EmergencyExit = 252,
    A310Flow2CargoSmokeTest = 253,
    A310Flow2EgpwsTest = 254,
    A310Flow2Autobrake = 255,
    A310Flow2RudderTrim = 256,
    A310Flow2TcasMode = 257,
    A310Flow2CargoSmokeForward = 258,
    A310Flow2CargoSmokeAft = 259,
    A310Flow2CargoSmokeBulk = 260,
    A310Flow3ApuMaster = 261,
    A310Flow3ApuStart = 262,
    A310Flow3ApuAvailable = 263,
    A310Flow3ApuBleed = 264,
    A310Flow3ApuGenerator = 265,
    A310Flow4Ignition = 266,
    A310Flow4Pack1 = 267,
    A310Flow4Pack2 = 268,
    A310Flow4Engine1Starter = 269,
    A310Flow4Engine2Starter = 270,
    A310Flow4Engine1FuelLever = 271,
    A310Flow4Engine2FuelLever = 272,
    A310FuelPump1 = 273,
    A310FuelPump2 = 274,
    A310FuelPump3 = 275,
    A310FuelPump4 = 276,
    A310FuelPump5 = 277,
    A310FuelPump6 = 278,
    A310FuelPump7 = 279,
    A310FuelPump8 = 280,
    A310FuelPump9 = 281,
    A310FuelPump10 = 282,
    A310FuelPump11 = 283,
    A310FuelPump12 = 284,
    A310Flow5WeatherRadarMode = 285,
    A310Flow5AutobrakeMax = 286,
    A310Flow5SpoilersArmed = 287,
    A310GearHandleStatus = 288,
    A310CaptainAltimeterStandard = 289,
    A310FirstOfficerAltimeterStandard = 290,
    A310StandbyAltimeterStandard = 291,
    PmdgNg3Data = unchecked((int)SimConnectContractConstants.PmdgNg3DataDefinition),
    PmdgNg3Control = unchecked((int)SimConnectContractConstants.PmdgNg3ControlDefinition),
    Pmdg777Data = unchecked((int)Pmdg777ControlProfile.DataDefinition),
    Pmdg777Control = unchecked((int)Pmdg777ControlProfile.ControlDefinition)
}

internal enum CopilotEvent
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
    RotorBrake,
    SetAutopilotAirspeed,
    GsxExternalSystemSet = 400,
    GsxExternalSystemToggle = 401
}

internal enum NotificationGroup
{
    Gsx = 400
}

internal enum EfbCommBusEvent
{
    // CommBus uses the SimConnect client-event ID namespace. Keep this
    // directly after the standard copilot events (0-13) and away from the
    // GSX event IDs (400-401).
    Command = 14
}

internal enum Priority
{
    Highest = 1
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
internal struct AircraftData
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string Title;

    public double OnGround;
    public double GroundSpeed;
    public double LongitudinalVelocity;
    public double MagneticHeading;
    public double Engine1Combustion;
    public double Engine2Combustion;
    public double Engine1Starter;
    public double Engine2Starter;
    public double Engine1N1;
    public double Engine2N1;
    public double Engine1N2;
    public double Engine2N2;
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
    public double LeftRunwayTurnoffLightCircuit;
    public double RightRunwayTurnoffLightCircuit;
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
    public double AutopilotSelectedAirspeed;
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
    public double LeftGearPosition;
    public double CenterGearPosition;
    public double RightGearPosition;
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
    public double Nav1DmeNm;
    public double Nav2DmeNm;
    public double TotalFuelWeightPounds;
    public double SayIntentionsIntercom1Receiving;
    public double SayIntentionsIntercom2Receiving;
    public double SayIntentionsIntercom3Receiving;
    public double GsxCouatlStarted;
    public double GsxRemoteControl;
    public double IniBuildsIgnitionKnob;
    public double IniBuildsTurnoffLightSwitch;
    public double Pmdg777EmergencyLightsGuard;
    public double Pmdg777PassengerOxygenGuard;
    public double Pmdg777PrimaryFlightComputersGuard;
    public double Pmdg777FireOverheatTestSwitch;
    public double Pmdg777FirstOfficerOxygenTestSwitch;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct GsxValue
{
    public GsxValue(double value)
    {
        Value = value;
    }

    public double Value;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct MobiFlightMessage
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
internal struct MobiFlightFloat
{
    public float Value;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct PmdgNg3RawData
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = SimConnectContractConstants.PmdgNg3DataSize)]
    public byte[] Data;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct PmdgNg3Control
{
    public uint Event;
    public uint Parameter;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct Pmdg777RawData
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = Pmdg777ControlProfile.DataSize)]
    public byte[] Data;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct Pmdg777Control
{
    public uint Event;
    public uint Parameter;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct FlightCalloutData
{
    public double OnGround;
    public double IndicatedAirspeed;
    public double VerticalSpeed;
    public double AltitudeAboveGround;
    public double Engine1N1;
    public double Engine2N1;
}
