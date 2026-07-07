using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.AircraftIdentity;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class AircraftIdentityResolverTests
{
    [TestMethod]
    public void ResolverMatchesAircraftTitleAndFindsThumbnail()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "MSFS2024_AI-identity-tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            var aircraftDirectory = Path.Combine(
                root,
                "Community",
                "pmdg-aircraft-738",
                "SimObjects",
                "Airplanes",
                "Test Aircraft",
                "presets",
                "Test Livery");
            var configDirectory = Path.Combine(aircraftDirectory, "config");
            var thumbnailDirectory = Path.Combine(aircraftDirectory, "thumbnail");
            Directory.CreateDirectory(configDirectory);
            Directory.CreateDirectory(thumbnailDirectory);
            File.WriteAllText(
                Path.Combine(configDirectory, "aircraft.cfg"),
                """
                [FLTSIM.0]
                title="Test 737"
                ui_manufacturer="Boeing"
                ui_type="737-800"
                ui_variation="House"
                ui_createdby="PMDG"
                """);
            var thumbnailPath = Path.Combine(thumbnailDirectory, "thumbnail_variation.png");
            File.WriteAllBytes(thumbnailPath, new byte[] { 1, 2, 3 });
            var liveryThumbnailDirectory = Path.Combine(
                root,
                "Community",
                "pmdg-aircraft-738",
                "SimObjects",
                "Airplanes",
                "Test Aircraft",
                "liveries",
                "pmdg",
                "PMDG House PAX BW",
                "thumbnail");
            Directory.CreateDirectory(liveryThumbnailDirectory);
            var liveryThumbnailPath = Path.Combine(liveryThumbnailDirectory, "thumbnail.png");
            File.WriteAllBytes(liveryThumbnailPath, new byte[] { 4, 5, 6 });

            var resolver = new AircraftIdentityResolver(new[] { Path.Combine(root, "Community") });

            var identity = resolver.Resolve("Test 737");

            Assert.IsNotNull(identity);
            Assert.AreEqual("Boeing 737-800", identity!.DisplayName);
            Assert.AreEqual("House - PMDG", identity.DisplayVariation);
            Assert.AreEqual(liveryThumbnailPath, identity.ThumbnailPath);
            CollectionAssert.AreEqual(
                new[] { liveryThumbnailPath, thumbnailPath },
                identity.ThumbnailPaths.ToArray());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
