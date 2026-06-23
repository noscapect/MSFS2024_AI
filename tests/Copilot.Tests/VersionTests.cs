using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class VersionTests
{
    [TestMethod]
    public void ApplicationHasPublishableSemanticVersion()
    {
        var version = typeof(AircraftState).Assembly.GetName().Version;

        Assert.IsNotNull(version);
        Assert.IsTrue(version!.Major > 0 || version.Minor > 0);
    }
}
