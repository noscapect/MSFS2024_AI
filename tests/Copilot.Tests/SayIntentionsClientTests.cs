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
