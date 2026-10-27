using HololensSatelliteViewer.Services;
using System;
using System.IO;
using Xunit;

namespace HololensSatelliteViewer.Tests
{
    public class AirportLocationTests
    {
        [Fact]
        public void ParseAirportCatalogIgnoresHeaderAndComments()
        {
            AirportLocation[] airports = LocationSettingsModel.ParseAirportCatalog(new[]
            {
                "# airport data",
                "iata\tname\tlatitude\tlongitude",
                "OSL\tOslo Airport\t60.1939\t11.1004"
            });

            Assert.Single(airports);
            Assert.Equal("OSL", airports[0].Iata);
            Assert.Equal(60.1939, airports[0].Latitude, 4);
        }

        [Fact]
        public void FindThreeClosestAirportsReturnsNearestFirst()
        {
            AirportLocation[] airports =
            {
                new AirportLocation("AAA", "Far", 0.0, 10.0),
                new AirportLocation("BBB", "Nearest", 0.1, 0.1),
                new AirportLocation("CCC", "Middle", 1.0, 1.0),
                new AirportLocation("DDD", "Also near", -0.5, 0.5)
            };

            AirportLocation[] closest = LocationSettingsModel.FindThreeClosestAirports(airports, 0.0, 0.0);

            Assert.Equal(new[] { "BBB", "DDD", "CCC" }, new[] { closest[0].Iata, closest[1].Iata, closest[2].Iata });
        }

        [Fact]
        public void ParseAirportCatalogRejectsMalformedRows()
        {
            Assert.Throws<System.FormatException>(() =>
                LocationSettingsModel.ParseAirportCatalog(new[] { "OSL\tOslo\t91\t11" }));
        }

        [Fact]
        public void BundledAirportCatalogParsesAndContainsMajorAirports()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Services", "airports.tsv");
            AirportLocation[] airports = LocationSettingsModel.ParseAirportCatalog(File.ReadAllLines(path));

            Assert.True(airports.Length > 8000);
            Assert.Contains(airports, airport => airport.Iata == "OSL");
            Assert.Contains(airports, airport => airport.Iata == "JFK");
        }
    }
}
