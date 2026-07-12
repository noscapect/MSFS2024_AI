using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Telemetry;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class FbwCrewOxygenTests
{
    [TestMethod]
    public void TypedReadbackOneMeansCrewOxygenOff()
    {
        Assert.IsFalse(FbwStateResolvers.ResolveCrewOxygen(
            commandedValue: null,
            commandedUtc: null,
            typedValue: true,
            untypedValue: false));
    }

    [TestMethod]
    public void TypedReadbackZeroMeansCrewOxygenOn()
    {
        Assert.IsTrue(FbwStateResolvers.ResolveCrewOxygen(
            commandedValue: null,
            commandedUtc: null,
            typedValue: false,
            untypedValue: true));
    }

    [TestMethod]
    public void LiveReadbackBeatsRecentCommandedState()
    {
        Assert.IsFalse(FbwStateResolvers.ResolveCrewOxygen(
            commandedValue: true,
            commandedUtc: DateTime.UtcNow,
            typedValue: true,
            untypedValue: null));
    }

    [TestMethod]
    public void BatteryLiveReadbackBeatsStaleCommandedState()
    {
        Assert.IsFalse(FbwStateResolvers.ResolveBattery(
            commandedPushbuttonAuto: true,
            typedPushbuttonAuto: false,
            untypedPushbuttonAuto: null,
            genericMasterBattery: 1));
    }

    [TestMethod]
    public void SelectorLiveReadbackBeatsRecentCommandedState()
    {
        Assert.AreEqual(
            0,
            FbwStateResolvers.ResolveSelector(
                commandedValue: 2,
                commandedUtc: DateTime.UtcNow,
                typedValue: 0,
                untypedValue: null));
    }

    [TestMethod]
    public void SelectorAcceptsRecentCommandWhenUntypedReadbackConfirmsIt()
    {
        Assert.AreEqual(
            1,
            FbwStateResolvers.ResolveSelector(
                commandedValue: 1,
                commandedUtc: DateTime.UtcNow,
                typedValue: 0,
                untypedValue: 1));
    }

    [TestMethod]
    public void SelectorUsesTypedReadbackWhenNoCommandIsActive()
    {
        Assert.AreEqual(
            0,
            FbwStateResolvers.ResolveSelector(
                commandedValue: null,
                commandedUtc: null,
                typedValue: 0,
                untypedValue: 1));
    }

    [TestMethod]
    public void BoolLiveReadbackBeatsRecentCommandedState()
    {
        Assert.IsFalse(FbwStateResolvers.ResolveBool(
            commandedValue: true,
            typedValue: false,
            untypedValue: null));
    }
}
