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
}
