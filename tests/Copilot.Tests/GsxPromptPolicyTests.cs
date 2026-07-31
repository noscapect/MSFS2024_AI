using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Gsx;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class GsxPromptPolicyTests
{
    [DataTestMethod]
    [DataRow("[GSX] Waiting your confirmation for good engine start (Confirm from the GSX Menu)")]
    [DataRow("Confirm good engine Start")]
    public void GoodEngineStartStatusRequiresMenu(string status)
    {
        Assert.IsTrue(GsxPromptPolicy.RequiresGoodEngineStartMenu(new[] { status }));
    }

    [TestMethod]
    public void RoutinePushbackStatusDoesNotOpenMenu()
    {
        Assert.IsFalse(GsxPromptPolicy.RequiresGoodEngineStartMenu(
            new[] { "Pushback underway", "Release parking brakes" }));
    }

    [DataTestMethod]
    [DataRow("Confirm good engine start")]
    [DataRow("Engine start is good")]
    public void FindGoodEngineStartConfirmationSelectsPositiveResponse(string choiceText)
    {
        var menu = new GsxMenuSnapshot(
            "Confirm",
            new[] { "Cancel", choiceText });

        Assert.AreEqual(1, GsxPromptPolicy.FindGoodEngineStartConfirmation(menu));
    }

    [TestMethod]
    public void FindGoodEngineStartConfirmationDoesNotGuessUnrelatedChoices()
    {
        var menu = new GsxMenuSnapshot(
            "Services",
            new[] { "Request boarding", "Cancel pushback" });

        Assert.IsNull(GsxPromptPolicy.FindGoodEngineStartConfirmation(menu));
    }

    [TestMethod]
    public void RootServicesMenuIsInformational()
    {
        var menu = new GsxMenuSnapshot(
            "Activate Services at EBBR/Brussels National",
            new[]
            {
                "Request Deboarding",
                "Request Catering service",
                "Request Refueling",
                "Request Boarding",
                "Prepare for Push-back and Departure",
                "Operate Jetway",
                "Customize this Parking position"
            });

        Assert.IsTrue(GsxPromptPolicy.IsRootServicesMenu(menu));
    }

    [TestMethod]
    public void OperationalConfirmationIsNotRootServicesMenu()
    {
        var menu = new GsxMenuSnapshot(
            "Confirm good engine start",
            new[] { "Confirm good engine start", "Abort pushback" });

        Assert.IsFalse(GsxPromptPolicy.IsRootServicesMenu(menu));
    }
}
