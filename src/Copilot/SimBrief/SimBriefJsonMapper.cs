using System.Collections;
using System.Globalization;
using System.Web.Script.Serialization;

namespace Msfs2024Ai.Copilot.SimBrief;

internal static class SimBriefJsonMapper
{
    public static ImportedFlightPlan Parse(string json, DateTime importedUtc)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("SimBrief returned an empty response.");
        }

        var root = new JavaScriptSerializer
        {
            MaxJsonLength = 8 * 1024 * 1024,
            RecursionLimit = 256
        }.DeserializeObject(json) as IDictionary<string, object>
            ?? throw new InvalidDataException("SimBrief returned invalid JSON.");

        if (Text(root, "fetch", "status").Equals("Error", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                Text(root, "fetch", "message") is { Length: > 0 } message
                    ? message
                    : "SimBrief could not find a generated flight plan.");
        }

        var result = new ImportedFlightPlan
        {
            ImportedUtc = importedUtc,
            GeneratedUtc = UnixDate(root, "params", "time_generated"),
            Airac = Text(root, "params", "airac"),
            Units = Text(root, "params", "units"),
            FlightNumber = CombineFlightNumber(
                Text(root, "general", "icao_airline"),
                Text(root, "general", "flight_number")),
            AircraftIcao = Text(root, "aircraft", "icaocode").ToUpperInvariant(),
            AircraftRegistration = Text(root, "aircraft", "reg"),
            OriginIcao = Text(root, "origin", "icao_code").ToUpperInvariant(),
            DestinationIcao = Text(root, "destination", "icao_code").ToUpperInvariant(),
            AlternateIcao = Text(root, "alternate", "icao_code").ToUpperInvariant(),
            OriginRunway = Text(root, "origin", "plan_rwy").ToUpperInvariant(),
            DestinationRunway = Text(root, "destination", "plan_rwy").ToUpperInvariant(),
            Route = FirstText(root,
                new[] { "general", "route_ifps" },
                new[] { "general", "route" }),
            CruiseAltitudeFeet = Integer(root, "general", "initial_altitude"),
            CostIndex = Integer(root, "general", "costindex"),
            TransitionAltitudeFeet = Integer(root, "origin", "trans_alt"),
            BlockFuel = Number(root, "fuel", "plan_ramp")
                        ?? Number(root, "fuel", "plan_block")
        };

        result.TakeoffV1Knots = FindInteger(root, "speeds_v1", "v1");
        result.TakeoffVrKnots = FindInteger(root, "speeds_vr", "vr");
        result.TakeoffV2Knots = FindInteger(root, "speeds_v2", "v2");
        result.TakeoffFlaps = FindText(root, "flap_setting", "flaps");
        return result;
    }

    private static string CombineFlightNumber(string airline, string number) =>
        string.IsNullOrWhiteSpace(airline) ? number : $"{airline}{number}";

    private static string FirstText(
        IDictionary<string, object> root,
        params string[][] paths)
    {
        foreach (var path in paths)
        {
            var value = Text(root, path);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return "";
    }

    private static string Text(IDictionary<string, object> root, params string[] path) =>
        Value(root, path)?.ToString()?.Trim() ?? "";

    private static int? Integer(IDictionary<string, object> root, params string[] path)
    {
        var value = Text(root, path).Replace(",", "");
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static double? Number(IDictionary<string, object> root, params string[] path)
    {
        var value = Text(root, path).Replace(",", "");
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static DateTime? UnixDate(IDictionary<string, object> root, params string[] path)
    {
        var value = Text(root, path);
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : null;
    }

    private static object? Value(object current, IReadOnlyList<string> path)
    {
        foreach (var segment in path)
        {
            if (current is not IDictionary<string, object> dictionary
                || !dictionary.TryGetValue(segment, out current!))
            {
                return null;
            }
        }
        return current;
    }

    private static int? FindInteger(object node, params string[] names)
    {
        var text = FindText(node, names);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            && result is >= 50 and <= 300
                ? result
                : null;
    }

    private static string FindText(object? node, params string[] names)
    {
        if (node is IDictionary<string, object> dictionary)
        {
            foreach (var name in names)
            {
                var pair = dictionary.FirstOrDefault(item =>
                    item.Key.Equals(name, StringComparison.OrdinalIgnoreCase));
                var direct = pair.Value?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(direct)
                    && pair.Value is not IDictionary<string, object>
                    && pair.Value is not IEnumerable<object>)
                {
                    return direct!;
                }
            }
            foreach (var value in dictionary.Values)
            {
                var nested = FindText(value, names);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
        }
        else if (node is IEnumerable enumerable && node is not string)
        {
            foreach (var item in enumerable)
            {
                var nested = FindText(item, names);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
        }
        return "";
    }
}
