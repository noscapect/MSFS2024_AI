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
    [DataRow("A320neo V2", "15917c228c9d008e953ceb8ac7e596242872cc311c287c88a6fcc952b859237b")]
    [DataRow("A321", "d124127c113e36519543c9a1a6257596b80274323ff59b0d3a009cefaf84162d")]
    [DataRow("A330-300 (GE)", "be805223109798b45c70bc23d94e470351361f6bdf3b6513acc431852a41d606")]
    [DataRow("FlyByWire A32NX", "5eb8febdc30332c34692959d107c0ba5e2d5f5ed0a13962393c0f2d2a1d44cbe")]
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
