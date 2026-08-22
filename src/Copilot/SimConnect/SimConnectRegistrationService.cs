using Microsoft.FlightSimulator.SimConnect;
using Msfs2024Ai.Copilot.AircraftAdapters.IniBuildsA310;
using Msfs2024Ai.Copilot.AircraftAdapters.Pmdg777;
using Msfs2024Ai.Copilot.Efb;
using Msfs2024Ai.Copilot;
using System.Runtime.InteropServices;

namespace Msfs2024Ai.Copilot.Simulation;

internal static class SimConnectRegistrationService
{
    public static void RegisterCore(SimConnect sender)
    {
        sender.AddToDataDefinition(Definition.AircraftState, "TITLE", null, SIMCONNECT_DATATYPE.STRING256, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "SIM ON GROUND", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "GROUND VELOCITY", "Knots", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "VELOCITY BODY Z", "Feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "PLANE HEADING DEGREES MAGNETIC", "Degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "GENERAL ENG COMBUSTION:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "GENERAL ENG COMBUSTION:2", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "GENERAL ENG STARTER ACTIVE:1", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "GENERAL ENG STARTER ACTIVE:2", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "TURB ENG CORRECTED N1:1", "Percent", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "TURB ENG CORRECTED N1:2", "Percent", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "TURB ENG CORRECTED N2:1", "Percent", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "TURB ENG CORRECTED N2:2", "Percent", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
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
        sender.AddToDataDefinition(Definition.AircraftState, "CIRCUIT SWITCH ON:21", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "CIRCUIT SWITCH ON:22", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
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
        sender.AddToDataDefinition(Definition.AircraftState, "AUTOPILOT AIRSPEED HOLD VAR", "Knots", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
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
        sender.AddToDataDefinition(Definition.AircraftState, "GEAR LEFT POSITION", "Percent Over 100", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "GEAR CENTER POSITION", "Percent Over 100", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "GEAR RIGHT POSITION", "Percent Over 100", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
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
        sender.AddToDataDefinition(Definition.AircraftState, "NAV DME:1", "Nautical miles", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "NAV DME:2", "Nautical miles", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "FUEL TOTAL QUANTITY WEIGHT", "Pounds", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:SIAI_INTERCOM1_RECEIVING", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:SIAI_INTERCOM2_RECEIVING", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:SIAI_INTERCOM3_RECEIVING", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:FSDT_GSX_COUATL_STARTED", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:FSDT_GSX_SET_REMOTECONTROL", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:INI_IGNITION_KNOB", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:INI_TURNOFF_LIGHT_SWITCH", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:switch_50_a", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:switch_53_a", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:switch_56_a", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:switch_89_a", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.AircraftState, "L:switch_1066_a", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.RegisterDataDefineStruct<AircraftData>(Definition.AircraftState);
        sender.AddToDataDefinition(Definition.FlightCalloutState, "SIM ON GROUND", "Bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.FlightCalloutState, "AIRSPEED INDICATED", "Knots", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.FlightCalloutState, "VERTICAL SPEED", "Feet per minute", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.FlightCalloutState, "PLANE ALT ABOVE GROUND", "Feet", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.FlightCalloutState, "TURB ENG CORRECTED N1:1", "Percent", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(Definition.FlightCalloutState, "TURB ENG CORRECTED N1:2", "Percent", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sender.RegisterDataDefineStruct<FlightCalloutData>(Definition.FlightCalloutState);
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
        sender.MapClientEventToSimEvent(CopilotEvent.SetAutopilotAirspeed, "AP_SPD_VAR_SET");
    }

    public static void RegisterGsx(SimConnect sender)
    {
        sender.AddToDataDefinition(
            Definition.GsxRemoteControl,
            "L:FSDT_GSX_SET_REMOTECONTROL",
            "Number",
            SIMCONNECT_DATATYPE.FLOAT64,
            0,
            SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(
            Definition.GsxMenuOpen,
            "L:FSDT_GSX_MENU_OPEN",
            "Number",
            SIMCONNECT_DATATYPE.FLOAT64,
            0,
            SimConnect.SIMCONNECT_UNUSED);
        sender.AddToDataDefinition(
            Definition.GsxMenuChoice,
            "L:FSDT_GSX_MENU_CHOICE",
            "Number",
            SIMCONNECT_DATATYPE.FLOAT64,
            0,
            SimConnect.SIMCONNECT_UNUSED);
        sender.RegisterDataDefineStruct<GsxValue>(Definition.GsxRemoteControl);
        sender.RegisterDataDefineStruct<GsxValue>(Definition.GsxMenuOpen);
        sender.RegisterDataDefineStruct<GsxValue>(Definition.GsxMenuChoice);
        sender.MapClientEventToSimEvent(
            CopilotEvent.GsxExternalSystemSet,
            "EXTERNAL_SYSTEM_SET");
        sender.MapClientEventToSimEvent(
            CopilotEvent.GsxExternalSystemToggle,
            "EXTERNAL_SYSTEM_TOGGLE");
        sender.AddClientEventToNotificationGroup(
            NotificationGroup.Gsx,
            CopilotEvent.GsxExternalSystemSet,
            false);
        sender.AddClientEventToNotificationGroup(
            NotificationGroup.Gsx,
            CopilotEvent.GsxExternalSystemToggle,
            false);
        sender.SetNotificationGroupPriority(
            NotificationGroup.Gsx,
            (uint)Priority.Highest);
    }

    public static void RegisterMobiFlight(SimConnect sender)
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
    }

    public static void RegisterPmdgNg3(SimConnect sender)
    {
            sender.MapClientDataNameToID("PMDG_NG3_Data", ClientDataArea.PmdgNg3Data);
            sender.AddToClientDataDefinition(
                ClientDataDefinition.PmdgNg3Data,
                0,
                SimConnectContractConstants.PmdgNg3DataSize,
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
    }

    public static void RegisterPmdg777(SimConnect sender)
    {
        sender.MapClientDataNameToID(
            Pmdg777ControlProfile.DataName,
            ClientDataArea.Pmdg777Data);
        sender.AddToClientDataDefinition(
            ClientDataDefinition.Pmdg777Data,
            0,
            Pmdg777ControlProfile.DataSize,
            0,
            0);
        sender.RegisterStruct<SIMCONNECT_RECV_CLIENT_DATA, Pmdg777RawData>(
            ClientDataDefinition.Pmdg777Data);
        sender.RequestClientData(
            ClientDataArea.Pmdg777Data,
            Request.Pmdg777Data,
            ClientDataDefinition.Pmdg777Data,
            SIMCONNECT_CLIENT_DATA_PERIOD.VISUAL_FRAME,
            SIMCONNECT_CLIENT_DATA_REQUEST_FLAG.CHANGED,
            0,
            0,
            0);
        sender.MapClientDataNameToID(
            Pmdg777ControlProfile.ControlName,
            ClientDataArea.Pmdg777Control);
        sender.AddToClientDataDefinition(
            ClientDataDefinition.Pmdg777Control,
            0,
            (uint)Marshal.SizeOf<Pmdg777Control>(),
            0,
            0);
        sender.RegisterStruct<SIMCONNECT_RECV_CLIENT_DATA, Pmdg777Control>(
            ClientDataDefinition.Pmdg777Control);
        sender.RequestClientData(
            ClientDataArea.Pmdg777Control,
            Request.Pmdg777Control,
            ClientDataDefinition.Pmdg777Control,
            SIMCONNECT_CLIENT_DATA_PERIOD.VISUAL_FRAME,
            SIMCONNECT_CLIENT_DATA_REQUEST_FLAG.CHANGED,
            0,
            0,
            0);
    }

    public static void RegisterEfb(SimConnect sender)
    {
        sender.SubscribeToCommBusEvent(
            EfbCommBusEvent.Command,
            EfbCompanionProtocol.CommandEventName);
    }

    public static void RegisterCoreRequests(SimConnect sender)
    {
        sender.RequestDataOnSimObject(
            Request.AircraftState,
            Definition.AircraftState,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
            0,
            0,
            0);
        sender.RequestDataOnSimObject(
            Request.FlightCalloutState,
            Definition.FlightCalloutState,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.VISUAL_FRAME,
            SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
            0,
            2,
            0);
    }

    public static void RegisterMobiFlightRuntime(
        SimConnect sender,
        string runtimeClientName)
    {
        sender.MapClientDataNameToID(
            $"{runtimeClientName}.LVars",
            ClientDataArea.MobiFlightRuntimeLVars);
        sender.CreateClientData(
            ClientDataArea.MobiFlightRuntimeLVars,
            4096,
            SIMCONNECT_CREATE_CLIENT_DATA_FLAG.DEFAULT);
        sender.MapClientDataNameToID(
            $"{runtimeClientName}.Command",
            ClientDataArea.MobiFlightRuntimeCommand);
        sender.CreateClientData(
            ClientDataArea.MobiFlightRuntimeCommand,
            1024,
            SIMCONNECT_CREATE_CLIENT_DATA_FLAG.DEFAULT);
        sender.MapClientDataNameToID(
            $"{runtimeClientName}.Response",
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
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeEngineModeSelector, Request.NativeEngineModeSelector, 98 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.NativeA320RunwayTurnoffSelector, Request.NativeA320RunwayTurnoffSelector, 99 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Battery1Auto, Request.A310Battery1Auto, 100 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Battery2Auto, Request.A310Battery2Auto, 101 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Battery3Auto, Request.A310Battery3Auto, 102 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310HydraulicEngine1, Request.A310HydraulicEngine1, 103 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310HydraulicEngine1A, Request.A310HydraulicEngine1A, 104 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310HydraulicEngine2, Request.A310HydraulicEngine2, 105 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310HydraulicEngine2B, Request.A310HydraulicEngine2B, 106 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310HydraulicElectric, Request.A310HydraulicElectric, 107 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310CaptainWiper, Request.A310CaptainWiper, 108 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310FirstOfficerWiper, Request.A310FirstOfficerWiper, 109 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310WeatherRadarSystem, Request.A310WeatherRadarSystem, 110 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Irs1, Request.A310Irs1, 111 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Irs2, Request.A310Irs2, 112 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Irs3, Request.A310Irs3, 113 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310OxygenSupply, Request.A310OxygenSupply, 114 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310ApuFireTest, Request.A310ApuFireTest, 115 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310ApuLoopTest, Request.A310ApuLoopTest, 116 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310AnnunciatorTest, Request.A310AnnunciatorTest, 117 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310NavLogoLight, Request.A310NavLogoLight, 118 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310BeaconLight, Request.A310BeaconLight, 119 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310TaxiLight, Request.A310TaxiLight, 120 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310LeftLandingLight, Request.A310LeftLandingLight, 121 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310RightLandingLight, Request.A310RightLandingLight, 122 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310WingLight, Request.A310WingLight, 123 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310LeftRunwayTurnoffLight, Request.A310LeftRunwayTurnoffLight, 124 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310RightRunwayTurnoffLight, Request.A310RightRunwayTurnoffLight, 125 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2Seatbelts, Request.A310Flow2Seatbelts, 126 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2NoSmoking, Request.A310Flow2NoSmoking, 127 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2Ats1, Request.A310Flow2Ats1, 128 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2Ats2, Request.A310Flow2Ats2, 129 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2PitchTrim1, Request.A310Flow2PitchTrim1, 130 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2PitchTrim2, Request.A310Flow2PitchTrim2, 131 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2YawDamper1, Request.A310Flow2YawDamper1, 132 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2YawDamper2, Request.A310Flow2YawDamper2, 133 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2WindowHeat1, Request.A310Flow2WindowHeat1, 134 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2WindowHeat2, Request.A310Flow2WindowHeat2, 135 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2WindowHeat3, Request.A310Flow2WindowHeat3, 136 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2WindowHeat4, Request.A310Flow2WindowHeat4, 137 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2ProbeHeatCaptain, Request.A310Flow2ProbeHeatCaptain, 138 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2ProbeHeatFirstOfficer, Request.A310Flow2ProbeHeatFirstOfficer, 139 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2ProbeHeatStandby, Request.A310Flow2ProbeHeatStandby, 140 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2EmergencyExit, Request.A310Flow2EmergencyExit, 141 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2CargoSmokeTest, Request.A310Flow2CargoSmokeTest, 142 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2EgpwsTest, Request.A310Flow2EgpwsTest, 143 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2Autobrake, Request.A310Flow2Autobrake, 144 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2RudderTrim, Request.A310Flow2RudderTrim, 145 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2TcasMode, Request.A310Flow2TcasMode, 146 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2CargoSmokeForward, Request.A310Flow2CargoSmokeForward, 147 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2CargoSmokeAft, Request.A310Flow2CargoSmokeAft, 148 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow2CargoSmokeBulk, Request.A310Flow2CargoSmokeBulk, 149 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow3ApuMaster, Request.A310Flow3ApuMaster, 150 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow3ApuStart, Request.A310Flow3ApuStart, 151 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow3ApuAvailable, Request.A310Flow3ApuAvailable, 152 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow3ApuBleed, Request.A310Flow3ApuBleed, 153 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow3ApuGenerator, Request.A310Flow3ApuGenerator, 154 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow4Ignition, Request.A310Flow4Ignition, 155 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow4Pack1, Request.A310Flow4Pack1, 156 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow4Pack2, Request.A310Flow4Pack2, 157 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow4Engine1Starter, Request.A310Flow4Engine1Starter, 158 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow4Engine2Starter, Request.A310Flow4Engine2Starter, 159 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow4Engine1FuelLever, Request.A310Flow4Engine1FuelLever, 160 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow4Engine2FuelLever, Request.A310Flow4Engine2FuelLever, 161 * sizeof(float));
        for (var index = 0; index < A310ControlProfile.FuelPumpStates.Count; index++)
        {
            RegisterMobiFlightFloat(
                sender,
                (ClientDataDefinition)((int)ClientDataDefinition.A310FuelPump1 + index),
                (Request)((int)Request.A310FuelPump1 + index),
                (162 + index) * sizeof(float));
        }
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow5WeatherRadarMode, Request.A310Flow5WeatherRadarMode, 174 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow5AutobrakeMax, Request.A310Flow5AutobrakeMax, 175 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310Flow5SpoilersArmed, Request.A310Flow5SpoilersArmed, 176 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310GearHandleStatus, Request.A310GearHandleStatus, 177 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310CaptainAltimeterStandard, Request.A310CaptainAltimeterStandard, 178 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310FirstOfficerAltimeterStandard, Request.A310FirstOfficerAltimeterStandard, 179 * sizeof(float));
        RegisterMobiFlightFloat(sender, ClientDataDefinition.A310StandbyAltimeterStandard, Request.A310StandbyAltimeterStandard, 180 * sizeof(float));
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
}
