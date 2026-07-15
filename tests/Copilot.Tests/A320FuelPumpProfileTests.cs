using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.AircraftAdapters.IniBuildsA320;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class A320FuelPumpProfileTests
{
    [TestMethod]
    public void ProfileLocksAllSixLiveVerifiedA320Mappings()
    {
        var expected = new[]
        {
            ("L1", "INI_OUTER_TANK_LEFT", "__FUEL_ENG1_L1IsPressed", "INI_OUTER_TANK_LEFT_PUMP_ON"),
            ("L2", "INI_INNER_TANK_LEFT", "__FUEL_ENG1_L2IsPressed", "INI_INNER_TANK_LEFT_PUMP_ON"),
            ("C1", "INI_CENTER_TANK_LEFT", "__FUEL_CTR_1IsPressed", "INI_CENTER_TANK_LEFT_PUMP_ON"),
            ("C2", "INI_CENTER_TANK_RIGHT", "__FUEL_CTR_2IsPressed", "INI_CENTER_TANK_RIGHT_PUMP_ON"),
            ("R1", "INI_INNER_TANK_RIGHT", "__FUEL_ENG2_R1IsPressed", "INI_INNER_TANK_RIGHT_PUMP_ON"),
            ("R2", "INI_OUTER_TANK_RIGHT", "__FUEL_ENG2_R2IsPressed", "INI_OUTER_TANK_RIGHT_PUMP_ON")
        };

        Assert.AreEqual(expected.Length, A320FuelPumpProfile.Pumps.Count);
        for (var index = 0; index < expected.Length; index++)
        {
            var actual = A320FuelPumpProfile.Pumps[index];
            Assert.AreEqual(expected[index].Item1, actual.Name);
            Assert.AreEqual(expected[index].Item2, actual.SelectorLVar);
            Assert.AreEqual(expected[index].Item3, actual.PressAnimationLVar);
            Assert.AreEqual(expected[index].Item4, actual.ReadbackLVar);
        }
    }

    [TestMethod]
    public void ToggleCommandsRemainA320Specific()
    {
        Assert.AreEqual(
            "(L:INI_OUTER_TANK_LEFT) ! (>L:INI_OUTER_TANK_LEFT) " +
            "(L:__FUEL_ENG1_L1IsPressed) ! (>L:__FUEL_ENG1_L1IsPressed)",
            A320FuelPumpProfile.BuildToggleCommand(0));
        Assert.AreEqual(
            "(L:INI_OUTER_TANK_RIGHT) ! (>L:INI_OUTER_TANK_RIGHT) " +
            "(L:__FUEL_ENG2_R2IsPressed) ! (>L:__FUEL_ENG2_R2IsPressed)",
            A320FuelPumpProfile.BuildToggleCommand(5));
    }

    [TestMethod]
    public void VerificationRequiresEveryA320PumpReadback()
    {
        Assert.IsTrue(
            A320FuelPumpProfile.AreConfigured(new double[] { 1, 1, 1, 1, 1, 1 }));
        Assert.IsFalse(
            A320FuelPumpProfile.AreConfigured(new double[] { 1, 1, 1, 0, 1, 1 }));
        Assert.IsTrue(
            A320FuelPumpProfile.AreAllOff(new double[] { 0, 0, 0, 0, 0, 0 }));
        Assert.IsFalse(
            A320FuelPumpProfile.AreAllOff(new double[] { 0, 0, 0, 0, 0, 1 }));
    }
}
