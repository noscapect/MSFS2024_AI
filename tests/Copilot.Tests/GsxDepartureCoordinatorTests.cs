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
}
