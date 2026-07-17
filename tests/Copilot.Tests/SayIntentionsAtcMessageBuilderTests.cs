using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.SayIntentions;

namespace Copilot.Tests;

[TestClass]
public sealed class SayIntentionsAtcMessageBuilderTests
{
    [TestMethod]
    public void BuildIfrClearance_UsesActiveFlightContext()
    {
        var message = SayIntentionsAtcMessageBuilder.Build(
            "captain-ifr-clearance",
            "KLM123",
            "",
            "ebbr",
            "eham",
            "C5");

        Assert.AreEqual(
            "KLM123, at EBBR, request IFR clearance to EHAM, ready to copy",
            message);
    }

    [TestMethod]
    public void BuildPushback_FallsBackWithoutMissingFlightFields()
    {
        var message = SayIntentionsAtcMessageBuilder.Build(
            "captain-pushback-clearance",
            "",
            "BAW42",
            "",
            "",
            "");

        Assert.AreEqual(
            "BAW42, at the gate, request pushback and engine start",
            message);
    }

    [TestMethod]
    public void Build_RejectsUnrelatedProcedureStep()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            SayIntentionsAtcMessageBuilder.Build(
                "captain-seatbelts",
                "KLM123",
                "",
                "EHAM",
                "EBBR",
                "D52"));
    }
}
