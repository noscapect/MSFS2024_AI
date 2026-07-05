using Msfs2024Ai.Copilot.Checklists;

namespace Msfs2024Ai.Copilot.Procedures;

internal static class ProcedureCatalog
{
    public static IReadOnlyList<ProcedureDefinition> ForAircraft(AircraftState? state) =>
        state?.IsSupportedBoeing737 == true
            ? B737ProcedureLibrary.GateToGate
            : A320ProcedureLibrary.GateToGate;

    public static ProcedureDefinition? Find(AircraftState? state, string id) =>
        state?.IsSupportedBoeing737 == true
            ? B737ProcedureLibrary.Find(id)
            : A320ProcedureLibrary.Find(id);

    public static ChecklistDefinition? FindChecklist(AircraftState? state, string procedureId) =>
        state?.IsSupportedBoeing737 == true
            ? B737ChecklistLibrary.FindForProcedure(procedureId)
            : A320ChecklistLibrary.FindForProcedure(procedureId);
}
