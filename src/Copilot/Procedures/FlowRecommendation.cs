using Msfs2024Ai.Copilot.Domain;

namespace Msfs2024Ai.Copilot.Procedures;

internal sealed class FlowRecommendation
{
    public FlowRecommendation(ProcedureDefinition? procedure, bool overdue, string reason)
    {
        Procedure = procedure;
        Overdue = overdue;
        Reason = reason;
    }

    public ProcedureDefinition? Procedure { get; }
    public bool Overdue { get; }
    public string Reason { get; }
}

internal static class FlowRecommendationEngine
{
    public static FlowRecommendation Recommend(
        AircraftState state,
        IReadOnlyCollection<string> completedProcedureIds)
    {
        var phase = OperationalPhaseDetector.Detect(state);
        var procedures = ProcedureCatalog.ForAircraft(state);
        if (procedures.Count == 0)
        {
            return new FlowRecommendation(
                null,
                overdue: false,
                "No supported flow catalog is available for this aircraft yet.");
        }

        var recommendedId = phase switch
        {
            OperationalPhase.ColdAndDark => "power-up-initial-setup",
            OperationalPhase.CockpitPreparation => "power-up-initial-setup",
            OperationalPhase.ReadyForBoarding => "flight-computer-preflight",
            OperationalPhase.BeforeStart => "apu-start-pushback",
            OperationalPhase.EngineStart => "engine-start-sequence",
            OperationalPhase.AfterStart => "after-start-taxi",
            OperationalPhase.TaxiOut => "after-start-taxi",
            OperationalPhase.BeforeTakeoff => "before-takeoff",
            OperationalPhase.Takeoff => "takeoff-climb",
            OperationalPhase.Climb => "takeoff-climb",
            OperationalPhase.Cruise => "cruise",
            OperationalPhase.DescentPreparation => "descent-preparation",
            OperationalPhase.Descent => "approach-landing",
            OperationalPhase.Approach => "approach-landing",
            OperationalPhase.Landing => "approach-landing",
            OperationalPhase.AfterLanding => "after-landing-taxi",
            OperationalPhase.TaxiIn => "after-landing-taxi",
            OperationalPhase.Shutdown => "parking-shutdown",
            OperationalPhase.Secured => "parking-shutdown",
            _ => "power-up-initial-setup"
        };

        var firstIncomplete = procedures
            .FirstOrDefault(procedure => !completedProcedureIds.Contains(procedure.Id))
            ?? procedures[procedures.Count - 1];
        var phaseProcedure = ProcedureCatalog.Find(state, recommendedId) ?? firstIncomplete;
        var firstIncompleteIndex = procedures.IndexOf(firstIncomplete);
        var phaseIndex = procedures.IndexOf(phaseProcedure);
        var overdue = firstIncompleteIndex < phaseIndex;

        return new FlowRecommendation(
            firstIncomplete,
            overdue,
            overdue
                ? $"Earlier flow not completed before {phase}."
                : firstIncomplete.Id == phaseProcedure.Id
                    ? $"Recommended for current phase: {phase}."
                    : $"Next incomplete gameplay flow; detected phase: {phase}.");
    }

    private static int IndexOf(
        this IReadOnlyList<ProcedureDefinition> procedures,
        ProcedureDefinition value)
    {
        for (var index = 0; index < procedures.Count; index++)
        {
            if (procedures[index].Id == value.Id)
            {
                return index;
            }
        }

        return -1;
    }
}
