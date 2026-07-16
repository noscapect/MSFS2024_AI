using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.AircraftAdapters.IniBuildsA330;

namespace Copilot.Tests;

[TestClass]
public sealed class A330ControlProfileTests
{
    [DataTestMethod]
    [DataRow(0, 2.0)]
    [DataRow(1, 1.0)]
    [DataRow(2, 0.0)]
    public void SignCommandsMapNormalizedPositionsToA330PhysicalOrder(
        int normalizedPosition,
        double expectedPhysicalPosition)
    {
        Assert.AreEqual(
            expectedPhysicalPosition,
            A330ControlProfile.ToPhysicalSignPosition(normalizedPosition),
            0.001);
        Assert.AreEqual(
            normalizedPosition,
            A330ControlProfile.NormalizeSignPosition(expectedPhysicalPosition),
            0.001);
    }

    [DataTestMethod]
    [DataRow(4.0, 4)]
    [DataRow(3.0, 3)]
    [DataRow(1.0, 1)]
    [DataRow(0.0, 1)]
    public void FlapRetractionPlanSendsOneSpacedCommandPerRemainingDetent(
        double currentHandleIndex,
        int expectedSteps)
    {
        Assert.AreEqual(
            expectedSteps,
            A330ControlProfile.FlapRetractionStepCount(currentHandleIndex));
        Assert.IsTrue(A330ControlProfile.FlapStepIntervalMilliseconds >= 500);
        Assert.IsFalse(A330ControlProfile.FlapsRetractOneDetentCommand.Contains(
            ") 16384",
            StringComparison.Ordinal));
    }
}
