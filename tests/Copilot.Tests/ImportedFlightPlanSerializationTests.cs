using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.SimBrief;
using System.Text;
using System.Xml.Serialization;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class ImportedFlightPlanSerializationTests
{
    [TestMethod]
    public void StructuredNavigationFieldsRoundTripThroughSessionXml()
    {
        var source = new ImportedFlightPlan
        {
            SidIdentifier = "NORK2S",
            StarIdentifier = "BETU2A",
            DestinationTransitionLevelFeet = 5500,
            Navlog = new List<ImportedFlightPlanFix>
            {
                new()
                {
                    Identifier = "NORKU",
                    Latitude = 52.1234,
                    Longitude = 4.5678,
                    IsSidStar = true
                }
            }
        };
        var serializer = new XmlSerializer(typeof(ImportedFlightPlan));
        using var stream = new MemoryStream();

        serializer.Serialize(stream, source);
        stream.Position = 0;
        var restored = (ImportedFlightPlan)serializer.Deserialize(stream)!;

        Assert.AreEqual("NORK2S", restored.SidIdentifier);
        Assert.AreEqual("BETU2A", restored.StarIdentifier);
        Assert.AreEqual(5500, restored.DestinationTransitionLevelFeet);
        Assert.AreEqual(1, restored.Navlog.Count);
        Assert.AreEqual("NORKU", restored.Navlog[0].Identifier);
        Assert.IsTrue(restored.Navlog[0].IsSidStar);
    }

    [TestMethod]
    public void OlderCachedFlightWithoutNavigationFieldsRemainsCompatible()
    {
        const string xml =
            "<ImportedFlightPlan><OriginIcao>EHAM</OriginIcao>" +
            "<DestinationIcao>EGLL</DestinationIcao></ImportedFlightPlan>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        var restored = (ImportedFlightPlan)new XmlSerializer(
            typeof(ImportedFlightPlan)).Deserialize(stream)!;

        Assert.AreEqual("EHAM", restored.OriginIcao);
        Assert.AreEqual("EGLL", restored.DestinationIcao);
        Assert.IsNotNull(restored.Navlog);
        Assert.AreEqual(0, restored.Navlog.Count);
    }
}
