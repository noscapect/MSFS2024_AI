using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.AircraftAdapters.IniBuildsA321;
using Msfs2024Ai.Copilot.Procedures;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class A321RegressionBaselineTests
{
    [TestMethod]
    public void ProcedureCatalogReturnsOnlyTheDedicatedA321Definitions()
    {
        var selected = ProcedureCatalog.ForAircraft(
            new AircraftState { Title = "A321" });

        Assert.AreEqual(12, selected.Count);
        Assert.AreEqual(A321ProcedureLibrary.GateToGate.Count, selected.Count);
        for (var index = 0; index < selected.Count; index++)
        {
            Assert.AreSame(A321ProcedureLibrary.GateToGate[index], selected[index]);
        }
    }

    [TestMethod]
    public void FlapCommandsRemainOnTheLiveVerifiedA321Mapping()
    {
        const string increment =
            "MF.SimVars.Set.16384 (A:FLAPS NUM HANDLE POSITIONS, Number) / " +
            "(>B:HANDLING_Flaps_Inc)";
        const string decrement =
            "MF.SimVars.Set.16384 (A:FLAPS NUM HANDLE POSITIONS, Number) / " +
            "(>B:HANDLING_Flaps_Dec)";

        Assert.AreEqual(increment, A321ControlProfile.BuildTakeoffFlapsCommand());
        Assert.AreEqual(increment, A321ControlProfile.BuildFlapsExtensionCommand());
        Assert.AreEqual(decrement, A321ControlProfile.BuildFlapsCleanCommand(false));
        Assert.AreEqual(
            "MF.SimVars.Set.0 (>B:HANDLING_Flaps_Set)",
            A321ControlProfile.BuildFlapsCleanCommand(true));
    }

    [TestMethod]
    public void PhysicalHandleRemainsTheOnlyA321FlapDetentAuthority()
    {
        Assert.IsFalse(A321ControlProfile.FlapsAtDetent(1, 0));
        Assert.IsTrue(A321ControlProfile.FlapsAtDetent(1, 1));
        Assert.IsFalse(A321ControlProfile.FlapsAtDetent(0, 1));
        Assert.IsTrue(A321ControlProfile.FlapsAtDetent(0, 0));
    }

    [TestMethod]
    public void SignMappingsAndVerificationRemainA321Specific()
    {
        Assert.AreEqual(
            12887035727064807174UL,
            A321ControlProfile.GetSignInputEventHash(0));
        Assert.AreEqual(
            12889273306186432835UL,
            A321ControlProfile.GetSignInputEventHash(1));
        Assert.AreEqual(
            15249578372676866282UL,
            A321ControlProfile.GetSignInputEventHash(2));

        Assert.IsTrue(A321ControlProfile.SignSelectorAtPosition(1, 1));
        Assert.IsFalse(A321ControlProfile.SignSelectorAtPosition(0, 1));
        Assert.IsFalse(A321ControlProfile.SignSelectorAtPosition(null, 1));
    }
}
