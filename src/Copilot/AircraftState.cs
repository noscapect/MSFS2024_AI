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
    public double? Engine1StartSwitchPosition { get; set; }
    public double? Engine2StartSwitchPosition { get; set; }
    public double Engine1N1Percent { get; set; }
    public double Engine2N1Percent { get; set; }
    public double Engine1EgtCelsius { get; set; }
    public double Engine2EgtCelsius { get; set; }
    public double Engine1FuelFlowPph { get; set; }
    public double Engine2FuelFlowPph { get; set; }
    public double? EngineModeSelectorPosition { get; set; }
    public double? FbwEngine1State { get; set; }
    public double? FbwEngine2State { get; set; }
    public bool Battery1On { get; set; }
    public bool Battery2On { get; set; }
    public double Battery1Voltage { get; set; }
    public double Battery2Voltage { get; set; }
    public bool ExternalPowerAvailable { get; set; }
    public bool ExternalPowerOn { get; set; }
    public bool ExternalPowerAvailableUnindexed { get; set; }
    public bool ExternalPowerOnUnindexed { get; set; }
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
    public bool ApuSpoolingOrAvailable { get; set; }
    public bool ApuBleedOn { get; set; }
    public bool ApuBleedWarmupComplete { get; set; } = true;
    public double? LeftPackSwitchPosition { get; set; }
    public double? RightPackSwitchPosition { get; set; }
    public double? IsolationValvePosition { get; set; }
    public double LeftDuctPressurePsi { get; set; }
    public double RightDuctPressurePsi { get; set; }
    public double ApuFlapPercent { get; set; }
    public bool ApuGeneratorActive { get; set; }
    public bool ApuGeneratorSwitchOn { get; set; }
    public bool ApuGeneratorPowerEstablished { get; set; }
    public bool EngineGeneratorsOn { get; set; }
    public bool ApuGenOffBus { get; set; }
    public bool AcTransferBus1Powered { get; set; }
    public bool AcTransferBus2Powered { get; set; }
    public bool TransferBus1Off { get; set; }
    public bool TransferBus2Off { get; set; }
    public bool BoeingElectricHydraulicPumpsOn { get; set; }
    public bool BoeingElectricHydraulicPump1On { get; set; }
    public bool BoeingElectricHydraulicPump2On { get; set; }
    public bool BoeingElectricHydraulicPump1LowPressure { get; set; }
    public bool BoeingElectricHydraulicPump2LowPressure { get; set; }
    public double ApuVolts { get; set; }
    public bool FuelPumpsConfigured { get; set; }
    public double CenterFuelQuantityPounds { get; set; }
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
    public int EffectiveApproachFlaps1SpeedKnots =>
        IsIniBuildsA321Lr ? 230 : ApproachFlaps1SpeedKnots;
    public int EffectiveApproachFlaps2SpeedKnots =>
        IsIniBuildsA321Lr ? 215 : ApproachFlaps2SpeedKnots;
    public int EffectiveApproachFlaps3SpeedKnots =>
        IsIniBuildsA321Lr ? 195 : ApproachLandingConfigSpeedKnots;
    public int EffectiveApproachFlapsFullSpeedKnots =>
        IsIniBuildsA321Lr ? 186 : ApproachLandingConfigSpeedKnots;
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
    public int? BoeingTakeoffFlaps { get; set; }
    public int? BoeingLandingFlaps { get; set; }
    public int? BoeingLandingVrefKnots { get; set; }
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
    public bool RunwayTurnoffLightsOn { get; set; }
    public double? GearHandlePosition { get; set; }
    public bool GearHandleDown { get; set; }
    public double PitchDegrees { get; set; }
    public bool AutopilotMasterOn { get; set; }
    public bool AutopilotApproachHoldOn { get; set; }
    public bool AutopilotGlideslopeHoldOn { get; set; }
    public bool Nav1HasLocalizer { get; set; }
    public bool Nav1HasGlideslope { get; set; }
    public bool Nav2HasLocalizer { get; set; }
    public bool Nav2HasGlideslope { get; set; }
    public double Nav1ActiveFrequencyMhz { get; set; }
    public double Nav2ActiveFrequencyMhz { get; set; }
    public double Nav1CourseDegrees { get; set; }
    public double Nav2CourseDegrees { get; set; }
    public double Adirs1SelectorState { get; set; }
    public double Adirs2SelectorState { get; set; }
    public double Adirs3SelectorState { get; set; }
    public bool AdirsOnBattery { get; set; }
    public bool IrsLeftAlignLightOn { get; set; }
    public bool IrsRightAlignLightOn { get; set; }
    public bool IrsLeftOnDcLightOn { get; set; }
    public bool IrsRightOnDcLightOn { get; set; }
    public bool IrsLeftFault { get; set; }
    public bool IrsRightFault { get; set; }
    public bool IrsAligned { get; set; } = true;
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

    public bool IsIniBuildsA321Lr =>
        string.Equals(Title, "A321", StringComparison.OrdinalIgnoreCase)
        || Title.IndexOf("A321", StringComparison.OrdinalIgnoreCase) >= 0;

    public bool IsIniBuildsA320Family =>
        IsA320NeoV2 || IsIniBuildsA321Lr;

    public bool IsFlyByWireA320Neo =>
        Title.IndexOf("A32NX", StringComparison.OrdinalIgnoreCase) >= 0
        || Title.IndexOf("FlyByWire", StringComparison.OrdinalIgnoreCase) >= 0;

    public bool IsSupportedA320 =>
        IsIniBuildsA320Family || IsFlyByWireA320Neo;

    public bool IsPmdg737 =>
        Title.IndexOf("PMDG", StringComparison.OrdinalIgnoreCase) >= 0
        && Title.IndexOf("737", StringComparison.OrdinalIgnoreCase) >= 0
        || Title.IndexOf("737-800", StringComparison.OrdinalIgnoreCase) >= 0
        || Title.IndexOf("738", StringComparison.OrdinalIgnoreCase) >= 0;

    public bool IsPmdg737800 =>
        IsPmdg737
        && (Title.IndexOf("737-800", StringComparison.OrdinalIgnoreCase) >= 0
            || Title.IndexOf("738", StringComparison.OrdinalIgnoreCase) >= 0);

    public bool IsSupportedBoeing737 => IsPmdg737800;

    public bool IsSupportedAircraft =>
        IsSupportedA320 || IsSupportedBoeing737;

    public string AircraftFamilyLabel =>
        IsSupportedBoeing737
            ? "PMDG 737-800"
            : IsSupportedA320
                ? "Airbus A320-family"
                : "Unsupported aircraft";

    public bool EnginesOff => !Engine1Running && !Engine2Running;
    public bool BoeingAutolandRadioSetupLooksReady =>
        IsSupportedBoeing737
        && Nav1HasLocalizer
        && Nav1HasGlideslope
        && Nav2HasLocalizer
        && Nav2HasGlideslope
        && Math.Abs(Nav1ActiveFrequencyMhz - Nav2ActiveFrequencyMhz) < 0.01
        && CourseDifferenceDegrees(Nav1CourseDegrees, Nav2CourseDegrees) <= 1;

    public bool BoeingAutolandApproachModesLookReady =>
        IsSupportedBoeing737
        && AutopilotMasterOn
        && AutopilotApproachHoldOn
        && AutopilotGlideslopeHoldOn;

    public string BoeingAutolandReadinessSummary
    {
        get
        {
            if (!IsSupportedBoeing737)
            {
                return "Autoland: not a Boeing 737 profile";
            }

            var nav = Math.Abs(Nav1ActiveFrequencyMhz - Nav2ActiveFrequencyMhz) < 0.01
                ? "NAV radios match"
                : $"NAV mismatch {Nav1ActiveFrequencyMhz:F2}/{Nav2ActiveFrequencyMhz:F2}";
            var courseDelta = CourseDifferenceDegrees(Nav1CourseDegrees, Nav2CourseDegrees);
            var crs = courseDelta <= 1
                ? "courses match"
                : $"course mismatch {Nav1CourseDegrees:F0}/{Nav2CourseDegrees:F0}";
            var loc = Nav1HasLocalizer && Nav2HasLocalizer
                ? "LOC valid both"
                : $"LOC L/R {(Nav1HasLocalizer ? "OK" : "NO")}/{(Nav2HasLocalizer ? "OK" : "NO")}";
            var gs = Nav1HasGlideslope && Nav2HasGlideslope
                ? "GS valid both"
                : $"GS L/R {(Nav1HasGlideslope ? "OK" : "NO")}/{(Nav2HasGlideslope ? "OK" : "NO")}";
            var app = AutopilotApproachHoldOn ? "APP active" : "APP not active";
            var gsMode = AutopilotGlideslopeHoldOn ? "GS mode active" : "GS mode not active";

            return $"Autoland: {nav}, {crs}, {loc}, {gs}, {app}, {gsMode}";
        }
    }

    private static double CourseDifferenceDegrees(double left, double right)
    {
        var diff = Math.Abs((left % 360 + 360) % 360 - (right % 360 + 360) % 360);
        return diff > 180 ? 360 - diff : diff;
    }

    public bool EngineModeIgnStart =>
        EngineModeSelectorPosition.HasValue
        && Math.Abs(EngineModeSelectorPosition.Value - 2) < 0.1;
    public bool EngineModeNormal =>
        EngineModeSelectorPosition.HasValue
        && Math.Abs(EngineModeSelectorPosition.Value - 1) < 0.1;
    public bool Engine1StartSwitchGround =>
        Engine1StartSwitchPosition.HasValue
        && Math.Abs(Engine1StartSwitchPosition.Value) < 0.1;
    public bool Engine2StartSwitchGround =>
        Engine2StartSwitchPosition.HasValue
        && Math.Abs(Engine2StartSwitchPosition.Value) < 0.1;
    public bool EngineStartSwitchesContinuous =>
        Engine1StartSwitchPosition.HasValue
        && Engine2StartSwitchPosition.HasValue
        && Math.Abs(Engine1StartSwitchPosition.Value - 2) < 0.1
        && Math.Abs(Engine2StartSwitchPosition.Value - 2) < 0.1;
    public bool EngineGeneratorPowerEstablished =>
        !IsPmdg737
        || Engine1Running
        && Engine2Running
        && EngineGeneratorsOn
        && !TransferBus1Off
        && !TransferBus2Off
        && AcTransferBus1Powered
        && AcTransferBus2Powered;
    public bool PacksOffForEngineStart =>
        LeftPackSwitchPosition.HasValue
        && RightPackSwitchPosition.HasValue
        && Math.Abs(LeftPackSwitchPosition.Value) < 0.1
        && Math.Abs(RightPackSwitchPosition.Value) < 0.1;
    public bool PacksAuto =>
        LeftPackSwitchPosition.HasValue
        && RightPackSwitchPosition.HasValue
        && Math.Abs(LeftPackSwitchPosition.Value - 1) < 0.1
        && Math.Abs(RightPackSwitchPosition.Value - 1) < 0.1;
    public bool IsolationValveOpen =>
        IsolationValvePosition.HasValue
        && Math.Abs(IsolationValvePosition.Value - 2) < 0.1;
    public bool IsolationValveAuto =>
        IsolationValvePosition.HasValue
        && Math.Abs(IsolationValvePosition.Value - 1) < 0.1;
    public bool EngineStartAirAvailable =>
        !IsPmdg737
        || ApuBleedOn
        && IsolationValveOpen
        && LeftDuctPressurePsi >= 10
        && RightDuctPressurePsi >= 10;
    public bool BoeingTakeoffFlapsSet =>
        !IsPmdg737
        || FlapsAtBoeingSetting(BoeingTakeoffFlaps.GetValueOrDefault(5));
    public bool BoeingLandingFlapsSet =>
        !IsPmdg737
        || FlapsAtBoeingSetting(BoeingLandingFlaps.GetValueOrDefault(30));
    public bool BoeingLandingDataSet =>
        !IsPmdg737
        || BoeingLandingFlaps.GetValueOrDefault() > 0
        && BoeingLandingVrefKnots.GetValueOrDefault() > 0;
    public int EffectiveBoeingApproachTargetSpeedKnots =>
        IsPmdg737 && BoeingLandingVrefKnots.GetValueOrDefault() > 0
            ? BoeingLandingVrefKnots.GetValueOrDefault() + 5
            : ApproachLandingConfigSpeedKnots;
    public bool BoeingLandingSpeedStable =>
        !IsPmdg737
        || IndicatedAirspeedKnots > 0
        && IndicatedAirspeedKnots <= EffectiveBoeingApproachTargetSpeedKnots + 10;
    public bool BoeingApproachStable =>
        !IsPmdg737
        || BoeingLandingDataSet
        && GearHandleDown
        && BoeingLandingFlapsSet
        && GroundSpoilersArmed
        && BoeingLandingSpeedStable;
    public bool GearHandleUp =>
        GearHandlePosition.HasValue
            ? GearHandlePosition.Value <= 0.1
            : !GearHandleDown;
    public bool Engine1FuelFlowDetected =>
        Engine1FuelFlowPph > 0
        || IsPmdg737
        && Engine1Running
        || IsFlyByWireA320Neo
        && (Engine1StarterActive || Engine1Running)
        && Engine1N1Percent >= 3;
    public bool Engine2FuelFlowDetected =>
        Engine2FuelFlowPph > 0
        || IsPmdg737
        && Engine2Running
        || IsFlyByWireA320Neo
        && (Engine2StarterActive || Engine2Running)
        && Engine2N1Percent >= 3;
    public bool Engine1StartStabilized =>
        IsFlyByWireA320Neo && FbwEngine1State == 1
        || Engine1Running
        && !Engine1StarterActive
        && Engine1N1Percent >= 15
        && Engine1FuelFlowDetected;
    public bool Engine2StartStabilized =>
        IsFlyByWireA320Neo && FbwEngine2State == 1
        || Engine2Running
        && !Engine2StarterActive
        && Engine2N1Percent >= 15
        && Engine2FuelFlowDetected;
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

    public bool PmdgIrsSelectorsNav =>
        !IsSupportedBoeing737
        || Math.Abs(Adirs1SelectorState - 2) < 0.1
        && Math.Abs(Adirs2SelectorState - 2) < 0.1;

    public bool PmdgIrsOnDcExtinguished =>
        !IsSupportedBoeing737
        || !IrsLeftOnDcLightOn
        && !IrsRightOnDcLightOn;

    public bool PmdgIrsReady =>
        !IsSupportedBoeing737
        || PmdgIrsSelectorsNav
        && IrsAligned
        && PmdgIrsOnDcExtinguished
        && !IrsLeftAlignLightOn
        && !IrsRightAlignLightOn
        && !IrsLeftFault
        && !IrsRightFault;

    public bool FlapsAtDetent(int detent)
    {
        if (IsSupportedBoeing737)
        {
            return Boeing737FlapsAtDetent(detent);
        }

        if (IsIniBuildsA321Lr)
        {
            return A321FlapsAtDetent(detent);
        }

        return LegacyA320FlapsAtDetent(detent);
    }

    public bool BoeingFlapsAtSetting(int flaps) =>
        IsSupportedBoeing737
            ? FlapsAtBoeingSetting(flaps)
            : FlapsAtDetent(flaps);

    private bool LegacyA320FlapsAtDetent(int detent)
    {
        if (Math.Abs(FlapsHandleIndex - detent) < 0.1)
        {
            return true;
        }

        // The FlyByWire A32NX can briefly report inconsistent handle values
        // while the physical flap surfaces have already reached the requested
        // position. In the landing test it reported handle detent 1 with the
        // surfaces clean, and also used 5 for FULL while our flow model uses 4.
        // When the handle readback is flagged as suspicious, use the surface
        // position as the truth source instead of blocking the procedure.
        if (!FlapReadbackSane || detent == 4 && FlapsHandleIndex > 4.1)
        {
            return FlapSurfacesMatchDetent(detent);
        }

        return false;
    }

    private bool A321FlapsAtDetent(int detent)
    {
        if (Math.Abs(FlapsHandleIndex - detent) < 0.1)
        {
            return true;
        }

        if (!FlapReadbackSane)
        {
            return FlapSurfacesMatchDetent(detent);
        }

        return false;
    }

    private bool Boeing737FlapsAtDetent(int detent)
    {
        if (detent == 0)
        {
            return FlapsHandleIndex <= 0.1 || FlapSurfacesMatchDetent(0);
        }

        var handleIndex = (int)Math.Round(FlapsHandleIndex);
        return detent switch
        {
            1 => handleIndex >= 1 || FlapSurfacesMatchBoeingFlaps(1),
            2 => handleIndex >= 3 || FlapSurfacesMatchBoeingFlaps(5),
            4 => handleIndex >= 7 || FlapSurfacesMatchBoeingFlaps(30),
            _ => Math.Abs(FlapsHandleIndex - detent) < 0.1
        };
    }

    private bool FlapSurfacesMatchBoeingFlaps(int flaps)
    {
        var maximumSurface =
            Math.Max(LeftFlapPositionPercent, RightFlapPositionPercent);
        return flaps switch
        {
            1 => maximumSurface is >= 1 and <= 10,
            2 => maximumSurface is >= 5 and <= 18,
            5 => maximumSurface is >= 10 and <= 30,
            10 => maximumSurface is >= 20 and <= 45,
            15 => maximumSurface is >= 30 and <= 55,
            25 => maximumSurface is >= 50 and <= 75,
            30 => maximumSurface >= 65,
            40 => maximumSurface >= 80,
            _ => false
        };
    }

    private bool FlapsAtBoeingSetting(int flaps)
    {
        return flaps switch
        {
            <= 0 => FlapsAtDetent(0),
            1 => Boeing737FlapsAtDetent(1),
            2 => Math.Round(FlapsHandleIndex) >= 2 || FlapSurfacesMatchBoeingFlaps(2),
            5 => Boeing737FlapsAtDetent(2),
            10 => Math.Round(FlapsHandleIndex) >= 4 || FlapSurfacesMatchBoeingFlaps(10),
            15 => Math.Round(FlapsHandleIndex) >= 5 || FlapSurfacesMatchBoeingFlaps(15),
            25 => Math.Round(FlapsHandleIndex) >= 6 || FlapSurfacesMatchBoeingFlaps(25),
            30 => Boeing737FlapsAtDetent(4),
            40 => Math.Round(FlapsHandleIndex) >= 8 || FlapSurfacesMatchBoeingFlaps(40),
            _ => FlapsHandleIndex > 0
        };
    }

    private bool FlapSurfacesMatchDetent(int detent)
    {
        var maximumSurface =
            Math.Max(LeftFlapPositionPercent, RightFlapPositionPercent);
        return detent switch
        {
            0 => maximumSurface <= 1,
            1 => maximumSurface is >= 5 and <= 18,
            2 => maximumSurface is >= 18 and <= 35,
            3 => maximumSurface is >= 35 and <= 75,
            4 => maximumSurface >= 90,
            _ => false
        };
    }
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
