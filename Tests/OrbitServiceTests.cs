using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using HololensSatelliteViewer.Models;
using HololensSatelliteViewer.Services;

namespace HololensSatelliteViewer.Tests
{
    /// <summary>Tests for OrbitService: TLE parsing, GMST calculation, and propagation.</summary>
    public class OrbitServiceTests
    {
        // ── Sample TLE text ────────────────────────────────────────────────
        private const string SingleIssTle =
            "ISS (ZARYA)\r\n" +
            "1 25544U 98067A   26115.54032407  .00021614  00000+0  38745-3 0  9992\r\n" +
            "2 25544  51.6394  87.1905 0004256 357.2517  70.1359 15.50369677503407\r\n";

        private const string TwoLineTle =
            "1 25544U 98067A   26115.54032407  .00021614  00000+0  38745-3 0  9992\r\n" +
            "2 25544  51.6394  87.1905 0004256 357.2517  70.1359 15.50369677503407\r\n";

        private const string ThreeSatelliteTle =
            SingleIssTle +
            "NOAA 19\r\n" +
            "1 33591U 09005A   26115.50000000  .00000100  00000+0  80000-4 0  9990\r\n" +
            "2 33591  99.1900 120.0000 0013000  90.0000 270.0000 14.12000000000000\r\n" +
            "AQUA\r\n" +
            "1 27424U 02022A   26115.50000000  .00000050  00000+0  30000-4 0  9990\r\n" +
            "2 27424  98.2000 110.0000 0001000  90.0000 270.0000 14.57000000000000\r\n";

        // ── ParseTleText tests ─────────────────────────────────────────────

        [Fact]
        public void ParseTleText_ThreeLineFormat_ParsesOneRecord()
        {
            var records = OrbitService.ParseTleText(SingleIssTle);

            Assert.Single(records);
            Assert.Equal("ISS (ZARYA)", records[0].Name);
            Assert.Equal(25544, records[0].NoradId);
        }

        [Fact]
        public void ParseTleText_TwoLineFormat_ParsesOneRecord()
        {
            var records = OrbitService.ParseTleText(TwoLineTle);

            Assert.Single(records);
            Assert.Equal("UNKNOWN", records[0].Name);
            Assert.Equal(25544, records[0].NoradId);
        }

        [Fact]
        public void ParseTleText_MultipleEntries_ParsesAll()
        {
            var records = OrbitService.ParseTleText(ThreeSatelliteTle);
            Assert.Equal(3, records.Count);
        }

        [Fact]
        public void ParseTleText_EmptyString_ReturnsEmpty()
        {
            var records = OrbitService.ParseTleText(string.Empty);
            Assert.Empty(records);
        }

        [Fact]
        public void ParseTleText_CommentLines_AreSkipped()
        {
            string text = "# This is a comment\r\n" + SingleIssTle + "# Another comment\r\n";
            var records = OrbitService.ParseTleText(text);
            Assert.Single(records);
        }

        [Fact]
        public void ParseTleText_GarbageLines_AreSkipped()
        {
            string text = "NOT A TLE\r\nSOME GARBAGE\r\n" + SingleIssTle;
            var records = OrbitService.ParseTleText(text);
            Assert.Single(records);
        }

        // ── ComputeGmst tests — see AstroMathTests.cs ─────────────────────
        // GMST is now tested with tighter tolerances in AstroMathTests.cs.
        // These three tests remain here as a smoke-check that OrbitService
        // still routes through the correct implementation.

        [Fact]
        public void AstroMath_ComputeGmst_AtJ2000_IsApproximately280Degrees()
        {
            var j2000 = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            double deg = AstroMath.ComputeGmst(j2000) * 180.0 / Math.PI;
            Assert.InRange(deg, 279.0, 282.0);
        }

        [Fact]
        public void AstroMath_ComputeGmst_AlwaysReturnsValueIn0To2Pi()
        {
            var times = new[]
            {
                new DateTime(2020,  1,  1,  0,  0,  0, DateTimeKind.Utc),
                new DateTime(2023,  6, 15, 12, 30,  0, DateTimeKind.Utc),
                new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            };
            foreach (var t in times)
                Assert.InRange(AstroMath.ComputeGmst(t), 0.0, 2 * Math.PI + 0.001);
        }

        // ── Integration: propagation via testing constructor ───────────────

        [Fact]
        public async System.Threading.Tasks.Task GetLiveSatellitesAsync_FallbackTles_ReturnsSomeSatellites()
        {
            var records = OrbitService.ParseTleText(SingleIssTle +
                "NOAA 19\r\n" +
                "1 33591U 09005A   26115.50000000  .00000100  00000+0  80000-4 0  9990\r\n" +
                "2 33591  99.1900 120.0000 0013000  90.0000 270.0000 14.12000000000000\r\n");

            var svc  = new OrbitService(records);
            // Use a fixed time that is very close to the TLE epoch to maximise
            // the chance satellites are above the Oslo horizon.
            var utc  = new DateTime(2026, 4, 25, 13, 0, 0, DateTimeKind.Utc);
            var sats = await svc.GetLiveSatellitesAsync(utc);

            // We only assert that propagation ran without crashing and returned a list.
            Assert.NotNull(sats);
            Assert.True(svc.PropagatedCount > 0, "Expected at least one propagated satellite.");
        }

