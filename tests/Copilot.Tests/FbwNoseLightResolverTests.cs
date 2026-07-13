using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Telemetry;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class FbwNoseLightResolverTests
{
    [TestMethod]
    public void OffSelectorWinsOverStaleGenericTaxiLight()
    {
        var position = FbwStateResolvers.ResolveNoseLightSelectorPosition(
            selectorPosition: 2,
            commandedValue: 2,
            commandedUtc: DateTime.UtcNow,
            takeoffCircuitOn: 0,
            taxiCircuitOn: 0,
            taxiLightOn: 1);

        Assert.AreEqual(2, position);
    }

    [DataTestMethod]
    [DataRow(0d)]
    [DataRow(1d)]
    [DataRow(2d)]
    public void UsesAuthoritativeFbwSelectorForEveryPosition(double selectorPosition)
    {
        var position = FbwStateResolvers.ResolveNoseLightSelectorPosition(
            selectorPosition,
            commandedValue: null,
            commandedUtc: null,
            takeoffCircuitOn: 0,
            taxiCircuitOn: 0,
            taxiLightOn: 0);

        Assert.AreEqual(selectorPosition, position);
    }
}
