using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.SimBrief;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class SimBriefNavigationSummaryTests
{
    [TestMethod]
    public void FormatsStructuredOfpNavigationWithoutClaimingExternalValidation()
    {
        var plan = new ImportedFlightPlan
        {
            Airac = "2608",
            OriginIcao = "EHAM",
            OriginRunway = "24",
            SidIdentifier = "NORK2S",
            SidTransition = "NORKU",
            TransitionAltitudeFeet = 3000,
            OriginTransitionLevelFeet = 4500,
            DestinationIcao = "EGLL",
            DestinationRunway = "27R",
            StarIdentifier = "LOGA2H",
            StarTransition = "LOGAN",
            DestinationTransitionAltitudeFeet = 6000,
            DestinationTransitionLevelFeet = 7000,
            NavigraphRoute = "NORKU N873 LOGAN",
            Route = "fallback route",
            Navlog = new List<ImportedFlightPlanFix>
            {
                new(),
                new()
            }
        };

        Assert.AreEqual("OFP AIRAC 2608", SimBriefNavigationSummary.Airac(plan));
        Assert.AreEqual(
            $"EHAM runway 24 | SID NORK2S via NORKU | " +
            $"TA {3000:N0} ft / TL {4500:N0} ft",
            SimBriefNavigationSummary.Departure(plan));
        Assert.AreEqual(
            $"EGLL runway 27R | STAR LOGA2H via LOGAN | " +
            $"TA {6000:N0} ft / TL {7000:N0} ft",
            SimBriefNavigationSummary.Arrival(plan));
        Assert.AreEqual(
            "NORKU N873 LOGAN",
            SimBriefNavigationSummary.PreferredRoute(plan));
        Assert.AreEqual(
            "Structured navlog: 2 fixes",
            SimBriefNavigationSummary.Navlog(plan));
    }

    [TestMethod]
    public void MissingOptionalNavigationDataHasNeutralFallbacks()
    {
        var plan = new ImportedFlightPlan { Route = "DCT BETUS" };

        Assert.AreEqual(
            "OFP AIRAC unavailable",
            SimBriefNavigationSummary.Airac(plan));
        Assert.AreEqual(
            "-- runway -- | SID -- | TA -- / TL --",
            SimBriefNavigationSummary.Departure(plan));
        Assert.AreEqual(
            "DCT BETUS",
            SimBriefNavigationSummary.PreferredRoute(plan));
        Assert.AreEqual(
            "Structured navlog unavailable",
            SimBriefNavigationSummary.Navlog(plan));
    }
}
