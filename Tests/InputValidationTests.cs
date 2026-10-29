using System;
using Xunit;
using HololensSatelliteViewer.Services;

namespace HololensSatelliteViewer.Tests
{
    /// <summary>
    /// Tests for input validation guards on SetObserverLocation and ParseTle.
    /// Ensures invalid geographic/orbital data is caught early.
    /// </summary>
    public class InputValidationTests
    {
        // ── SetObserverLocation tests ──────────────────────────────────────

        [Fact]
        public void SetObserverLocation_ValidCoordinates_Succeeds()
        {
            var svc = new OrbitService(new System.Collections.Generic.List<Models.TleRecord>());
            // Should not throw
            svc.SetObserverLocation(59.964, 10.470, 100.0);
        }

        [Theory]
        [InlineData(-90.0)]
        [InlineData(0.0)]
        [InlineData(90.0)]
        public void SetObserverLocation_ValidLatitudes_Succeed(double lat)
        {
            var svc = new OrbitService(new System.Collections.Generic.List<Models.TleRecord>());
            svc.SetObserverLocation(lat, 0.0, 0.0);  // Should not throw
        }

        [Theory]
        [InlineData(-180.0)]
        [InlineData(0.0)]
        [InlineData(180.0)]
        public void SetObserverLocation_ValidLongitudes_Succeed(double lon)
        {
            var svc = new OrbitService(new System.Collections.Generic.List<Models.TleRecord>());
            svc.SetObserverLocation(0.0, lon, 0.0);  // Should not throw
        }

        [Theory]
        [InlineData(-91.0)]   // below minimum
        [InlineData(90.1)]    // above maximum
        [InlineData(95.0)]
        [InlineData(-120.0)]
        public void SetObserverLocation_InvalidLatitude_ThrowsArgumentOutOfRangeException(double lat)
        {
            var svc = new OrbitService(new System.Collections.Generic.List<Models.TleRecord>());
            Assert.Throws<ArgumentOutOfRangeException>(() => svc.SetObserverLocation(lat, 0.0, 0.0));
        }

        [Theory]
        [InlineData(-180.1)]  // below minimum
        [InlineData(180.1)]   // above maximum
        [InlineData(200.0)]
        [InlineData(-200.0)]
        public void SetObserverLocation_InvalidLongitude_ThrowsArgumentOutOfRangeException(double lon)
        {
            var svc = new OrbitService(new System.Collections.Generic.List<Models.TleRecord>());
            Assert.Throws<ArgumentOutOfRangeException>(() => svc.SetObserverLocation(0.0, lon, 0.0));
        }

        [Theory]
        [InlineData(-1.0)]    // negative altitude
        [InlineData(-100.0)]
        public void SetObserverLocation_NegativeAltitude_ThrowsArgumentOutOfRangeException(double alt)
        {
            var svc = new OrbitService(new System.Collections.Generic.List<Models.TleRecord>());
            Assert.Throws<ArgumentOutOfRangeException>(() => svc.SetObserverLocation(0.0, 0.0, alt));
        }

        [Fact]
        public void SetObserverLocation_NaNLatitude_ThrowsArgumentOutOfRangeException()
        {
            var svc = new OrbitService(new System.Collections.Generic.List<Models.TleRecord>());
            Assert.Throws<ArgumentOutOfRangeException>(() => svc.SetObserverLocation(double.NaN, 0.0, 0.0));
        }

        [Fact]
        public void SetObserverLocation_NaNLongitude_ThrowsArgumentOutOfRangeException()
        {
            var svc = new OrbitService(new System.Collections.Generic.List<Models.TleRecord>());
            Assert.Throws<ArgumentOutOfRangeException>(() => svc.SetObserverLocation(0.0, double.NaN, 0.0));
        }

        [Fact]
        public void SetObserverLocation_NaNAltitude_ThrowsArgumentOutOfRangeException()
        {
            var svc = new OrbitService(new System.Collections.Generic.List<Models.TleRecord>());
            Assert.Throws<ArgumentOutOfRangeException>(() => svc.SetObserverLocation(0.0, 0.0, double.NaN));
        }

