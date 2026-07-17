using System.Globalization;

namespace Msfs2024Ai.Copilot.SayIntentions;

internal static class SayIntentionsFrequencySelector
{
    public static SayIntentionsFrequency? SelectForStep(
        string stepId,
        IEnumerable<SayIntentionsFrequency> frequencies)
    {
        var priorities = stepId switch
        {
            "captain-ifr-clearance" => new[] { "CLR", "DEL", "CD", "GND", "TWR", "CTAF" },
            "captain-pushback-clearance" => new[] { "GND", "TWR", "CTAF" },
            _ => Array.Empty<string>()
        };

        var usable = frequencies
            .Where(item => TryParseFrequency(item.Frequency, out _))
            .ToList();
        foreach (var type in priorities)
        {
            var match = usable.FirstOrDefault(item =>
                string.Equals(item.Type?.Trim(), type, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    public static bool TryParseFrequency(string? value, out double frequencyMhz) =>
        double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out frequencyMhz)
        && frequencyMhz is >= 118 and <= 136.975;
}
