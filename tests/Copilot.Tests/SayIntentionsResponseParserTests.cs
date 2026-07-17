using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.SayIntentions;

namespace Copilot.Tests;

[TestClass]
public sealed class SayIntentionsResponseParserTests
{
    [TestMethod]
    public void ParseCurrentFrequencies_ReadsActiveAirportStations()
    {
        const string json = """
            {"airport":"EGSH","frequencies":[
              {"station":"TWR","freq":"124.255","long_station":"Tower"}
            ]}
            """;

        var result = SayIntentionsResponseParser.ParseCurrentFrequencies(json);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("EGSH", result[0].Airport);
        Assert.AreEqual("TWR", result[0].Type);
        Assert.AreEqual("124.255", result[0].Frequency);
    }

    [TestMethod]
    public void FrequencySelector_UsesClearanceGroundThenTowerFallback()
    {
        var frequencies = new[]
        {
            new SayIntentionsFrequency { Type = "TWR", Frequency = "124.255" },
            new SayIntentionsFrequency { Type = "GND", Frequency = "121.900" },
            new SayIntentionsFrequency { Type = "CLR", Frequency = "121.980" }
        };

        Assert.AreEqual("CLR", SayIntentionsFrequencySelector.SelectForStep(
            "captain-ifr-clearance", frequencies)!.Type);
        Assert.AreEqual("GND", SayIntentionsFrequencySelector.SelectForStep(
            "captain-pushback-clearance", frequencies)!.Type);
        Assert.AreEqual("TWR", SayIntentionsFrequencySelector.SelectForStep(
            "captain-ifr-clearance", frequencies.Take(1))!.Type);
    }

    [TestMethod]
    public void CommunicationMatcher_RejectsEchoedOutgoingRequest()
    {
        const string request = "KLM1701, request IFR clearance";
        Assert.IsFalse(SayIntentionsCommunicationMatcher.IsGenuineReply(
            new SayIntentionsCommunication
            {
                OutgoingMessage = request,
                IncomingMessage = request
            },
            request));
        Assert.IsTrue(SayIntentionsCommunicationMatcher.IsGenuineReply(
            new SayIntentionsCommunication
            {
                OutgoingMessage = request,
                IncomingMessage = "KLM1701, cleared to EHAM"
            },
            request));
    }

    [TestMethod]
    public void ParseWeather_ReadsOperationalBriefingAndFrequencies()
    {
        const string json = """
            {
              "airports": [{
                "airport": "EHAM", "atis": "Information Alpha",
                "metar": "EHAM METAR", "taf": "EHAM TAF",
                "active_runway": "18R", "wind_direction": 190, "wind_speed": 12
              }],
              "comms": [{
                "type": "GROUND", "freq": "121.900",
                "callsign": "Schiphol Ground", "airport": "EHAM"
              }]
            }
            """;

        var result = SayIntentionsResponseParser.ParseWeather(json);

        Assert.AreEqual(1, result.Airports.Count);
        Assert.AreEqual("18R", result.Airports[0].ActiveRunway);
        Assert.AreEqual(190, result.Airports[0].WindDirection);
        Assert.AreEqual("121.900", result.Frequencies[0].Frequency);
    }

    [TestMethod]
    public void ParseCommunications_ReadsBothDirections()
    {
        const string json = """
            {"comm_history":[{"id":4721,
              "stamp_zulu":"2026-07-17T12:00:00Z", "station_name":"Ground",
              "channel":"COM1", "outgoing_message":"Request clearance",
              "incoming_message":"Cleared to EHAM"
            }]}
            """;

        var result = SayIntentionsResponseParser.ParseCommunications(json);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(4721, result[0].Id);
        Assert.AreEqual("Request clearance", result[0].OutgoingMessage);
        Assert.AreEqual("Cleared to EHAM", result[0].IncomingMessage);
    }

    [TestMethod]
    public void ParseParking_ReadsAssignedGate()
    {
        var result = SayIntentionsResponseParser.ParseParking(
            "{\"parking\":{\"name\":\"Gate D52\",\"lat\":52.3,\"lon\":4.7,\"heading\":180}}");

        Assert.IsNotNull(result);
        Assert.AreEqual("Gate D52", result.Name);
        Assert.AreEqual(180d, result.Heading);
    }
}
