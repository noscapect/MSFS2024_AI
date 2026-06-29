namespace Msfs2024Ai.Copilot;

internal sealed class AircraftState
{
    public string Title { get; set; } = "";
    public bool OnGround { get; set; }
    public double GroundSpeedKnots { get; set; }
    public bool Engine1Running { get; set; }
    public bool Engine2Running { get; set; }
    public bool Engine1StarterActive { get; set; }
    public bool Engine2StarterActive { get; set; }
    public double Engine1N1Percent { get; set; }
    public double Engine2N1Percent { get; set; }
    public double Engine1EgtCelsius { get; set; }
    public double Engine2EgtCelsius { get; set; }
    public double Engine1FuelFlowPph { get; set; }
    public double Engine2FuelFlowPph { get; set; }
    public bool Battery1On { get; set; }
    public bool Battery2On { get; set; }
    public bool ExternalPowerAvailable { get; set; }
    public bool ExternalPowerOn { get; set; }
    public bool CockpitDisplaysReady { get; set; }
    public bool ParkingBrakeSet { get; set; }
    public bool BeaconOn { get; set; }
    public bool NavigationLightsOn { get; set; }
    public bool LogoLightsOn { get; set; }
    public double? NavLogoSelectorPosition { get; set; }
    public double ApuRpmPercent { get; set; }
    public double ApuStarterPercent { get; set; }
    public bool ApuMasterSwitchOn { get; set; }
    public bool ApuAvailable { get; set; }
    public bool ApuStartButtonOn { get; set; }
    public bool ApuBleedOn { get; set; }
    public double ApuFlapPercent { get; set; }
    public bool ApuGeneratorActive { get; set; }
    public bool ApuGeneratorSwitchOn { get; set; }
    public double ApuVolts { get; set; }
    public bool FuelPumpsConfigured { get; set; }
    public double FuelPump1State { get; set; }
    public double FuelPump2State { get; set; }
    public double FuelPump3State { get; set; }
    public double FuelPump4State { get; set; }
    public double FuelPump5State { get; set; }
    public double FuelPump6State { get; set; }
    public double AltitudeAboveGroundFeet { get; set; }
    public double IndicatedAltitudeFeet { get; set; }
    public int TransitionAltitudeFeet { get; set; }
    public bool CaptainAltimeterStandard { get; set; }
    public bool FirstOfficerAltimeterStandard { get; set; }
    public double IndicatedAirspeedKnots { get; set; }
    public int TakeoffV1SpeedKnots { get; set; }
    public int TakeoffRotateSpeedKnots { get; set; }
    public double? ApproachDistanceToTouchdownNm { get; set; }
    public string ApproachDistanceSource { get; set; } = "";
    public int ApproachFlaps1DistanceNm { get; set; } = 15;
    public int ApproachFlaps1AltitudeFeet { get; set; } = 10000;
    public int ApproachFlaps1SpeedKnots { get; set; } = 220;
    public int ApproachFlaps2DistanceNm { get; set; } = 10;
    public int ApproachFlaps2AltitudeAglFeet { get; set; } = 4000;
    public int ApproachFlaps2SpeedKnots { get; set; } = 200;
    public int ApproachGearDistanceNm { get; set; } = 7;
    public int ApproachGearAltitudeAglFeet { get; set; } = 2500;
    public int ApproachGearSpeedKnots { get; set; } = 210;
    public int ApproachLandingConfigDistanceNm { get; set; } = 5;
    public int ApproachLandingConfigAltitudeAglFeet { get; set; } = 1800;
    public int ApproachLandingConfigSpeedKnots { get; set; } = 185;
    public double VerticalSpeedFeetPerMinute { get; set; }
    public double GForce { get; set; }
    public double RadioHeightFeet { get; set; }
    public double DecisionHeightFeet { get; set; }
    public bool Engine1ReverseEngaged { get; set; }
    public bool Engine2ReverseEngaged { get; set; }
    public bool AutobrakesActive { get; set; }
    public double LeftSpoilerPositionPercent { get; set; }
    public double RightSpoilerPositionPercent { get; set; }
    public double FlapsHandleIndex { get; set; }
    public double LeftFlapPositionPercent { get; set; }
    public double RightFlapPositionPercent { get; set; }
    public bool FlapReadbackSane { get; set; } = true;
    public IReadOnlyList<string> TelemetryIssues { get; set; } =
        Array.Empty<string>();
    public bool GroundSpoilersArmed { get; set; }
    public double? AutobrakeLevel { get; set; }
    public double? WeatherRadarPwsSelectorPosition { get; set; }
    public double? NoseLightSelectorPosition { get; set; }
    public double? LeftLandingLightSelectorPosition { get; set; }
    public double? RightLandingLightSelectorPosition { get; set; }
    public bool GearHandleDown { get; set; }
    public double PitchDegrees { get; set; }
    public bool AutopilotMasterOn { get; set; }
    public double Adirs1SelectorState { get; set; }
    public double Adirs2SelectorState { get; set; }
    public double Adirs3SelectorState { get; set; }
    public bool AdirsOnBattery { get; set; }
    public bool CrewOxygenOn { get; set; }
    public double? StrobeSelectorPosition { get; set; }
    public bool ApuFireTestActive { get; set; }
    public bool ApuFireWarningLit { get; set; }
    public bool ApuFireSoundActive { get; set; }
    public bool Engine1FireTestActive { get; set; }
    public bool Engine1FireWarningLit { get; set; }
    public bool Engine1FireSoundActive { get; set; }
    public bool Engine2FireTestActive { get; set; }
    public bool Engine2FireWarningLit { get; set; }
    public bool Engine2FireSoundActive { get; set; }
    public bool ApuFireTestCompleted { get; set; }
    public bool Engine1FireTestCompleted { get; set; }
    public bool Engine2FireTestCompleted { get; set; }
    public double? SeatbeltSelectorPosition { get; set; }
    public bool SeatbeltSignsOn { get; set; }
    public double? NoSmokingSelectorPosition { get; set; }
    public bool NoSmokingSignsOn { get; set; }
    public double? EmergencyExitSelectorPosition { get; set; }
    public double? TransponderAtcState { get; set; }
    public bool? TcasAltitudeReportingOn { get; set; }
    public double? TcasMode { get; set; }
    public double? TransponderModeSelectorPosition { get; set; }
    public bool TransponderStandby { get; set; }
    public bool AtcClearedIfr { get; set; }
    public IReadOnlyList<AircraftExitState> Exits { get; set; } =
        Array.Empty<AircraftExitState>();

