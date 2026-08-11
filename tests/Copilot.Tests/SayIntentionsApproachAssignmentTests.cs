using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.SayIntentions;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class SayIntentionsApproachAssignmentTests
{
    [DataTestMethod]
    [DataRow("Expect ILS runway 6", "06", true)]
    [DataRow("Cleared ILS06 approach via RIV1B", "06", true)]
    [DataRow("Cleared RNAV approach runway 36R", "36R", false)]
    public void ParsesApproachAssignments(
        string message,
        string expectedRunway,
        bool expectedIls)
    {
        Assert.IsTrue(SayIntentionsApproachAssignment.TryParse(
            message,
            out var assignment));
        Assert.AreEqual(expectedRunway, assignment.Runway);
        Assert.AreEqual(expectedIls, assignment.IsIls);
    }

    [TestMethod]
    public void IgnoresTaxiRunwayReferences()
    {
        Assert.IsFalse(SayIntentionsApproachAssignment.TryParse(
            "Taxi to runway 24 via Alpha",
            out _));
    }
}
