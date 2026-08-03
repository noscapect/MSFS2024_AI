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
      "aircraft": { "icaocode": "A20N", "reg": "PH-ABC", "max_passengers": "180" },
      "origin": { "icao_code": "EHAM", "plan_rwy": "24", "trans_alt": "3000" },
      "destination": { "icao_code": "EBBR", "plan_rwy": "25L" },
      "alternate": { "icao_code": "EHEH" },
      "fuel": { "taxi": "250", "plan_takeoff": "7873", "plan_ramp": "8123", "plan_landing": "4200" },
      "weights": {
        "pax_count": "166", "pax_count_actual": "164",
        "bag_count": "166", "bag_count_actual": "160",
        "pax_weight": "84", "bag_weight": "20", "freight_added": "600",
        "cargo": "3800", "payload": "17576", "oew": "42100",
        "est_zfw": "59676", "est_ramp": "67799", "est_tow": "67549", "est_ldw": "63876"
      },
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
        Assert.AreEqual(250d, plan.TaxiFuel);
        Assert.AreEqual(7873d, plan.TakeoffFuel);
        Assert.AreEqual(4200d, plan.LandingFuel);
        Assert.AreEqual(164, plan.PassengerCount);
        Assert.AreEqual(180, plan.MaximumPassengerCount);
        Assert.AreEqual(160, plan.BaggageCount);
        Assert.AreEqual(84d, plan.PassengerWeight);
        Assert.AreEqual(20d, plan.BaggageWeight);
        Assert.AreEqual(600d, plan.FreightWeight);
        Assert.AreEqual(3800d, plan.CargoWeight);
        Assert.AreEqual(17576d, plan.PayloadWeight);
        Assert.AreEqual(42100d, plan.OperatingEmptyWeight);
        Assert.AreEqual(59676d, plan.ZeroFuelWeight);
        Assert.AreEqual(67799d, plan.RampWeight);
        Assert.AreEqual(67549d, plan.TakeoffWeight);
        Assert.AreEqual(63876d, plan.LandingWeight);
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
