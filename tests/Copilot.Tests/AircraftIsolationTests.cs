using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.AircraftAdapters;
using Msfs2024Ai.Copilot.Procedures;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class AircraftIsolationTests
{
    [DataTestMethod]
    [DataRow("A320neo V2", (int)AircraftVariant.IniBuildsA320NeoV2)]
    [DataRow("A321", (int)AircraftVariant.IniBuildsA321Lr)]
    [DataRow("A321LR", (int)AircraftVariant.IniBuildsA321Lr)]
    [DataRow("A330-300 (GE)", (int)AircraftVariant.IniBuildsA330)]
    [DataRow("Airbus A320neo FlyByWire", (int)AircraftVariant.FlyByWireA320Neo)]
    [DataRow("FlyByWire A32NX", (int)AircraftVariant.FlyByWireA320Neo)]
    [DataRow("737-800 PAX BW TC", (int)AircraftVariant.Pmdg737800)]
    [DataRow("PMDG 737-800", (int)AircraftVariant.Pmdg737800)]
    [DataRow("FlyByWire A380X", (int)AircraftVariant.Unsupported)]
    [DataRow("Fenix A320", (int)AircraftVariant.Unsupported)]
    [DataRow("Boeing 737 MAX 8", (int)AircraftVariant.Unsupported)]
    [DataRow("Headwind A330-900neo", (int)AircraftVariant.Unsupported)]
    [DataRow("Generic A330", (int)AircraftVariant.Unsupported)]
    public void ResolverSelectsExactlyOneImplementation(
        string title,
        int expectedValue)
    {
        var expected = (AircraftVariant)expectedValue;
        Assert.AreEqual(expected, AircraftVariantResolver.Resolve(title));

        var state = new AircraftState { Title = title };
        var activeFlags = new[]
        {
            state.IsA320NeoV2,
            state.IsIniBuildsA321Lr,
            state.IsIniBuildsA330,
            state.IsFlyByWireA320Neo,
            state.IsFlyByWireA380X,
            state.IsPmdg737800
        }.Count(flag => flag);

        Assert.AreEqual(
            expected == AircraftVariant.Unsupported ? 0 : 1,
            activeFlags,
            $"Aircraft '{title}' crossed an implementation boundary.");
    }

    [TestMethod]
    public void EveryReleasedAircraftRoutesToItsDedicatedProcedureObjects()
    {
        AssertDedicatedLibrary("A320neo V2", A320ProcedureLibrary.GateToGate);
        AssertDedicatedLibrary("A321", A321ProcedureLibrary.GateToGate);
        AssertDedicatedLibrary("A330-300 (GE)", A330ProcedureLibrary.GateToGate);
        AssertDedicatedLibrary("FlyByWire A32NX", FbwA320ProcedureLibrary.GateToGate);
        AssertDedicatedLibrary("PMDG 737-800", B737ProcedureLibrary.GateToGate);
    }

    [TestMethod]
    public void ReleasedAircraftLibrariesShareNoProcedureOrStepInstances()
    {
        var libraries = new[]
        {
            A320ProcedureLibrary.GateToGate,
            A321ProcedureLibrary.GateToGate,
            A330ProcedureLibrary.GateToGate,
            FbwA320ProcedureLibrary.GateToGate,
            B737ProcedureLibrary.GateToGate
        };

        for (var left = 0; left < libraries.Length; left++)
        {
            for (var right = left + 1; right < libraries.Length; right++)
            {
                Assert.IsFalse(
                    libraries[left].Any(leftProcedure =>
                        libraries[right].Any(rightProcedure =>
                            ReferenceEquals(leftProcedure, rightProcedure))),
                    $"Procedure objects are shared by released libraries {left} and {right}.");

                var leftSteps = libraries[left].SelectMany(item => item.Steps).ToArray();
                var rightSteps = libraries[right].SelectMany(item => item.Steps).ToArray();
                Assert.IsFalse(
                    leftSteps.Any(leftStep =>
                        rightSteps.Any(rightStep => ReferenceEquals(leftStep, rightStep))),
                    $"Procedure step objects are shared by released libraries {left} and {right}.");
            }
        }
    }

    [TestMethod]
    public void A330AutomaticActionsUseOnlyTheDedicatedA330CommandNamespace()
    {
        var commands = A330ProcedureLibrary.GateToGate
            .SelectMany(procedure => procedure.Steps)
            .Where(step => step.Kind == ProcedureStepKind.AutomaticAction)
            .Select(step => step.Command)
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .ToArray();

        Assert.IsTrue(commands.Length > 0, "The A330 profile must contain automatic actions.");
        Assert.IsTrue(
            commands.All(command => command!.StartsWith("a330 ", StringComparison.Ordinal)),
            "An A330 automatic action escaped the dedicated 'a330' command namespace.");

        var otherAircraftCommands = new[]
            {
                A320ProcedureLibrary.GateToGate,
                A321ProcedureLibrary.GateToGate,
                FbwA320ProcedureLibrary.GateToGate,
                B737ProcedureLibrary.GateToGate
            }
            .SelectMany(library => library)
            .SelectMany(procedure => procedure.Steps)
            .Select(step => step.Command)
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .ToHashSet(StringComparer.Ordinal);

        Assert.IsFalse(
            commands.Any(otherAircraftCommands.Contains),
            "An A330 automatic command is shared with another aircraft profile.");
    }

    [TestMethod]
    public void PmdgAutomaticActionsUseOnlyTheDedicatedPmdgCommandNamespace()
    {
        var commands = B737ProcedureLibrary.GateToGate
            .SelectMany(procedure => procedure.Steps)
            .Where(step => step.Kind == ProcedureStepKind.AutomaticAction)
            .Select(step => step.Command)
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .ToArray();

        Assert.IsTrue(commands.Length > 0, "The PMDG profile must contain automatic actions.");
        Assert.IsTrue(
            commands.All(command => command!.StartsWith("pmdg ", StringComparison.Ordinal)),
            "A PMDG automatic action escaped the dedicated 'pmdg' command namespace.");

        var otherAircraftCommands = new[]
            {
                A320ProcedureLibrary.GateToGate,
                A321ProcedureLibrary.GateToGate,
                FbwA320ProcedureLibrary.GateToGate
            }
            .SelectMany(library => library)
            .SelectMany(procedure => procedure.Steps)
            .Select(step => step.Command)
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .ToHashSet(StringComparer.Ordinal);

        Assert.IsFalse(
            commands.Any(otherAircraftCommands.Contains),
            "A PMDG automatic command is shared with a released Airbus profile.");
    }

    [TestMethod]
    public void UnsupportedAircraftCannotInheritA320Flows()
    {
        var state = new AircraftState { Title = "Future aircraft not yet implemented" };

        Assert.AreEqual(0, ProcedureCatalog.ForAircraft(state).Count);
        Assert.IsNull(ProcedureCatalog.Find(state, "power-up-initial-setup"));
        Assert.IsNull(ProcedureCatalog.FindChecklist(state, "power-up-initial-setup"));
    }

    [TestMethod]
    public void EveryAircraftRequestsIfrClearanceAfterFlightComputerProgramming()
    {
        var profiles = new[]
        {
            (A320ProcedureLibrary.FlightComputerAndPreFlight, A320ProcedureLibrary.ApuStartAndPushback, "mcdu-perf"),
            (A321ProcedureLibrary.FlightComputerAndPreFlight, A321ProcedureLibrary.ApuStartAndPushback, "mcdu-perf"),
            (A330ProcedureLibrary.FlightComputerAndPreFlight, A330ProcedureLibrary.ApuStartAndPushback, "mcdu-perf"),
            (FbwA320ProcedureLibrary.FlightComputerAndPreFlight, FbwA320ProcedureLibrary.ApuStartAndPushback, "mcdu-perf"),
            (B737ProcedureLibrary.FlightComputerAndPreFlight, B737ProcedureLibrary.ApuStartAndPushback, "fmc-perf")
        };

        foreach (var (preflight, apuStart, programmingStepId) in profiles)
        {
            var programmingIndex = preflight.Steps
                .Select((step, index) => (step, index))
                .Single(item => item.step.Id == programmingStepId)
                .index;
            var clearanceIndex = preflight.Steps
                .Select((step, index) => (step, index))
                .Single(item => item.step.Id == "captain-ifr-clearance")
                .index;

            Assert.AreEqual(
                programmingIndex + 1,
                clearanceIndex,
                $"{preflight.Name} must request IFR clearance immediately after programming.");
            Assert.IsFalse(
                apuStart.Steps.Any(step => step.Id == "captain-ifr-clearance"),
                $"{apuStart.Name} must not defer IFR clearance until APU start.");
        }
    }

    private static void AssertDedicatedLibrary(
        string title,
        IReadOnlyList<ProcedureDefinition> expected)
    {
        var actual = ProcedureCatalog.ForAircraft(new AircraftState { Title = title });

        Assert.AreEqual(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.AreSame(expected[index], actual[index]);
        }
    }
}
