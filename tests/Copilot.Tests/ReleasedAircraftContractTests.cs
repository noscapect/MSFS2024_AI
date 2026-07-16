using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Procedures;

namespace Msfs2024Ai.Copilot.Tests;

/// <summary>
/// Deliberate change detector for aircraft already validated gate-to-gate.
/// Updating another aircraft must not alter these contracts. A maintainer must
/// explicitly review and update a fingerprint when intentionally changing the
/// corresponding released aircraft.
/// </summary>
[TestClass]
public sealed class ReleasedAircraftContractTests
{
    [DataTestMethod]
    [DataRow("A320neo V2", "8ca4d441ed99c53d9b42ecb60107edf4b2171ce49c88f6d1252a917cbb96a2f8")]
    [DataRow("A321", "f8253aa4c4533730444503354a5c0305bc524f7ddbf73ef5c393c3f9d0f41afd")]
    [DataRow("A330-300 (GE)", "79e7d827ac283632247a8b5468547c18654aa5e9227c5c262567e257f6f2b773")]
    [DataRow("FlyByWire A32NX", "0d3992feff81319fc263abc4aa76e724da0072a7d6ecfe8981388a95067d02a0")]
    [DataRow("PMDG 737-800", "d6b49172b1b2ae4155b84abebd2ee476af28c3310fd9326bcd7b43d7a0dc74ab")]
    public void GateToGateStructureRemainsStable(string title, string expectedFingerprint)
    {
        Assert.AreEqual(expectedFingerprint, Fingerprint(title));
    }

    private static string Fingerprint(string title)
    {
        var state = new AircraftState { Title = title };
        var lines = new List<string>();
        foreach (var procedure in ProcedureCatalog.ForAircraft(state))
        {
            lines.Add($"P|{procedure.Id}|{procedure.Name}");
            foreach (var step in procedure.Steps)
            {
                lines.Add(
                    $"S|{step.Id}|{step.Label}|{step.Kind}|{step.AssignedRole}|" +
                    $"{step.Command}|{step.ManualInstruction}|{step.RequireCommandExecution}");
            }

            var checklist = ProcedureCatalog.FindChecklist(state, procedure.Id);
            if (checklist == null)
            {
                lines.Add("C|none");
                continue;
            }

            lines.Add($"C|{checklist.ProcedureId}|{checklist.Name}");
            lines.AddRange(checklist.Items.Select(item =>
                $"I|{item.Challenge}|{item.ExpectedResponse}"));
        }

        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(
            Encoding.UTF8.GetBytes(string.Join("\n", lines)));
        return string.Concat(bytes.Select(value => value.ToString("x2")));
    }
}
