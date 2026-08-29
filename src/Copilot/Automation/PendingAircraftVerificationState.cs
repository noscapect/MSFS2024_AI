using Msfs2024Ai.Copilot.Domain;

namespace Msfs2024Ai.Copilot.Automation;

internal enum PendingVerificationStatus
{
    None,
    Waiting,
    Verified,
    TimedOut
}

internal interface IPendingVerification
{
    DateTime DeadlineUtc { get; }
}

internal sealed class PendingVerificationResult<T> where T : class, IPendingVerification
{
    public PendingVerificationResult(PendingVerificationStatus status, T? pending)
    {
        Status = status;
        Pending = pending;
    }

    public PendingVerificationStatus Status { get; }
    public T? Pending { get; }
}

internal sealed class PendingExternalPowerProcedure : IPendingVerification
{
    public PendingExternalPowerProcedure(bool desiredOn, DateTime deadlineUtc)
    {
        DesiredOn = desiredOn;
        DeadlineUtc = deadlineUtc;
    }

    public bool DesiredOn { get; }
    public DateTime DeadlineUtc { get; }
}

internal sealed class PendingBeaconProcedure : IPendingVerification
{
    public PendingBeaconProcedure(bool desiredOn, DateTime deadlineUtc)
    {
        DesiredOn = desiredOn;
        DeadlineUtc = deadlineUtc;
    }

    public bool DesiredOn { get; }
    public DateTime DeadlineUtc { get; }
}

internal sealed class PendingNavLogoSelectorProcedure : IPendingVerification
{
    public PendingNavLogoSelectorProcedure(int desiredPosition, DateTime deadlineUtc)
    {
        DesiredPosition = desiredPosition;
        DeadlineUtc = deadlineUtc;
    }

    public int DesiredPosition { get; }
    public DateTime DeadlineUtc { get; }
}

internal sealed class PendingBatteryProcedure : IPendingVerification
{
    public PendingBatteryProcedure(int batteryNumber, bool desiredOn, DateTime deadlineUtc)
    {
        BatteryNumber = batteryNumber;
        DesiredOn = desiredOn;
        DeadlineUtc = deadlineUtc;
    }

    public int BatteryNumber { get; }
    public bool DesiredOn { get; }
    public DateTime DeadlineUtc { get; }
}

internal sealed class PendingNativeAction : IPendingVerification
{
    public PendingNativeAction(
        string name,
        Func<AircraftState, bool> verify,
        bool desiredOn,
        string desiredLabel,
        DateTime deadlineUtc,
        bool logProgressToDashboard)
    {
        Name = name;
        Verify = verify;
        DesiredOn = desiredOn;
        DesiredLabel = desiredLabel;
        DeadlineUtc = deadlineUtc;
        LogProgressToDashboard = logProgressToDashboard;
    }

    public string Name { get; }
    public Func<AircraftState, bool> Verify { get; }
    public bool DesiredOn { get; }
    public string DesiredLabel { get; }
    public DateTime DeadlineUtc { get; }
    public bool LogProgressToDashboard { get; }
}

internal sealed class PendingAircraftVerificationState
{
    public PendingExternalPowerProcedure? ExternalPower { get; private set; }
    public PendingBeaconProcedure? Beacon { get; private set; }
    public PendingNavLogoSelectorProcedure? NavLogoSelector { get; private set; }
    public PendingBatteryProcedure? Battery { get; private set; }
    public PendingNativeAction? NativeAction { get; private set; }

    public bool HasPendingVerifications =>
        ExternalPower != null || Beacon != null || NavLogoSelector != null
        || Battery != null || NativeAction != null;

    public bool NativeActionPending => NativeAction != null;

    public void BeginExternalPower(bool desiredOn, DateTime deadlineUtc) =>
        ExternalPower = new PendingExternalPowerProcedure(desiredOn, deadlineUtc);

