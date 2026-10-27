using HololensSatelliteViewer.Services;
using HololensSatelliteViewer.Utilities;
using Xunit;

namespace HololensSatelliteViewer.Tests
{
    public class GeohashConverterTests
    {
        [Fact]
        public void DecodeReturnsCellCenterForKnownGeohash()
        {
            GeohashCoordinate coordinate = GeohashConverter.Decode("u4pruyd");

            Assert.Equal(57.6487, coordinate.Latitude, 4);
            Assert.Equal(10.4075, coordinate.Longitude, 4);
        }

        [Theory]
        [InlineData("")]
        [InlineData("invalid!")]
        public void TryDecodeGeohashRejectsInvalidInput(string geohash)
        {
            double latitude;
            double longitude;

            Assert.False(LocationSettingsModel.TryDecodeGeohash(geohash, out latitude, out longitude));
        }
    }
}
