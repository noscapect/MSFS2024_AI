using System.Net;
using System.Net.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.SayIntentions;
using Msfs2024Ai.Copilot.Settings;

namespace Copilot.Tests;

[TestClass]
public sealed class SayIntentionsClientTests
{
    [TestMethod]
    public async Task CopilotHandoffUsesSetVarAndNeverSetFreq()
    {
        var apiHandler = new RecordingHandler();
        using var client = new SayIntentionsClient(
            new RecordingHandler(),
            apiHandler);
        Assert.IsTrue(SayIntentionsFlightContext.TryParse(
            "{\"flight_details\":{\"api_key\":\"secret\","
            + "\"hostname\":\"https://apipri.sayintentions.ai\","
            + "\"flight_id\":\"42\",\"current_flight\":{}}}",
            out var context));

        Assert.IsTrue(await client.SetCopilotCommunicationsAsync(context!, true));

        Assert.IsNotNull(apiHandler.LastRequestUri);
        Assert.AreEqual("/sapi/setVar", apiHandler.LastRequestUri.AbsolutePath);
        StringAssert.Contains(apiHandler.LastRequestUri.Query, "var=SIAI_COPILOT");
        StringAssert.Contains(apiHandler.LastRequestUri.Query, "value=1");
        StringAssert.Contains(apiHandler.LastRequestUri.Query, "category=L");
        Assert.IsFalse(
            apiHandler.LastRequestUri.AbsoluteUri.IndexOf(
                "setFreq",
                StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [TestMethod]
    public async Task OperationalCalloutsUseCopilotVoiceChannel()
    {
        var apiHandler = new RecordingHandler();
        using var client = new SayIntentionsClient(
            new RecordingHandler(),
            apiHandler);
        Assert.IsTrue(SayIntentionsFlightContext.TryParse(
            "{\"flight_details\":{\"api_key\":\"secret\","
            + "\"hostname\":\"https://apipri.sayintentions.ai\","
            + "\"flight_id\":\"42\",\"current_flight\":{}}}",
            out var context));

        Assert.IsTrue(await client.SayCopilotCalloutAsync(context!, "Flaps up"));

        Assert.IsNotNull(apiHandler.LastRequestUri);
        Assert.AreEqual("/sapi/sayAs", apiHandler.LastRequestUri.AbsolutePath);
        StringAssert.Contains(apiHandler.LastRequestUri.Query, "channel=INTERCOM1_IN");
        StringAssert.Contains(apiHandler.LastRequestUri.Query, "message=Flaps%20up");
        StringAssert.Contains(apiHandler.LastRequestUri.Query, "rephrase=0");
    }

    [TestMethod]
    public async Task CopilotAtcInstructionUsesPilotToCopilotIntercomAndNeverTunesRadio()
    {
        var apiHandler = new RecordingHandler();
        using var client = new SayIntentionsClient(
            new RecordingHandler(),
            apiHandler);
        Assert.IsTrue(SayIntentionsFlightContext.TryParse(
            "{\"flight_details\":{\"api_key\":\"secret\","
            + "\"hostname\":\"https://apipri.sayintentions.ai\","
            + "\"flight_id\":\"42\",\"current_flight\":{}}}",
            out var context));

        Assert.IsTrue(await client.AskCopilotAsync(
            context!,
            "Please handle the radios and obtain our IFR clearance now."));

        Assert.IsNotNull(apiHandler.LastRequestUri);
        Assert.AreEqual("/sapi/sayAs", apiHandler.LastRequestUri.AbsolutePath);
        StringAssert.Contains(apiHandler.LastRequestUri.Query, "channel=INTERCOM1");
        StringAssert.Contains(apiHandler.LastRequestUri.Query, "message=Please%20handle");
        Assert.IsFalse(
            apiHandler.LastRequestUri.Query.IndexOf(
                "rephrase=",
                StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.IsFalse(
            apiHandler.LastRequestUri.Query.IndexOf(
                "channel=COM1",
                StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.IsFalse(
            apiHandler.LastRequestUri.AbsoluteUri.IndexOf(
                "setFreq",
                StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [TestMethod]
    public async Task PushbackUsesNativeCopilotCallbackAction()
    {
        var apiHandler = new RecordingHandler();
        using var client = new SayIntentionsClient(
            new RecordingHandler(),
            apiHandler);
        Assert.IsTrue(SayIntentionsFlightContext.TryParse(
            "{\"flight_details\":{\"api_key\":\"secret\","
            + "\"hostname\":\"https://apipri.sayintentions.ai\","
            + "\"flight_id\":\"42\",\"current_flight\":{}}}",
            out var context));

        Assert.IsTrue(await client.TriggerCopilotActionAsync(
            context!,
            "preflight_request_push_and_start"));

        Assert.IsNotNull(apiHandler.LastRequestUri);
        Assert.AreEqual("/sapi/sendCallback", apiHandler.LastRequestUri.AbsolutePath);
        StringAssert.Contains(apiHandler.LastRequestUri.Query, "event=copilot_request");
        StringAssert.Contains(
            apiHandler.LastRequestUri.Query,
            "action_name=preflight_request_push_and_start");
        Assert.IsFalse(
            apiHandler.LastRequestUri.AbsoluteUri.IndexOf(
                "sayAs",
                StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [TestMethod]
    public async Task IfrUsesNativeCopilotCallbackAction()
    {
        var apiHandler = new RecordingHandler();
        using var client = new SayIntentionsClient(
            new RecordingHandler(),
            apiHandler);
        Assert.IsTrue(SayIntentionsFlightContext.TryParse(
            "{\"flight_details\":{\"api_key\":\"secret\","
            + "\"hostname\":\"https://apipri.sayintentions.ai\","
            + "\"flight_id\":\"42\",\"current_flight\":{}}}",
            out var context));

        Assert.IsTrue(await client.TriggerCopilotActionAsync(
            context!,
            "preflight_request_clearance_ifr"));

        Assert.IsNotNull(apiHandler.LastRequestUri);
        Assert.AreEqual("/sapi/sendCallback", apiHandler.LastRequestUri.AbsolutePath);
        StringAssert.Contains(apiHandler.LastRequestUri.Query, "event=copilot_request");
        StringAssert.Contains(
            apiHandler.LastRequestUri.Query,
            "action_name=preflight_request_clearance_ifr");
    }

    [TestMethod]
    public void CopilotCommunicationsAreEnabledByDefault()
    {
        Assert.IsTrue(new CopilotSettings().UseSayIntentionsCopilotCommunications);
    }


    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"OK\"}")
            });
        }
    }
}
