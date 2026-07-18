using System.Net.Http;

namespace Msfs2024Ai.Copilot.SayIntentions;

internal sealed class SayIntentionsClient : IDisposable
{
    private static readonly Uri LocalFlightEndpoint =
        new("http://127.0.0.1:63287/flightJSON");

    private readonly HttpClient _localClient;
    private readonly HttpClient _apiClient;

    public SayIntentionsClient()
        : this(new HttpClientHandler(), new HttpClientHandler())
    {
    }

    internal SayIntentionsClient(
        HttpMessageHandler localHandler,
        HttpMessageHandler apiHandler)
    {
        _localClient = new HttpClient(localHandler) { Timeout = TimeSpan.FromSeconds(2) };
        _apiClient = new HttpClient(apiHandler) { Timeout = TimeSpan.FromSeconds(6) };
        _apiClient.DefaultRequestHeaders.UserAgent.ParseAdd("MSFS2024-Virtual-First-Officer/0.9.2");
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

    public async Task<bool> SendAtcTransmissionAsync(
        SayIntentionsFlightContext context,
        string message,
        int com = 1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(message));
        }
        if (com is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(com));
        }

        var json = await GetSapiJsonAsync(
                context,
                "sayAs",
                new Dictionary<string, string>
                {
                    ["channel"] = $"COM{com}",
                    ["message"] = message
                },
                cancellationToken)
            .ConfigureAwait(false);
        return !ContainsError(json);
    }

    public async Task<SayIntentionsWeatherResult> GetWeatherAsync(
        SayIntentionsFlightContext context,
        CancellationToken cancellationToken = default)
    {
        var airports = new[] { context.OriginIcao, context.DestinationIcao }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (airports.Length == 0)
        {
            return new SayIntentionsWeatherResult();
        }

        var json = await GetSapiJsonAsync(
                context,
                "getWX",
                new Dictionary<string, string>
                {
                    ["icao"] = string.Join(",", airports),
                    ["with_comms"] = "1"
                },
                cancellationToken)
            .ConfigureAwait(false);
        return SayIntentionsResponseParser.ParseWeather(json);
    }

    public async Task<IReadOnlyList<SayIntentionsCommunication>> GetCommunicationsAsync(
        SayIntentionsFlightContext context,
        CancellationToken cancellationToken = default)
    {
        var json = await GetSapiJsonAsync(
                context,
                "getCommsHistory",
                null,
                cancellationToken)
            .ConfigureAwait(false);
        return SayIntentionsResponseParser.ParseCommunications(json);
    }

    public async Task<IReadOnlyList<SayIntentionsFrequency>> GetCurrentFrequenciesAsync(
        SayIntentionsFlightContext context,
        CancellationToken cancellationToken = default)
    {
        var json = await GetSapiJsonAsync(
                context,
                "getCurrentFrequencies",
                null,
                cancellationToken)
            .ConfigureAwait(false);
        return SayIntentionsResponseParser.ParseCurrentFrequencies(json);
    }

    public async Task<bool> SetCopilotCommunicationsAsync(
        SayIntentionsFlightContext context,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var json = await GetSapiJsonAsync(
                context,
                "setVar",
                new Dictionary<string, string>
                {
                    ["var"] = "SIAI_COPILOT",
                    ["value"] = enabled ? "1" : "0",
                    ["category"] = "L"
                },
                cancellationToken)
            .ConfigureAwait(false);
        return !ContainsError(json);
    }

    public async Task<SayIntentionsParking?> GetParkingAsync(
        SayIntentionsFlightContext context,
        CancellationToken cancellationToken = default)
    {
        var json = await GetSapiJsonAsync(
                context,
                "getParking",
                null,
                cancellationToken)
            .ConfigureAwait(false);
        return SayIntentionsResponseParser.ParseParking(json);
    }

    private async Task<string> GetSapiJsonAsync(
        SayIntentionsFlightContext context,
        string endpointName,
        IReadOnlyDictionary<string, string>? parameters,
        CancellationToken cancellationToken)
    {
        var query = new List<string>
        {
            "api_key=" + Uri.EscapeDataString(context.ApiKey)
        };
        if (parameters != null)
        {
            query.AddRange(parameters.Select(
                pair => Uri.EscapeDataString(pair.Key)
                        + "=" + Uri.EscapeDataString(pair.Value)));
        }

        var uri = new UriBuilder(new Uri(context.ApiHost, "sapi/" + endpointName))
        {
            Query = string.Join("&", query)
        }.Uri;
        using var response = await _apiClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"SayIntentions returned HTTP {(int)response.StatusCode}.");
        }

        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    private static bool ContainsError(string json) =>
        !string.IsNullOrWhiteSpace(json)
        && json.IndexOf("\"error\"", StringComparison.OrdinalIgnoreCase) >= 0;

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
