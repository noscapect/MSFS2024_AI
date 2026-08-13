namespace Msfs2024Ai.Copilot.AircraftAdapters.Pmdg777;

/// <summary>
/// Read-only subset of PMDG_777X_Data required for Flow 1. Offsets and the
/// complete structure size are derived from the ordered declarations in the
/// PMDG_777X_SDK.h shipped with pmdg-aircraft-77w. C++ bool and unsigned char
/// fields occupy one byte and the complete structure is aligned to four bytes.
/// </summary>
internal sealed class Pmdg777SdkData
{
    public bool AdiruOn { get; private set; }
    public bool CabinUtilityOn { get; private set; }
    public bool IfePassengerSeatsOn { get; private set; }
    public bool BatteryOn { get; private set; }
    public bool ApuGeneratorOn { get; private set; }
    public byte ApuSelector { get; private set; }
    public bool BusTiesAuto { get; private set; }
    public bool PrimaryExternalPowerOn { get; private set; }
    public bool SecondaryExternalPowerOn { get; private set; }
    public bool PrimaryExternalPowerAvailable { get; private set; }
    public bool SecondaryExternalPowerAvailable { get; private set; }
    public bool CenterPrimaryPumpsOff { get; private set; }
    public bool DemandPumpsOff { get; private set; }
    public bool WipersOff { get; private set; }
    public byte EmergencyLightsSelector { get; private set; }
    public bool PacksOff { get; private set; }
    public bool RecirculationFansOff { get; private set; }
    public bool NavigationLightOn { get; private set; }
    public bool LogoLightOn { get; private set; }
    public bool GearLeverDown { get; private set; }
    public bool AlternateFlapsOff { get; private set; }
    public bool ParkingBrakeSet { get; private set; }

    public static bool TryParse(byte[]? data, out Pmdg777SdkData state)
    {
        state = new Pmdg777SdkData();
        if (data == null || data.Length < Pmdg777ControlProfile.DataSize)
        {
            return false;
        }

        bool BoolAt(int offset) => data[offset] != 0;

        state.AdiruOn = BoolAt(28);
        state.CabinUtilityOn = BoolAt(33);
        state.IfePassengerSeatsOn = BoolAt(35);
        state.BatteryOn = BoolAt(37);
        state.ApuGeneratorOn = BoolAt(40);
        state.ApuSelector = data[41];
        state.BusTiesAuto = BoolAt(43) && BoolAt(44);
        state.PrimaryExternalPowerOn = BoolAt(49);
        state.SecondaryExternalPowerOn = BoolAt(50);
        state.PrimaryExternalPowerAvailable = BoolAt(51);
        state.SecondaryExternalPowerAvailable = BoolAt(52);
        state.WipersOff = data[65] == 0 && data[66] == 0;
        state.EmergencyLightsSelector = data[67];
        state.CenterPrimaryPumpsOff = !BoolAt(84) && !BoolAt(85);
        state.DemandPumpsOff = data[86] == 0
                               && data[87] == 0
                               && data[88] == 0
                               && data[89] == 0;
        state.PacksOff = !BoolAt(173) && !BoolAt(174);
        state.RecirculationFansOff = !BoolAt(177) && !BoolAt(178);
        state.NavigationLightOn = BoolAt(113);
        state.LogoLightOn = BoolAt(114);
        state.GearLeverDown = data[212] == 1;
        state.AlternateFlapsOff = !BoolAt(415) && data[416] == 1;
        state.ParkingBrakeSet = BoolAt(424);
        return true;
    }
}
