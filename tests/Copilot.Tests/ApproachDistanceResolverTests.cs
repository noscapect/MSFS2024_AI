using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Telemetry;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class ApproachDistanceResolverTests
{
    [TestMethod]
    public void AtcRunwayDistanceHasPriority()
    {
        var result = ApproachDistanceResolver.Resolve(
            true, 10 * 1852, true, 6, true, 7);

        Assert.IsTrue(result.DistanceNm.HasValue);
        Assert.AreEqual(10, result.DistanceNm.Value, 0.01);
        Assert.AreEqual("ATC runway", result.Source);
    }

    [TestMethod]
    public void LocalizerDmeIsAcceptedAsRunwayDistance()
    {
        var result = ApproachDistanceResolver.Resolve(
            false, 0, true, 14.5, false, 1.5);

        Assert.IsTrue(result.DistanceNm.HasValue);
        Assert.AreEqual(14.5, result.DistanceNm.Value, 0.01);
        Assert.AreEqual("NAV1 ILS DME", result.Source);
    }

    [TestMethod]
    public void DmeWithoutLocalizerIsRejectedAsUnrelatedNavigationDistance()
    {
        var result = ApproachDistanceResolver.Resolve(
            false, 0, false, 1.5, false, 4.8);

        Assert.IsNull(result.DistanceNm);
        Assert.AreEqual(string.Empty, result.Source);
    }
}
