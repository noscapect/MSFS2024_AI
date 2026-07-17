using System.Collections;
using System.Web.Script.Serialization;

namespace Msfs2024Ai.Copilot.SayIntentions;

internal sealed class SayIntentionsFlightContext
{
    private SayIntentionsFlightContext(
        string apiKey,
        Uri apiHost,
        string callsign,
        string originIcao,
        string destinationIcao,
        string assignedGate,
        string currentAirport)
    {
        ApiKey = apiKey;
        ApiHost = apiHost;
        Callsign = callsign;
        OriginIcao = originIcao;
        DestinationIcao = destinationIcao;
        AssignedGate = assignedGate;
        CurrentAirport = currentAirport;
    }

    internal string ApiKey { get; }
    internal Uri ApiHost { get; }
    public string Callsign { get; }
    public string OriginIcao { get; }
    public string DestinationIcao { get; }
    public string AssignedGate { get; }
    public string CurrentAirport { get; }

    public string RouteLabel =>
        !string.IsNullOrWhiteSpace(OriginIcao) && !string.IsNullOrWhiteSpace(DestinationIcao)
            ? $"{OriginIcao}-{DestinationIcao}"
            : "active flight";

    public static bool TryParse(string? json, out SayIntentionsFlightContext? context)
    {
        context = null;
        if (string.IsNullOrWhiteSpace(json) || json!.Trim() == "{}")
        {
            return false;
        }

        try
        {
            var root = new JavaScriptSerializer().DeserializeObject(json) as IDictionary<string, object>;
            var details = Dictionary(root, "flight_details");
            var apiKey = Text(details, "api_key");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return false;
            }

            var currentFlight = Dictionary(details, "current_flight");
            context = new SayIntentionsFlightContext(
                apiKey,
                SafeApiHost(Text(details, "hostname")),
                Text(details, "callsign_icao", Text(details, "callsign")),
                Text(currentFlight, "flight_origin"),
                Text(currentFlight, "flight_destination"),
                Text(currentFlight, "assigned_gate"),
                Text(details, "current_airport"));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static IDictionary<string, object>? Dictionary(
        IDictionary<string, object>? source,
        string key) =>
        source != null && source.TryGetValue(key, out var value)
            ? value as IDictionary<string, object>
            : null;

    private static string Text(
        IDictionary<string, object>? source,
        string key,
        string fallback = "") =>
        source != null && source.TryGetValue(key, out var value) && value != null
            ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? fallback
            : fallback;

    private static Uri SafeApiHost(string candidate)
    {
        var fallback = new Uri("https://apipri.sayintentions.ai/");
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var host)
            || host.Scheme != Uri.UriSchemeHttps
            || !(host.Host.Equals("sayintentions.ai", StringComparison.OrdinalIgnoreCase)
                 || host.Host.EndsWith(".sayintentions.ai", StringComparison.OrdinalIgnoreCase)))
        {
            return fallback;
        }

        return new Uri(host.GetLeftPart(UriPartial.Authority) + "/");
    }
}
