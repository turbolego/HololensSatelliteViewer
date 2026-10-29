using System;
using Xunit;
using HololensSatelliteViewer.Models;
using HololensSatelliteViewer.Services;

namespace HololensSatelliteViewer.Tests
{
    /// <summary>Tests for the simplified SGP4 propagator.</summary>
    public class Sgp4ServiceTests
    {
        // ISS TLE from the OrbitService fallback (epoch 2026-04-25 ~12:58 UTC)
        private static readonly TleRecord IssTle = new TleRecord
        {
            Name    = "ISS (ZARYA)",
            NoradId = 25544,
            Line1   = "1 25544U 98067A   26115.54032407  .00021614  00000+0  38745-3 0  9992",
            Line2   = "2 25544  51.6394  87.1905 0004256 357.2517  70.1359 15.50369677503407"
        };

        // Approximate GEO TLE (eccentricity ~0, inclination ~0, period ~24 h)
        private static readonly TleRecord GeoTle = new TleRecord
        {
            Name    = "GEO-TEST",
            NoradId = 99999,
            Line1   = "1 99999U 00000A   26115.50000000  .00000010  00000+0  00000+0 0  9990",
            Line2   = "2 99999   0.0500   0.0000 0003000   0.0000   0.0000  1.00273800000000"
        };

        private static readonly Sgp4Service Svc = new Sgp4Service();

        // Epoch for ISS TLE: year 2026, day 115.54032407
        private static DateTime IssEpoch()
        {
            int    year    = 2026;
            double dayFrac = 115.54032407;
            return new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                       .AddDays(dayFrac - 1.0);
        }

        [Fact]
        public void Propagate_IssAtEpoch_AltitudeIsInLeoRange()
        {
            var sat = Svc.Propagate(IssTle, IssEpoch());

            // ISS orbits at ~400 km; allow a generous tolerance for the simplified model
            Assert.InRange(sat.AltitudeKm, 300.0, 500.0);
        }

        [Fact]
        public void Propagate_IssAtEpoch_EciMagnitudeIsKmScale()
        {
            var sat = Svc.Propagate(IssTle, IssEpoch());

            double mag = Math.Sqrt(sat.X * sat.X + sat.Y * sat.Y + sat.Z * sat.Z);

            // ECI vector must be in km (≈ 6378 + 400 km), NOT in Earth-radii (~1.06)
            Assert.InRange(mag, 6500.0, 7200.0);
        }

        [Fact]
        public void Propagate_IssOneMinuteLater_PositionHasChanged()
        {
            var t0  = IssEpoch();
            var sat0 = Svc.Propagate(IssTle, t0);
            var sat1 = Svc.Propagate(IssTle, t0.AddMinutes(1.0));

            double dx = sat1.X - sat0.X;
            double dy = sat1.Y - sat0.Y;
            double dz = sat1.Z - sat0.Z;
            double displacement = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            // ISS moves ~7.7 km/s; straight-line chord over 60 s can reach ~920 km
            Assert.InRange(displacement, 100.0, 1100.0);
        }

        [Fact]
        public void Propagate_GeoTle_AltitudeIsGeostationary()
        {
            var t   = new DateTime(2026, 4, 25, 12, 0, 0, DateTimeKind.Utc);
            var sat = Svc.Propagate(GeoTle, t);

            // GEO altitude ≈ 35 786 km; allow ±1 000 km for the simplified model
            Assert.InRange(sat.AltitudeKm, 34_000.0, 37_000.0);
        }

        [Fact]
        public void Propagate_SetsTimestamp()
        {
            var t   = IssEpoch();
            var sat = Svc.Propagate(IssTle, t);
            Assert.Equal(t, sat.Timestamp);
        }

        [Fact]
        public void Propagate_SetsNameAndNoradId()
        {
            var sat = Svc.Propagate(IssTle, IssEpoch());
            Assert.Equal("ISS (ZARYA)", sat.Name);
            Assert.Equal(25544, sat.NoradId);
        }

        // ── Input guard tests ──────────────────────────────────────────────

        [Fact]
        public void Propagate_NullTle_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Svc.Propagate(null!, IssEpoch()));
        }

        [Fact]
        public void Propagate_TleWithShortLine1_ThrowsArgumentException()
        {
            var bad = new TleRecord { Name = "X", Line1 = "1 25544", Line2 = IssTle.Line2 };
            Assert.Throws<ArgumentException>(() => Svc.Propagate(bad, IssEpoch()));
        }

        [Fact]
        public void Propagate_TleWithShortLine2_ThrowsArgumentException()
        {
            var bad = new TleRecord { Name = "X", Line1 = IssTle.Line1, Line2 = "2 25544" };
            Assert.Throws<ArgumentException>(() => Svc.Propagate(bad, IssEpoch()));
        }

        [Fact]
        public void Propagate_TleWithNullLine1_ThrowsArgumentException()
        {
            var bad = new TleRecord { Name = "X", Line1 = null!, Line2 = IssTle.Line2 };
            Assert.Throws<ArgumentException>(() => Svc.Propagate(bad, IssEpoch()));
        }

        // ── Output range validation ────────────────────────────────────────

        [Fact]
        public void Propagate_LatitudeIsWithinValidRange()
        {
            var sat = Svc.Propagate(IssTle, IssEpoch());
            Assert.InRange(sat.Latitude, -90.0, 90.0);
        }

        [Fact]
        public void Propagate_LongitudeIsWithinValidRange()
        {
            var sat = Svc.Propagate(IssTle, IssEpoch());
            Assert.InRange(sat.Longitude, -180.0, 180.0);
        }

        [Fact]
        public void Propagate_LeoVelocityIsReasonable()
        {
            var sat = Svc.Propagate(IssTle, IssEpoch());
            // ISS orbital speed ≈ 7.66 km/s; allow ±1 km/s for the simplified model
            Assert.InRange(sat.VelocityKmPerSec, 6.5, 8.5);
        }

        [Fact]
        public void Propagate_TimestampMatchesInputTime()
        {
            var t   = IssEpoch();
            var sat = Svc.Propagate(IssTle, t);
            Assert.Equal(t, sat.Timestamp);
            Assert.Equal(DateTimeKind.Utc, sat.Timestamp.Kind);
        }
    }
}
