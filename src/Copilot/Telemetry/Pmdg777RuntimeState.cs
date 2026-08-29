using Msfs2024Ai.Copilot.AircraftAdapters.Pmdg777;

namespace Msfs2024Ai.Copilot.Telemetry;

internal readonly struct Pmdg777RuntimeUpdate
{
    public Pmdg777RuntimeUpdate(bool accepted, bool becameDataReady)
    {
        Accepted = accepted;
        BecameDataReady = becameDataReady;
    }

    public bool Accepted { get; }
    public bool BecameDataReady { get; }
}

/// <summary>
/// Owns PMDG 777 SDK readbacks and transient observations. Command transport,
/// queue scheduling, and procedure orchestration remain in CopilotService.
/// </summary>
internal sealed class Pmdg777RuntimeState
{
    public Pmdg777SdkData? State { get; private set; }
    public Pmdg777Control ControlState { get; private set; }
    public bool DataReady { get; private set; }
    public bool ControlReady { get; private set; }
    public bool FireOverheatTestObserved { get; private set; }
    public bool FirstOfficerOxygenTestObserved { get; private set; }
    public DateTime? AdiruOffSinceUtc { get; private set; }
    public bool HasRawSnapshot => _rawSnapshot != null;

    private byte[]? _rawSnapshot;
    private string? _flowOneSignature;

    public Pmdg777RuntimeUpdate ApplyAircraftData(byte[]? data)
    {
        if (!Pmdg777SdkData.TryParse(data, out var state))
        {
            return new Pmdg777RuntimeUpdate(false, false);
        }

        State = state;
        var becameDataReady = !DataReady;
        DataReady = true;
        return new Pmdg777RuntimeUpdate(true, becameDataReady);
    }

    public bool ApplyControlData(Pmdg777Control control)
    {
        var becameReady = !ControlReady;
        ControlState = control;
        ControlReady = true;
        return becameReady;
    }

    public void RecordFireAndOxygenObservations(double fireOverheatTestSwitch, double oxygenTestSwitch)
    {
        FireOverheatTestObserved |= fireOverheatTestSwitch > 0.5;
        FirstOfficerOxygenTestObserved |= oxygenTestSwitch > 0.5;
    }

    public void SetPendingControl(Pmdg777Control control) => ControlState = control;

    public void ObserveAdiruState(DateTime utcNow)
    {
        if (State?.AdiruOn == true)
        {
            AdiruOffSinceUtc = null;
        }
        else if (State != null && !AdiruOffSinceUtc.HasValue)
        {
            AdiruOffSinceUtc = utcNow;
        }
    }

    public string? ObserveRawDataChanges(byte[]? data)
    {
        if (data == null || data.Length < Pmdg777ControlProfile.DataSize)
        {
            return null;
        }

        if (_rawSnapshot == null)
        {
            _rawSnapshot = (byte[])data.Clone();
            return "PMDG 777X raw-data change monitor initialized for Flow 1/2 validation.";
        }

        var changes = new List<string>();
        for (var offset = 0; offset < Pmdg777ControlProfile.DataSize; offset++)
        {
            if (_rawSnapshot[offset] != data[offset])
            {
                changes.Add($"{offset}:{_rawSnapshot[offset]}>{data[offset]}");
            }
        }

        if (changes.Count == 0)
        {
            return null;
        }

        Buffer.BlockCopy(data, 0, _rawSnapshot, 0, Pmdg777ControlProfile.DataSize);
        return $"PMDG 777X raw-data changes: {string.Join(", ", changes)}.";
    }

