using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot;
using Msfs2024Ai.Copilot.AircraftAdapters.Pmdg777;
using System.Runtime.InteropServices;

namespace Copilot.Tests;

[TestClass]
public sealed class SimConnectContractTests
{
    [TestMethod]
    public void DefinitionAndRequestIdsRemainStable()
    {
        Assert.AreEqual(0, (int)Definition.AircraftState);
        Assert.AreEqual(1, (int)Definition.FlightCalloutState);
        Assert.AreEqual(400, (int)Definition.GsxRemoteControl);
        Assert.AreEqual(402, (int)Definition.GsxMenuChoice);

        Assert.AreEqual(0, (int)Request.AircraftState);
        Assert.AreEqual(500, (int)Request.FlightCalloutState);
        Assert.AreEqual(501, (int)Request.MobiFlightResponse);
        Assert.AreEqual(110, (int)Request.MobiFlightRuntimeResponse);
        Assert.AreEqual(300, (int)Request.PmdgNg3Data);
        Assert.AreEqual(301, (int)Request.PmdgNg3Control);
        Assert.AreEqual(Pmdg777ControlProfile.DataRequestId, (int)Request.Pmdg777Data);
        Assert.AreEqual(Pmdg777ControlProfile.ControlRequestId, (int)Request.Pmdg777Control);
    }

    [TestMethod]
    public void ClientDataAndEventIdsRemainStable()
    {
        Assert.AreEqual(100, (int)ClientDataArea.MobiFlightCommand);
        Assert.AreEqual(112, (int)ClientDataArea.MobiFlightRuntimeResponse);
        Assert.AreEqual(
            unchecked((int)SimConnectContractConstants.PmdgNg3DataId),
            (int)ClientDataArea.PmdgNg3Data);
        Assert.AreEqual(
            unchecked((int)Pmdg777ControlProfile.ControlId),
            (int)ClientDataArea.Pmdg777Control);

        Assert.AreEqual(100, (int)ClientDataDefinition.MobiFlightMessage);
        Assert.AreEqual(110, (int)ClientDataDefinition.MobiFlightRuntimeMessage);
        Assert.AreEqual(
            unchecked((int)SimConnectContractConstants.PmdgNg3DataDefinition),
            (int)ClientDataDefinition.PmdgNg3Data);
        Assert.AreEqual(
            unchecked((int)Pmdg777ControlProfile.ControlDefinition),
            (int)ClientDataDefinition.Pmdg777Control);

        Assert.AreEqual(0, (int)CopilotEvent.SetExternalPower);
        Assert.AreEqual(14, (int)EfbCommBusEvent.Command);
        Assert.AreEqual(400, (int)CopilotEvent.GsxExternalSystemSet);
        Assert.AreEqual(400, (int)NotificationGroup.Gsx);
        Assert.AreEqual(1, (int)Priority.Highest);
    }

    [TestMethod]
    public void MarshaledDataLayoutsRemainStable()
    {
        Assert.AreEqual(1552, Marshal.SizeOf<AircraftData>());
        Assert.AreEqual(48, Marshal.SizeOf<FlightCalloutData>());
        Assert.AreEqual(8, Marshal.SizeOf<GsxValue>());
        Assert.AreEqual(1024, Marshal.SizeOf<MobiFlightMessage>());
        Assert.AreEqual(4, Marshal.SizeOf<MobiFlightFloat>());
        Assert.AreEqual(
            SimConnectContractConstants.PmdgNg3DataSize,
            Marshal.SizeOf<PmdgNg3RawData>());
        Assert.AreEqual(8, Marshal.SizeOf<PmdgNg3Control>());
        Assert.AreEqual(Pmdg777ControlProfile.DataSize, Marshal.SizeOf<Pmdg777RawData>());
        Assert.AreEqual(8, Marshal.SizeOf<Pmdg777Control>());
    }
}
