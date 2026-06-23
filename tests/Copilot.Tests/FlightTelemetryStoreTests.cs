using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Telemetry;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class FlightTelemetryStoreTests
{
    [TestMethod]
    public void StoreRetainsOnlyThreeFlightsAndReplaysConfiguredState()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "MSFS2024_AI-tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            for (var flight = 0; flight < 4; flight++)
            {
                using var store = new FlightTelemetryStore(directory);
                store.Record(
                    new AircraftState
                    {
                        Title = "A320neo V2",
                        Battery1On = true,
                        OnGround = flight == 0,
                        IndicatedAltitudeFeet = 8000 + flight,
                        ApproachFlaps1SpeedKnots = 215
                    },
                    new DateTime(2026, 6, 23, 10, flight, 0, DateTimeKind.Utc));
            }

            using var reader = new FlightTelemetryStore(directory);
            Assert.AreEqual(3, reader.Recordings.Count);
            var replay = reader.Load(reader.Recordings[0]);
            Assert.AreEqual(1, replay.Count);
            Assert.AreEqual(215, replay[0].ApproachFlaps1SpeedKnots);
            Assert.AreEqual(8003, replay[0].IndicatedAltitudeFeet);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
