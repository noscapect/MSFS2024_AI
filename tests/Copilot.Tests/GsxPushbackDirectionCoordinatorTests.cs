using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Gsx;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class GsxPushbackDirectionCoordinatorTests
{
    private static readonly GsxMenuSnapshot DirectionMenu = new(
        "Select pushback direction",
        new[]
        {
            "Nose Right/Tail Left (LEFT)",
            "Nose Left/Tail Right (RIGHT)",
            "Straight pushback (manual stop, max 100 m)"
        });

    [DataTestMethod]
    [DataRow("Push and start approved. Face North-East.", 45d)]
    [DataRow("Push approved, facing south west.", 225d)]
    [DataRow("Face NORTHWEST", 315d)]
    public void ParsesSayIntentionsFacingDirection(string message, double expected)
    {
        Assert.IsTrue(GsxPushbackDirectionCoordinator.TryParseTargetHeading(
            message,
            out var actual));
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ClockwiseTurnSelectsExplicitNoseRightChoice()
    {
        Assert.AreEqual(
            0,
            GsxPushbackDirectionCoordinator.FindChoice(DirectionMenu, 315, 45));
    }

    [TestMethod]
    public void CounterClockwiseTurnSelectsExplicitNoseLeftChoice()
    {
        Assert.AreEqual(
            1,
            GsxPushbackDirectionCoordinator.FindChoice(DirectionMenu, 135, 45));
    }

    [TestMethod]
    public void MatchingHeadingSelectsStraightPushback()
    {
        Assert.AreEqual(
            2,
            GsxPushbackDirectionCoordinator.FindChoice(DirectionMenu, 40, 45));
    }

    [TestMethod]
    public void OppositeHeadingSelectsDeterministicNoseRightChoice()
    {
        Assert.AreEqual(
            0,
            GsxPushbackDirectionCoordinator.FindChoice(DirectionMenu, 225, 45));
    }
}
