using System.Net.Http;

namespace Msfs2024Ai.Copilot.SayIntentions;

internal sealed class SayIntentionsClient : IDisposable
{
    private static readonly Uri LocalFlightEndpoint =
        new("http://127.0.0.1:63287/flightJSON");

    private readonly HttpClient _localClient;
    private readonly HttpClient _apiClient;

    public SayIntentionsClient()
    {
        _localClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        _apiClient = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        _apiClient.DefaultRequestHeaders.UserAgent.ParseAdd("MSFS2024-Virtual-First-Officer/0.8.1");
    }

    public async Task<SayIntentionsDiscoveryResult> DiscoverAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _localClient
                .GetAsync(LocalFlightEndpoint, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return SayIntentionsDiscoveryResult.ClientUnavailable();
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return SayIntentionsFlightContext.TryParse(json, out var context)
                ? SayIntentionsDiscoveryResult.Connected(context!)
                : SayIntentionsDiscoveryResult.NoActiveFlight();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SayIntentionsDiscoveryResult.ClientUnavailable();
        }
        catch (HttpRequestException)
        {
            return SayIntentionsDiscoveryResult.ClientUnavailable();
        }
    }

    public async Task<bool> SayCopilotCalloutAsync(
        SayIntentionsFlightContext context,
        string phrase,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phrase) || phrase.Length > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(phrase));
        }

        var endpoint = new Uri(context.ApiHost, "sapi/sayAs");
        var uri = new UriBuilder(endpoint)
        {
            Query = "api_key=" + Uri.EscapeDataString(context.ApiKey)
                    + "&channel=INTERCOM1_IN"
                    + "&message=" + Uri.EscapeDataString(phrase)
                    + "&rephrase=0"
        }.Uri;

        using var response = await _apiClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public void Dispose()
    {
        _localClient.Dispose();
        _apiClient.Dispose();
    }
}

internal enum SayIntentionsConnectionState
{
    ClientUnavailable,
    NoActiveFlight,
    Connected
}

internal sealed class SayIntentionsDiscoveryResult
{
    private SayIntentionsDiscoveryResult(
        SayIntentionsConnectionState state,
        SayIntentionsFlightContext? context)
    {
        State = state;
        Context = context;
    }

    public SayIntentionsConnectionState State { get; }
    public SayIntentionsFlightContext? Context { get; }

    public static SayIntentionsDiscoveryResult ClientUnavailable() =>
        new(SayIntentionsConnectionState.ClientUnavailable, null);

    public static SayIntentionsDiscoveryResult NoActiveFlight() =>
        new(SayIntentionsConnectionState.NoActiveFlight, null);

    public static SayIntentionsDiscoveryResult Connected(SayIntentionsFlightContext context) =>
        new(SayIntentionsConnectionState.Connected, context);
}
