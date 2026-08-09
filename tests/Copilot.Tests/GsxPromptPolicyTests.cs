using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Gsx;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class GsxPromptPolicyTests
{
    [TestMethod]
    public void CachedGoodEngineMenuCannotBeAnsweredAfterStatusAdvances()
    {
        var menu = new GsxMenuSnapshot(
            "Interrupt pushback?",
            new[]
            {
                "Confirm good engine Start",
                "Stop here and complete pushback procedure"
            });

        Assert.IsFalse(GsxPromptPolicy.CanAnswerGoodEngineStart(
            true,
            new[] { "[GSX] Have a good trip" },
            menu));
    }

    [TestMethod]
    public void LiveGoodEngineMenuCanBeAnsweredWhilePromptIsCurrent()
    {
        var menu = new GsxMenuSnapshot(
            "Interrupt pushback?",
            new[] { "Confirm good engine Start", "Abort pushback" });

        Assert.IsTrue(GsxPromptPolicy.CanAnswerGoodEngineStart(
            true,
            new[]
            {
                "[GSX] Waiting your confirmation for good engine start (Confirm from the GSX Menu)"
            },
            menu));
    }

    [TestMethod]
    public void ClosedCachedMenuCannotAcceptRemoteChoice()
    {
        var menu = new GsxMenuSnapshot(
            "Select handling operator",
            new[] { "Operator A", "Operator B" });

        Assert.IsFalse(GsxPromptPolicy.CanSubmitRemoteChoice(false, menu, 0));
        Assert.IsTrue(GsxPromptPolicy.CanSubmitRemoteChoice(true, menu, 0));
        Assert.IsFalse(GsxPromptPolicy.CanSubmitRemoteChoice(true, menu, 2));
    }

    [TestMethod]
    public void RecentHiddenQuestionCanBeSubmittedWithoutReopeningGsx()
    {
        var received = new DateTime(
            2026,
            8,
            9,
            23,
            42,
            25,
            DateTimeKind.Utc);

        Assert.IsTrue(GsxPromptPolicy.CanSubmitRecentHiddenChoice(
            true,
            true,
            received,
            received.AddSeconds(8)));
        Assert.IsFalse(GsxPromptPolicy.CanSubmitRecentHiddenChoice(
            true,
            true,
            received,
            received.AddSeconds(26)));
        Assert.IsFalse(GsxPromptPolicy.CanSubmitRecentHiddenChoice(
            false,
            true,
            received,
            received.AddSeconds(8)));
    }

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
