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

    [TestMethod]
    public void BackwardPushbackIsNotDetectedAsForwardTaxi()
    {
        var state = new AircraftState
        {
            OnGround = true,
            ParkingBrakeSet = false,
            GroundSpeedKnots = 3,
            LongitudinalVelocityKnots = -3
        };

        Assert.IsFalse(state.ForwardTaxiDetected);
    }

    [TestMethod]
    public void ForwardMovementWithBrakeReleasedIsDetectedAsTaxi()
    {
        var state = new AircraftState
        {
            OnGround = true,
            ParkingBrakeSet = false,
            GroundSpeedKnots = 3,
            LongitudinalVelocityKnots = 3
        };

        Assert.IsTrue(state.ForwardTaxiDetected);
    }

    [TestMethod]
    public void TakeoffCalloutGatesUseExactConfiguredSpeeds()
    {
        var state = new AircraftState
        {
            TakeoffV1SpeedKnots = 140,
            TakeoffRotateSpeedKnots = 143
        };

        state.IndicatedAirspeedKnots = 99.9;
        Assert.IsFalse(state.HundredKnotsCalloutReached);
        Assert.IsFalse(state.V1CalloutReached);

        state.IndicatedAirspeedKnots = 100;
        Assert.IsTrue(state.HundredKnotsCalloutReached);

        state.IndicatedAirspeedKnots = 139.9;
        Assert.IsFalse(state.V1CalloutReached);

        state.IndicatedAirspeedKnots = 140;
        Assert.IsTrue(state.V1CalloutReached);
        Assert.IsFalse(state.RotateCalloutReached);

        state.IndicatedAirspeedKnots = 142.9;
        Assert.IsFalse(state.RotateCalloutReached);

        state.IndicatedAirspeedKnots = 143;
        Assert.IsTrue(state.RotateCalloutReached);
    }

    [TestMethod]
    public void PhysicalGearPositionsOverrideAStaleHandleReadback()
    {
        var state = new AircraftState
        {
            GearHandlePosition = 1,
            GearHandleDown = true,
            LeftGearPosition = 0,
            CenterGearPosition = 0,
            RightGearPosition = 0
        };

        Assert.IsFalse(state.GearHandleUp);
        Assert.IsTrue(state.GearUpVerified);
        Assert.IsFalse(state.GearDownVerified);
    }

    [TestMethod]
    public void GearVerificationRequiresAllThreeGearToReachTheTarget()
    {
        var state = new AircraftState
        {
            LeftGearPosition = 0,
            CenterGearPosition = 0.5,
            RightGearPosition = 0
        };

        Assert.IsFalse(state.GearUpVerified);
        Assert.IsFalse(state.GearDownVerified);
    }
}
