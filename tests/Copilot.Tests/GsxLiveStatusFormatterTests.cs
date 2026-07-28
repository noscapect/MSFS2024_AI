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
        Assert.IsTrue(state.SummaryText.Contains("180 / 220 passengers"));
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
}
