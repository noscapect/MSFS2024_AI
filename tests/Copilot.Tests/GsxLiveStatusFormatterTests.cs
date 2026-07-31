using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Gsx;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class GsxLiveStatusFormatterTests
{
    [TestMethod]
    public void FormatsBoardingPassengerProgressCorrectly()
    {
        var tooltips = new[]
        {
            "[GSX] 180/220 passengers boarded",
            "[GSX] Departure clearance requested"
        };

        var state = GsxLiveStatusFormatter.Format(tooltips, null, true, true, true);

        Assert.AreEqual("180 / 220 passengers (82%)", state.PassengerProgressText);
        Assert.AreEqual(82, state.PassengerPercent);
        Assert.AreEqual(180, state.PassengerCurrent);
        Assert.AreEqual(220, state.PassengerTotal);
        Assert.IsTrue(state.BoardingInProgress);
        Assert.IsFalse(state.BoardingComplete);
        Assert.IsTrue(state.SummaryText.Contains("180 / 220 passengers"));
    }

    [TestMethod]
    public void MarksBoardingCompleteWhenPassengerTotalIsReached()
    {
        var state = GsxLiveStatusFormatter.Format(
            new[] { "[GSX] 140/140 passengers boarded" },
            null,
            true,
            true,
            true);

        Assert.IsTrue(state.BoardingComplete);
        Assert.IsFalse(state.BoardingInProgress);
    }

    [TestMethod]
    public void MarksBoardingCompleteFromCompletionStatus()
    {
        var state = GsxLiveStatusFormatter.Format(
            new[] { "[GSX] Boarding completed" },
            null,
            true,
            true,
            true);

        Assert.IsTrue(state.BoardingComplete);
        Assert.IsFalse(state.BoardingInProgress);
    }

    [TestMethod]
    public void FormatsActionRequiredFromTooltipAndMenu()
    {
        var tooltips = new[]
        {
            "[GSX] Waiting for your action: close Door 1L",
            "[GSX] Release parking brakes"
        };

        var state = GsxLiveStatusFormatter.Format(tooltips, null, true, true, true);

        Assert.IsTrue(state.HasActionRequired);
        Assert.AreEqual("close Door 1L | Release parking brakes", state.ActionRequiredText);
    }

    [TestMethod]
    public void RootServicesMenuDoesNotCreateActionRequired()
    {
        var menu = new GsxMenuSnapshot(
            "Activate Services at EBBR/Brussels National",
            new[]
            {
                "Request Deboarding",
                "Request Catering service",
                "Request Refueling",
                "Request Boarding",
                "Prepare for Push-back and Departure"
            });

        var state = GsxLiveStatusFormatter.Format(
            Array.Empty<string>(),
            menu,
            true,
            true,
            true);

        Assert.IsFalse(state.HasActionRequired);
        Assert.IsNull(state.ActionRequiredText);
    }

    [TestMethod]
    public void OperationalMenuStillCreatesActionRequired()
    {
        var menu = new GsxMenuSnapshot(
            "Select pushback direction",
            new[] { "Straight", "Tail left", "Tail right" });

        var state = GsxLiveStatusFormatter.Format(
            Array.Empty<string>(),
            menu,
            true,
            true,
            true);

        Assert.IsTrue(state.HasActionRequired);
        Assert.AreEqual(
            "Select: Select pushback direction",
            state.ActionRequiredText);
    }
}
