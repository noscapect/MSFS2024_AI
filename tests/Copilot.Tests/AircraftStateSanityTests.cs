using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Diagnostics;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class AircraftStateSanityTests
{
    [TestMethod]
    public void CleanHandleWithExtendedSurfacesIsRejected()
    {
        var state = new AircraftState
        {
            FlapsHandleIndex = 0,
            LeftFlapPositionPercent = 20,
            RightFlapPositionPercent = 20
        };

        var issues = AircraftStateSanity.Evaluate(state);

        Assert.IsTrue(issues.Any(issue => issue.Contains("CLEAN")));
    }

    [TestMethod]
    public void ExtendedHandleWithRetractedSurfacesIsRejected()
    {
        var state = new AircraftState
        {
            FlapsHandleIndex = 2,
            LeftFlapPositionPercent = 0,
            RightFlapPositionPercent = 0
        };

        var issues = AircraftStateSanity.Evaluate(state);

        Assert.IsTrue(issues.Any(issue => issue.Contains("retracted")));
    }

    [TestMethod]
    public void MatchingFlapStateIsAccepted()
    {
        var state = new AircraftState
        {
            FlapsHandleIndex = 1,
            LeftFlapPositionPercent = 10,
            RightFlapPositionPercent = 10
        };

        Assert.AreEqual(0, AircraftStateSanity.Evaluate(state).Count);
    }
}
