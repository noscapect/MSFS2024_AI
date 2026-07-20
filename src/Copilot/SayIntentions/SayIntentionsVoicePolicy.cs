namespace Msfs2024Ai.Copilot.SayIntentions;

internal static class SayIntentionsVoicePolicy
{
    public static bool BypassesQueue(string? stepId) =>
        stepId is "captain-takeoff" or "thrust-set"
            or "fo-100-knots" or "hundred-knots"
            or "fo-v1" or "v1"
            or "fo-rotate" or "rotate";

    public static TimeSpan MaxQueueAge(string? stepId)
    {
        if (IsEngineStartCallout(stepId))
        {
            return TimeSpan.FromSeconds(35);
        }

        return stepId switch
        {
            "captain-takeoff" or "thrust-set"
                or "fo-100-knots" or "hundred-knots"
                or "fo-v1" or "v1"
                or "fo-rotate" or "rotate"
                or "positive-climb" or "airborne"
                or "fo-gear-up"
                or "fo-gear-down"
                or "fo-approaching-minimums"
                or "fo-minimums"
                or "fo-spoilers-callout"
                or "fo-reverse-callout"
                or "fo-decel-callout"
                or "stable-approach" => TimeSpan.FromSeconds(6),
            "fo-ground-spoilers" or "fo-ground-spoilers-disarm"
                or "fo-spoilers-arm"
                or "fo-takeoff-flaps" or "fo-flaps-takeoff"
                or "fo-flaps-one" or "fo-flaps-two"
                or "fo-flaps-three" or "fo-flaps-full"
                or "fo-flaps-five" or "fo-flaps-fifteen"
                or "fo-flaps-landing" or "fo-flaps"
                or "fo-flaps-zero" or "fo-flaps-up" => TimeSpan.FromSeconds(8),
            _ => TimeSpan.FromSeconds(45)
        };
    }

    public static bool IsEngineStartCallout(string? stepId) =>
        stepId is "captain-engine-one"
            or "captain-engine-two"
            or "fo-engine-one-starter"
            or "fo-engine-two-starter"
            or "fo-engine-one-fuel"
            or "fo-engine-two-fuel"
            or "fo-engine-one-stable"
            or "fo-engine-two-stable";
}

internal sealed class SayIntentionsQueuedCallout
{
    public SayIntentionsQueuedCallout(
        string phrase,
        int priority,
        DateTime createdUtc,
        TimeSpan maxQueueAge)
    {
        Phrase = phrase;
        Priority = priority;
        CreatedUtc = createdUtc;
        MaxQueueAge = maxQueueAge;
    }

    public string Phrase { get; }
    public int Priority { get; }
    public DateTime CreatedUtc { get; }
    public TimeSpan MaxQueueAge { get; }
}
