namespace Msfs2024Ai.Copilot.Telemetry;

/// <summary>
/// Selects only distance sources that identify the runway/ILS itself. Generic
/// GPS target and waypoint distances are deliberately excluded because they
/// describe the next flight-plan fix and can reach zero many miles before the
/// runway.
/// </summary>
internal static class ApproachDistanceResolver
{
    private const double MetersPerNauticalMile = 1852.0;

    public static (double? DistanceNm, string Source) Resolve(
        bool atcRunwaySelected,
        double atcRunwayDistanceMeters,
        bool nav1HasLocalizer,
        double nav1DmeNm,
        bool nav2HasLocalizer,
        double nav2DmeNm)
    {
        if (atcRunwaySelected)
        {
            var runwayDistance = FromMeters(atcRunwayDistanceMeters);
            if (runwayDistance.HasValue)
            {
                return (runwayDistance, "ATC runway");
            }
        }

        if (nav1HasLocalizer && IsUsableNm(nav1DmeNm))
        {
            return (nav1DmeNm, "NAV1 ILS DME");
        }

        if (nav2HasLocalizer && IsUsableNm(nav2DmeNm))
        {
            return (nav2DmeNm, "NAV2 ILS DME");
        }

        return (null, "");
    }

    private static double? FromMeters(double meters)
    {
        if (double.IsNaN(meters)
            || double.IsInfinity(meters)
            || meters <= 0
            || meters > 100 * MetersPerNauticalMile)
        {
            return null;
        }

        return meters / MetersPerNauticalMile;
    }

    private static bool IsUsableNm(double distanceNm) =>
        !double.IsNaN(distanceNm)
        && !double.IsInfinity(distanceNm)
        && distanceNm > 0
        && distanceNm <= 100;
}
