namespace Msfs2024Ai.Copilot.Gsx;

internal enum GsxDepartureAction
{
    Boarding,
    PrepareForDeparture
}

internal static class GsxDepartureCoordinator
{
    public static bool IsPushbackUnderway(
        bool onGround,
        bool parkingBrakeSet,
        double groundSpeedKnots) =>
        onGround && !parkingBrakeSet && groundSpeedKnots >= 0.1;

    public static bool EngineStartPhaseStarted(
        bool engineModeIgnStart,
        bool engine1StarterActive,
        bool engine2StarterActive,
        bool engine1FuelFlowDetected,
        bool engine2FuelFlowDetected,
        bool engine1Running,
        bool engine2Running) =>
        engineModeIgnStart
        || engine1StarterActive
        || engine2StarterActive
        || engine1FuelFlowDetected
        || engine2FuelFlowDetected
        || engine1Running
        || engine2Running;

    public static int? FindChoice(
        GsxMenuSnapshot menu,
        GsxDepartureAction action)
    {
        for (var index = 0; index < menu.Choices.Count; index++)
        {
            var value = Normalize(menu.Choices[index]);
            var match = action switch
            {
                GsxDepartureAction.Boarding =>
                    value.Contains("boarding")
                    && !value.Contains("deboarding")
                    && (value.Contains("request")
                        || value.Contains("start")
                        || value.Contains("board")),
                GsxDepartureAction.PrepareForDeparture =>
                    value.Contains("prepare")
                    && value.Contains("departure")
                    && value.Contains("pushback"),
                _ => false
            };

            if (match)
            {
                return index;
            }
        }

        return null;
    }

    private static string Normalize(string value) =>
        new string(value
                .ToLowerInvariant()
                .Where(character => char.IsLetterOrDigit(character)
                                    || char.IsWhiteSpace(character))
                .ToArray())
            .Replace("push back", "pushback");
}
