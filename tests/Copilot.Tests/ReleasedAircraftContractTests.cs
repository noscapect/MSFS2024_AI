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
    [DataRow("A320neo V2", "1b92bbef1984a0732fb6571456c3a8e5442e995fc3b9dc0fe2f7a1247cb37ce7")]
    [DataRow("A321", "11511c822cc68e717d107e5c3c68aa51fbb785556737fc29ceaa9db1ba61187c")]
    [DataRow("A330-300 (GE)", "b1fe2d3d6a6d7f6d09cbc3e6636156b226f782f937a6a376beafaaabf6fbf17f")]
    [DataRow("FlyByWire A32NX", "ec2e51dcc118cf20ed87f6b26e50ad31f6a2cd67216d272e9fcb98f8c43cd908")]
    [DataRow("PMDG 737-800", "8934f9e8ec753502d6a1f7ab686e30ee7bdcaf30670bae8f675b08a399533a10")]
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
