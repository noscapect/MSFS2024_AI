using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Telemetry;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class FbwFlapsResolverTests
{
    [TestMethod]
    public void NativeHandleRemainsAuthoritativeWhenGenericTelemetryIsStale()
    {
        Assert.AreEqual(
            0d,
            FbwStateResolvers.ResolveFlapsHandleIndex(
                nativeHandleIndex: 0,
                genericHandleIndex: 4));
    }

    [TestMethod]
    public void GenericHandleIsUsedUntilNativeTelemetryArrives()
    {
        Assert.AreEqual(
            2d,
            FbwStateResolvers.ResolveFlapsHandleIndex(
                nativeHandleIndex: null,
                genericHandleIndex: 2));
    }
}
