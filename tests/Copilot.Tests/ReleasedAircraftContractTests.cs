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
    [DataRow("A320neo V2", "1de08f2dfdda88cd1e2786238da7334de7bc0ddeaedde2ba1cf0bb73ba17f378")]
    [DataRow("A321", "44d2937927d9019e1ee784e15f47cc5a28a779c3d2c07e2da17c01f3d81bd7a9")]
    [DataRow("A330-300 (GE)", "4411edb31c1bc4d5320a50f4a4a98135435ba225b4a601531a450d0bf25de444")]
    [DataRow("FlyByWire A32NX", "6d6d0bb273c2694fe15636a0740bcb7d060f895a8ce54cf2e7a70f048af33eb9")]
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
