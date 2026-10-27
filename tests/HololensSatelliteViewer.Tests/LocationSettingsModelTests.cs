using HololensSatelliteViewer.Services;
using Xunit;

namespace HololensSatelliteViewer.Tests
{
    public class LocationSettingsModelTests
    {
        [Theory]
        [InlineData("59.9139, 10.7522", 59.9139, 10.7522)]
        [InlineData("-33.8; 151.2", -33.8, 151.2)]
        [InlineData("40.7 -74.0", 40.7, -74.0)]
        public void TryParseCoordinateStringAcceptsValidCoordinates(
            string input,
            double expectedLatitude,
            double expectedLongitude)
        {
            double latitude;
            double longitude;

            Assert.True(LocationSettingsModel.TryParseCoordinateString(input, out latitude, out longitude));
            Assert.Equal(expectedLatitude, latitude, 4);
            Assert.Equal(expectedLongitude, longitude, 4);
        }

        [Theory]
        [InlineData("")]
        [InlineData("91, 0")]
        [InlineData("0, 181")]
        [InlineData("NaN, 0")]
        [InlineData("1, 2, 3")]
        public void TryParseCoordinateStringRejectsInvalidCoordinates(string input)
        {
            double latitude;
            double longitude;

            Assert.False(LocationSettingsModel.TryParseCoordinateString(input, out latitude, out longitude));
        }

        [Fact]
        public void AdjustLatitudeClampsAtPoles()
        {
            Assert.Equal(90.0, LocationSettingsModel.AdjustLatitude(89.5, 1.0));
            Assert.Equal(-90.0, LocationSettingsModel.AdjustLatitude(-89.5, -1.0));
        }

        [Fact]
        public void AdjustLongitudeWrapsAcrossAntimeridian()
        {
            Assert.Equal(-179.0, LocationSettingsModel.AdjustLongitude(180.0, 1.0));
            Assert.Equal(179.0, LocationSettingsModel.AdjustLongitude(-180.0, -1.0));
        }
    }
}
