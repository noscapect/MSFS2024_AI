using Msfs2024Ai.Copilot.Domain;

namespace Msfs2024Ai.Copilot.AircraftAdapters.Pmdg777;

/// <summary>
/// Isolation boundary for the PMDG 777-300ER SDK. The identifiers are from
/// PMDG_777X_SDK.h shipped with pmdg-aircraft-77w. The 777 must never reuse the
/// 737 NG3 data layout or command namespace.
/// </summary>
internal static class Pmdg777ControlProfile
{
    public const int DataSize = 684;
    public const int DataRequestId = 370;
    public const int ControlRequestId = 371;
    public const string DataName = "PMDG_777X_Data";
    public const uint DataId = 0x504D4447;
    public const uint DataDefinition = 0x504D4448;
    public const string ControlName = "PMDG_777X_Control";
    public const uint ControlId = 0x504D4449;
    public const uint ControlDefinition = 0x504D444A;
    public const uint ThirdPartyEventIdMinimum = 0x00011000;
    public const uint BatterySwitchEvent = ThirdPartyEventIdMinimum + 1;
    public const uint ApuSelectorEvent = ThirdPartyEventIdMinimum + 3;
    public const uint PrimaryExternalPowerSwitchEvent = ThirdPartyEventIdMinimum + 8;
    public const uint SecondaryExternalPowerSwitchEvent = ThirdPartyEventIdMinimum + 7;
    public const uint AdiruSwitchEvent = ThirdPartyEventIdMinimum + 59;
    public const uint IfePassengerSeatsSwitchEvent = ThirdPartyEventIdMinimum + 17;
    public const uint CabinUtilitySwitchEvent = ThirdPartyEventIdMinimum + 18;
    public const uint EmergencyLightsSwitchEvent = ThirdPartyEventIdMinimum + 49;
    public const uint EmergencyLightsGuardEvent = ThirdPartyEventIdMinimum + 50;
    public const uint PassengerOxygenGuardEvent = ThirdPartyEventIdMinimum + 53;
    public const uint ThrustAsymmetryCompensationEvent = ThirdPartyEventIdMinimum + 54;
    public const uint PrimaryFlightComputersEvent = ThirdPartyEventIdMinimum + 55;
    public const uint PrimaryFlightComputersGuardEvent = ThirdPartyEventIdMinimum + 56;
    public const uint ApuGeneratorSwitchEvent = ThirdPartyEventIdMinimum + 2;
    public const uint ApuBleedSwitchEvent = ThirdPartyEventIdMinimum + 131;
    public const uint EngineGeneratorOneSwitchEvent = ThirdPartyEventIdMinimum + 9;
    public const uint EngineGeneratorTwoSwitchEvent = ThirdPartyEventIdMinimum + 10;
    public const uint BackupGeneratorOneSwitchEvent = ThirdPartyEventIdMinimum + 11;
    public const uint BackupGeneratorTwoSwitchEvent = ThirdPartyEventIdMinimum + 12;
    public const uint LeftSideWindowHeatEvent = ThirdPartyEventIdMinimum + 45;
    public const uint LeftForwardWindowHeatEvent = ThirdPartyEventIdMinimum + 46;
    public const uint RightForwardWindowHeatEvent = ThirdPartyEventIdMinimum + 47;
    public const uint RightSideWindowHeatEvent = ThirdPartyEventIdMinimum + 48;
    public const uint LeftEnginePrimaryHydraulicPumpEvent = ThirdPartyEventIdMinimum + 39;
    public const uint RightEnginePrimaryHydraulicPumpEvent = ThirdPartyEventIdMinimum + 42;
    public const uint NavigationLightSwitchEvent = ThirdPartyEventIdMinimum + 115;
    public const uint FirstOfficerFlightDirectorSwitchEvent = ThirdPartyEventIdMinimum + 230;
    public const uint LnavSwitchEvent = ThirdPartyEventIdMinimum + 211;
    public const uint VnavSwitchEvent = ThirdPartyEventIdMinimum + 212;
    public const uint TransponderModeSelectorEvent = ThirdPartyEventIdMinimum + 749;
    public const uint EngineDisplaySwitchEvent = ThirdPartyEventIdMinimum + 234;
    public const uint EngineOneFuelControlEvent = ThirdPartyEventIdMinimum + 520;
    public const uint EngineTwoFuelControlEvent = ThirdPartyEventIdMinimum + 521;
    public const uint FlapsUpEvent = ThirdPartyEventIdMinimum + 5071;
    public const uint FlapsOneEvent = ThirdPartyEventIdMinimum + 5072;
    public const uint FlapsFiveEvent = ThirdPartyEventIdMinimum + 5073;
    public const uint FlapsFifteenEvent = ThirdPartyEventIdMinimum + 5074;
    public const uint FlapsTwentyEvent = ThirdPartyEventIdMinimum + 5075;
    public const uint FlapsTwentyFiveEvent = ThirdPartyEventIdMinimum + 5076;
    public const uint FlapsThirtyEvent = ThirdPartyEventIdMinimum + 5077;
    public const int ApproachFlapsOneCommandSpeedKnots = 260;
    public const int ApproachFlapsFiveCommandSpeedKnots = 240;
    public const int ApproachFlapsFifteenCommandSpeedKnots = 225;
    public const int ApproachFlapsTwentyCommandSpeedKnots = 215;
    public const uint GearLeverEvent = ThirdPartyEventIdMinimum + 295;
    public const uint AutobrakeSelectorEvent = ThirdPartyEventIdMinimum + 292;
    public const uint SpeedbrakeDownEvent = ThirdPartyEventIdMinimum + 4981;
    public const uint SpeedbrakeArmEvent = ThirdPartyEventIdMinimum + 4982;
    public const uint BeaconSwitchEvent = ThirdPartyEventIdMinimum + 114;
    public const int HumanControlIntervalMilliseconds = 900;
    public const uint MouseLeftSingle = 0x20000000;
    public const uint FlapsPresetParameter = MouseLeftSingle;
    public const string PackageName = "pmdg-aircraft-77w";
    public const string OptionsFileName = "777_Options.ini";
    public const string DataBroadcastSetting = "[SDK] EnableDataBroadcast=1";

    public static bool ApproachFlapCommandWouldRetract(int currentLever, int targetLever) =>
        currentLever > targetLever;

    public static int LandingFlapsCommandSpeedKnots(int landingFlaps) =>
        landingFlaps switch
        {
            20 => 215,
            25 => 195,
            30 => 175,
            _ => 0
        };

    public static IReadOnlyList<AircraftCapability> Capabilities { get; } =
        new[]
        {
            new AircraftCapability(
                "aircraft-identity",
                "Exact PMDG 777-300ER detection",
                CapabilitySupport.Supported,
                "MSFS aircraft title and pmdg-aircraft-77w package"),
            new AircraftCapability(
                "sdk-telemetry",
                "PMDG 777X SDK data broadcast",
                CapabilitySupport.ReadOnly,
                DataName),
            new AircraftCapability(
                "sdk-controls",
                "PMDG 777X SDK control events",
                CapabilitySupport.Supported,
                "Readback-backed Flow 1 power-up and Flow 2 First Officer preflight actions"),
            new AircraftCapability(
                "procedures",
                "Dedicated 777 gate-to-gate procedures",
                CapabilitySupport.Supported,
                "Flows 1-8 use PMDG command/readback integration; the remaining arrival flows stay visible for later integration")
        };
}
