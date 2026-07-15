using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.AircraftAdapters;
using Msfs2024Ai.Copilot.SimBrief;

namespace Copilot.Tests;

[TestClass]
public sealed class SimBriefOperationalContextTests
{
    [TestMethod]
    public void BlockFuel_ConvertsPoundsToKilograms()
    {
        var plan = new ImportedFlightPlan { BlockFuel = 22046.2262185, Units = "lbs" };
        Assert.AreEqual(10000, SimBriefOperationalContext.BlockFuelKilograms(plan)!.Value, 0.01);
    }

    [TestMethod]
    public void TakeoffFlaps_NormalizesPerAircraftFamily()
    {
        Assert.AreEqual(5, SimBriefOperationalContext.TakeoffFlapSetting(
            new ImportedFlightPlan { TakeoffFlaps = "Flaps 5" }, AircraftVariant.Pmdg737800));
        Assert.AreEqual(1, SimBriefOperationalContext.TakeoffFlapSetting(
            new ImportedFlightPlan { TakeoffFlaps = "1+F" }, AircraftVariant.IniBuildsA320NeoV2));
    }

    [TestMethod]
    public void TakeoffComparison_ReportsCockpitDifferencesWithoutBlocking()
    {
        var plan = new ImportedFlightPlan { TakeoffV1Knots = 137, TakeoffVrKnots = 139, TakeoffFlaps = "5" };
        Assert.AreEqual("SimBrief/FMC match", SimBriefOperationalContext.TakeoffComparison(
            plan, AircraftVariant.Pmdg737800, 137, 139, 5));
        StringAssert.Contains(SimBriefOperationalContext.TakeoffComparison(
            plan, AircraftVariant.Pmdg737800, 135, 139, 5), "V1 137/135");
    }
}
