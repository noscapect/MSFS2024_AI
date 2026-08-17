namespace Msfs2024Ai.Copilot.Procedures;

internal static class FlightProgressTransitionPolicy
{
    public static bool ShouldShowTaxiToHoldingPoint(
        AircraftState state,
        bool procedureActive,
        bool afterStartTaxiCompleted,
        bool beforeTakeoffCompleted,
        bool parkingShutdownCompleted,
        string? recommendedProcedureId) =>
        state.OnGround
        && !procedureActive
        && !state.BeforeTakeoffHoldEligible
        && afterStartTaxiCompleted
        && !beforeTakeoffCompleted
        && !parkingShutdownCompleted
        && string.Equals(
            recommendedProcedureId,
            "before-takeoff",
            StringComparison.OrdinalIgnoreCase);
}