    public bool RequiredDoorsClosed =>
        Exits.Any(exit => exit.IsRequiredDoor)
        && Exits.Where(exit => exit.IsRequiredDoor).All(exit => exit.OpenPercent <= 0.5);
    public bool AnyRequiredDoorOpen =>
        Exits.Any(exit => exit.IsRequiredDoor && exit.OpenPercent > 0.5);

    public bool IsA320NeoV2 =>
        string.Equals(Title, "A320neo V2", StringComparison.OrdinalIgnoreCase);

    public bool IsFlyByWireA320Neo =>
        Title.IndexOf("A32NX", StringComparison.OrdinalIgnoreCase) >= 0
        || Title.IndexOf("FlyByWire", StringComparison.OrdinalIgnoreCase) >= 0;

    public bool IsSupportedA320 =>
        IsA320NeoV2 || IsFlyByWireA320Neo;

    public bool EnginesOff => !Engine1Running && !Engine2Running;
    public bool Engine1StartStabilized =>
        Engine1Running
        && !Engine1StarterActive
        && Engine1N1Percent >= 15
        && Engine1FuelFlowPph > 0;
    public bool Engine2StartStabilized =>
        Engine2Running
        && !Engine2StarterActive
        && Engine2N1Percent >= 15
        && Engine2FuelFlowPph > 0;
    public bool ReverseThrustEngaged =>
        Engine1ReverseEngaged || Engine2ReverseEngaged;
    public bool GroundSpoilersDeployed =>
        LeftSpoilerPositionPercent > 5
        && RightSpoilerPositionPercent > 5;

    public bool AllFuelPumpsOff =>
        FuelPump1State < 0.5
        && FuelPump2State < 0.5
        && FuelPump3State < 0.5
        && FuelPump4State < 0.5
        && FuelPump5State < 0.5
        && FuelPump6State < 0.5;

    public bool AllAdirsNav =>
        Math.Abs(Adirs1SelectorState - 1) < 0.1
        && Math.Abs(Adirs2SelectorState - 1) < 0.1
        && Math.Abs(Adirs3SelectorState - 1) < 0.1;

    public bool FlapsAtDetent(int detent) =>
        Math.Abs(FlapsHandleIndex - detent) < 0.1;
}

internal sealed class AircraftExitState
{
    public AircraftExitState(
        int index,
        double type,
        double openPercent,
        double positionX,
        double positionY,
        double positionZ)
    {
        Index = index;
        Type = type;
        OpenPercent = openPercent;
        PositionX = positionX;
        PositionY = positionY;
        PositionZ = positionZ;
    }

    public int Index { get; }
    public double Type { get; }
    public double OpenPercent { get; }
    public double PositionX { get; }
    public double PositionY { get; }
    public double PositionZ { get; }
    public bool IsConfigured =>
        Math.Abs(PositionX) >= 0.01
        || Math.Abs(PositionY) >= 0.01
        || Math.Abs(PositionZ) >= 0.01;
    public bool IsRequiredDoor => IsConfigured && Type is >= 0 and <= 1;
}
