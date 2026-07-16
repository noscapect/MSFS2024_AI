using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.SimBrief;

namespace Copilot.Tests;

[TestClass]
public sealed class SimBriefImportValidatorTests
{
    [TestMethod]
    public void IniBuildsA330_AcceptsA333AndRejectsA339Plan()
    {
        var generated = new DateTime(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);
        var matching = SimBriefImportValidator.Validate(
            new ImportedFlightPlan { AircraftIcao = "A333", GeneratedUtc = generated },
            new[] { "A333" },
            generated.AddHours(1));
        var mismatch = SimBriefImportValidator.Validate(
            new ImportedFlightPlan { AircraftIcao = "A339", GeneratedUtc = generated },
            new[] { "A333" },
            generated.AddHours(1));

        Assert.AreEqual(0, matching.Count);
        Assert.AreEqual(1, mismatch.Count);
        StringAssert.Contains(mismatch[0], "A339");
    }

    [TestMethod]
    public void Validate_WarnsForStaleAndMismatchedAircraft()
    {
        var now = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
        var warnings = SimBriefImportValidator.Validate(
            new ImportedFlightPlan
            {
                AircraftIcao = "B738",
                GeneratedUtc = now.AddHours(-30)
            },
            new[] { "A20N" },
            now);

        Assert.AreEqual(2, warnings.Count);
        StringAssert.Contains(warnings[0], "30 hours old");
        StringAssert.Contains(warnings[1], "does not match");
    }

    [TestMethod]
    public void Validate_AcceptsFreshMatchingAircraft()
    {
        var now = DateTime.UtcNow;
        var warnings = SimBriefImportValidator.Validate(
            new ImportedFlightPlan { AircraftIcao = "A20N", GeneratedUtc = now },
            new[] { "A20N" },
            now);

        Assert.AreEqual(0, warnings.Count);
    }
}
