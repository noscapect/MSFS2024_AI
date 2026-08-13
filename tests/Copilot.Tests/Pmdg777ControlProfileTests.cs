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
        Assert.AreEqual("PMDG_777X_Data", Pmdg777ControlProfile.DataName);
        Assert.AreEqual(0x504D4447U, Pmdg777ControlProfile.DataId);
        Assert.AreEqual(0x504D4448U, Pmdg777ControlProfile.DataDefinition);
        Assert.AreEqual("PMDG_777X_Control", Pmdg777ControlProfile.ControlName);
        Assert.AreEqual(0x504D4449U, Pmdg777ControlProfile.ControlId);
        Assert.AreEqual(0x504D444AU, Pmdg777ControlProfile.ControlDefinition);
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
            CapabilitySupport.ReadOnly,
            Pmdg777ControlProfile.Capabilities.Single(item =>
                item.Id == "procedures").Support);
        Assert.IsTrue(Pmdg777ControlProfile.Capabilities
            .Where(item => item.Id is not "aircraft-identity" and not "sdk-telemetry" and not "procedures")
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
        data[113] = 1;
        data[114] = 1;
        data[212] = 1;
        data[416] = 1;
        data[424] = 1;

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
}
