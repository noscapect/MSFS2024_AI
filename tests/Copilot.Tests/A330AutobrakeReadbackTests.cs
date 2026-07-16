using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class A330AutobrakeReadbackTests
{
    [TestMethod]
    public void LowTransitionOverridesPreviouslyLatchedHighButton()
    {
        var readback = new A330AutobrakeReadback();
        readback.Update(0, 0);
        readback.Update(1, 0);
        readback.Update(2, 1);

        Assert.AreEqual(3d, readback.Level);

        readback.Update(0, 1);

        Assert.AreEqual(1d, readback.Level);
    }

    [TestMethod]
    public void StartupWithLowAndStaleHighResolvesToLow()
    {
        var readback = new A330AutobrakeReadback();
        readback.Update(0, 1);
        readback.Update(1, 0);
        readback.Update(2, 1);

        Assert.AreEqual(1d, readback.Level);
    }

    [TestMethod]
    public void ReleasingSelectedLowResolvesOffDespiteStaleHigh()
    {
        var readback = new A330AutobrakeReadback();
        readback.Update(0, 1);
        readback.Update(1, 0);
        readback.Update(2, 1);

        readback.Update(0, 0);

        Assert.AreEqual(0d, readback.Level);
    }
}
