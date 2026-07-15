using Msfs2024Ai.Copilot.Checklists;

namespace Msfs2024Ai.Copilot.Procedures;

internal static class ProcedureCatalog
{
    public static IReadOnlyList<ProcedureDefinition> ForAircraft(AircraftState? state) =>
        ResolveLibrary(state).Procedures;

    public static ProcedureDefinition? Find(AircraftState? state, string id) =>
        ResolveLibrary(state).FindProcedure(id);

    public static ChecklistDefinition? FindChecklist(AircraftState? state, string procedureId) =>
        ResolveLibrary(state).FindChecklist(procedureId);

    private static AircraftProcedureLibrary ResolveLibrary(AircraftState? state)
    {
        if (state?.IsSupportedBoeing737 == true)
        {
            return new AircraftProcedureLibrary(
                B737ProcedureLibrary.GateToGate,
                B737ProcedureLibrary.Find,
                B737ChecklistLibrary.FindForProcedure);
        }

        if (state?.IsIniBuildsA330 == true)
        {
            return new AircraftProcedureLibrary(
                A330ProcedureLibrary.GateToGate,
                A330ProcedureLibrary.Find,
                A330ChecklistLibrary.FindForProcedure);
        }

        if (state?.IsIniBuildsA321Lr == true)
        {
            return new AircraftProcedureLibrary(
                A321ProcedureLibrary.GateToGate,
                A321ProcedureLibrary.Find,
                A321ChecklistLibrary.FindForProcedure);
        }

        if (state?.IsFlyByWireA320Neo == true)
        {
            return new AircraftProcedureLibrary(
                FbwA320ProcedureLibrary.GateToGate,
                FbwA320ProcedureLibrary.Find,
                FbwA320ChecklistLibrary.FindForProcedure);
        }

        return new AircraftProcedureLibrary(
            A320ProcedureLibrary.GateToGate,
            A320ProcedureLibrary.Find,
            A320ChecklistLibrary.FindForProcedure);
    }

    private sealed class AircraftProcedureLibrary
    {
        public AircraftProcedureLibrary(
            IReadOnlyList<ProcedureDefinition> procedures,
            Func<string, ProcedureDefinition?> findProcedure,
            Func<string, ChecklistDefinition?> findChecklist)
        {
            Procedures = procedures;
            FindProcedure = findProcedure;
            FindChecklist = findChecklist;
        }

        public IReadOnlyList<ProcedureDefinition> Procedures { get; }
        public Func<string, ProcedureDefinition?> FindProcedure { get; }
        public Func<string, ChecklistDefinition?> FindChecklist { get; }
    }
}
