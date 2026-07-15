using Msfs2024Ai.Copilot.Domain;

namespace Msfs2024Ai.Copilot.Settings;

public sealed class CopilotSettings
{
    public CrewRole PilotFlying { get; set; } = CrewRole.Captain;
    public AutomationPolicy AutomationPolicy { get; set; } = AutomationPolicy.AutomaticWhenSupported;
    public bool RequireManualStepConfirmation { get; set; } = true;
    public bool EnableStandardCallouts { get; set; } = true;
    public bool AutoAdvanceObservedSteps { get; set; } = true;
    public int TransitionAltitudeFeet { get; set; } = 5000;
    public int TakeoffV1SpeedKnots { get; set; } = 140;
    public int TakeoffRotateSpeedKnots { get; set; } = 143;
    public int ApproachFlaps1DistanceNm { get; set; } = 15;
    public int ApproachFlaps1AltitudeFeet { get; set; } = 10000;
    public int ApproachFlaps1SpeedKnots { get; set; } = 230;
    public int ApproachFlaps2DistanceNm { get; set; } = 10;
    public int ApproachFlaps2AltitudeAglFeet { get; set; } = 4000;
    public int ApproachFlaps2SpeedKnots { get; set; } = 200;
    public int ApproachGearDistanceNm { get; set; } = 7;
    public int ApproachGearAltitudeAglFeet { get; set; } = 2500;
    public int ApproachGearSpeedKnots { get; set; } = 210;
    public int ApproachLandingConfigDistanceNm { get; set; } = 5;
    public int ApproachLandingConfigAltitudeAglFeet { get; set; } = 1800;
    public int ApproachLandingConfigSpeedKnots { get; set; } = 185;
    public List<AircraftApproachOverride> AircraftApproachOverrides { get; set; } = new();
    public bool AutoChainEarlierFlows { get; set; }
    public bool AutoChainFlow6To7 { get; set; } = true;
    public bool AutoChainFlow10To11 { get; set; } = true;
    public bool AutoChainFlow11To12 { get; set; } = true;
    public string SimBriefPilotId { get; set; } = "";
    public string SimBriefUsername { get; set; } = "";
    public bool SimBriefAutoImportOnNewFlight { get; set; }
}
