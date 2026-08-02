using Msfs2024Ai.Copilot.Domain;

namespace Msfs2024Ai.Copilot.AircraftAdapters.IniBuildsA310;

/// <summary>
/// Isolation boundary for the MSFS iniBuilds A310-300. The state names below
/// are present in the locally installed aircraft's panel-state files. They are
/// evidence for readback subscriptions. Only controls with a separately
/// documented command and an independent live readback may become automatic.
/// </summary>
internal static class A310ControlProfile
{
    public const string Battery1State = "a310_bat1_on";
    public const string Battery2State = "a310_bat2_on";
    public const string Battery3State = "a310_bat3_on";
    public const string ApuMasterState = "a310_apu_master_switch";
    public const string ApuStartButtonState = "a310_apu_start_button";
    public const string ApuAvailableState = "a310_apu_available";
    public const string ApuBleedState = "a310_apu_bleed";
    public const string ApuGeneratorState = "a310_apu_gen_on";
    // Writable cockpit control from the published MobiFlight A310 preset.
    // The similarly named a310_inner_tank_pump2_left variable is read-only system state.
    public const string LeftInnerTankPump2State = "A310_INNER_TANK2_LEFT";
    public const string IgnitionSelectorState = "a310_eng_ignition_switch";
    public const string Pack1State = "a310_bleed_pack1_percent";
    public const string Pack2State = "a310_bleed_pack2_percent";
    public const string HydraulicEngine1State = "a310_hyd_eng1_switch_pos";
    public const string HydraulicEngine1AState = "a310_hyd_eng1_a_switch_pos";
    public const string HydraulicEngine2State = "a310_hyd_eng2_switch_pos";
    public const string HydraulicEngine2BState = "a310_hyd_eng2_b_switch_pos";
    public const string HydraulicElectricState = "a310_hyd_elec_status";
    public const string CaptainWiperState = "A310_CPT_WIPER_KNOB";
    public const string FirstOfficerWiperState = "A310_FO_WIPER_KNOB";
    public const string WeatherRadarSystemState = "a310_wxr_sys";
    public const string Irs1State = "a310_irs1_state";
    public const string Irs2State = "a310_irs2_state";
    public const string Irs3State = "a310_irs3_state";
    public const string OxygenLowPressureSupplyState = "a310_oxygen_low_pressure_supply";
    public const string ApuFireTestState = "a310_apu_fire_test";
    public const string ApuLoopTestSwitchState = "a310_apu_loop_test_switch";
    public const string AnnunciatorLightTestState = "a300dr_annunciator_light_test";
    public const string NavLogoLightState = "a310_nav_logo_light_switch";
    public const string BeaconLightState = "a310_beacon_light_switch";
    public const string TaxiLightState = "a310_taxi_lights_switch";
    public const string LeftLandingLightState = "a310_landing_light_l_switch";
    public const string RightLandingLightState = "a310_landing_light_r_switch";
    public const string WingLightState = "a310_wing_light_switch";
    public const string LeftRunwayTurnoffLightState = "a310_rwy_turnoff_l_switch";
    public const string RightRunwayTurnoffLightState = "a310_rwy_turnoff_r_switch";
    public const string SeatbeltsState = "a310_seatbelts_switch";
    public const string NoSmokingState = "a310_no_smoking_switch";
    public const string AtsMaster1State = "a310_autothrottle_master_switch1";
    public const string AtsMaster2State = "a310_autothrottle_master_switch2";
    public const string PitchTrim1State = "a310_pitch_trim1";
    public const string PitchTrim2State = "a310_pitch_trim2";
    public const string YawDamper1State = "a310_yaw_damper1";
    public const string YawDamper2State = "a310_yaw_damper2";
    public const string WindowHeat1State = "a300_window_heat1";
    public const string WindowHeat2State = "a300_window_heat2";
    public const string WindowHeat3State = "a300_window_heat3";
    public const string WindowHeat4State = "a300_window_heat4";
    public const string ProbeHeatCaptainState = "a300_probe_heat_capt";
    public const string ProbeHeatFirstOfficerState = "a300_probe_heat_copilot";
    public const string ProbeHeatStandbyState = "a300_probe_heat_standby";
    public const string EmergencyExitState = "a310_emer_exit_switch";
    public const string CargoSmokeTestState = "a300dr_cargo_compt_loop_switch";
    public const string CargoSmokeForwardIndicationState = "a300dr_cargo_forward_smoke";
    public const string CargoSmokeAftIndicationState = "a300dr_cargo_after_smoke";
    public const string CargoSmokeBulkIndicationState = "a300dr_cargo_after_bulk_smoke";
    public const string EgpwsTestState = "a310_gpws_test";
    public const string AutobrakeState = "a310_autobrake_level";
    public const string RudderTrimState = "a310_total_rudder_trim";
    public const string RudderTrimResetState = "a310_reset_rudder_trim_command";
    public const string TcasPedestalModeState = "a310_tcas_mode_pedestal";
    public const string AcBus1OffState = "a310_ac_bus1_off";
    public const string AcBus2OffState = "a310_ac_bus2_off";
    public const string FlapsLeftState = "a310_flaps_ratio1";
    public const string FlapsRightState = "a310_flaps_ratio2";

    public static string BatteryAutoCalculatorCode(int batteryNumber) =>
        $"1 (>L:{batteryNumber switch
        {
            1 => Battery1State,
            2 => Battery2State,
            3 => Battery3State,
            _ => throw new ArgumentOutOfRangeException(nameof(batteryNumber))
        }})";

    public static string SetCalculatorCode(string stateName, int value) =>
        $"{value} (>L:{stateName})";

    public static IReadOnlyList<AircraftCapability> Capabilities { get; } =
        new[]
        {
            Capability("aircraft-state", "Basic aircraft and flight state", CapabilitySupport.Supported, "SimConnect SimVars"),
            Capability("native-readback", "A310 panel-state readback framework", CapabilitySupport.ReadOnly, "A310 native state variables via MobiFlight runtime"),
            Capability("electrical", "Three battery selectors and electrical sources", CapabilitySupport.ReadOnly, "BAT 1/2/3 AUTO command with independent native readback; remaining electrical controls require validation"),
            Capability("irs", "Three IRS mode selectors and ISDU", CapabilitySupport.ManualRequired, "A310 Input Events require live capture"),
            Capability("fire-tests", "APU and engine fire tests", CapabilitySupport.ManualRequired, "Held test events and independent warning readback require live capture"),
            Capability("apu", "APU master, start, generator and bleed", CapabilitySupport.ManualRequired, "A310 Input Events require live capture"),
            Capability("engine-start", "Ignition, start switches and fuel levers", CapabilitySupport.ManualRequired, "A310 start controls require live capture"),
            Capability("lights", "A310 external-light panel", CapabilitySupport.ManualRequired, "A310 selector events require live capture"),
            Capability("slats-flaps", "A310 combined slat/flap schedule", CapabilitySupport.ManualRequired, "Detent command and readback require live capture"),
            Capability("autoflight", "ATS, FCP, PROFILE and LAND modes", CapabilitySupport.ManualRequired, "A310 autoflight controls require live capture"),
            Capability("transponder", "ATC and TCAS modes", CapabilitySupport.ManualRequired, "A310 pedestal control events require live capture")
        };

    private static AircraftCapability Capability(
        string id,
        string name,
        CapabilitySupport support,
        string interfaceName) =>
        new(id, name, support, interfaceName);
}
