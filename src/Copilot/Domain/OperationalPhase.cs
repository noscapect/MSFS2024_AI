namespace Msfs2024Ai.Copilot.Domain;

internal enum OperationalPhase
{
    Unknown,
    ColdAndDark,
    CockpitPreparation,
    ReadyForBoarding,
    BeforeStart,
    EngineStart,
    AfterStart,
    TaxiOut,
    BeforeTakeoff,
    Takeoff,
    Climb,
    Cruise,
    DescentPreparation,
    Descent,
    Approach,
    Landing,
    AfterLanding,
    TaxiIn,
    Shutdown,
    Secured
}

internal static class OperationalPhaseDetector
{
    public static OperationalPhase Detect(AircraftState state)
    {
        if (!state.IsSupportedAircraft)
        {
            return OperationalPhase.Unknown;
        }

        if (state.OnGround && state.EnginesOff)
        {
            if (!state.Battery1On && !state.Battery2On && !state.ExternalPowerOn)
            {
                return OperationalPhase.ColdAndDark;
            }

            return OperationalPhase.CockpitPreparation;
        }

        if (state.OnGround && (state.Engine1Running || state.Engine2Running))
        {
            return state.GroundSpeedKnots > 0.5
                ? OperationalPhase.TaxiOut
                : OperationalPhase.AfterStart;
        }

        if (!state.OnGround && state.AltitudeAboveGroundFeet < 1500)
        {
            return OperationalPhase.Takeoff;
        }

        if (!state.OnGround
            && state.VerticalSpeedFeetPerMinute > 300
            && state.AltitudeAboveGroundFeet < 10000)
        {
            return OperationalPhase.Climb;
        }

        if (!state.OnGround
            && Math.Abs(state.VerticalSpeedFeetPerMinute) < 300
            && state.AltitudeAboveGroundFeet >= 10000)
        {
            return OperationalPhase.Cruise;
        }

        return OperationalPhase.Unknown;
    }
}
