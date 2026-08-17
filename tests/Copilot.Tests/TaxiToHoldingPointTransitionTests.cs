using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot;
using Msfs2024Ai.Copilot.Procedures;

namespace Copilot.Tests;

[TestClass]
public sealed class TaxiToHoldingPointTransitionTests
{
    [TestMethod]
    public void WaitingOnGroundWithNoActiveFlow_ShowsTransition()
    {
        var state = new AircraftState
        {
            OnGround = true,
            BeforeTakeoffHoldEligible = false
        };

        Assert.IsTrue(FlightProgressTransitionPolicy.ShouldShowTaxiToHoldingPoint(
            state,
            procedureActive: false,
            afterStartTaxiCompleted: true,
            beforeTakeoffCompleted: false,
            parkingShutdownCompleted: false,
            recommendedProcedureId: "before-takeoff"));
    }

    [TestMethod]
    public void AirborneAircraft_DoesNotShowStaleTransition()
    {
        var state = new AircraftState
        {
            OnGround = false,
            BeforeTakeoffHoldEligible = false
        };

        Assert.IsFalse(FlightProgressTransitionPolicy.ShouldShowTaxiToHoldingPoint(
            state,
            procedureActive: false,
            afterStartTaxiCompleted: true,
            beforeTakeoffCompleted: false,
            parkingShutdownCompleted: false,
            recommendedProcedureId: "before-takeoff"));
    }

    [TestMethod]
    public void ActiveLaterFlow_DoesNotShowStaleTransition()
    {
        var state = new AircraftState
        {
            OnGround = true,
            BeforeTakeoffHoldEligible = false
        };

        Assert.IsFalse(FlightProgressTransitionPolicy.ShouldShowTaxiToHoldingPoint(
            state,
            procedureActive: true,
            afterStartTaxiCompleted: true,
            beforeTakeoffCompleted: false,
            parkingShutdownCompleted: false,
            recommendedProcedureId: "before-takeoff"));
    }

    [TestMethod]
    public void CompletedShutdown_DoesNotReturnToDepartureTransition()
    {
        var state = new AircraftState
        {
            OnGround = true,
            BeforeTakeoffHoldEligible = false
        };

        Assert.IsFalse(FlightProgressTransitionPolicy.ShouldShowTaxiToHoldingPoint(
            state,
            procedureActive: false,
            afterStartTaxiCompleted: true,
            beforeTakeoffCompleted: false,
            parkingShutdownCompleted: true,
            recommendedProcedureId: "before-takeoff"));
    }
}
