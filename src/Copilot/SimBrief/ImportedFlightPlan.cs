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
    public string NavigraphRoute { get; set; } = "";
    public string SidIdentifier { get; set; } = "";
    public string SidTransition { get; set; } = "";
    public string StarIdentifier { get; set; } = "";
    public string StarTransition { get; set; } = "";
    public int? CruiseAltitudeFeet { get; set; }
    public int? CostIndex { get; set; }
    public int? TransitionAltitudeFeet { get; set; }
    public int? OriginTransitionLevelFeet { get; set; }
    public int? DestinationTransitionAltitudeFeet { get; set; }
    public int? DestinationTransitionLevelFeet { get; set; }
    public int? TakeoffV1Knots { get; set; }
    public int? TakeoffVrKnots { get; set; }
    public int? TakeoffV2Knots { get; set; }
    public string TakeoffFlaps { get; set; } = "";
    public double? BlockFuel { get; set; }
    public double? TaxiFuel { get; set; }
    public double? TakeoffFuel { get; set; }
    public double? LandingFuel { get; set; }
    public int? PassengerCount { get; set; }
    public int? MaximumPassengerCount { get; set; }
    public int? BaggageCount { get; set; }
    public double? PassengerWeight { get; set; }
    public double? BaggageWeight { get; set; }
    public double? FreightWeight { get; set; }
    public double? CargoWeight { get; set; }
    public double? PayloadWeight { get; set; }
    public double? OperatingEmptyWeight { get; set; }
    public double? ZeroFuelWeight { get; set; }
    public double? RampWeight { get; set; }
    public double? TakeoffWeight { get; set; }
    public double? LandingWeight { get; set; }
    public string Units { get; set; } = "";
    public List<ImportedFlightPlanFix> Navlog { get; set; } = new();

    public string RouteLabel =>
        string.IsNullOrWhiteSpace(OriginIcao) || string.IsNullOrWhiteSpace(DestinationIcao)
            ? "Imported flight"
            : $"{OriginIcao}-{DestinationIcao}";
}

public sealed class ImportedFlightPlanFix
{
    public string Identifier { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Stage { get; set; } = "";
    public string ViaAirway { get; set; } = "";
    public string IcaoRegion { get; set; } = "";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? AltitudeFeet { get; set; }
    public bool IsSidStar { get; set; }
}
