using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Gsx;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class GsxArrivalGateCoordinatorTests
{
    [DataTestMethod]
    [DataRow(true, true, true, true, true, true)]
    [DataRow(false, true, true, true, true, false)]
    [DataRow(true, false, true, true, true, false)]
    [DataRow(true, true, false, true, true, false)]
    [DataRow(true, true, true, false, true, false)]
    [DataRow(true, true, true, true, false, false)]
    public void BridgeRequiresBothLiveIntegrationsAndArrivalState(
        bool gsxConnected,
        bool gsxOwned,
        bool sayIntentionsAvailable,
        bool onGround,
        bool arrivalFlow,
        bool expected)
    {
        Assert.AreEqual(
            expected,
            GsxArrivalGateCoordinator.IsBridgeAvailable(
                gsxConnected,
                gsxOwned,
                sayIntentionsAvailable,
                onGround,
                arrivalFlow));
    }

    [TestMethod]
    public void ExtractAssignedStandReadsTaxiGateClearance()
    {
        var stand = GsxArrivalGateCoordinator.ExtractAssignedStand(
            "Taxi to Gate C6 via VS, V, Z and B.");

        Assert.AreEqual("C6", stand);
    }

    [TestMethod]
    public void RootPositionMenuSelectsTerminalGroupWithoutCompletingStand()
    {
        var menu = new GsxMenuSnapshot(
            "Select Position at EHAM/Schiphol",
            new[]
            {
                "Select from Map",
                "Gate B (17 suitable parkings)",
                "Gate C (14 suitable parkings)",
                "Gate D (31 suitable parkings)"
            });

        var selection = GsxArrivalGateCoordinator.FindSelection(menu, "Gate C6");

        Assert.IsNotNull(selection);
        Assert.AreEqual(2, selection!.ChoiceIndex);
        Assert.IsFalse(selection.CompletesSelection);
    }

    [TestMethod]
    public void TerminalMenuSelectsExactStand()
    {
        var menu = new GsxMenuSnapshot(
            "Select Gate C at EHAM/Schiphol",
            new[] { "Gate C5", "Gate C6", "Gate C60", "Back" });

        var selection = GsxArrivalGateCoordinator.FindSelection(menu, "C6");

        Assert.IsNotNull(selection);
        Assert.AreEqual(1, selection!.ChoiceIndex);
        Assert.IsTrue(selection.CompletesSelection);
    }

    [TestMethod]
    public void UnrelatedGsxQuestionIsNeverAnswered()
    {
        var menu = new GsxMenuSnapshot(
            "Request FollowMe?",
            new[] { "Yes", "No", "Request Progressive Taxi" });

        Assert.IsNull(GsxArrivalGateCoordinator.FindSelection(menu, "C6"));
    }

    [TestMethod]
    public void MissingSayIntentionsStandLeavesGsxMenuManual()
    {
        var menu = new GsxMenuSnapshot(
            "Select Position at EHAM/Schiphol",
            new[] { "Gate C (14 suitable parkings)" });

        Assert.IsNull(GsxArrivalGateCoordinator.FindSelection(menu, null));
    }
}
