using System.Globalization;
using System.Reflection;
using System.Text;

namespace Msfs2024Ai.Copilot.Telemetry;

internal sealed class FlightTelemetryStore : IDisposable
{
    private static readonly PropertyInfo[] RecordedProperties =
        typeof(AircraftState)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property =>
                property.CanRead
                && property.CanWrite
                && IsSupported(property.PropertyType))
            .ToArray();

    private readonly string _directory;
    private StreamWriter? _writer;
    private string? _partialPath;
    private DateTime _nextSampleUtc;
    private DateTime? _landedAndStoppedSinceUtc;
    private bool _wasAirborne;

    public FlightTelemetryStore(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MSFS2024_AI",
            "flights");
        Directory.CreateDirectory(_directory);
        RecoverPartialRecordings();
        Prune();
    }

    public IReadOnlyList<string> Recordings =>
        Directory.GetFiles(_directory, "flight-*.csv")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(3)
            .ToArray();

    public void Record(AircraftState state, DateTime utcNow)
    {
        if (_writer == null)
        {
            var powered =
                state.Battery1On
                || state.Battery2On
                || state.ExternalPowerOn
                || state.Engine1Running
                || state.Engine2Running
                || !state.OnGround;
            if (!state.IsSupportedA320 || !powered)
            {
                return;
            }

            StartRecording(utcNow);
        }

        if (utcNow < _nextSampleUtc)
        {
            return;
        }

        _nextSampleUtc = utcNow.AddSeconds(1);
        if (!state.OnGround)
        {
            _wasAirborne = true;
            _landedAndStoppedSinceUtc = null;
        }
        else if (_wasAirborne && state.GroundSpeedKnots <= 1)
        {
            _landedAndStoppedSinceUtc ??= utcNow;
        }
        else
        {
            _landedAndStoppedSinceUtc = null;
        }

        _writer!.WriteLine(Serialize(state, utcNow));
        _writer.Flush();

        if (_landedAndStoppedSinceUtc.HasValue
            && utcNow - _landedAndStoppedSinceUtc.Value >= TimeSpan.FromSeconds(60))
        {
            CompleteRecording();
        }
    }

    public IReadOnlyList<AircraftState> Load(string path)
    {
        var lines = File.ReadLines(path).ToArray();
        if (lines.Length < 2)
        {
            return Array.Empty<AircraftState>();
        }
        var propertyMap = ParseCsv(lines[0])
            .Skip(1)
            .Select(name => RecordedProperties.FirstOrDefault(property =>
                string.Equals(property.Name, name, StringComparison.Ordinal)))
            .ToArray();
        return lines
            .Skip(1)
            .Select(line => Deserialize(line, propertyMap))
            .Where(state => state != null)
            .Cast<AircraftState>()
            .ToArray();
    }

    public void Dispose()
    {
        CompleteRecording();
    }

    private void StartRecording(DateTime utcNow)
    {
        _partialPath = Path.Combine(
            _directory,
            $"flight-{utcNow:yyyyMMdd-HHmmss}.csv.part");
        _writer = new StreamWriter(_partialPath, false, new UTF8Encoding(false));
        _writer.WriteLine(
            string.Join(",", new[] { "Utc" }.Concat(
                RecordedProperties.Select(property => property.Name))));
        _nextSampleUtc = DateTime.MinValue;
        _wasAirborne = false;
        _landedAndStoppedSinceUtc = null;
    }

    private void CompleteRecording()
    {
        if (_writer == null || _partialPath == null)
        {
            return;
        }

        _writer.Dispose();
        _writer = null;
        var finalPath = _partialPath.Substring(
            0,
            _partialPath.Length - ".part".Length);
        if (File.Exists(finalPath))
        {
            File.Delete(finalPath);
        }
        File.Move(_partialPath, finalPath);
        _partialPath = null;
        Prune();
    }

    private void RecoverPartialRecordings()
    {
        foreach (var partial in Directory.GetFiles(_directory, "*.csv.part"))
        {
            var finalPath = partial.Substring(
                0,
                partial.Length - ".part".Length);
            if (!File.Exists(finalPath))
            {
                File.Move(partial, finalPath);
            }
            else
            {
                File.Delete(partial);
            }
        }
    }

    private void Prune()
    {
        foreach (var oldFile in Directory.GetFiles(_directory, "flight-*.csv")
                     .OrderByDescending(File.GetLastWriteTimeUtc)
                     .Skip(3))
        {
            File.Delete(oldFile);
        }
    }

    private static string Serialize(AircraftState state, DateTime utcNow)
    {
        var values = new List<string> { utcNow.ToString("O", CultureInfo.InvariantCulture) };
        values.AddRange(RecordedProperties.Select(property =>
            Escape(FormatValue(property.GetValue(state)))));
        return string.Join(",", values);
    }

    private static AircraftState? Deserialize(
        string line,
        IReadOnlyList<PropertyInfo?> propertyMap)
    {
        var values = ParseCsv(line);
        if (values.Count != propertyMap.Count + 1)
        {
            return null;
        }

        var state = new AircraftState();
        for (var index = 0; index < propertyMap.Count; index++)
        {
            var property = propertyMap[index];
            if (property == null)
            {
                continue;
            }
            property.SetValue(state, ParseValue(values[index + 1], property.PropertyType));
        }
        return state;
    }

    private static bool IsSupported(Type type)
    {
        var actual = Nullable.GetUnderlyingType(type) ?? type;
        return actual == typeof(string)
               || actual == typeof(bool)
               || actual == typeof(int)
               || actual == typeof(double);
    }

    private static string FormatValue(object? value) =>
        value switch
        {
            null => "",
            bool boolean => boolean ? "1" : "0",
            IFormattable formattable =>
                formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? ""
        };

    private static object? ParseValue(string value, Type type)
    {
        var nullable = Nullable.GetUnderlyingType(type);
        if (nullable != null && string.IsNullOrEmpty(value))
        {
            return null;
        }

        var actual = nullable ?? type;
        if (actual == typeof(string))
        {
            return value;
        }
        if (actual == typeof(bool))
        {
            return value == "1"
                   || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
        if (actual == typeof(int))
        {
            return int.Parse(value, CultureInfo.InvariantCulture);
        }
        return double.Parse(value, CultureInfo.InvariantCulture);
    }

    private static string Escape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"'))
        {
            return value;
        }
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static IReadOnlyList<string> ParseCsv(string line)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                values.Add(value.ToString());
                value.Clear();
            }
            else
            {
                value.Append(character);
            }
        }
        values.Add(value.ToString());
        return values;
    }
}
