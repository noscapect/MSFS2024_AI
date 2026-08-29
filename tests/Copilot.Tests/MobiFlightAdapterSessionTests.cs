using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Simulation;

namespace Copilot.Tests;

[TestClass]
public sealed class MobiFlightAdapterSessionTests
{
    [TestMethod]
    public void NewSession_IsNotReady()
    {
        var session = new MobiFlightAdapterSession();

        Assert.IsFalse(session.AdapterReady);
        Assert.IsFalse(session.RuntimeReady);
        Assert.IsNull(session.RuntimeInitializedUtc);
    }

    [TestMethod]
    public void Session_ReadinessAndSettlementUseTheExistingTwoSecondBoundary()
    {
        var session = new MobiFlightAdapterSession();
        var initializedUtc = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);

        session.MarkAdapterReady();
        session.MarkRuntimeReady(initializedUtc);

        Assert.IsTrue(session.AdapterReady);
        Assert.IsTrue(session.RuntimeReady);
        Assert.AreEqual(initializedUtc, session.RuntimeInitializedUtc);
        Assert.IsFalse(session.HasRuntimeSettled(initializedUtc.AddMilliseconds(1999)));
        Assert.IsTrue(session.HasRuntimeSettled(initializedUtc.AddSeconds(2)));
    }

    [TestMethod]
    public void ConnectionReset_ClearsSessionAndAllowsAnotherInitialization()
    {
        var session = new MobiFlightAdapterSession();
        session.MarkAdapterReady();
        session.MarkRuntimeReady(DateTime.UtcNow);

        session.ResetConnectionState();

        Assert.IsFalse(session.AdapterReady);
        Assert.IsFalse(session.RuntimeReady);
        Assert.IsNull(session.RuntimeInitializedUtc);

        session.MarkAdapterReady();
        session.MarkRuntimeReady(DateTime.UtcNow);

        Assert.IsTrue(session.AdapterReady);
        Assert.IsTrue(session.RuntimeReady);
    }

    [TestMethod]
    public void RuntimeReset_PreservesAdapterHandshakeState()
    {
        var session = new MobiFlightAdapterSession();
        session.MarkAdapterReady();
        session.MarkRuntimeReady(DateTime.UtcNow);

        session.ResetRuntimeState();

        Assert.IsTrue(session.AdapterReady);
        Assert.IsFalse(session.RuntimeReady);
        Assert.IsNull(session.RuntimeInitializedUtc);
    }

    [TestMethod]
    public void RuntimeCatalog_PreservesTheV27OrderedRegistrationLayout()
    {
        var commands = new MobiFlightAdapterSession().RuntimeRegistrationCommands.ToList();

        Assert.AreEqual("MSFS2024_AI_Copilot_v27", MobiFlightAdapterSession.RuntimeClientName);
        Assert.AreEqual(183, commands.Count);
        Assert.AreEqual("MF.SimVars.Clear", commands[0]);
        CollectionAssert.AreEqual(
            new[]
            {
                "MF.SimVars.Add.(L:INI_OVHD_ELEC_BAT_1_PB_IS_AUTO_SWITCH)",
                "MF.SimVars.Add.(L:INI_OVHD_ELEC_BAT_2_PB_IS_AUTO_SWITCH)",
                "MF.SimVars.Add.(L:INI_OUTER_TANK_LEFT_PUMP_ON)",
                "MF.SimVars.Add.(L:INI_INNER_TANK_LEFT_PUMP_ON)",
                "MF.SimVars.Add.(L:INI_CENTER_TANK_LEFT_PUMP_ON)",
                "MF.SimVars.Add.(L:INI_CENTER_TANK_RIGHT_PUMP_ON)",
                "MF.SimVars.Add.(L:INI_INNER_TANK_RIGHT_PUMP_ON)",
                "MF.SimVars.Add.(L:INI_OUTER_TANK_RIGHT_PUMP_ON)"
            },
            commands.Skip(1).Take(8).ToList());
        Assert.IsTrue(commands.IndexOf("MF.SimVars.Add.(L:INI_APU_AVAILABLE)")
                      < commands.IndexOf("MF.SimVars.Add.(L:INI_IRS1_STATE)"));
        Assert.IsTrue(commands.IndexOf("MF.SimVars.Add.(L:A32NX_OVHD_ADIRS_IR_1_MODE_SELECTOR_KNOB, Enum)")
                      < commands.IndexOf("MF.SimVars.Add.(L:A32NX_OVHD_ADIRS_ON_BAT_IS_ILLUMINATED, Bool)"));
        Assert.IsTrue(commands.IndexOf("MF.SimVars.Add.(L:A32NX_EXT_PWR_AVAIL:1, Bool)")
                      < commands.IndexOf("MF.SimVars.Add.(L:A32NX_OVHD_ELEC_EXT_PWR_4_PB_IS_ON, Bool)"));
        Assert.AreEqual(
            2,
            commands.Count(command =>
                command == "MF.SimVars.Add.(L:A32NX_EXT_PWR_AVAIL:1, Bool)"));
        Assert.IsTrue(commands.IndexOf("MF.SimVars.Add.(L:INI_IGNITION_KNOB)")
                      < commands.IndexOf("MF.SimVars.Add.(L:INI_TURNOFF_LIGHT_SWITCH)"));
        Assert.AreEqual("MF.SimVars.Add.(L:a310_bat1_on)", commands[101]);
        Assert.AreEqual("MF.DummyCmd", commands[commands.Count - 1]);
        Assert.AreEqual(
            "278173dbc175e299fd37e4ecdfe40540b0a27b92ceecbe005e1360e1e40c8252",
            Sha256(commands));
    }

    private static string Sha256(IEnumerable<string> commands)
    {
        using var algorithm = SHA256.Create();
        var bytes = algorithm.ComputeHash(
            Encoding.UTF8.GetBytes(string.Join("\n", commands)));
        return string.Concat(bytes.Select(value => value.ToString("x2")));
    }
}