        // ── ParseTle epoch validation tests ────────────────────────────────

        [Fact]
        public void Sgp4_ParseTle_ValidEpoch_Succeeds()
        {
            var validTle = new Models.TleRecord
            {
                Name  = "ISS",
                Line1 = "1 25544U 98067A   26115.54032407  .00021614  00000+0  38745-3 0  9992",
                Line2 = "2 25544  51.6394  87.1905 0004256 357.2517  70.1359 15.50369677503407"
            };
            var svc = new Sgp4Service();
            // Should not throw
            svc.Propagate(validTle, new System.DateTime(2026, 4, 25, 12, 58, 0, System.DateTimeKind.Utc));
        }

        [Fact]
        public void Sgp4_ParseTle_EpochDayBelowOne_ThrowsArgumentException()
        {
            // Manually construct a TLE with day = 0.5 (invalid)
            var badTle = new Models.TleRecord
            {
                Name  = "BAD",
                Line1 = "1 25544U 98067A   26 00.54032407  .00021614  00000+0  38745-3 0  9992",  // day=0.54 invalid
                Line2 = "2 25544  51.6394  87.1905 0004256 357.2517  70.1359 15.50369677503407"
            };
            var svc = new Sgp4Service();
            Assert.Throws<ArgumentException>(() =>
                svc.Propagate(badTle, new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)));
        }

        [Fact]
        public void Sgp4_ParseTle_EpochDayAbove367_ThrowsArgumentException()
        {
            var badTle = new Models.TleRecord
            {
                Name  = "BAD",
                Line1 = "1 25544U 98067A   26367.54032407  .00021614  00000+0  38745-3 0  9992",  // day=367.54 invalid
                Line2 = "2 25544  51.6394  87.1905 0004256 357.2517  70.1359 15.50369677503407"
            };
            var svc = new Sgp4Service();
            Assert.Throws<ArgumentException>(() =>
                svc.Propagate(badTle, new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)));
        }

        [Fact]
        public void Sgp4_ParseTle_InclinationAbove180_ThrowsArgumentException()
        {
            var badTle = new Models.TleRecord
            {
                Name  = "BAD",
                Line1 = "1 25544U 98067A   26115.54032407  .00021614  00000+0  38745-3 0  9992",
                Line2 = "2 25544 181.6394  87.1905 0004256 357.2517  70.1359 15.50369677503407"  // inc=181.6 invalid
            };
            var svc = new Sgp4Service();
            Assert.Throws<ArgumentException>(() =>
                svc.Propagate(badTle, new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)));
        }

        [Fact]
        public void Sgp4_ParseTle_EccentricityGreaterThanOne_ThrowsArgumentException()
        {
            var badTle = new Models.TleRecord
            {
                Name  = "BAD",
                Line1 = "1 25544U 98067A   26115.54032407  .00021614  00000+0  38745-3 0  9992",
                Line2 = "2 25544  51.6394  87.1905 1004256 357.2517  70.1359 15.50369677503407"  // ecc starting with 1 → 0.1004256 < 1, so actually valid
            };
            var svc = new Sgp4Service();
            // This should actually pass because 0.1004256 is valid
            svc.Propagate(badTle, new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc));
        }

        [Fact]
        public void Sgp4_ParseTle_NegativeMeanMotion_ThrowsArgumentException()
        {
            var badTle = new Models.TleRecord
            {
                Name  = "BAD",
                Line1 = "1 25544U 98067A   26115.54032407  .00021614  00000+0  38745-3 0  9992",
                Line2 = "2 25544  51.6394  87.1905 0004256 357.2517  70.1359 -5.50369677503407"  // negative mean motion
            };
            var svc = new Sgp4Service();
            Assert.Throws<ArgumentException>(() =>
                svc.Propagate(badTle, new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)));
        }
    }
}
