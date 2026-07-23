using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Gsx;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class GsxDepartureCoordinatorTests
{
    [TestMethod]
    public void FindChoice_SelectsBoardingButNotDeboarding()
    {
        var menu = new GsxMenuSnapshot(
            "Services",
            new[] { "Request Deboarding", "Request Boarding", "Request Catering" });

        Assert.AreEqual(
            1,
            GsxDepartureCoordinator.FindChoice(menu, GsxDepartureAction.Boarding));
    }

    [TestMethod]
    public void FindChoice_SelectsDeboarding()
    {
        var menu = new GsxMenuSnapshot(
            "Services",
            new[] { "Request Boarding", "Request Deboarding", "Request Catering" });

        Assert.AreEqual(
            1,
            GsxDepartureCoordinator.FindChoice(menu, GsxDepartureAction.Deboarding));
    }

    [TestMethod]
    public void FindChoice_AcceptsStartDeboarding()
    {
        var menu = new GsxMenuSnapshot(
            "Arrival",
            new[] { "Operate Jetway", "Start Deboarding" });

        Assert.AreEqual(
            1,
            GsxDepartureCoordinator.FindChoice(menu, GsxDepartureAction.Deboarding));
    }

    [TestMethod]
    public void FindChoice_AcceptsPushBackSpelling()
    {
        var menu = new GsxMenuSnapshot(
            "Departure",
            new[] { "Prepare for Push-Back and Departure" });

        Assert.AreEqual(
            0,
            GsxDepartureCoordinator.FindChoice(
                menu,
                GsxDepartureAction.PrepareForDeparture));
    }

    [TestMethod]
    public void FindChoice_DoesNotGuessAnUnrelatedChoice()
    {
        var menu = new GsxMenuSnapshot(
            "Services",
            new[] { "Request Catering", "Operate Jetway" });

        Assert.IsNull(
            GsxDepartureCoordinator.FindChoice(menu, GsxDepartureAction.Boarding));
    }

    [TestMethod]
    public void PushbackRequiresBrakeReleaseAndActualMovement()
    {
        Assert.IsFalse(GsxDepartureCoordinator.IsPushbackUnderway(true, true, 1));
        Assert.IsFalse(GsxDepartureCoordinator.IsPushbackUnderway(true, false, 0.05));
        Assert.IsFalse(GsxDepartureCoordinator.IsPushbackUnderway(false, false, 20));
        Assert.IsTrue(GsxDepartureCoordinator.IsPushbackUnderway(true, false, 0.1));
    }

    [DataTestMethod]
    [DataRow(true, false, false, false, false, false, false)]
    [DataRow(false, true, false, false, false, false, false)]
    [DataRow(false, false, true, false, false, false, false)]
    [DataRow(false, false, false, true, false, false, false)]
    [DataRow(false, false, false, false, true, false, false)]
    [DataRow(false, false, false, false, false, true, false)]
    [DataRow(false, false, false, false, false, false, true)]
    public void EngineStartPhaseStartedAcceptsAnyEngineStartEvidence(
        bool engineModeIgnStart,
        bool engine1StarterActive,
        bool engine2StarterActive,
        bool engine1FuelFlowDetected,
        bool engine2FuelFlowDetected,
        bool engine1Running,
        bool engine2Running)
    {
        Assert.IsTrue(GsxDepartureCoordinator.EngineStartPhaseStarted(
            engineModeIgnStart,
            engine1StarterActive,
            engine2StarterActive,
            engine1FuelFlowDetected,
            engine2FuelFlowDetected,
            engine1Running,
            engine2Running));
    }

    [TestMethod]
    public void EngineStartPhaseStartedRejectsColdAircraft()
    {
        Assert.IsFalse(GsxDepartureCoordinator.EngineStartPhaseStarted(
            false,
            false,
            false,
            false,
            false,
            false,
            false));
    }
}
