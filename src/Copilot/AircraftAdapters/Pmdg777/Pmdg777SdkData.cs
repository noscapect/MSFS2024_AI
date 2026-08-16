namespace Msfs2024Ai.Copilot.AircraftAdapters.Pmdg777;

/// <summary>
/// Read-only subset of PMDG_777X_Data required for Flows 1 and 2. Offsets and the
/// complete structure size are derived from the ordered declarations in the
/// PMDG_777X_SDK.h shipped with pmdg-aircraft-77w. C++ bool and unsigned char
/// fields occupy one byte and the complete structure is aligned to four bytes.
/// </summary>
internal sealed class Pmdg777SdkData
{
    private const int AircraftModelOffset = 542;
    private const byte Boeing777300ErModel = 6;

    public bool AdiruOn { get; private set; }
    public bool CabinUtilityOn { get; private set; }
    public bool IfePassengerSeatsOn { get; private set; }
    public bool BatteryOn { get; private set; }
    public bool ApuGeneratorOn { get; private set; }
    public byte ApuSelector { get; private set; }
    public bool BusTiesAuto { get; private set; }
    public bool PrimaryExternalPowerOn { get; private set; }
    public bool SecondaryExternalPowerOn { get; private set; }
    public bool PrimaryExternalPowerAvailable { get; private set; }
    public bool SecondaryExternalPowerAvailable { get; private set; }
    public bool CenterPrimaryPumpsOff { get; private set; }
    public bool DemandPumpsOff { get; private set; }
    public bool WipersOff { get; private set; }
    public byte EmergencyLightsSelector { get; private set; }
    public bool PacksOff { get; private set; }
    public bool RecirculationFansOff { get; private set; }
    public bool NavigationLightOn { get; private set; }
    public bool LogoLightOn { get; private set; }
    public bool GearLeverDown { get; private set; }
    public bool AlternateFlapsOff { get; private set; }
    public bool ParkingBrakeSet { get; private set; }
    public bool FirstOfficerFlightDirectorOn { get; private set; }
    public bool LnavArmed { get; private set; }
    public bool VnavArmed { get; private set; }
    public bool ServiceInterphoneOff { get; private set; }
    public bool PassengerOxygenNormal { get; private set; }
    public bool FirstOfficerSourcesNormal { get; private set; }
    public bool FirstOfficerDisplaysReady { get; private set; }
    public bool SpeedbrakeDown { get; private set; }
    public bool FlapsUp { get; private set; }
    public bool FuelControlsCutoff { get; private set; }
    public bool TransponderStandby { get; private set; }
    public ushort McpAltitude { get; private set; }
    public byte FmcTakeoffFlaps { get; private set; }
    public byte FmcLandingFlaps { get; private set; }
    public byte FmcLandingVref { get; private set; }
    public bool LandingFlapsSet { get; private set; }
    public byte FmcV1 { get; private set; }
    public byte FmcVr { get; private set; }
    public byte FmcV2 { get; private set; }
    public ushort FmcCruiseAltitude { get; private set; }
    public float FmcDistanceToDestination { get; private set; }
    public string FmcFlightNumber { get; private set; } = string.Empty;
    public bool FmcPerformanceInputComplete { get; private set; }
    public bool PreflightChecklistComplete { get; private set; }
    public bool IrsAligned { get; private set; }
    public bool ThrustAsymmetryCompensationAuto { get; private set; }
    public bool PrimaryFlightComputersAuto { get; private set; }
    public bool ApuGeneratorSwitchOn { get; private set; }
    public bool ApuRunning { get; private set; }
    public bool ApuGeneratorPowerEstablished { get; private set; }
    public bool ApuBleedAirAvailable { get; private set; }
    public bool BeforeStartChecklistComplete { get; private set; }
    public bool BeaconOn { get; private set; }
    public bool HydraulicsBeforeStart { get; private set; }
    public bool FuelPumpsBeforeStart { get; private set; }
    public bool CenterFuelPumpsRequired { get; private set; }
    public bool TransponderXpndr { get; private set; }
    public bool EngineGeneratorOneSwitchOn { get; private set; }
    public bool EngineGeneratorTwoSwitchOn { get; private set; }
    public bool BackupGeneratorOneSwitchOn { get; private set; }
    public bool BackupGeneratorTwoSwitchOn { get; private set; }
    public bool LeftSideWindowHeatOn { get; private set; }
    public bool LeftForwardWindowHeatOn { get; private set; }
    public bool RightForwardWindowHeatOn { get; private set; }
    public bool RightSideWindowHeatOn { get; private set; }
    public bool LeftEnginePrimaryHydraulicPumpOn { get; private set; }
    public bool RightEnginePrimaryHydraulicPumpOn { get; private set; }
    public bool FirePanelNormal { get; private set; }
    public bool EngineControlPanelNormal { get; private set; }
    public bool FuelPanelPreflight { get; private set; }
    public bool AntiIceAuto { get; private set; }
    public bool ExteriorLightsPreflight { get; private set; }
    public bool AirPanelPreflight { get; private set; }
    public bool AutobrakeRto { get; private set; }
    public bool TransponderAltitudeSourceNormal { get; private set; }
    public bool SeatBeltsOff { get; private set; }
    public bool SeatBeltsAuto { get; private set; }
    public bool NoSmokingAuto { get; private set; }
    public bool FuelToRemainSelectorIn { get; private set; }
    public bool TemperatureControlsPreflight { get; private set; }
    public bool FirstOfficerNdMap { get; private set; }
    public byte GearLeverRaw { get; private set; }
    public byte AlternateFlapsArmRaw { get; private set; }
    public byte AlternateFlapsControlRaw { get; private set; }
    public byte ParkingBrakeRaw { get; private set; }
    public bool SecondaryEngineDisplaySelected { get; private set; }
    public bool EngineOneStartSelectorStart { get; private set; }
    public bool EngineTwoStartSelectorStart { get; private set; }
    public bool EngineOneStartValveOpen { get; private set; }
    public bool EngineTwoStartValveOpen { get; private set; }
    public bool EngineOneFuelControlRun { get; private set; }
    public bool EngineTwoFuelControlRun { get; private set; }
    public bool WheelChocksSet { get; private set; }
    public bool ApuSelectorOff { get; private set; }
    public bool EngineBleedsAuto { get; private set; }
    public bool PacksAuto { get; private set; }
    public bool ApuBleedOff { get; private set; }
    public bool ApuBleedAuto { get; private set; }
    public byte FlapsLever { get; private set; }
    public bool TakeoffFlapsSet { get; private set; }
    public bool TransponderTaRa { get; private set; }
    public bool TaxiLightsSet { get; private set; }
    public bool TakeoffLightsSet { get; private set; }
    public bool ClimbLightsSet { get; private set; }
    public bool GearLeverUp { get; private set; }
    public bool BeforeTaxiChecklistComplete { get; private set; }
    public bool BeforeTakeoffChecklistComplete { get; private set; }
    public bool AfterTakeoffChecklistComplete { get; private set; }
    public bool SpeedbrakeArmed { get; private set; }
    public byte AutobrakeSelector { get; private set; }
    public bool LandingLightsOn { get; private set; }
    public bool AfterLandingLightsSet { get; private set; }
    public bool FuelPumpsOff { get; private set; }
    public bool HydraulicsShutdown { get; private set; }

