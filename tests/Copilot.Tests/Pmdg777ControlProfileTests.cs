using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.AircraftAdapters.Pmdg777;
using Msfs2024Ai.Copilot.Domain;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class Pmdg777ControlProfileTests
{
    [TestMethod]
    public void SdkBoundaryUsesOfficial777XClientDataIdentifiers()
    {
        Assert.AreEqual(684, Pmdg777ControlProfile.DataSize);
        Assert.AreEqual(370, Pmdg777ControlProfile.DataRequestId);
        Assert.AreEqual(371, Pmdg777ControlProfile.ControlRequestId);
        Assert.AreEqual("PMDG_777X_Data", Pmdg777ControlProfile.DataName);
        Assert.AreEqual(0x504D4447U, Pmdg777ControlProfile.DataId);
        Assert.AreEqual(0x504D4448U, Pmdg777ControlProfile.DataDefinition);
        Assert.AreEqual("PMDG_777X_Control", Pmdg777ControlProfile.ControlName);
        Assert.AreEqual(0x504D4449U, Pmdg777ControlProfile.ControlId);
        Assert.AreEqual(0x504D444AU, Pmdg777ControlProfile.ControlDefinition);
        Assert.AreEqual(69633U, Pmdg777ControlProfile.BatterySwitchEvent);
        Assert.AreEqual(69640U, Pmdg777ControlProfile.PrimaryExternalPowerSwitchEvent);
        Assert.AreEqual(69639U, Pmdg777ControlProfile.SecondaryExternalPowerSwitchEvent);
        Assert.IsTrue(Pmdg777ControlProfile.HumanControlIntervalMilliseconds >= 750,
            "777 grouped FO controls must be visibly separated at a human operating pace.");
        Assert.AreEqual(69691U, Pmdg777ControlProfile.AdiruSwitchEvent);
        Assert.AreEqual(69681U, Pmdg777ControlProfile.EmergencyLightsSwitchEvent);
        Assert.AreEqual(69682U, Pmdg777ControlProfile.EmergencyLightsGuardEvent);
        Assert.AreEqual(69685U, Pmdg777ControlProfile.PassengerOxygenGuardEvent);
        Assert.AreEqual(69686U, Pmdg777ControlProfile.ThrustAsymmetryCompensationEvent);
        Assert.AreEqual(69687U, Pmdg777ControlProfile.PrimaryFlightComputersEvent);
        Assert.AreEqual(69688U, Pmdg777ControlProfile.PrimaryFlightComputersGuardEvent);
        Assert.AreEqual(69747U, Pmdg777ControlProfile.NavigationLightSwitchEvent);
        Assert.AreEqual(69862U, Pmdg777ControlProfile.FirstOfficerFlightDirectorSwitchEvent);
        Assert.AreEqual(70381U, Pmdg777ControlProfile.TransponderModeSelectorEvent);
        Assert.AreEqual(0x20000000U, Pmdg777ControlProfile.MouseLeftSingle);
    }

    [TestMethod]
    public void SdkBoundaryCannotFallBackToThe737Ng3Namespace()
    {
        Assert.IsFalse(Pmdg777ControlProfile.DataName.Contains("NG3", StringComparison.Ordinal));
        Assert.IsFalse(Pmdg777ControlProfile.ControlName.Contains("NG3", StringComparison.Ordinal));
        Assert.AreEqual("pmdg-aircraft-77w", Pmdg777ControlProfile.PackageName);
        Assert.AreEqual("777_Options.ini", Pmdg777ControlProfile.OptionsFileName);
    }

    [TestMethod]
    public void OnlyExactIdentityIsEnabledDuringBootstrap()
    {
        Assert.AreEqual(
            CapabilitySupport.Supported,
            Pmdg777ControlProfile.Capabilities.Single(item =>
                item.Id == "aircraft-identity").Support);
        Assert.AreEqual(
            CapabilitySupport.ReadOnly,
            Pmdg777ControlProfile.Capabilities.Single(item =>
                item.Id == "sdk-telemetry").Support);
        Assert.AreEqual(
            CapabilitySupport.Supported,
            Pmdg777ControlProfile.Capabilities.Single(item =>
                item.Id == "procedures").Support);
        Assert.AreEqual(
            CapabilitySupport.Supported,
            Pmdg777ControlProfile.Capabilities.Single(item =>
                item.Id == "sdk-controls").Support);
        Assert.IsTrue(Pmdg777ControlProfile.Capabilities
            .Where(item => item.Id is not "aircraft-identity" and not "sdk-telemetry" and not "procedures" and not "sdk-controls")
            .All(item => item.Support == CapabilitySupport.NotImplemented));
    }

    [TestMethod]
    public void FlowOneSdkParserMapsTheShippedStructureOffsets()
    {
        var data = new byte[Pmdg777ControlProfile.DataSize];
        data[28] = 1;
        data[37] = 1;
        data[43] = 1;
        data[44] = 1;
        data[49] = 1;
        data[50] = 1;
        data[51] = 1;
        data[52] = 1;
        data[67] = 1;
        data[15] = 1;
        data[31] = 1;
        data[40] = 1;
        data[53] = 1;
        data[54] = 1;
        data[57] = 1;
        data[58] = 1;
        data[71] = 1;
        data[72] = 1;
        data[73] = 1;
        data[74] = 1;
        data[82] = 1;
        data[83] = 1;
        data[98] = 1;
        data[113] = 1;
        data[113] = 1;
        data[114] = 1;
        data[212] = 1;
        data[416] = 1;
        data[424] = 1;
        data[542] = 6;

        Assert.IsTrue(Pmdg777SdkData.TryParse(data, out var state));
        Assert.IsTrue(state.AdiruOn);
        Assert.IsTrue(state.BatteryOn);
        Assert.IsTrue(state.BusTiesAuto);
        Assert.IsTrue(state.PrimaryExternalPowerOn);
        Assert.IsTrue(state.SecondaryExternalPowerOn);
        Assert.IsTrue(state.PrimaryExternalPowerAvailable);
        Assert.IsTrue(state.SecondaryExternalPowerAvailable);
        Assert.IsTrue(state.CenterPrimaryPumpsOff);
        Assert.IsTrue(state.DemandPumpsOff);
        Assert.IsTrue(state.WipersOff);
        Assert.AreEqual(1, state.EmergencyLightsSelector);
        Assert.IsTrue(state.PacksOff);
        Assert.IsTrue(state.RecirculationFansOff);
        Assert.IsTrue(state.NavigationLightOn);
        Assert.IsTrue(state.LogoLightOn);
        Assert.IsTrue(state.GearLeverDown);
        Assert.IsTrue(state.AlternateFlapsOff);
        Assert.IsTrue(state.ParkingBrakeSet);
        Assert.AreEqual((byte)1, state.GearLeverRaw);
        Assert.AreEqual((byte)0, state.AlternateFlapsArmRaw);
        Assert.AreEqual((byte)1, state.AlternateFlapsControlRaw);
        Assert.AreEqual((byte)1, state.ParkingBrakeRaw);
    }

    [TestMethod]
    public void FlowOneSdkParserRejectsAnUnverifiedStructureSize()
    {
        Assert.IsFalse(Pmdg777SdkData.TryParse(new byte[683], out _));
    }

    [TestMethod]
    public void FlowOneSdkParserRejectsAnUnpublishedZeroFilledBlock()
    {
        Assert.IsFalse(Pmdg777SdkData.TryParse(new byte[Pmdg777ControlProfile.DataSize], out _));
    }

    [TestMethod]
    public void FlowOneSdkParserUsesTheLive777PrimarySecondaryExternalPowerOrder()
    {
        var data = new byte[Pmdg777ControlProfile.DataSize];
        data[542] = 6;
        data[50] = 1;
        data[52] = 1;

        Assert.IsTrue(Pmdg777SdkData.TryParse(data, out var state));
        Assert.IsTrue(state.PrimaryExternalPowerOn);
        Assert.IsTrue(state.PrimaryExternalPowerAvailable);
        Assert.IsFalse(state.SecondaryExternalPowerOn);
        Assert.IsFalse(state.SecondaryExternalPowerAvailable);
    }

    [TestMethod]
    public void FlowTwoSdkParserMapsExactPreflightReadbacks()
    {
        var data = new byte[Pmdg777ControlProfile.DataSize];
        data[542] = 6;
        data[15] = 1;
        data[31] = 1;
        data[40] = 1;
        data[53] = 1;
        data[54] = 1;
        data[57] = 1;
        data[58] = 1;
        data[67] = 1;
        data[71] = 1;
        data[72] = 1;
        data[73] = 1;
        data[74] = 1;
        data[82] = 1;
        data[83] = 1;
        data[98] = 1;
        data[241] = 0;
        data[242] = 0;
        data[243] = 0;
        data[316] = 0x10;
        data[317] = 0x27;
        data[326] = 1;
        data[420] = 0;
        data[421] = 0;
        data[422] = 0;
        data[423] = 0;
        data[449] = 0;
        data[512] = 1;
        data[113] = 1;
        data[138] = 1;
        data[139] = 1;
        data[140] = 1;
        data[141] = 1;
        data[142] = 1;
        data[170] = 1;
        data[171] = 1;
        data[172] = 1;
        data[158] = 1;
        data[179] = 30;
        data[180] = 30;
        data[277] = 2;
        foreach (var offset in new[] { 173, 174, 175, 176, 177, 178, 182, 192, 193, 194, 195, 196, 197, 204, 205 })
        {
            data[offset] = 1;
        }
        data[538] = 4;
        data[540] = 3;
        data[541] = 2;
        data[546] = 15;
        data[547] = 140;
        data[548] = 145;
        data[549] = 150;
        data[558] = 0xB0;
        data[559] = 0xAD;
        data[566] = 1;
        BitConverter.GetBytes(2200f).CopyTo(data, 572);
        System.Text.Encoding.ASCII.GetBytes("PMDG777  ").CopyTo(data, 576);
        data[588] = 1;
        data[586] = 1;
        data[589] = 1;

        Assert.IsTrue(Pmdg777SdkData.TryParse(data, out var state));
        Assert.IsTrue(state.ServiceInterphoneOff);
        Assert.IsTrue(state.PassengerOxygenNormal);
        Assert.IsTrue(state.FirstOfficerSourcesNormal);
        Assert.IsTrue(state.FirstOfficerDisplaysReady);
        Assert.IsTrue(state.SpeedbrakeDown);
        Assert.IsTrue(state.FlapsUp);
        Assert.IsTrue(state.FuelControlsCutoff);
        Assert.IsTrue(state.TransponderStandby);
        Assert.IsTrue(state.IrsAligned);
        Assert.IsTrue(state.ThrustAsymmetryCompensationAuto);
        Assert.IsTrue(state.PrimaryFlightComputersAuto);
        Assert.IsTrue(state.ApuGeneratorSwitchOn);
        Assert.IsTrue(state.EngineGeneratorOneSwitchOn);
        Assert.IsTrue(state.EngineGeneratorTwoSwitchOn);
        Assert.IsTrue(state.BackupGeneratorOneSwitchOn);
        Assert.IsTrue(state.BackupGeneratorTwoSwitchOn);
        Assert.IsTrue(state.LeftSideWindowHeatOn);
        Assert.IsTrue(state.LeftForwardWindowHeatOn);
        Assert.IsTrue(state.RightForwardWindowHeatOn);
        Assert.IsTrue(state.RightSideWindowHeatOn);
        Assert.IsTrue(state.LeftEnginePrimaryHydraulicPumpOn);
        Assert.IsTrue(state.RightEnginePrimaryHydraulicPumpOn);
        Assert.IsTrue(state.FirePanelNormal);
        Assert.IsTrue(state.EngineControlPanelNormal);
        Assert.IsTrue(state.FuelPanelPreflight);
        Assert.IsTrue(state.AntiIceAuto);
        Assert.IsTrue(state.ExteriorLightsPreflight);
        Assert.IsTrue(state.AirPanelPreflight);
        Assert.IsTrue(state.AutobrakeRto);
        Assert.IsTrue(state.TransponderAltitudeSourceNormal);
        Assert.IsTrue(state.NoSmokingAuto);
        Assert.IsTrue(state.SeatBeltsOff);
        Assert.IsFalse(state.SeatBeltsAuto);
        Assert.IsTrue(state.FuelToRemainSelectorIn);
        Assert.IsTrue(state.TemperatureControlsPreflight);
        Assert.IsTrue(state.FirstOfficerNdMap);
        Assert.AreEqual((ushort)10000, state.McpAltitude);
        Assert.IsTrue(state.FirstOfficerFlightDirectorOn);
        Assert.AreEqual((ushort)44464, state.FmcCruiseAltitude);
        Assert.AreEqual(2200f, state.FmcDistanceToDestination);
        Assert.AreEqual("PMDG777", state.FmcFlightNumber);
        Assert.IsTrue(state.FmcPerformanceInputComplete);
        Assert.IsTrue(state.PreflightChecklistComplete);
        Assert.IsTrue(state.ApuRunning);
        Assert.IsTrue(state.ApuGeneratorPowerEstablished);
        Assert.IsTrue(state.ApuBleedAirAvailable);
        Assert.IsTrue(state.BeforeStartChecklistComplete);
    }

    [TestMethod]
    public void SdkParserDistinguishesSeatBeltsAutoFromPreflightOff()
    {
        var data = new byte[Pmdg777ControlProfile.DataSize];
        data[542] = 6;
        data[99] = 1;

        Assert.IsTrue(Pmdg777SdkData.TryParse(data, out var state));
        Assert.IsTrue(state.SeatBeltsAuto);
        Assert.IsFalse(state.SeatBeltsOff);
    }

    [TestMethod]
    public void SdkParserMapsBeforeStartSystemsFromIndependentOffsets()
    {
        var data = new byte[Pmdg777ControlProfile.DataSize];
        data[542] = 6;
        data[84] = 1;
        data[85] = 1;
        data[86] = 1;
        data[87] = 1;
        data[88] = 1;
        data[89] = 1;
        data[112] = 1;
        data[148] = 1;
        data[149] = 1;
        data[150] = 1;
        data[151] = 1;
        data[449] = 2;
        BitConverter.GetBytes(500f).CopyTo(data, 496);

        Assert.IsTrue(Pmdg777SdkData.TryParse(data, out var state));
        Assert.IsTrue(state.HydraulicsBeforeStart);
        Assert.IsTrue(state.FuelPumpsBeforeStart);
        Assert.IsFalse(state.CenterFuelPumpsRequired);
        Assert.IsTrue(state.BeaconOn);
        Assert.IsTrue(state.TransponderXpndr);
        Assert.IsFalse(state.TransponderStandby);
    }

    [TestMethod]
    public void SdkParserMapsTaxiTakeoffAndClimbReadbacks()
    {
        var data = new byte[Pmdg777ControlProfile.DataSize];
        data[542] = 6;
        data[41] = 0;
        data[173] = 1;
        data[174] = 1;
        data[192] = 1;
        data[193] = 1;
        data[194] = 0;
        data[421] = 2;
        data[546] = 5;
        data[449] = 4;
        data[117] = 1;
        data[118] = 1;
        data[119] = 1;
        data[590] = 1;
        data[591] = 1;
        data[592] = 1;

        Assert.IsTrue(Pmdg777SdkData.TryParse(data, out var state));
        Assert.IsTrue(state.ApuSelectorOff);
        Assert.IsTrue(state.EngineBleedsAuto);
        Assert.IsTrue(state.PacksAuto);
        Assert.IsTrue(state.ApuBleedOff);
        Assert.IsTrue(state.TakeoffFlapsSet);
        Assert.IsTrue(state.TransponderTaRa);
        Assert.IsTrue(state.TaxiLightsSet);
        Assert.IsTrue(state.GearLeverUp);
        Assert.IsTrue(state.BeforeTaxiChecklistComplete);
        Assert.IsTrue(state.BeforeTakeoffChecklistComplete);
        Assert.IsTrue(state.AfterTakeoffChecklistComplete);
    }
}
