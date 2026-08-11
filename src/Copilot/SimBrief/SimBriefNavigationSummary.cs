namespace Msfs2024Ai.Copilot.SimBrief;

internal static class SimBriefNavigationSummary
{
    public static string Airac(ImportedFlightPlan? plan)
    {
        var airac = plan?.Airac;
        return string.IsNullOrWhiteSpace(airac)
            ? "OFP AIRAC unavailable"
            : $"OFP AIRAC {airac!.Trim()}";
    }

    public static string Departure(ImportedFlightPlan plan) =>
        $"{AirportRunway(plan.OriginIcao, plan.OriginRunway)} | " +
        $"SID {Procedure(plan.SidIdentifier, plan.SidTransition)} | " +
        Transition(
            plan.TransitionAltitudeFeet,
            plan.OriginTransitionLevelFeet);

    public static string Arrival(ImportedFlightPlan plan) =>
        $"{AirportRunway(plan.DestinationIcao, plan.DestinationRunway)} | " +
        $"STAR {Procedure(plan.StarIdentifier, plan.StarTransition)} | " +
        Transition(
            plan.DestinationTransitionAltitudeFeet,
            plan.DestinationTransitionLevelFeet);

    public static string PreferredRoute(ImportedFlightPlan plan) =>
        !string.IsNullOrWhiteSpace(plan.NavigraphRoute)
            ? plan.NavigraphRoute.Trim()
            : !string.IsNullOrWhiteSpace(plan.Route)
                ? plan.Route.Trim()
                : "--";

    public static string Navlog(ImportedFlightPlan plan) =>
        plan.Navlog.Count == 0
            ? "Structured navlog unavailable"
            : $"Structured navlog: {plan.Navlog.Count} fixes";

    private static string AirportRunway(string? airport, string? runway) =>
        $"{Value(airport)} runway {Value(runway)}";

    private static string Procedure(string? identifier, string? transition)
    {
        var procedure = Value(identifier);
        return string.IsNullOrWhiteSpace(transition)
            ? procedure
            : $"{procedure} via {transition!.Trim()}";
    }

    private static string Transition(int? altitudeFeet, int? levelFeet)
    {
        var altitude = altitudeFeet.HasValue
            ? $"TA {altitudeFeet.Value:N0} ft"
            : "TA --";
        var level = levelFeet.HasValue
            ? $"TL {levelFeet.Value:N0} ft"
            : "TL --";
        return $"{altitude} / {level}";
    }

    private static string Value(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "--" : value!.Trim();
}
