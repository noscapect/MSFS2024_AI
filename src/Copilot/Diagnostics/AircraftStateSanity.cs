namespace Msfs2024Ai.Copilot.Diagnostics;

internal static class AircraftStateSanity
{
    public static IReadOnlyList<string> Evaluate(AircraftState state)
    {
        var issues = new List<string>();
        if (!IsFinite(state.FlapsHandleIndex)
            || state.FlapsHandleIndex < -0.1
            || state.FlapsHandleIndex > 5.1
            || Math.Abs(state.FlapsHandleIndex - Math.Round(state.FlapsHandleIndex)) > 0.1)
        {
            issues.Add($"Invalid flap handle index {state.FlapsHandleIndex:F2}.");
            return issues;
        }

        if (!IsPercent(state.LeftFlapPositionPercent)
            || !IsPercent(state.RightFlapPositionPercent))
        {
            issues.Add("Invalid flap-surface position telemetry.");
            return issues;
        }

        var maximumSurface =
            Math.Max(state.LeftFlapPositionPercent, state.RightFlapPositionPercent);
        if (state.FlapsHandleIndex < 0.1 && maximumSurface > 5)
        {
            issues.Add(
                $"Flap handle reports CLEAN while surfaces report {maximumSurface:F1}%.");
        }
        else if (state.FlapsHandleIndex >= 0.9 && maximumSurface < 1)
        {
            issues.Add(
                $"Flap handle reports detent {state.FlapsHandleIndex:F0} " +
                "while surfaces remain retracted.");
        }
        else if (state.FlapsHandleIndex >= 3.9 && maximumSurface < 75)
        {
            issues.Add(
                $"Flap handle reports FULL while surfaces report only {maximumSurface:F1}%.");
        }

        return issues;
    }

    private static bool IsPercent(double value) =>
        IsFinite(value) && value is >= 0 and <= 100.1;

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);
}
