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
    [DataRow("A320neo V2", "623f780fedfb5076c6a0c6ed9ea3150d944e3dd4f6f74d32a5978cd86feb819a")]
    [DataRow("A321", "d16326b277a20b388eab6c2f2731439e12f6d1528ad9605d8fe7fce46542fbcf")]
    [DataRow("A330-300 (GE)", "c3b071b665f6c0f01b3957b87c3aca04ee3eed1d0e39c5e690b8e1ab584fa1ee")]
    [DataRow("FlyByWire A32NX", "dad26734b8110c0dd1f4dce68cdff9bfdb7d7b7299871871b58f2a95d99f235c")]
    [DataRow("PMDG 737-800", "48329cfd7f7397cb57ca65007c655657d947d78812bc8a27e0a29e1f09e8dc5e")]
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
