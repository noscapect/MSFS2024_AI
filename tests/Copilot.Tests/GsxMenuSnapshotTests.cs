using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Gsx;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class GsxMenuSnapshotTests
{
    [TestMethod]
    public void Parse_PreservesDynamicTitleAndChoiceOrder()
    {
        var result = GsxMenuSnapshot.Parse(new[]
        {
            "  Select Service  ",
            "Request Boarding",
            "Request Pushback",
            ""
        });

        Assert.AreEqual("Select Service", result.Title);
        CollectionAssert.AreEqual(
            new[] { "Request Boarding", "Request Pushback" },
            result.Choices.ToArray());
    }

    [TestMethod]
    public void Parse_EmptyInputReturnsEmptySnapshot()
    {
        var result = GsxMenuSnapshot.Parse(Array.Empty<string>());

        Assert.IsTrue(result.IsEmpty);
        Assert.AreEqual(0, result.Choices.Count);
    }
}
