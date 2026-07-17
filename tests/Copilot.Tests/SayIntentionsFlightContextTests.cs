using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.SayIntentions;

namespace Copilot.Tests;

[TestClass]
public sealed class SayIntentionsFlightContextTests
{
    [TestMethod]
    public void TryParse_ReadsActiveFlightWithoutExposingCredentialInLabels()
    {
        const string json = """
            {
              "flight_details": {
                "api_key": "secret-value",
                "hostname": "https://apipri.sayintentions.ai",
                "callsign": "KLM123",
                "current_airport": "EHAM",
                "current_flight": {
                  "flight_origin": "EHAM",
                  "flight_destination": "EBBR",
                  "assigned_gate": "D52"
                }
              }
            }
            """;

        Assert.IsTrue(SayIntentionsFlightContext.TryParse(json, out var context));
        Assert.IsNotNull(context);
        Assert.AreEqual("KLM123", context.Callsign);
        Assert.AreEqual("EHAM-EBBR", context.RouteLabel);
        Assert.AreEqual("D52", context.AssignedGate);
        Assert.IsFalse(context.RouteLabel.Contains("secret-value"));
    }

    [TestMethod]
    public void TryParse_RejectsEmptyOrCredentialFreePayload()
    {
        Assert.IsFalse(SayIntentionsFlightContext.TryParse("{}", out _));
        Assert.IsFalse(SayIntentionsFlightContext.TryParse(
            "{\"flight_details\":{\"callsign\":\"KLM123\"}}", out _));
    }

    [TestMethod]
    public void TryParse_RejectsUntrustedApiHost()
    {
        const string json = """
            {
              "flight_details": {
                "api_key": "secret-value",
                "hostname": "https://attacker.example",
                "current_flight": {}
              }
            }
            """;

        Assert.IsTrue(SayIntentionsFlightContext.TryParse(json, out var context));
        Assert.AreEqual("apipri.sayintentions.ai", context!.ApiHost.Host);
    }
}
