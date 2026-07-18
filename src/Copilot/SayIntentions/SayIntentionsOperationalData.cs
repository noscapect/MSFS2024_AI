using System.Collections;
using System.Web.Script.Serialization;

namespace Msfs2024Ai.Copilot.SayIntentions;

internal sealed class SayIntentionsWeather
{
    public string Airport { get; set; } = "";
    public string Atis { get; set; } = "";
    public string Metar { get; set; } = "";
    public string Taf { get; set; } = "";
    public string ActiveRunway { get; set; } = "";
    public int? WindDirection { get; set; }
    public int? WindSpeed { get; set; }
}

internal sealed class SayIntentionsFrequency
{
    public string Airport { get; set; } = "";
    public string Type { get; set; } = "";
    public string Frequency { get; set; } = "";
    public string Callsign { get; set; } = "";
}

internal sealed class SayIntentionsCommunication
{
    public long Id { get; set; }
    public string TimestampUtc { get; set; } = "";
    public string Station { get; set; } = "";
    public string Channel { get; set; } = "";
    public string Frequency { get; set; } = "";
    public string OutgoingMessage { get; set; } = "";
    public string IncomingMessage { get; set; } = "";
}

internal sealed class SayIntentionsParking
{
    public string Name { get; set; } = "";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? Heading { get; set; }
}

internal sealed class SayIntentionsWeatherResult
{
    public List<SayIntentionsWeather> Airports { get; } = new();
    public List<SayIntentionsFrequency> Frequencies { get; } = new();
}

internal static class SayIntentionsResponseParser
{
    public static IReadOnlyList<SayIntentionsFrequency> ParseCurrentFrequencies(string json)
    {
        var root = ParseRoot(json);
        var airport = Text(root, "airport");
        return Objects(root, "frequencies")
            .Select(item => new SayIntentionsFrequency
            {
                Airport = airport,
                Type = Text(item, "station"),
                Frequency = Text(item, "freq"),
                Callsign = Text(item, "long_station")
            })
            .ToList();
    }

    public static SayIntentionsWeatherResult ParseWeather(string json)
    {
        var result = new SayIntentionsWeatherResult();
        var root = ParseRoot(json);
        foreach (var item in Objects(root, "airports"))
        {
            result.Airports.Add(new SayIntentionsWeather
            {
                Airport = Text(item, "airport"),
                Atis = Text(item, "atis"),
                Metar = Text(item, "metar"),
                Taf = Text(item, "taf"),
                ActiveRunway = Text(item, "active_runway"),
                WindDirection = Integer(item, "wind_direction"),
                WindSpeed = Integer(item, "wind_speed")
            });
        }

        foreach (var item in Objects(root, "comms"))
        {
            result.Frequencies.Add(new SayIntentionsFrequency
            {
                Airport = Text(item, "airport"),
                Type = Text(item, "type"),
                Frequency = Text(item, "freq"),
                Callsign = Text(item, "callsign")
            });
        }

        return result;
    }

    public static IReadOnlyList<SayIntentionsCommunication> ParseCommunications(string json) =>
        Objects(ParseRoot(json), "comm_history")
            .Select(item => new SayIntentionsCommunication
            {
                Id = Long(item, "id") ?? 0,
                TimestampUtc = Text(item, "stamp_zulu"),
                Station = Text(item, "station_name", Text(item, "ident")),
                Channel = Text(item, "channel"),
                Frequency = Text(item, "frequency"),
                OutgoingMessage = Text(item, "outgoing_message"),
                IncomingMessage = Text(item, "incoming_message")
            })
            .ToList();

    public static SayIntentionsParking? ParseParking(string json)
    {
        var parking = Object(ParseRoot(json), "parking");
        if (parking == null)
        {
            return null;
        }

        return new SayIntentionsParking
        {
            Name = Text(parking, "name"),
            Latitude = Number(parking, "lat"),
            Longitude = Number(parking, "lon"),
            Heading = Number(parking, "heading")
        };
    }

    private static IDictionary<string, object> ParseRoot(string json) =>
        new JavaScriptSerializer().DeserializeObject(json) as IDictionary<string, object>
        ?? new Dictionary<string, object>();

    private static IDictionary<string, object>? Object(
        IDictionary<string, object> source,
        string key) =>
        source.TryGetValue(key, out var value)
            ? value as IDictionary<string, object>
            : null;

    private static IEnumerable<IDictionary<string, object>> Objects(
        IDictionary<string, object> source,
        string key)
    {
        if (!source.TryGetValue(key, out var value) || value is not IEnumerable values)
        {
            yield break;
        }

        foreach (var item in values)
        {
            if (item is IDictionary<string, object> dictionary)
            {
                yield return dictionary;
            }
        }
    }

    private static string Text(
        IDictionary<string, object> source,
        string key,
        string fallback = "") =>
        source.TryGetValue(key, out var value) && value != null
            ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? fallback
            : fallback;

    private static int? Integer(IDictionary<string, object> source, string key) =>
        source.TryGetValue(key, out var value)
        && int.TryParse(
            Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
            out var parsed)
            ? parsed
            : null;

    private static double? Number(IDictionary<string, object> source, string key) =>
        source.TryGetValue(key, out var value)
        && double.TryParse(
            Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;

    private static long? Long(IDictionary<string, object> source, string key) =>
        source.TryGetValue(key, out var value)
        && long.TryParse(
            Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
            out var parsed)
            ? parsed
            : null;
}
