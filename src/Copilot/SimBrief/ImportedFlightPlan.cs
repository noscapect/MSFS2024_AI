namespace Msfs2024Ai.Copilot.SimBrief;

public sealed class ImportedFlightPlan
{
    public DateTime ImportedUtc { get; set; }
    public DateTime? GeneratedUtc { get; set; }
    public string Airac { get; set; } = "";
    public string FlightNumber { get; set; } = "";
    public string AircraftIcao { get; set; } = "";
    public string AircraftRegistration { get; set; } = "";
    public string OriginIcao { get; set; } = "";
    public string DestinationIcao { get; set; } = "";
    public string AlternateIcao { get; set; } = "";
    public string OriginRunway { get; set; } = "";
    public string DestinationRunway { get; set; } = "";
    public string Route { get; set; } = "";
    public int? CruiseAltitudeFeet { get; set; }
    public int? CostIndex { get; set; }
    public int? TransitionAltitudeFeet { get; set; }
    public int? TakeoffV1Knots { get; set; }
    public int? TakeoffVrKnots { get; set; }
    public int? TakeoffV2Knots { get; set; }
    public string TakeoffFlaps { get; set; } = "";
    public double? BlockFuel { get; set; }
    public string Units { get; set; } = "";

    public string RouteLabel =>
        string.IsNullOrWhiteSpace(OriginIcao) || string.IsNullOrWhiteSpace(DestinationIcao)
            ? "Imported flight"
            : $"{OriginIcao}-{DestinationIcao}";
}
