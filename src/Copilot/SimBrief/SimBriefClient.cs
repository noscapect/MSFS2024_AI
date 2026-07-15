using System.Net;
using System.Net.Http;

namespace Msfs2024Ai.Copilot.SimBrief;

internal sealed class SimBriefClient
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public async Task<ImportedFlightPlan> FetchLatestAsync(
        string pilotId,
        string username,
        CancellationToken cancellationToken = default)
    {
        var query = !string.IsNullOrWhiteSpace(pilotId)
            ? $"userid={Uri.EscapeDataString(pilotId.Trim())}"
            : !string.IsNullOrWhiteSpace(username)
                ? $"username={Uri.EscapeDataString(username.Trim())}"
                : throw new InvalidOperationException("Enter a SimBrief Pilot ID or username first.");
        var url = $"https://www.simbrief.com/api/xml.fetcher.php?{query}&json=1";
        using var response = await HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"SimBrief returned HTTP {(int)response.StatusCode}.");
        }
        return SimBriefJsonMapper.Parse(json, DateTime.UtcNow);
    }

    private static HttpClient CreateHttpClient()
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MSFS2024-Virtual-First-Officer/0.8");
        return client;
    }
}
