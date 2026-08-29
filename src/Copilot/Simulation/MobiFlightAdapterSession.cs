using Msfs2024Ai.Copilot.AircraftAdapters.IniBuildsA310;
using Msfs2024Ai.Copilot.AircraftAdapters.IniBuildsA320;

namespace Msfs2024Ai.Copilot.Simulation;

internal sealed class MobiFlightAdapterSession
{
    // Change the schema suffix whenever the ordered runtime LVar list changes.
    // MobiFlight client-data layouts persist for the simulator session.
    public const string RuntimeClientName = "MSFS2024_AI_Copilot_v27";

    public bool AdapterReady { get; private set; }
    public bool RuntimeReady { get; private set; }
    public DateTime? RuntimeInitializedUtc { get; private set; }

    public void MarkAdapterReady() => AdapterReady = true;

    public void MarkRuntimeReady(DateTime utcNow)
    {
        RuntimeReady = true;
        RuntimeInitializedUtc = utcNow;
    }

    public bool HasRuntimeSettled(DateTime utcNow) =>
        RuntimeInitializedUtc.HasValue
        && utcNow - RuntimeInitializedUtc.Value >= TimeSpan.FromSeconds(2);

    public void ResetConnectionState()
    {
        AdapterReady = false;
        ResetRuntimeState();
    }

    public void ResetRuntimeState()
    {
        RuntimeReady = false;
        RuntimeInitializedUtc = null;
    }

    public IReadOnlyList<string> RuntimeRegistrationCommands => BuildRuntimeRegistrationCommands();