    public string? ObserveFlowOneDiagnostic(Pmdg777SdkData sdk, bool emergencyLightsGuardClosed)
    {
        var signature =
            $"BAT={OnOff(sdk.BatteryOn)} "
            + $"EXT_AVAIL={OnOff(sdk.PrimaryExternalPowerAvailable)}/{OnOff(sdk.SecondaryExternalPowerAvailable)} "
            + $"EXT_ON={OnOff(sdk.PrimaryExternalPowerOn)}/{OnOff(sdk.SecondaryExternalPowerOn)} "
            + $"HYD_SAFE={OnOff(sdk.CenterPrimaryPumpsOff && sdk.DemandPumpsOff)} "
            + $"WIPERS_OFF={OnOff(sdk.WipersOff)} GEAR_DOWN={OnOff(sdk.GearLeverDown)} "
            + $"ALT_FLAPS_OFF={OnOff(sdk.AlternateFlapsOff)} PARK_BRAKE={OnOff(sdk.ParkingBrakeSet)} "
            + $"RAW_GEAR={sdk.GearLeverRaw} RAW_ALT={sdk.AlternateFlapsArmRaw}/{sdk.AlternateFlapsControlRaw} "
            + $"RAW_PARK={sdk.ParkingBrakeRaw} "
            + $"NAV={OnOff(sdk.NavigationLightOn)} LOGO={OnOff(sdk.LogoLightOn)} "
            + $"PACKS_OFF={OnOff(sdk.PacksOff)} RECIRC_OFF={OnOff(sdk.RecirculationFansOff)} "
            + $"ADIRU={OnOff(sdk.AdiruOn)} IRS_ALIGNED={OnOff(sdk.IrsAligned)} EMER={sdk.EmergencyLightsSelector} "
            + $"EMER_GUARD={(emergencyLightsGuardClosed ? "CLOSED" : "OPEN")} "
            + $"FO_FD={OnOff(sdk.FirstOfficerFlightDirectorOn)} FO_SRC={OnOff(sdk.FirstOfficerSourcesNormal)} "
            + $"FO_DSP={OnOff(sdk.FirstOfficerDisplaysReady)} CONSOLE={OnOff(sdk.SpeedbrakeDown)}/{OnOff(sdk.FlapsUp)}/{OnOff(sdk.FuelControlsCutoff)}/{OnOff(sdk.TransponderStandby)} "
            + $"FLOW2_PANEL={OnOff(sdk.ThrustAsymmetryCompensationAuto)}/{OnOff(sdk.PrimaryFlightComputersAuto)}/{OnOff(sdk.FirePanelNormal)}/{OnOff(sdk.EngineControlPanelNormal)}/{OnOff(sdk.FuelPanelPreflight)}/{OnOff(sdk.AntiIceAuto)}/{OnOff(sdk.ExteriorLightsPreflight)}/{OnOff(sdk.AirPanelPreflight)}/{OnOff(sdk.AutobrakeRto)}/{OnOff(sdk.TransponderAltitudeSourceNormal)} "
            + $"FLOW2_DETAIL=SEAT_OFF_{OnOff(sdk.SeatBeltsOff)}/SEAT_AUTO_{OnOff(sdk.SeatBeltsAuto)}/NOSMOKE_AUTO_{OnOff(sdk.NoSmokingAuto)}/FUELSEL_{OnOff(sdk.FuelToRemainSelectorIn)}/TEMP_{OnOff(sdk.TemperatureControlsPreflight)}/FO_ND_MAP_{OnOff(sdk.FirstOfficerNdMap)}/FIRETEST_{OnOff(FireOverheatTestObserved)}/OXYTEST_{OnOff(FirstOfficerOxygenTestObserved)} "
            + $"FMC={sdk.FmcFlightNumber}/{sdk.FmcCruiseAltitude}/{sdk.FmcDistanceToDestination:0.0}/{OnOff(sdk.FmcPerformanceInputComplete)} "
            + $"MCP_ALT={sdk.McpAltitude} TO={sdk.FmcTakeoffFlaps}/{sdk.FmcV1}/{sdk.FmcVr}/{sdk.FmcV2} "
            + $"FLAPS={sdk.FlapsLever}/{OnOff(sdk.TakeoffFlapsSet)} ECL_PREFLIGHT={OnOff(sdk.PreflightChecklistComplete)}";
        if (string.Equals(signature, _flowOneSignature, StringComparison.Ordinal))
        {
            return null;
        }

        _flowOneSignature = signature;
        return $"PMDG 777 Flow 1/2 readbacks: {signature}.";
    }

    public void ClearObservedTests()
    {
        FireOverheatTestObserved = false;
        FirstOfficerOxygenTestObserved = false;
    }

    public void ResetConnectionState()
    {
        State = null;
        ControlState = default;
        DataReady = false;
        ControlReady = false;
        ClearObservedTests();
        AdiruOffSinceUtc = null;
        _rawSnapshot = null;
        _flowOneSignature = null;
    }

    private static string OnOff(bool value) => value ? "ON" : "OFF";
}
