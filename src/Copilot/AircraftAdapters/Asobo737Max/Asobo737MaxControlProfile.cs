namespace Msfs2024Ai.Copilot.AircraftAdapters.Asobo737Max;

internal static class Asobo737MaxControlProfile
{
    public const double ApuStart = 0;
    public const double ApuOn = 1;
    public const double ApuOff = 2;

    public const double PackAuto = 1;
    public const double PackOff = 2;

    public const double EngineBleedOff = 0;
    public const double EngineBleedOn = 1;

    public const double ElectricHydraulicPumpOn = 0;
    public const double ElectricHydraulicPumpOff = 1;

    public const double TaxiLightAuto = 0;
    public const double TaxiLightOff = 1;

    public const double RunwayTurnoffLightOn = 0;
    public const double RunwayTurnoffLightOff = 1;

    public const double AutothrottleDisarmed = 0;
    public const double AutothrottleArmed = 1;

    public const double TransponderStandby = 0;
    public const double TransponderAuto = 1;
    public const double TransponderOn = 2;
    public const double TransponderTaRa = 3;

    public const double LandingLightOn = 0;
    public const double LandingLightOff = 1;

    public static bool IsElectricHydraulicPumpOn(double inputEventValue) =>
        Math.Abs(inputEventValue - ElectricHydraulicPumpOn) < 0.1;

    public static bool IsTaxiLightAuto(double inputEventValue) =>
        Math.Abs(inputEventValue - TaxiLightAuto) < 0.1;

    public static double NormalizeTaxiLightPosition(double inputEventValue) =>
        IsTaxiLightAuto(inputEventValue) ? 1 : 2;

    public static bool IsRunwayTurnoffLightOn(double inputEventValue) =>
        Math.Abs(inputEventValue - RunwayTurnoffLightOn) < 0.1;

    public static bool IsAutothrottleArmed(double inputEventValue) =>
        Math.Abs(inputEventValue - AutothrottleArmed) < 0.1;

    public static bool IsTransponderTaRa(double inputEventValue) =>
        Math.Abs(inputEventValue - TransponderTaRa) < 0.1;

    public static bool IsTransponderAuto(double inputEventValue) =>
        Math.Abs(inputEventValue - TransponderAuto) < 0.1;

    public static bool IsLandingLightOn(double inputEventValue) =>
        Math.Abs(inputEventValue - LandingLightOn) < 0.1;

    public static bool IsGearHandleDown(double simulatorGearHandleValue) =>
        simulatorGearHandleValue >= 0.5;

    public static double NormalizeGearHandlePosition(double simulatorGearHandleValue) =>
        IsGearHandleDown(simulatorGearHandleValue) ? 2 : 0;

    public const double IsolationValveOpen = 0;
    public const double IsolationValveAuto = 1;

    public const double AntiCollisionOn = 0;
    public const double AntiCollisionOff = 1;

    public const double ExternalPowerOn = 0;
    public const double ExternalPowerNeutral = 1;
    public const double ExternalPowerOff = 2;

    public static double NormalizePackPosition(double inputEventValue) =>
        2 - Math.Round(inputEventValue, MidpointRounding.AwayFromZero);

    public static double NormalizeIsolationValvePosition(double inputEventValue) =>
        2 - Math.Round(inputEventValue, MidpointRounding.AwayFromZero);

    public static bool IsAntiCollisionOn(double inputEventValue) =>
        Math.Abs(inputEventValue - AntiCollisionOn) < 0.1;
}
