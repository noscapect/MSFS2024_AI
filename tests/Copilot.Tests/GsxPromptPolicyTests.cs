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

    [TestMethod]
    public void DetectsPushbackDirectionQuestion()
    {
        var menu = new GsxMenuSnapshot(
            "Select pushback direction",
            new[]
            {
                "Nose Right/Tail Left (LEFT)",
                "Nose Left/Tail Right (RIGHT)",
                "Straight pushback (manual stop, max 100 m)"
            });

        Assert.IsTrue(GsxPromptPolicy.IsPushbackDirectionMenu(menu));
    }

    [TestMethod]
    public void MatchesRefreshedChoiceByPromptAndLabelInsteadOfOldIndex()
    {
        var refreshed = new GsxMenuSnapshot(
            "Select Push-back Direction",
            new[]
            {
                "QuickEdit Pushback",
                "Nose Left / Tail Right (RIGHT)",
                "Nose Right / Tail Left (LEFT)"
            });

        Assert.AreEqual(
            1,
            GsxPromptPolicy.FindMatchingChoice(
                refreshed,
                "Select pushback direction",
                "Nose Left/Tail Right (RIGHT)"));
    }

    [TestMethod]
    public void RefreshedChoiceRejectsChangedQuestion()
    {
        var changed = new GsxMenuSnapshot(
            "Confirm good engine start",
            new[] { "Confirm good engine start", "Abort pushback" });

        Assert.IsNull(GsxPromptPolicy.FindMatchingChoice(
            changed,
            "Select pushback direction",
            "Nose Left/Tail Right (RIGHT)"));
    }
}
