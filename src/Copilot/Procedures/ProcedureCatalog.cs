using Msfs2024Ai.Copilot.AircraftAdapters;
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
        if (state == null)
        {
            return A320Library();
        }

        switch (state.Variant)
        {
            case AircraftVariant.Pmdg737800:
                return new AircraftProcedureLibrary(
                    B737ProcedureLibrary.GateToGate,
                    B737ProcedureLibrary.Find,
                    B737ChecklistLibrary.FindForProcedure);
            case AircraftVariant.Asobo737Max8:
                return new AircraftProcedureLibrary(
                    Asobo737MaxProcedureLibrary.GateToGate,
                    Asobo737MaxProcedureLibrary.Find,
                    Asobo737MaxChecklistLibrary.FindForProcedure);
            case AircraftVariant.IniBuildsA330:
                return new AircraftProcedureLibrary(
                A330ProcedureLibrary.GateToGate,
                A330ProcedureLibrary.Find,
                A330ChecklistLibrary.FindForProcedure);
            case AircraftVariant.IniBuildsA321Lr:
                return new AircraftProcedureLibrary(
                A321ProcedureLibrary.GateToGate,
                A321ProcedureLibrary.Find,
                A321ChecklistLibrary.FindForProcedure);
            case AircraftVariant.FlyByWireA320Neo:
                return new AircraftProcedureLibrary(
                FbwA320ProcedureLibrary.GateToGate,
                FbwA320ProcedureLibrary.Find,
                FbwA320ChecklistLibrary.FindForProcedure);
            case AircraftVariant.IniBuildsA320NeoV2:
                return A320Library();
            default:
                return AircraftProcedureLibrary.Unsupported;
        }
    }

    private static AircraftProcedureLibrary A320Library() =>
        new(
            A320ProcedureLibrary.GateToGate,
            A320ProcedureLibrary.Find,
            A320ChecklistLibrary.FindForProcedure);

    private sealed class AircraftProcedureLibrary
    {
        public static AircraftProcedureLibrary Unsupported { get; } =
            new(
                Array.Empty<ProcedureDefinition>(),
                _ => null,
                _ => null);

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