    public void BeginBeacon(bool desiredOn, DateTime deadlineUtc) =>
        Beacon = new PendingBeaconProcedure(desiredOn, deadlineUtc);

    public void BeginNavLogo(int desiredPosition, DateTime deadlineUtc) =>
        NavLogoSelector = new PendingNavLogoSelectorProcedure(desiredPosition, deadlineUtc);

    public void BeginBattery(int batteryNumber, bool desiredOn, DateTime deadlineUtc) =>
        Battery = new PendingBatteryProcedure(batteryNumber, desiredOn, deadlineUtc);

    public void BeginNativeAction(
        string name,
        Func<AircraftState, bool> verify,
        bool desiredOn,
        string desiredLabel,
        DateTime deadlineUtc,
        bool logProgressToDashboard) =>
        NativeAction = new PendingNativeAction(
            name, verify, desiredOn, desiredLabel, deadlineUtc, logProgressToDashboard);

    public PendingVerificationResult<PendingExternalPowerProcedure> EvaluateExternalPower(
        AircraftState? state,
        DateTime utcNow) =>
        Evaluate(ExternalPower, state, utcNow,
            (pending, current) => current.ExternalPowerOn == pending.DesiredOn,
            () => ExternalPower = null);

    public PendingVerificationResult<PendingBeaconProcedure> EvaluateBeacon(
        AircraftState? state,
        DateTime utcNow) =>
        Evaluate(Beacon, state, utcNow,
            (pending, current) => current.BeaconOn == pending.DesiredOn,
            () => Beacon = null);

    public PendingVerificationResult<PendingNavLogoSelectorProcedure> EvaluateNavLogo(
        AircraftState? state,
        DateTime utcNow) =>
        Evaluate(NavLogoSelector, state, utcNow,
            (pending, current) =>
            {
                if (current.IsFlyByWireAirbus)
                {
                    var desiredOff = pending.DesiredPosition == 2;
                    if (desiredOff
                        ? !current.NavigationLightsOn && !current.LogoLightsOn
                        : current.NavigationLightsOn && current.LogoLightsOn)
                    {
                        return true;
                    }
                }

                return current.NavLogoSelectorPosition.HasValue
                       && Math.Abs(
                           current.NavLogoSelectorPosition.Value - pending.DesiredPosition) < 0.1;
            },
            () => NavLogoSelector = null);

    public PendingVerificationResult<PendingBatteryProcedure> EvaluateBattery(
        AircraftState? state,
        DateTime utcNow) =>
        Evaluate(Battery, state, utcNow,
            (pending, current) =>
                (pending.BatteryNumber == 1 ? current.Battery1On : current.Battery2On)
                == pending.DesiredOn,
            () => Battery = null);

    public PendingVerificationResult<PendingNativeAction> EvaluateNativeAction(
        AircraftState? state,
        DateTime utcNow) =>
        Evaluate(NativeAction, state, utcNow,
            (pending, current) => pending.Verify(current),
            () => NativeAction = null);

    public void Reset()
    {
        ExternalPower = null;
        Beacon = null;
        NavLogoSelector = null;
        Battery = null;
        NativeAction = null;
    }

    private static PendingVerificationResult<T> Evaluate<T>(
        T? pending,
        AircraftState? state,
        DateTime utcNow,
        Func<T, AircraftState, bool> isVerified,
        Action clear)
        where T : class, IPendingVerification
    {
        if (pending == null || state == null)
        {
            return new PendingVerificationResult<T>(PendingVerificationStatus.None, pending);
        }

        if (isVerified(pending, state))
        {
            clear();
            return new PendingVerificationResult<T>(PendingVerificationStatus.Verified, pending);
        }

        if (utcNow >= pending.DeadlineUtc)
        {
            clear();
            return new PendingVerificationResult<T>(PendingVerificationStatus.TimedOut, pending);
        }

        return new PendingVerificationResult<T>(PendingVerificationStatus.Waiting, pending);
    }
}
