using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.AircraftAdapters.Pmdg777;
using Msfs2024Ai.Copilot.Domain;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class Pmdg777ControlProfileTests
{
    [TestMethod]
    public void SdkBoundaryUsesOfficial777XClientDataIdentifiers()
    {
        Assert.AreEqual("PMDG_777X_Data", Pmdg777ControlProfile.DataName);
        Assert.AreEqual(0x504D4447U, Pmdg777ControlProfile.DataId);
        Assert.AreEqual(0x504D4448U, Pmdg777ControlProfile.DataDefinition);
        Assert.AreEqual("PMDG_777X_Control", Pmdg777ControlProfile.ControlName);
        Assert.AreEqual(0x504D4449U, Pmdg777ControlProfile.ControlId);
        Assert.AreEqual(0x504D444AU, Pmdg777ControlProfile.ControlDefinition);
    }

    [TestMethod]
    public void SdkBoundaryCannotFallBackToThe737Ng3Namespace()
    {
        Assert.IsFalse(Pmdg777ControlProfile.DataName.Contains("NG3", StringComparison.Ordinal));
        Assert.IsFalse(Pmdg777ControlProfile.ControlName.Contains("NG3", StringComparison.Ordinal));
        Assert.AreEqual("pmdg-aircraft-77w", Pmdg777ControlProfile.PackageName);
        Assert.AreEqual("777_Options.ini", Pmdg777ControlProfile.OptionsFileName);
    }

    [TestMethod]
    public void OnlyExactIdentityIsEnabledDuringBootstrap()
    {
        Assert.AreEqual(
            CapabilitySupport.Supported,
            Pmdg777ControlProfile.Capabilities.Single(item =>
                item.Id == "aircraft-identity").Support);
        Assert.IsTrue(Pmdg777ControlProfile.Capabilities
            .Where(item => item.Id != "aircraft-identity")
            .All(item => item.Support == CapabilitySupport.NotImplemented));
    }
}