    private static IReadOnlyList<string> BuildRuntimeRegistrationCommands()
    {
        var commands = new List<string>
        {
            "MF.SimVars.Clear",
            "MF.SimVars.Add.(L:INI_OVHD_ELEC_BAT_1_PB_IS_AUTO_SWITCH)",
            "MF.SimVars.Add.(L:INI_OVHD_ELEC_BAT_2_PB_IS_AUTO_SWITCH)"
        };
        commands.AddRange(A320FuelPumpProfile.Pumps.Select(
            pump => $"MF.SimVars.Add.(L:{pump.ReadbackLVar})"));
        commands.AddRange(new[]
        {
            "MF.SimVars.Add.(L:INI_LOGO_LIGHT_SWITCH)",
            "MF.SimVars.Add.(L:INI_APU_AVAILABLE)",
            "MF.SimVars.Add.(L:INI_APU_MASTER_SWITCH)",
            "MF.SimVars.Add.(L:INI_APU_START_BUTTON)",
            "MF.SimVars.Add.(L:INI_APU_BLEED_BUTTON)",
            "MF.SimVars.Add.(L:INI_APU_GEN_ON)",
            "MF.SimVars.Add.(L:INI_APU_FLAP_PERCENT)",
            "MF.SimVars.Add.(L:INI_IRS1_STATE)",
            "MF.SimVars.Add.(L:INI_IRS2_STATE)",
            "MF.SimVars.Add.(L:INI_IRS3_STATE)",
            "MF.SimVars.Add.(L:INI_IRS_ON_BATTERY)",
            "MF.SimVars.Add.(L:INI_CREW_SUPPLY)",
            "MF.SimVars.Add.(L:INI_STROBE_LIGHT_SWITCH)",
            "MF.SimVars.Add.(L:INI_APU_FIRE_TEST)",
            "MF.SimVars.Add.(L:INI_ENG1_FIRE_TEST)",
            "MF.SimVars.Add.(L:INI_ENG2_FIRE_TEST)",
            "MF.SimVars.Add.(L:A320_APU_FIRE_LIT)",
            "MF.SimVars.Add.(L:INI_APU_FIRE_SOUND)",
            "MF.SimVars.Add.(L:A320_ENG1_FIRE_LIT)",
            "MF.SimVars.Add.(L:INI_ENG1_FIRE_SOUND)",
            "MF.SimVars.Add.(L:A320_ENG2_FIRE_LIT)",
            "MF.SimVars.Add.(L:INI_ENG2_FIRE_SOUND)",
            "MF.SimVars.Add.(L:INI_SEATBELTS_SWITCH)",
            "MF.SimVars.Add.(L:INI_SEATBELTS_ON)",
            "MF.SimVars.Add.(L:INI_NO_SMOKING_SWITCH)",
            "MF.SimVars.Add.(L:INI_NO_SMOKING_ON)",
            "MF.SimVars.Add.(L:INI_EMER_EXIT_SWITCH)",
            "MF.SimVars.Add.(L:INI_TCAS_ATC_STATE)",
            "MF.SimVars.Add.(L:INI_TCAS_MODE_PEDESTAL)",
            "MF.SimVars.Add.(L:INI_TCAS_STBY_STATE)",
            "MF.SimVars.Add.(L:INI_SPOILERS_ARMED)",
            "MF.SimVars.Add.(L:INI_AUTOBRAKE_LEVEL)",
            "MF.SimVars.Add.(L:INI_TCAS_ALT_STATE)",
            "MF.SimVars.Add.(L:INI_GEAR_HANDLE_STATUS_ANIMATION)",
            "MF.SimVars.Add.(L:INI_WX_SYS_SWITCH)",
            "MF.SimVars.Add.(L:INI_TAXI_LIGHT_SWITCH)",
            "MF.SimVars.Add.(L:A320_LANDING_LIGHT_SWITCH_LEFT)",
            "MF.SimVars.Add.(L:A320_LANDING_LIGHT_SWITCH_RIGHT)",
            "MF.SimVars.Add.(L:A32NX_OVHD_ELEC_BAT_1_PB_IS_AUTO)",
            "MF.SimVars.Add.(L:A32NX_OVHD_ELEC_BAT_2_PB_IS_AUTO)",
            "MF.SimVars.Add.(L:A32NX_ELEC_BAT_1_POTENTIAL)",
            "MF.SimVars.Add.(L:A32NX_ELEC_BAT_2_POTENTIAL)",
            "MF.SimVars.Add.(L:A32NX_OVHD_ELEC_BAT_1_PB_IS_AUTO, Bool)",
            "MF.SimVars.Add.(L:A32NX_OVHD_ELEC_BAT_2_PB_IS_AUTO, Bool)",
            "MF.SimVars.Add.(L:A32NX_EXT_PWR_AVAIL:1)",
            "MF.SimVars.Add.(L:A32NX_OVHD_ELEC_EXT_PWR_PB_IS_ON)",
            "MF.SimVars.Add.(L:A32NX_EXT_PWR_AVAIL:1, Bool)",
            "MF.SimVars.Add.(L:A32NX_OVHD_ELEC_EXT_PWR_PB_IS_ON, Bool)",
            "MF.SimVars.Add.(L:A32NX_OVHD_ADIRS_IR_1_MODE_SELECTOR_KNOB)",
            "MF.SimVars.Add.(L:A32NX_OVHD_ADIRS_IR_2_MODE_SELECTOR_KNOB)",
            "MF.SimVars.Add.(L:A32NX_OVHD_ADIRS_IR_3_MODE_SELECTOR_KNOB)",
            "MF.SimVars.Add.(L:A32NX_OVHD_ADIRS_IR_1_MODE_SELECTOR_KNOB, Enum)",
            "MF.SimVars.Add.(L:A32NX_OVHD_ADIRS_IR_2_MODE_SELECTOR_KNOB, Enum)",
            "MF.SimVars.Add.(L:A32NX_OVHD_ADIRS_IR_3_MODE_SELECTOR_KNOB, Enum)",
            "MF.SimVars.Add.(L:A32NX_OVHD_ADIRS_ON_BAT_IS_ILLUMINATED, Bool)",
            "MF.SimVars.Add.(L:PUSH_OVHD_OXYGEN_CREW)",
            "MF.SimVars.Add.(L:PUSH_OVHD_OXYGEN_CREW, Bool)",
            "MF.SimVars.Add.(L:A32NX_LIGHTS_NAV_LOGO)",
            "MF.SimVars.Add.(L:A32NX_LIGHTS_NAV_LOGO, Enum)",
            "MF.SimVars.Add.(L:STROBE_0_AUTO)",
            "MF.SimVars.Add.(L:LIGHTING_STROBE_0)",
            "MF.SimVars.Add.(L:XMLVAR_SWITCH_OVHD_INTLT_SEATBELT_Position)",
            "MF.SimVars.Add.(L:XMLVAR_SWITCH_OVHD_INTLT_NOSMOKING_Position)",
            "MF.SimVars.Add.(L:XMLVAR_SWITCH_OVHD_INTLT_EMEREXIT_Position)",
            "MF.SimVars.Add.(L:A32NX_OVHD_APU_MASTER_SW_PB_IS_ON)",
            "MF.SimVars.Add.(L:A32NX_OVHD_APU_START_PB_IS_ON)",
            "MF.SimVars.Add.(L:A32NX_OVHD_APU_START_PB_IS_AVAILABLE)",
            "MF.SimVars.Add.(L:A32NX_OVHD_PNEU_APU_BLEED_PB_IS_ON)",
            "MF.SimVars.Add.(L:A32NX_TRANSPONDER_MODE)",
            "MF.SimVars.Add.(L:A32NX_PARK_BRAKE_LEVER_POS)",
            "MF.SimVars.Add.(L:A32NX_ENGINE_STATE:1)",
            "MF.SimVars.Add.(L:A32NX_ENGINE_STATE:2)",
            "MF.SimVars.Add.(L:A32NX_ENGINE_N1:1)",
            "MF.SimVars.Add.(L:A32NX_ENGINE_N1:2)",
            "MF.SimVars.Add.(L:A32NX_PNEU_ENG_1_STARTER_VALVE_OPEN)",
            "MF.SimVars.Add.(L:A32NX_PNEU_ENG_2_STARTER_VALVE_OPEN)",
            "MF.SimVars.Add.(L:A32NX_SPOILERS_ARMED)",
            "MF.SimVars.Add.(L:A32NX_FLAPS_HANDLE_INDEX)",
            "MF.SimVars.Add.(L:A32NX_AUTOBRAKES_ARMED_MODE)",
            "MF.SimVars.Add.(L:A32NX_SWITCH_RADAR_PWS_POSITION)",
            "MF.SimVars.Add.(L:A32NX_SWITCH_ATC_ALT)",
            "MF.SimVars.Add.(L:A32NX_SWITCH_TCAS_POSITION)",
            "MF.SimVars.Add.(L:A32NX_EXT_PWR_AVAIL:1, Bool)",
            "MF.SimVars.Add.(L:A32NX_OVHD_ELEC_EXT_PWR_1_PB_IS_ON, Bool)",
            "MF.SimVars.Add.(L:A32NX_EXT_PWR_AVAIL:2, Bool)",
            "MF.SimVars.Add.(L:A32NX_OVHD_ELEC_EXT_PWR_2_PB_IS_ON, Bool)",
            "MF.SimVars.Add.(L:A32NX_EXT_PWR_AVAIL:3, Bool)",
            "MF.SimVars.Add.(L:A32NX_OVHD_ELEC_EXT_PWR_3_PB_IS_ON, Bool)",
            "MF.SimVars.Add.(L:A32NX_EXT_PWR_AVAIL:4, Bool)",
            "MF.SimVars.Add.(L:A32NX_OVHD_ELEC_EXT_PWR_4_PB_IS_ON, Bool)",
            "MF.SimVars.Add.(L:INI_IGNITION_KNOB)",
            $"MF.SimVars.Add.(L:{A320RunwayTurnoffProfile.ReadbackLVar})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.Battery1State})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.Battery2State})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.Battery3State})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.HydraulicEngine1State})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.HydraulicEngine1AState})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.HydraulicEngine2State})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.HydraulicEngine2BState})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.HydraulicElectricState})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.CaptainWiperState})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.FirstOfficerWiperState})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.WeatherRadarSystemState})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.Irs1State})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.Irs2State})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.Irs3State})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.OxygenLowPressureSupplyState})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.ApuFireTestState})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.ApuLoopTestSwitchState})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.AnnunciatorLightTestState})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.NavLogoLightState})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.BeaconLightState})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.TaxiLightState})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.LeftLandingLightState})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.RightLandingLightState})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.WingLightState})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.LeftRunwayTurnoffLightState})",
            $"MF.SimVars.Add.(L:{A310ControlProfile.RightRunwayTurnoffLightState})"
        });
        commands.AddRange(new[]
        {
            A310ControlProfile.SeatbeltsState, A310ControlProfile.NoSmokingState,
            A310ControlProfile.AtsMaster1State, A310ControlProfile.AtsMaster2State,
            A310ControlProfile.PitchTrim1State, A310ControlProfile.PitchTrim2State,
            A310ControlProfile.YawDamper1State, A310ControlProfile.YawDamper2State,
            A310ControlProfile.WindowHeat1State, A310ControlProfile.WindowHeat2State,
            A310ControlProfile.WindowHeat3State, A310ControlProfile.WindowHeat4State,
            A310ControlProfile.ProbeHeatCaptainState, A310ControlProfile.ProbeHeatFirstOfficerState,
            A310ControlProfile.ProbeHeatStandbyState, A310ControlProfile.EmergencyExitState,
            A310ControlProfile.CargoSmokeTestState, A310ControlProfile.EgpwsTestState,
            A310ControlProfile.AutobrakeState, A310ControlProfile.RudderTrimState,
            A310ControlProfile.TcasPedestalModeState, A310ControlProfile.CargoSmokeForwardIndicationState,
            A310ControlProfile.CargoSmokeAftIndicationState, A310ControlProfile.CargoSmokeBulkIndicationState
        }.Select(stateName => $"MF.SimVars.Add.(L:{stateName})"));
        commands.AddRange(A310ControlProfile.OperationalRuntimeStates.Select(
            stateName => $"MF.SimVars.Add.(L:{stateName})"));
        commands.Add("MF.DummyCmd");
        return commands;
    }
}
