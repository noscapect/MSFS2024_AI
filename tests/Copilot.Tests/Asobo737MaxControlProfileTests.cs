using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.AircraftAdapters.Asobo737Max;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class Asobo737MaxControlProfileTests
{
    [TestMethod]
    public void ApuSelectorUsesStartZeroOnOneAndOffTwo()
    {
        Assert.AreEqual(0d, Asobo737MaxControlProfile.ApuStart);
        Assert.AreEqual(1d, Asobo737MaxControlProfile.ApuOn);
        Assert.AreEqual(2d, Asobo737MaxControlProfile.ApuOff);
    }

    [TestMethod]
    public void PackInputEventsUseAutoOneAndOffTwo()
    {
        Assert.AreEqual(1d, Asobo737MaxControlProfile.PackAuto);
        Assert.AreEqual(2d, Asobo737MaxControlProfile.PackOff);
        Assert.AreEqual(1d, Asobo737MaxControlProfile.NormalizePackPosition(1));
        Assert.AreEqual(0d, Asobo737MaxControlProfile.NormalizePackPosition(2));
    }

    [TestMethod]
    public void EngineBleedInputEventsUseOnOneAndOffZero()
    {
        Assert.AreEqual(1d, Asobo737MaxControlProfile.EngineBleedOn);
        Assert.AreEqual(0d, Asobo737MaxControlProfile.EngineBleedOff);
    }

    [TestMethod]
    public void ElectricHydraulicPumpInputEventsAreInverted()
    {
        Assert.AreEqual(0d, Asobo737MaxControlProfile.ElectricHydraulicPumpOn);
        Assert.AreEqual(1d, Asobo737MaxControlProfile.ElectricHydraulicPumpOff);
        Assert.IsTrue(Asobo737MaxControlProfile.IsElectricHydraulicPumpOn(0));
        Assert.IsFalse(Asobo737MaxControlProfile.IsElectricHydraulicPumpOn(1));
    }

    [TestMethod]
    public void TaxiLightUsesAutoZeroAndOffOne()
    {
        Assert.AreEqual(0d, Asobo737MaxControlProfile.TaxiLightAuto);
        Assert.AreEqual(1d, Asobo737MaxControlProfile.TaxiLightOff);
        Assert.IsTrue(Asobo737MaxControlProfile.IsTaxiLightAuto(0));
        Assert.IsFalse(Asobo737MaxControlProfile.IsTaxiLightAuto(1));
        Assert.AreEqual(1d, Asobo737MaxControlProfile.NormalizeTaxiLightPosition(0));
        Assert.AreEqual(2d, Asobo737MaxControlProfile.NormalizeTaxiLightPosition(1));
    }

    [TestMethod]
    public void RunwayTurnoffLightInputEventsAreInverted()
    {
        Assert.AreEqual(0d, Asobo737MaxControlProfile.RunwayTurnoffLightOn);
        Assert.AreEqual(1d, Asobo737MaxControlProfile.RunwayTurnoffLightOff);
        Assert.IsTrue(Asobo737MaxControlProfile.IsRunwayTurnoffLightOn(0));
        Assert.IsFalse(Asobo737MaxControlProfile.IsRunwayTurnoffLightOn(1));
    }

    [TestMethod]
    public void BeforeTakeoffControlsUseCapturedMaxValues()
    {
        Assert.AreEqual(0d, Asobo737MaxControlProfile.AutothrottleDisarmed);
        Assert.AreEqual(1d, Asobo737MaxControlProfile.AutothrottleArmed);
        Assert.IsFalse(Asobo737MaxControlProfile.IsAutothrottleArmed(0));
        Assert.IsTrue(Asobo737MaxControlProfile.IsAutothrottleArmed(1));

        Assert.AreEqual(3d, Asobo737MaxControlProfile.TransponderTaRa);
        Assert.IsTrue(Asobo737MaxControlProfile.IsTransponderTaRa(3));
        Assert.IsFalse(Asobo737MaxControlProfile.IsTransponderTaRa(0));

        Assert.AreEqual(0d, Asobo737MaxControlProfile.TransponderStandby);
        Assert.AreEqual(1d, Asobo737MaxControlProfile.TransponderAuto);
        Assert.AreEqual(2d, Asobo737MaxControlProfile.TransponderOn);
        Assert.IsTrue(Asobo737MaxControlProfile.IsTransponderAuto(1));
        Assert.IsFalse(Asobo737MaxControlProfile.IsTransponderAuto(0));

        Assert.AreEqual(0d, Asobo737MaxControlProfile.LandingLightOn);
        Assert.AreEqual(1d, Asobo737MaxControlProfile.LandingLightOff);
        Assert.IsTrue(Asobo737MaxControlProfile.IsLandingLightOn(0));
        Assert.IsFalse(Asobo737MaxControlProfile.IsLandingLightOn(1));

        Assert.IsTrue(Asobo737MaxControlProfile.IsGearHandleDown(1));
        Assert.IsFalse(Asobo737MaxControlProfile.IsGearHandleDown(0));
        Assert.AreEqual(2d, Asobo737MaxControlProfile.NormalizeGearHandlePosition(1));
        Assert.AreEqual(0d, Asobo737MaxControlProfile.NormalizeGearHandlePosition(0));
    }

    [TestMethod]
    public void IsolationInputEventsUseOpenZeroAndAutoOne()
    {
        Assert.AreEqual(0d, Asobo737MaxControlProfile.IsolationValveOpen);
        Assert.AreEqual(1d, Asobo737MaxControlProfile.IsolationValveAuto);
        Assert.AreEqual(2d, Asobo737MaxControlProfile.NormalizeIsolationValvePosition(0));
        Assert.AreEqual(1d, Asobo737MaxControlProfile.NormalizeIsolationValvePosition(1));
    }

    [TestMethod]
    public void AntiCollisionInputEventIsInverted()
    {
        Assert.AreEqual(0d, Asobo737MaxControlProfile.AntiCollisionOn);
        Assert.AreEqual(1d, Asobo737MaxControlProfile.AntiCollisionOff);
        Assert.IsTrue(Asobo737MaxControlProfile.IsAntiCollisionOn(0));
        Assert.IsFalse(Asobo737MaxControlProfile.IsAntiCollisionOn(1));
    }

    [TestMethod]
    public void ExternalPowerUsesMomentaryOffAndNeutralRelease()
    {
        Assert.AreEqual(2d, Asobo737MaxControlProfile.ExternalPowerOff);
        Assert.AreEqual(1d, Asobo737MaxControlProfile.ExternalPowerNeutral);
    }
}