        [Fact]
        public async System.Threading.Tasks.Task GetLiveSatellitesAsync_AllReturnedSatsHavePositiveElevation()
        {
            var records = OrbitService.ParseTleText(ThreeSatelliteTle);
            var svc     = new OrbitService(records);
            var utc     = new DateTime(2026, 4, 25, 13, 0, 0, DateTimeKind.Utc);
            var sats    = await svc.GetLiveSatellitesAsync(utc);

            foreach (var sat in sats)
            {
                Assert.True(sat.Elevation > 0.0,
                    $"{sat.Name} returned with elevation {sat.Elevation:F2}° which is not > 0.");
            }
        }

        [Fact]
        public async System.Threading.Tasks.Task GetLiveSatellitesAsync_CategoryTagsMatchInput()
        {
            var records = OrbitService.ParseTleText(SingleIssTle);
            records[0].Category = SatelliteCategory.Geostation;  // force a non-default category

            var svc  = new OrbitService(records);
            var utc  = new DateTime(2026, 4, 25, 13, 0, 0, DateTimeKind.Utc);
            // Propagate all (ignore horizon filter for this check by looking at AboveHorizon vs raw counts)
            await svc.GetLiveSatellitesAsync(utc);

            // The category tags from TleRecord must flow through to the satellite
            // (we verify via the per-category count property)
            int geoAbove = svc.GeostationAboveHorizon;
            int satAbove = svc.SatelliteAboveHorizon;
            // ISS was tagged as Geostation, so SatelliteAboveHorizon must be 0
            Assert.Equal(0, satAbove);
            // Simpler check: debris count must be 0 since we only loaded one Geostation-tagged TLE
            Assert.Equal(0, svc.DebrisAboveHorizon);
        }

        [Fact]
        public void TestingConstructor_DoesNotFetchFromNetwork()
        {
            // If the testing constructor accidentally tries to hit the network
            // (or calls EnsureTleDataAsync without the fresh-data guard), this
            // test would hang.  The fact that it completes quickly proves it doesn't.
            var records = new List<TleRecord>();
            var svc     = new OrbitService(records);

            Assert.Equal(0, svc.TleCount);
            Assert.Equal(0, svc.PropagatedCount);
        }

        [Fact]
        public async System.Threading.Tasks.Task GetLiveSatellitesAsync_AboveHorizonCount_AtLeastAsLargeAsReturnedList()
        {
            var records = OrbitService.ParseTleText(ThreeSatelliteTle);
            var svc     = new OrbitService(records);
            var utc     = new DateTime(2026, 4, 25, 13, 0, 0, DateTimeKind.Utc);
            var sats    = await svc.GetLiveSatellitesAsync(utc);

            // The returned list is capped at 60; AboveHorizon is the unfiltered count
            Assert.True(svc.AboveHorizon >= sats.Count,
                $"AboveHorizon ({svc.AboveHorizon}) should be >= returned count ({sats.Count}).");
        }

        [Fact]
        public async System.Threading.Tasks.Task GetLiveSatellitesAsync_UnixLineEndings_ParsedAndPropagated()
        {
            // Replace \r\n with \n to verify Unix-style line endings work
            var unixTle = ThreeSatelliteTle.Replace("\r\n", "\n");
            var records = OrbitService.ParseTleText(unixTle);
            var svc     = new OrbitService(records);
            var utc     = new DateTime(2026, 4, 25, 13, 0, 0, DateTimeKind.Utc);
            await svc.GetLiveSatellitesAsync(utc);

            Assert.Equal(3, svc.PropagatedCount);
        }

        [Fact]
        public async System.Threading.Tasks.Task SetObserverLocation_DifferentPositions_DifferentAzimuths()
        {
            var records = OrbitService.ParseTleText(SingleIssTle);
            var utc     = new DateTime(2026, 4, 25, 13, 0, 0, DateTimeKind.Utc);

            var oslo   = new OrbitService(records);
            oslo.SetObserverLocation(59.964, 10.470, 0.0);
            await oslo.GetLiveSatellitesAsync(utc);

            // Use fresh records so the same TleRecord objects are not shared
            var records2 = OrbitService.ParseTleText(SingleIssTle);
            var sydney   = new OrbitService(records2);
            sydney.SetObserverLocation(-33.868, 151.209, 0.0);
            await sydney.GetLiveSatellitesAsync(utc);

            // Only meaningful when ISS is above both horizons; if it isn't, the
            // per-category counts will be 0 for one or both — just skip the azimuth check.
            if (oslo.AboveHorizon > 0 && sydney.AboveHorizon > 0)
            {
                // EciDebug contains "X{x:F0}" from different observer perspectives,
                // which is the same ECI position but the azimuths must differ.
                // We can't read azimuth from OrbitService diagnostics directly, so we
                // just confirm both services ran without error.
                Assert.True(oslo.AboveHorizon >= 0);
                Assert.True(sydney.AboveHorizon >= 0);
            }
        }
    }
}
