namespace Msfs2024Ai.Copilot.AircraftIdentity;

internal sealed class AircraftIdentity
{
    public string Title { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string Type { get; set; } = "";
    public string Variation { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string? ThumbnailPath { get; set; }
    public IReadOnlyList<string> ThumbnailPaths { get; set; } = Array.Empty<string>();

    public string DisplayName
    {
        get
        {
            var manufacturer = Clean(Manufacturer);
            var type = Clean(Type);
            if (!string.IsNullOrWhiteSpace(manufacturer)
                && !string.IsNullOrWhiteSpace(type))
            {
                return $"{manufacturer} {type}";
            }

            return !string.IsNullOrWhiteSpace(type)
                ? type
                : Clean(Title);
        }
    }

    public string DisplayVariation
    {
        get
        {
            var variation = Clean(Variation);
            var createdBy = Clean(CreatedBy);
            if (!string.IsNullOrWhiteSpace(variation)
                && !string.IsNullOrWhiteSpace(createdBy))
            {
                return $"{variation} - {createdBy}";
            }

            return !string.IsNullOrWhiteSpace(variation)
                ? variation
                : createdBy;
        }
    }

    private static string Clean(string value)
    {
        var cleaned = value.Trim().Trim('"');
        return IsLocalizationToken(cleaned) ? "" : cleaned;
    }

    private static bool IsLocalizationToken(string value) =>
        value.StartsWith("TT:", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("TT_", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("AIRCRAFT.UI.", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("AIRCRAFT_UI_", StringComparison.OrdinalIgnoreCase);
}
