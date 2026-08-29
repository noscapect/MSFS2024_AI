using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot;
using Msfs2024Ai.Copilot.Automation;
using Msfs2024Ai.Copilot.Domain;

namespace Copilot.Tests;

[TestClass]
public sealed class PendingAircraftVerificationStateTests
{
    private static readonly DateTime Now = new(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void NewState_HasNoPendingVerifications()
    {
        var pending = new PendingAircraftVerificationState();

        Assert.IsNull(pending.ExternalPower);
        Assert.IsNull(pending.Beacon);
        Assert.IsNull(pending.NavLogoSelector);
        Assert.IsNull(pending.Battery);
        Assert.IsNull(pending.NativeAction);
    }

    [TestMethod]
    public void ExternalPowerAndBeacon_UseReadbackAndExactDeadlineBoundary()
    {
        var pending = new PendingAircraftVerificationState();
        pending.BeginExternalPower(true, Now.AddSeconds(5));

        Assert.AreEqual(PendingVerificationStatus.Waiting,
            pending.EvaluateExternalPower(new AircraftState { ExternalPowerOn = false }, Now.AddSeconds(4.999)).Status);
        Assert.AreEqual(PendingVerificationStatus.TimedOut,
            pending.EvaluateExternalPower(new AircraftState { ExternalPowerOn = false }, Now.AddSeconds(5)).Status);

        pending.BeginBeacon(true, Now.AddSeconds(5));
        Assert.AreEqual(PendingVerificationStatus.Verified,
            pending.EvaluateBeacon(new AircraftState { BeaconOn = true }, Now).Status);
        Assert.IsNull(pending.Beacon);
    }

    [TestMethod]
    public void NavLogoAndBattery_UseExistingSelectorAndBatteryMappings()
    {
        var pending = new PendingAircraftVerificationState();
        pending.BeginNavLogo(2, Now.AddSeconds(5));
        Assert.AreEqual(PendingVerificationStatus.Waiting,
            pending.EvaluateNavLogo(new AircraftState { NavLogoSelectorPosition = 1 }, Now).Status);
        Assert.AreEqual(PendingVerificationStatus.Verified,
            pending.EvaluateNavLogo(new AircraftState { NavLogoSelectorPosition = 2 }, Now).Status);

        pending.BeginBattery(1, true, Now.AddSeconds(5));
        Assert.AreEqual(PendingVerificationStatus.Waiting,
            pending.EvaluateBattery(new AircraftState { Battery1On = false, Battery2On = true }, Now).Status);
        pending.BeginBattery(2, true, Now.AddSeconds(5));
        Assert.AreEqual(PendingVerificationStatus.Waiting,
            pending.EvaluateBattery(new AircraftState { Battery1On = true, Battery2On = false }, Now).Status);
        Assert.AreEqual(PendingVerificationStatus.Verified,
            pending.EvaluateBattery(new AircraftState { Battery1On = false, Battery2On = true }, Now).Status);
    }

    [TestMethod]
    public void NativeAction_ExposesMetadataAndUsesExactTimeoutBoundary()
    {
        var pending = new PendingAircraftVerificationState();
        pending.BeginNativeAction("Test action", state => state.BeaconOn, true, "ON", Now.AddSeconds(8), false);

        var waiting = pending.EvaluateNativeAction(new AircraftState { BeaconOn = false }, Now);
        Assert.AreEqual(PendingVerificationStatus.Waiting, waiting.Status);
        Assert.AreEqual("Test action", waiting.Pending!.Name);
        Assert.AreEqual("ON", waiting.Pending.DesiredLabel);
        Assert.IsFalse(waiting.Pending.LogProgressToDashboard);
        Assert.AreEqual(PendingVerificationStatus.Verified,
            pending.EvaluateNativeAction(new AircraftState { BeaconOn = true }, Now).Status);

        pending.BeginNativeAction("Timeout", _ => false, false, "OFF", Now, true);
        Assert.AreEqual(PendingVerificationStatus.TimedOut,
            pending.EvaluateNativeAction(new AircraftState(), Now).Status);
    }

    [TestMethod]
    public void BeginningSameCategory_ReplacesPreviousDescriptor()
    {
        var pending = new PendingAircraftVerificationState();
        pending.BeginExternalPower(false, Now.AddSeconds(5));
        pending.BeginExternalPower(true, Now.AddSeconds(10));

        Assert.IsTrue(pending.ExternalPower!.DesiredOn);
        Assert.AreEqual(Now.AddSeconds(10), pending.ExternalPower.DeadlineUtc);
    }

    [TestMethod]
    public void Reset_ClearsEveryCategoryAndStateIsReusable()
    {
        var pending = new PendingAircraftVerificationState();
        pending.BeginExternalPower(true, Now);
        pending.BeginBeacon(true, Now);
        pending.BeginNavLogo(1, Now);
        pending.BeginBattery(1, true, Now);
        pending.BeginNativeAction("Test", _ => false, true, "ON", Now, true);

        pending.Reset();

        Assert.IsFalse(pending.HasPendingVerifications);
        pending.BeginBeacon(false, Now.AddSeconds(5));
        Assert.AreEqual(PendingVerificationStatus.Verified,
            pending.EvaluateBeacon(new AircraftState { BeaconOn = false }, Now).Status);
    }
}
