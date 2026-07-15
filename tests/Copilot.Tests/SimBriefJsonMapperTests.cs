using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.SimBrief;

namespace Copilot.Tests;

[TestClass]
public sealed class SimBriefJsonMapperTests
{
    private const string OfpJson = """
    {
      "params": { "time_generated": "1752500000", "units": "kgs", "airac": "2507" },
      "general": {
        "icao_airline": "KLM", "flight_number": "1234", "route": "NORKU N873 BETUS",
        "initial_altitude": "35000", "costindex": "18"
      },
      "aircraft": { "icaocode": "A20N", "reg": "PH-ABC" },
      "origin": { "icao_code": "EHAM", "plan_rwy": "24", "trans_alt": "3000" },
      "destination": { "icao_code": "EBBR", "plan_rwy": "25L" },
      "alternate": { "icao_code": "EHEH" },
      "fuel": { "plan_ramp": "8123" },
      "tlr": { "takeoff": { "speeds_v1": "137", "speeds_vr": "139", "speeds_v2": "143", "flap_setting": "1+F" } }
    }
    """;

    [TestMethod]
    public void Parse_NormalizesUsefulDispatchAndTlrValues()
    {
        var imported = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
        var plan = SimBriefJsonMapper.Parse(OfpJson, imported);

        Assert.AreEqual("KLM1234", plan.FlightNumber);
        Assert.AreEqual("A20N", plan.AircraftIcao);
        Assert.AreEqual("EHAM", plan.OriginIcao);
        Assert.AreEqual("EBBR", plan.DestinationIcao);
        Assert.AreEqual(35000, plan.CruiseAltitudeFeet);
        Assert.AreEqual(3000, plan.TransitionAltitudeFeet);
        Assert.AreEqual(137, plan.TakeoffV1Knots);
        Assert.AreEqual(139, plan.TakeoffVrKnots);
        Assert.AreEqual(143, plan.TakeoffV2Knots);
        Assert.AreEqual("1+F", plan.TakeoffFlaps);
        Assert.AreEqual(8123d, plan.BlockFuel);
        Assert.AreEqual(imported, plan.ImportedUtc);
    }

    [TestMethod]
    public void Parse_MissingOptionalFieldsRemainEmpty()
    {
        var plan = SimBriefJsonMapper.Parse(
            "{\"params\":{},\"general\":{},\"aircraft\":{},\"origin\":{},\"destination\":{}}",
            DateTime.UtcNow);

        Assert.IsNull(plan.TakeoffV1Knots);
        Assert.IsNull(plan.TransitionAltitudeFeet);
        Assert.AreEqual("", plan.AlternateIcao);
    }
}
