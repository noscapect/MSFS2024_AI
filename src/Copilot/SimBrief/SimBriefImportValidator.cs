namespace Msfs2024Ai.Copilot.SimBrief;

internal static class SimBriefImportValidator
{
    public static IReadOnlyList<string> Validate(
        ImportedFlightPlan plan,
        IEnumerable<string> expectedAircraftIcaos,
        DateTime utcNow)
    {
        var warnings = new List<string>();
        if (!plan.GeneratedUtc.HasValue)
        {
            warnings.Add("The OFP generation time is unavailable.");
        }
        else if (utcNow - plan.GeneratedUtc.Value > TimeSpan.FromHours(24))
        {
            warnings.Add($"The OFP is {(int)(utcNow - plan.GeneratedUtc.Value).TotalHours} hours old.");
        }

        var expected = expectedAircraftIcaos
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (expected.Count > 0
            && !string.IsNullOrWhiteSpace(plan.AircraftIcao)
            && !expected.Contains(plan.AircraftIcao))
        {
            warnings.Add(
                $"SimBrief aircraft {plan.AircraftIcao} does not match the detected aircraft ({string.Join("/", expected)}).");
        }
        return warnings;
    }
}
