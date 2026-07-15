using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Settings;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class AircraftApproachProfilesTests
{
    [TestMethod]
    public void ResolverSelectsIndependentSupportedAircraftProfiles()
    {
        Assert.AreEqual("inibuilds-a320neo-v2", AircraftApproachProfiles.Resolve("A320neo V2").Key);
        Assert.AreEqual("fbw-a32nx", AircraftApproachProfiles.Resolve("Airbus A320neo FlyByWire").Key);
        Assert.AreEqual("inibuilds-a321lr", AircraftApproachProfiles.Resolve("iniBuilds A321LR").Key);
        Assert.AreEqual("inibuilds-a330", AircraftApproachProfiles.Resolve("A330-300 (GE)").Key);
        Assert.AreEqual("pmdg-737-800", AircraftApproachProfiles.Resolve("737-800 PAX BW TC").Key);
    }

    [TestMethod]
    public void A321StandardPreservesSeparateConfigThreeAndFullSpeeds()
    {
        var schedule = AircraftApproachProfiles.EffectiveSchedule(
            "iniBuilds A321LR",
            Array.Empty<AircraftApproachOverride>());

        Assert.AreEqual(230, schedule.Flaps1SpeedKnots);
        Assert.AreEqual(215, schedule.Flaps2SpeedKnots);
        Assert.AreEqual(195, schedule.LandingConfigSpeedKnots);
        Assert.AreEqual(186, schedule.FlapsFullSpeedKnots);
    }

    [TestMethod]
    public void OverrideOnlyAppliesToItsAircraftProfile()
    {
        var overrides = new[]
        {
            new AircraftApproachOverride
            {
                ProfileKey = "inibuilds-a320neo-v2",
                Schedule = new ApproachScheduleSettings
                {
                    Flaps1DistanceNm = 18,
                    Flaps1AltitudeFeet = 9000,
                    Flaps1SpeedKnots = 225,
                    Flaps2DistanceNm = 12,
                    Flaps2AltitudeAglFeet = 4200,
                    Flaps2SpeedKnots = 198,
                    GearDistanceNm = 8,
                    GearAltitudeAglFeet = 2700,
                    GearSpeedKnots = 205,
                    LandingConfigDistanceNm = 6,
                    LandingConfigAltitudeAglFeet = 1900,
                    LandingConfigSpeedKnots = 180,
                    FlapsFullSpeedKnots = 175
                }
            }
        };

        var a320 = AircraftApproachProfiles.EffectiveSchedule("A320neo V2", overrides);
        var a321 = AircraftApproachProfiles.EffectiveSchedule("A321LR", overrides);

        Assert.AreEqual(18, a320.Flaps1DistanceNm);
        Assert.AreEqual(175, a320.FlapsFullSpeedKnots);
        Assert.AreEqual(15, a321.Flaps1DistanceNm);
        Assert.AreEqual(186, a321.FlapsFullSpeedKnots);
    }
}
