using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Procedures;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class ProcedureCatalogTests
{
    [TestMethod]
    public void Pmdg737UsesBoeingProcedureCatalog()
    {
        var state = new AircraftState { Title = "PMDG 737-800" };

        var flows = ProcedureCatalog.ForAircraft(state);

        Assert.AreEqual("1. 737 Power Up & Initial Setup", flows[0].Name);
        Assert.AreEqual(B737ProcedureLibrary.GateToGate.Count, flows.Count);
    }

    [TestMethod]
    public void Pmdg737PackageTitleUsesBoeingProcedureCatalog()
    {
        var state = new AircraftState { Title = "737-800 PAX BW TC" };

        var flows = ProcedureCatalog.ForAircraft(state);

        Assert.IsTrue(state.IsSupportedBoeing737);
        Assert.AreEqual("1. 737 Power Up & Initial Setup", flows[0].Name);
    }

    [TestMethod]
    public void AirbusStillUsesA320ProcedureCatalog()
    {
        var state = new AircraftState { Title = "A320neo V2" };

        var flows = ProcedureCatalog.ForAircraft(state);

        Assert.AreEqual("1. Power Up & Initial Setup", flows[0].Name);
        Assert.AreEqual(A320ProcedureLibrary.GateToGate.Count, flows.Count);
    }
}
