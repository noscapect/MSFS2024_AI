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
    public const string DataName = "PMDG_777X_Data";
    public const uint DataId = 0x504D4447;
    public const uint DataDefinition = 0x504D4448;
    public const string ControlName = "PMDG_777X_Control";
    public const uint ControlId = 0x504D4449;
    public const uint ControlDefinition = 0x504D444A;
    public const string PackageName = "pmdg-aircraft-77w";
    public const string OptionsFileName = "777_Options.ini";
    public const string DataBroadcastSetting = "[SDK] EnableDataBroadcast=1";

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
                CapabilitySupport.NotImplemented,
                ControlName),
            new AircraftCapability(
                "procedures",
                "Dedicated 777 gate-to-gate procedures",
                CapabilitySupport.ReadOnly,
                "Twelve manual/monitored flows; automatic controls disabled")
        };
}