    public static bool TryParse(byte[]? data, out Pmdg777SdkData state)
    {
        state = new Pmdg777SdkData();
        if (data == null
            || data.Length < Pmdg777ControlProfile.DataSize
            || data[AircraftModelOffset] != Boeing777300ErModel)
        {
            return false;
        }

        bool BoolAt(int offset) => data[offset] != 0;

        state.AdiruOn = BoolAt(28);
        state.ThrustAsymmetryCompensationAuto = BoolAt(31);
        state.PrimaryFlightComputersAuto = BoolAt(15);
        state.CabinUtilityOn = BoolAt(33);
        state.IfePassengerSeatsOn = BoolAt(35);
        state.BatteryOn = BoolAt(37);
        state.ApuGeneratorOn = BoolAt(40);
        state.ApuGeneratorSwitchOn = BoolAt(40);
        state.ApuSelector = data[41];
        state.BusTiesAuto = BoolAt(43) && BoolAt(44);
        // PMDG's live 777-300ER data has the primary/secondary entries in the
        // reverse order of the SDK header comment. EVT_OH_ELEC_GRD_PWR_PRIM_SWITCH
        // updates entry 1 (offsets 48/50/52), so use the demonstrated aircraft
        // order rather than the comment's array order.
        state.PrimaryExternalPowerOn = BoolAt(50);
        state.SecondaryExternalPowerOn = BoolAt(49);
        state.PrimaryExternalPowerAvailable = BoolAt(52);
        state.SecondaryExternalPowerAvailable = BoolAt(51);
        state.EngineGeneratorOneSwitchOn = BoolAt(53);
        state.EngineGeneratorTwoSwitchOn = BoolAt(54);
        state.BackupGeneratorOneSwitchOn = BoolAt(57);
        state.BackupGeneratorTwoSwitchOn = BoolAt(58);
        state.WipersOff = data[65] == 0 && data[66] == 0;
        state.EmergencyLightsSelector = data[67];
        state.ServiceInterphoneOff = !BoolAt(68);
        state.PassengerOxygenNormal = !BoolAt(69);
        state.LeftSideWindowHeatOn = BoolAt(71);
        state.LeftForwardWindowHeatOn = BoolAt(72);
        state.RightForwardWindowHeatOn = BoolAt(73);
        state.RightSideWindowHeatOn = BoolAt(74);
        state.LeftEnginePrimaryHydraulicPumpOn = BoolAt(82);
        state.RightEnginePrimaryHydraulicPumpOn = BoolAt(83);
        state.FirePanelNormal = !BoolAt(120)
                                && !BoolAt(121)
                                && data[127] == 0
                                && data[451] == 0
                                && data[452] == 0;
        state.EngineControlPanelNormal = BoolAt(138)
                                         && BoolAt(139)
                                         && data[140] == 1
                                         && data[141] == 1
                                         && BoolAt(142);
        state.FuelPanelPreflight = !BoolAt(146)
                                   && !BoolAt(147)
                                   && Enumerable.Range(148, 6).All(offset => !BoolAt(offset))
                                   && !BoolAt(154)
                                   && !BoolAt(155)
                                   && !BoolAt(156)
                                   && !BoolAt(157);
        state.AntiIceAuto = data[170] == 1 && data[171] == 1 && data[172] == 1;
        state.ExteriorLightsPreflight = !BoolAt(109)
                                        && !BoolAt(110)
                                        && !BoolAt(111)
                                        && !BoolAt(112)
                                        && BoolAt(113)
                                        && !BoolAt(116)
                                        && !BoolAt(117)
                                        && !BoolAt(118)
                                        && !BoolAt(119);
        state.AirPanelPreflight = BoolAt(173)
                                  && BoolAt(174)
                                  && BoolAt(175)
                                  && BoolAt(176)
                                  && BoolAt(177)
                                  && BoolAt(178)
                                  && BoolAt(182)
                                  && BoolAt(192)
                                  && BoolAt(193)
                                  && BoolAt(194)
                                  && BoolAt(195)
                                  && BoolAt(196)
                                  && BoolAt(197)
                                  && BoolAt(204)
                                  && BoolAt(205)
                                  && !BoolAt(208);
        state.ApuRunning = BoolAt(586);
        state.ApuGeneratorPowerEstablished = state.ApuRunning
                                             && state.ApuGeneratorSwitchOn
                                             && !BoolAt(39);
        state.ApuBleedAirAvailable = state.ApuRunning
                                     && BoolAt(194)
                                     && !BoolAt(200);
        state.BeaconOn = BoolAt(112);
        state.HydraulicsBeforeStart = BoolAt(84)
                                      && BoolAt(85)
                                      && data[86] == 1
                                      && data[87] == 1
                                      && data[88] == 1
                                      && data[89] == 1;
        state.CenterFuelPumpsRequired = BitConverter.ToSingle(data, 496) > 1000f;
        state.FuelPumpsBeforeStart = Enumerable.Range(148, 4).All(BoolAt)
                                           && (state.CenterFuelPumpsRequired
                                               ? BoolAt(152) && BoolAt(153)
                                               : !BoolAt(152) && !BoolAt(153));
        state.AutobrakeRto = data[222] == 0;
        state.AutobrakeSelector = data[222];
        state.TransponderAltitudeSourceNormal = !BoolAt(448);
        state.SeatBeltsOff = data[99] == 0;
        state.SeatBeltsAuto = data[99] == 1;
        state.NoSmokingAuto = data[98] == 1;
        state.FuelToRemainSelectorIn = !BoolAt(157) && data[158] == 1;
        state.TemperatureControlsPreflight = data[179] is >= 25 and <= 35
                                             && data[180] is >= 25 and <= 35;
        state.FirstOfficerNdMap = data[277] == 2;
        state.CenterPrimaryPumpsOff = !BoolAt(84) && !BoolAt(85);
        state.DemandPumpsOff = data[86] == 0
                               && data[87] == 0
                               && data[88] == 0
                               && data[89] == 0;
        state.PacksOff = !BoolAt(173) && !BoolAt(174);
        state.RecirculationFansOff = !BoolAt(177) && !BoolAt(178);
        state.NavigationLightOn = BoolAt(113);
        state.LogoLightOn = BoolAt(114);
        state.GearLeverRaw = data[212];
        state.AlternateFlapsArmRaw = data[415];
        state.AlternateFlapsControlRaw = data[416];
        state.ParkingBrakeRaw = data[424];
        state.EngineOneStartSelectorStart = data[140] == 0;
        state.EngineTwoStartSelectorStart = data[141] == 0;
        state.EngineOneFuelControlRun = BoolAt(422);
        state.EngineTwoFuelControlRun = BoolAt(423);
        state.EngineOneStartValveOpen = BoolAt(484);
        state.EngineTwoStartValveOpen = BoolAt(485);
        state.SecondaryEngineDisplaySelected = data[539] == 5;
        state.WheelChocksSet = BoolAt(585);
        state.ApuSelectorOff = data[41] == 0;
        state.PacksAuto = BoolAt(173) && BoolAt(174);
        state.EngineBleedsAuto = BoolAt(192) && BoolAt(193);
        state.ApuBleedOff = !BoolAt(194);
        state.ApuBleedAuto = BoolAt(194);
        state.FlapsLever = data[421];
        state.SpeedbrakeArmed = data[420] == 25;
        state.TransponderTaRa = data[449] == 4;
        state.TaxiLightsSet = BoolAt(116) && BoolAt(117) && BoolAt(118);
        state.TakeoffLightsSet = BoolAt(109)
                                 && BoolAt(110)
                                 && BoolAt(111)
                                 && BoolAt(116)
                                 && BoolAt(117)
                                 && BoolAt(119);
        state.ClimbLightsSet = !BoolAt(109)
                               && !BoolAt(110)
                               && !BoolAt(111)
                               && !BoolAt(114)
                               && !BoolAt(116)
                               && !BoolAt(117)
                               && !BoolAt(118);
        state.LandingLightsOn = BoolAt(109) && BoolAt(110) && BoolAt(111);
        state.AfterLandingLightsSet = !BoolAt(109)
                                      && !BoolAt(110)
                                      && !BoolAt(111)
                                      && BoolAt(116)
                                      && BoolAt(117)
                                      && BoolAt(118)
                                      && !BoolAt(119);
        state.FuelPumpsOff = Enumerable.Range(148, 6).All(offset => !BoolAt(offset));
        state.HydraulicsShutdown = BoolAt(82)
                                   && BoolAt(83)
                                   && Enumerable.Range(84, 6).All(offset => data[offset] == 0);
        state.GearLeverUp = state.GearLeverRaw == 0;
        state.BeforeTaxiChecklistComplete = BoolAt(590);
        state.BeforeTakeoffChecklistComplete = BoolAt(591);
        state.AfterTakeoffChecklistComplete = BoolAt(592);
        state.FirstOfficerSourcesNormal = !BoolAt(241)
                                                && !BoolAt(242)
                                                && !BoolAt(243);
        state.SpeedbrakeDown = data[420] == 0;
        state.FlapsUp = data[421] == 0;
        state.FuelControlsCutoff = !BoolAt(422) && !BoolAt(423);
        state.TransponderStandby = data[449] == 0;
        state.TransponderXpndr = data[449] == 2;
        state.McpAltitude = (ushort)(data[316] | data[317] << 8);
        state.FirstOfficerFlightDirectorOn = BoolAt(326);
        state.LnavArmed = BoolAt(359);
        state.VnavArmed = BoolAt(360);
        state.FirstOfficerDisplaysReady = data[538] == 4
                                           && data[540] == 3
                                           && data[541] == 2;
        state.FmcTakeoffFlaps = data[546];
        state.TakeoffFlapsSet = FlapsLeverMatchesSetting(
            state.FlapsLever,
            state.FmcTakeoffFlaps);
        state.FmcV1 = data[547];
        state.FmcVr = data[548];
        state.FmcV2 = data[549];
        state.FmcLandingFlaps = data[556];
        state.FmcLandingVref = data[557];
        state.LandingFlapsSet = FlapsLeverMatchesSetting(state.FlapsLever, state.FmcLandingFlaps);
        state.FmcCruiseAltitude = (ushort)(data[558] | data[559] << 8);
        state.FmcPerformanceInputComplete = BoolAt(566);
        state.FmcDistanceToDestination = BitConverter.ToSingle(data, 572);
        state.FmcFlightNumber = System.Text.Encoding.ASCII
            .GetString(data, 576, 9)
            .TrimEnd('\0', ' ');
        state.PreflightChecklistComplete = BoolAt(588);
        state.BeforeStartChecklistComplete = BoolAt(589);
        state.IrsAligned = BoolAt(512);
        state.GearLeverDown = state.GearLeverRaw == 1;
        state.AlternateFlapsOff = state.AlternateFlapsArmRaw == 0
                                  && state.AlternateFlapsControlRaw == 1;
        state.ParkingBrakeSet = state.ParkingBrakeRaw != 0;
        return true;
    }

    private static bool FlapsLeverMatchesSetting(byte lever, byte setting) =>
        setting switch
        {
            1 => lever == 1,
            5 => lever == 2,
            15 => lever == 3,
            20 => lever == 4,
            25 => lever == 5,
            30 => lever == 6,
            _ => false
        };
}
