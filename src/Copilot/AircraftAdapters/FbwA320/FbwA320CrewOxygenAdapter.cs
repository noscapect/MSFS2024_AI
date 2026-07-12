using Msfs2024Ai.Copilot;

namespace Msfs2024Ai.Copilot.AircraftAdapters.FbwA320;

internal static class FbwA320CrewOxygenAdapter
{
    public const ulong InputEventHash = 1174871640386391352UL;

    public static FbwA320CrewOxygenCommandPlan CreatePlan(
        AircraftState? state,
        bool desiredOn,
        bool? typedRawOffState = null,
        bool? untypedRawOffState = null)
    {
        if (state == null)
        {
            return FbwA320CrewOxygenCommandPlan.Blocked(
                "Crew oxygen blocked: aircraft state is unavailable.",
                3);
        }

        if (!state.IsFlyByWireA320Neo)
        {
            return FbwA320CrewOxygenCommandPlan.Blocked(
                "Crew oxygen blocked: loaded aircraft is not FBW A320.",
                3);
        }

        if (typedRawOffState.HasValue || untypedRawOffState.HasValue)
        {
            var rawOffState = typedRawOffState ?? untypedRawOffState!.Value;
            var liveCrewOxygenOn = !rawOffState;
            if (liveCrewOxygenOn == desiredOn)
            {
                return FbwA320CrewOxygenCommandPlan.AlreadySet(
                    $"Crew oxygen already {desiredOn.ToOnOff()}.");
            }
        }
        else if (!desiredOn && state.CrewOxygenOn == desiredOn)
        {
            return FbwA320CrewOxygenCommandPlan.AlreadySet(
                $"Crew oxygen already {desiredOn.ToOnOff()}.");
        }

        // PUSH_OVHD_OXYGEN_CREW is the pushbutton/OFF-side state:
        // 0 = oxygen supply ON, 1 = oxygen supply OFF.
        var rawState = desiredOn ? 0 : 1;
        return FbwA320CrewOxygenCommandPlan.Command(
            desiredOn,
            rawState,
            InputEventHash,
            new[]
            {
                $"MF.SimVars.Set.{rawState} (>L:PUSH_OVHD_OXYGEN_CREW)",
                $"MF.SimVars.Set.{rawState} (>L:PUSH_OVHD_OXYGEN_CREW, Bool)",
                "MF.DummyCmd"
            });
    }
}

internal sealed class FbwA320CrewOxygenCommandPlan
{
    private FbwA320CrewOxygenCommandPlan(
        FbwA320CrewOxygenCommandPlanKind kind,
        bool desiredOn,
        int rawState,
        ulong inputEventHash,
        IReadOnlyList<string> mobiFlightCommands,
        string? message,
        int exitCode)
    {
        Kind = kind;
        DesiredOn = desiredOn;
        RawState = rawState;
        InputEventHash = inputEventHash;
        MobiFlightCommands = mobiFlightCommands;
        Message = message;
        ExitCode = exitCode;
    }

    public FbwA320CrewOxygenCommandPlanKind Kind { get; }
    public bool DesiredOn { get; }
    public int RawState { get; }
    public ulong InputEventHash { get; }
    public IReadOnlyList<string> MobiFlightCommands { get; }
    public string? Message { get; }
    public int ExitCode { get; }

    public static FbwA320CrewOxygenCommandPlan Command(
        bool desiredOn,
        int rawState,
        ulong inputEventHash,
        IReadOnlyList<string> mobiFlightCommands) =>
        new(
            FbwA320CrewOxygenCommandPlanKind.Command,
            desiredOn,
            rawState,
            inputEventHash,
            mobiFlightCommands,
            null,
            0);

    public static FbwA320CrewOxygenCommandPlan AlreadySet(string message) =>
        new(
            FbwA320CrewOxygenCommandPlanKind.AlreadySet,
            false,
            0,
            0,
            Array.Empty<string>(),
            message,
            0);

    public static FbwA320CrewOxygenCommandPlan Blocked(string message, int exitCode) =>
        new(
            FbwA320CrewOxygenCommandPlanKind.Blocked,
            false,
            0,
            0,
            Array.Empty<string>(),
            message,
            exitCode);
}

internal enum FbwA320CrewOxygenCommandPlanKind
{
    Command,
    AlreadySet,
    Blocked
}
