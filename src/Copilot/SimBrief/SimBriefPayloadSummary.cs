using System.Globalization;

namespace Msfs2024Ai.Copilot.SimBrief;

internal static class SimBriefPayloadSummary
{
    private const double PoundsPerKilogram = 2.20462262185;

    public static double? Kilograms(double? value, string? units)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return IsPounds(units) ? value.Value / PoundsPerKilogram : value.Value;
    }

    public static double? PassengerMass(ImportedFlightPlan? plan) =>
        plan?.PassengerCount is > 0 && plan.PassengerWeight is > 0
            ? plan.PassengerCount.Value * plan.PassengerWeight.Value
            : null;

    public static double? BaggageMass(ImportedFlightPlan? plan) =>
        plan?.BaggageCount is > 0 && plan.BaggageWeight is > 0
            ? plan.BaggageCount.Value * plan.BaggageWeight.Value
            : null;

    public static string FormatWeight(double? value, string? units)
    {
        var kilograms = Kilograms(value, units);
        if (!kilograms.HasValue)
        {
            return "Not provided";
        }

        var pounds = kilograms.Value * PoundsPerKilogram;
        var tonnes = kilograms.Value / 1000d;
        return string.Format(
            CultureInfo.CurrentCulture,
            "{0:N0} kg   |   {1:N3} t   |   {2:N0} lb",
            kilograms.Value,
            tonnes,
            pounds);
    }

    public static string FormatWeightPerUnit(double? value, string? units)
    {
        var formatted = FormatWeight(value, units);
        return value.HasValue ? formatted + " each" : formatted;
    }

    public static string FormatCount(int? value) =>
        value.HasValue
            ? value.Value.ToString("N0", CultureInfo.CurrentCulture)
            : "Not provided";

    private static bool IsPounds(string? units) =>
        (units ?? string.Empty).Trim().StartsWith("lb", StringComparison.OrdinalIgnoreCase);
}
