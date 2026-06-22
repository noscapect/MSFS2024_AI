namespace Msfs2024Ai.Copilot.Domain;

internal enum CapabilitySupport
{
    Supported,
    ReadOnly,
    ManualRequired,
    NotImplemented
}

internal sealed class AircraftCapability
{
    public AircraftCapability(string id, string name, CapabilitySupport support, string interfaceName)
    {
        Id = id;
        Name = name;
        Support = support;
        InterfaceName = interfaceName;
    }

    public string Id { get; }
    public string Name { get; }
    public CapabilitySupport Support { get; }
    public string InterfaceName { get; }
}

internal static class A320Capabilities
{
    public static IReadOnlyList<AircraftCapability> All { get; } =
        new[]
        {
            new AircraftCapability("aircraft-state", "Aircraft state monitoring", CapabilitySupport.Supported, "SimVars"),
            new AircraftCapability("external-power", "External power control", CapabilitySupport.Supported, "SimConnect Key Event"),
            new AircraftCapability("beacon", "Beacon light control", CapabilitySupport.Supported, "SimConnect Key Event"),
            new AircraftCapability("navigation-lights", "NAV & LOGO selector", CapabilitySupport.Supported, "AIRLINER_LT_NAVLOGO Set + INI_LOGO_LIGHT_SWITCH readback"),
            new AircraftCapability("strobe-lights", "STROBE selector", CapabilitySupport.Supported, "AIRLINER_LT_STROBE + INI_STROBE_LIGHT_SWITCH readback"),
            new AircraftCapability("fire-tests", "APU and engine fire tests", CapabilitySupport.Supported, "Native held-state commands + independent light/sound readback"),
            new AircraftCapability("signs", "Seatbelt/no-smoking/emergency-exit selectors", CapabilitySupport.Supported, "Aircraft Input Events + native selector readback"),
            new AircraftCapability("transponder-mode", "Transponder STBY/AUTO/ON selector", CapabilitySupport.Supported, "Explicit B-events + INI_TCAS_STBY_STATE readback"),
            new AircraftCapability("atc-system", "ATC system 1/2 selector", CapabilitySupport.ManualRequired, "Exact mapping captured; isolated live verification pending"),
            new AircraftCapability("atc-clearances", "ATC clearance communication", CapabilitySupport.ManualRequired, "Pilot uses the dynamic MSFS ATC interface; app monitors IFR clearance state"),
            new AircraftCapability("logo-lights", "Generic logo-light SimVar", CapabilitySupport.ReadOnly, "Not selector-position readback"),
            new AircraftCapability("battery-1", "Battery 1 pushbutton", CapabilitySupport.Supported, "MobiFlight gauge event"),
            new AircraftCapability("battery-2", "Battery 2 pushbutton", CapabilitySupport.Supported, "MobiFlight gauge event"),
            new AircraftCapability("adirs", "ADIRS selectors", CapabilitySupport.Supported, "Live-verified Input Events with native IRS state and ON BAT readback"),
            new AircraftCapability("apu-state", "APU state monitoring", CapabilitySupport.Supported, "Native iniBuilds LVars"),
            new AircraftCapability("apu-control", "APU master/start/bleed controls", CapabilitySupport.Supported, "Native command/state LVars"),
            new AircraftCapability("apu-generator", "APU generator switch", CapabilitySupport.Supported, "Native iniBuilds LVar"),
            new AircraftCapability("fuel-pumps", "Main fuel pumps", CapabilitySupport.Supported, "Native iniBuilds LVars"),
            new AircraftCapability("mcdu", "MCDU data entry", CapabilitySupport.NotImplemented, "Aircraft-specific mapping"),
            new AircraftCapability("radios", "Radio management", CapabilitySupport.NotImplemented, "SimConnect"),
            new AircraftCapability("autoflight", "Autoflight monitoring and control", CapabilitySupport.NotImplemented, "SimConnect"),
            new AircraftCapability("ground-services", "Ground-service coordination", CapabilitySupport.NotImplemented, "Adapter pending")
        };
}
