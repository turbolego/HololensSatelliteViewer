using System;
using System.Collections.Generic;
using Xunit;
using HololensSatelliteViewer.Models;
using HololensSatelliteViewer.Services;

namespace HololensSatelliteViewer.Tests
{
    /// <summary>
    /// Direct unit tests for OrbitService.ComputeObserverRelativePosition —
    /// the ECI-to-topocentric transform that produces azimuth, elevation, and range.
    ///
    /// These tests bypass the SGP4 propagator entirely by constructing Satellite
    /// objects with analytically-known ECI positions.
    /// </summary>
    public class TopocentricTests
    {
        private const double Re       = 6378.137;          // Earth equatorial radius (km)
        private const double Deg2Rad  = Math.PI / 180.0;

        // All geometric tests use this fixed UTC moment.
        private static readonly DateTime T = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);

        // ── Helpers ────────────────────────────────────────────────────────

        /// <summary>Creates an OrbitService with the given observer position and no TLEs.</summary>
        private static OrbitService Observer(double lat, double lon, double altKm = 0.0)
        {
            var svc = new OrbitService(new List<TleRecord>());
            svc.SetObserverLocation(lat, lon, altKm);
            return svc;
        }

        /// <summary>
        /// Returns a Satellite whose ECI position places it directly above (lat, lon)
        /// at the given altitude, at time T.
        /// </summary>
        private static Satellite SatAbove(double lat, double lon, double altKm)
        {
            double g      = AstroMath.ComputeGmst(T);
            double eciLon = lon * Deg2Rad + g;
            double latRad = lat * Deg2Rad;
            double r      = Re + altKm;
            return new Satellite
            {
                X    = r * Math.Cos(latRad) * Math.Cos(eciLon),
                Y    = r * Math.Cos(latRad) * Math.Sin(eciLon),
                Z    = r * Math.Sin(latRad),
                Name = "SYNTH"
            };
        }

        // ── Elevation / range tests ────────────────────────────────────────

        [Fact]
        public void ComputeObserver_SatDirectlyOverhead_ElevationIs90()
        {
            var svc = Observer(0.0, 0.0);
            var sat = SatAbove(0.0, 0.0, 400.0);

            svc.ComputeObserverRelativePosition(sat, T);

            Assert.InRange(sat.Elevation, 89.9, 90.1);
        }

        [Fact]
        public void ComputeObserver_SatDirectlyOverhead_RangeEqualsAltitude()
        {
            const double alt = 500.0;
            var svc = Observer(0.0, 0.0);
            var sat = SatAbove(0.0, 0.0, alt);

            svc.ComputeObserverRelativePosition(sat, T);

            Assert.InRange(sat.RangeKm, alt - 1.0, alt + 1.0);
        }

        [Theory]
        [InlineData( 59.964,  10.470,   0.0)]   // Oslo
        [InlineData(-33.868, 151.209,   0.0)]   // Sydney
        [InlineData(  0.0,    0.0,      0.0)]   // Equator / Greenwich
        [InlineData( 90.0,    0.0,      0.0)]   // North Pole
        public void ComputeObserver_OverheadSat_AlwaysReturns90Elevation(double lat, double lon, double alt)
        {
            var svc = Observer(lat, lon, alt);
            var sat = SatAbove(lat, lon, 400.0);

            svc.ComputeObserverRelativePosition(sat, T);

            Assert.InRange(sat.Elevation, 89.5, 90.5);
        }

        [Fact]
        public void ComputeObserver_AzimuthIsInZeroTo360Range()
        {
            // Satellite not directly overhead → non-trivial azimuth
            var svc = Observer(59.964, 10.470);
            var sat = SatAbove(65.0, 25.0, 500.0);

            svc.ComputeObserverRelativePosition(sat, T);

            Assert.InRange(sat.Azimuth, 0.0, 360.0);
        }

        [Fact]
        public void ComputeObserver_ElevationIsInMinusNinetyToNinetyRange()
        {
            var svc = Observer(59.964, 10.470);
            var sat = SatAbove(65.0, 25.0, 500.0);

            svc.ComputeObserverRelativePosition(sat, T);

            Assert.InRange(sat.Elevation, -90.0, 90.0);
        }

        // ── Observer position sensitivity ─────────────────────────────────

        [Fact]
        public void ComputeObserver_TwoDifferentObservers_DifferentAzimuths()
        {
            // GEO satellite directly above the equator/Greenwich meridian.
            // From Oslo it's roughly south; from Sydney it's roughly north-west.
            var satOslo   = SatAbove(0.0, 0.0, 35_786.0);
            var satSydney = new Satellite { X = satOslo.X, Y = satOslo.Y, Z = satOslo.Z };

            Observer(59.964,  10.470).ComputeObserverRelativePosition(satOslo,   T);
            Observer(-33.868, 151.209).ComputeObserverRelativePosition(satSydney, T);

            Assert.NotEqual(satOslo.Azimuth, satSydney.Azimuth);
        }

        [Fact]
        public void ComputeObserver_HigherAltitudeObserver_SmallerRange()
        {
            // A mountain-top observer is physically closer to the satellite.
            const double alt = 400.0;
            var seaLevel = Observer(0.0, 0.0, 0.0);
            var mountain = Observer(0.0, 0.0, 8.0);   // 8 km altitude

            var satSea = SatAbove(0.0, 0.0, alt);
            var satMtn = new Satellite { X = satSea.X, Y = satSea.Y, Z = satSea.Z };

            seaLevel.ComputeObserverRelativePosition(satSea, T);
            mountain.ComputeObserverRelativePosition(satMtn, T);

            Assert.True(satMtn.RangeKm < satSea.RangeKm,
                $"Mountain range {satMtn.RangeKm:F2} km should be less than sea-level range {satSea.RangeKm:F2} km.");
        }

        [Fact]
        public void ComputeObserver_SatOnOppositeHemisphere_NegativeElevation()
        {
            // Satellite directly below the south pole from the perspective of an
            // observer at the north pole must be below the horizon.
            var svc = Observer(89.9, 0.0);   // near north pole
            var sat = SatAbove(-89.9, 0.0, 400.0);   // near south pole

            svc.ComputeObserverRelativePosition(sat, T);

            Assert.True(sat.Elevation < 0.0,
                $"A satellite on the opposite hemisphere should be below the horizon, got {sat.Elevation:F2}°.");
        }
    }
}
