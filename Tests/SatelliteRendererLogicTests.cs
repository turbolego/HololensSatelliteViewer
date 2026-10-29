using System;
using System.Numerics;
using Xunit;
using HololensSatelliteViewer.Models;
using HololensSatelliteViewer.Services;

namespace HololensSatelliteViewer.Tests
{
    /// <summary>
    /// Unit tests for SatelliteRenderer logic: gaze hit-testing and category visibility.
    /// These tests replicate the internal methods without requiring DirectX resources.
    /// </summary>
    public class SatelliteRendererLogicTests
    {
        // ── Category visibility filter logic ───────────────────────────────

        /// <summary>
        /// Tests the visibility helper used by SatelliteRenderer without DirectX.
        /// </summary>
        [Theory]
        [InlineData(true,  true,  true)]
        [InlineData(false, true,  true)]
        [InlineData(true,  false, true)]
        [InlineData(true,  true,  false)]
        [InlineData(false, false, false)]  // all hidden
        public void IsVisible_CategorySatellite_RespectsSatelliteToggle(bool sats, bool geo, bool deb)
        {
            var sat = new Satellite { Category = SatelliteCategory.Satellite };
            Assert.Equal(sats, SatelliteVisibility.IsVisible(sat, sats, geo, deb));
        }

        [Theory]
        [InlineData(true,  true,  true)]
        [InlineData(true,  false, true)]
        [InlineData(true,  true,  false)]
        [InlineData(false, false, false)]
        public void IsVisible_CategoryGeostation_RespectsGeostationToggle(bool sats, bool geo, bool deb)
        {
            var sat = new Satellite { Category = SatelliteCategory.Geostation };
            Assert.Equal(geo, SatelliteVisibility.IsVisible(sat, sats, geo, deb));
        }

        [Theory]
        [InlineData(true,  true,  true)]
        [InlineData(true,  true,  false)]
        [InlineData(true,  false, true)]
        [InlineData(false, false, false)]
        public void IsVisible_CategoryDebris_RespectsDebrisToggle(bool sats, bool geo, bool deb)
        {
            var sat = new Satellite { Category = SatelliteCategory.Debris };
            Assert.Equal(deb, SatelliteVisibility.IsVisible(sat, sats, geo, deb));
        }

        [Fact]
        public void IsVisible_DefaultCategory_TreatsAsSatellite()
        {
            // SatelliteCategory.Satellite is the default (= 0)
            var sat = new Satellite();  // Category defaults to Satellite
            Assert.True(SatelliteVisibility.IsVisible(sat, true, false, false));
            Assert.False(SatelliteVisibility.IsVisible(sat, false, false, false));
        }

        // ── Gaze hit-test cone logic (internal method) ────────────────────

        /// <summary>
        /// Tests the production ray/sphere hit test used by the toggle buttons.
        /// </summary>
        private const float HitRadius = 0.08f;

        [Fact]
        public void GazeHitsButton_DirectlyAhead_Hits()
        {
            var origin = Vector3.Zero;
            var forward = Vector3.UnitZ;
            var button = new Vector3(0, 0, 5);

            Assert.True(GazeHitTest.Hits(origin, forward, button, HitRadius));
        }

        [Fact]
        public void GazeHitsButton_SlightlyOffAxis_Hits()
        {
            // The target center is within the hit sphere around the gaze ray.
            var origin = Vector3.Zero;
            var forward = Vector3.UnitZ;
            var button = new Vector3(0.05f, 0, 5);

            Assert.True(GazeHitTest.Hits(origin, forward, button, HitRadius));
        }

        [Fact]
        public void GazeHitsButton_OutsideHitRadius_Misses()
        {
            var origin = Vector3.Zero;
            var forward = Vector3.UnitZ;
            var button = new Vector3(0.1f, 0, 5);

            Assert.False(GazeHitTest.Hits(origin, forward, button, HitRadius));
        }

        [Fact]
        public void GazeHitsButton_FarOffAxis_Misses()
        {
            var origin = Vector3.Zero;
            var forward = Vector3.UnitZ;
            var button = new Vector3(3, 0, 5);  // tan(θ) = 3/5 ≈ 31°

            Assert.False(GazeHitTest.Hits(origin, forward, button, HitRadius));
        }

        [Fact]
        public void GazeHitsButton_BehindGaze_Misses()
        {
            var origin = Vector3.Zero;
            var forward = Vector3.UnitZ;
            var button = new Vector3(0, 0, -5);  // behind the gaze direction

            Assert.False(GazeHitTest.Hits(origin, forward, button, HitRadius));
        }

        [Fact]
        public void GazeHitsButton_ZeroDirection_Misses()
        {
            var origin = Vector3.Zero;
            var forward = Vector3.Zero;
            var button = new Vector3(0, 0, 5);

            Assert.False(GazeHitTest.Hits(origin, forward, button, HitRadius));
        }

        [Fact]
        public void GazeHitsButton_DifferentOrigins_ConsistentGeometry()
        {
            // Two observers at different positions, same relative geometry to button
            var origin1 = Vector3.Zero;
            var origin2 = new Vector3(100, 100, 100);
            var forward = Vector3.Normalize(Vector3.UnitX + Vector3.UnitY + Vector3.UnitZ);

            var button1 = origin1 + forward * 5;  // directly ahead
            var button2 = origin2 + forward * 5;

            Assert.True(GazeHitTest.Hits(origin1, forward, button1, HitRadius));
            Assert.True(GazeHitTest.Hits(origin2, forward, button2, HitRadius));
        }

        [Fact]
        public void GazeHitsButton_StackedTargetsAreIndependent()
        {
            var origin = Vector3.Zero;
            var top = new Vector3(0, 0.35f, 1.15f);
            var middle = new Vector3(0, 0.17f, 1.15f);
            var bottom = new Vector3(0, -0.01f, 1.15f);

            Assert.True(GazeHitTest.Hits(origin, top, top, HitRadius));
            Assert.False(GazeHitTest.Hits(origin, top, middle, HitRadius));
            Assert.True(GazeHitTest.Hits(origin, middle, middle, HitRadius));
            Assert.False(GazeHitTest.Hits(origin, middle, bottom, HitRadius));
            Assert.True(GazeHitTest.Hits(origin, bottom, bottom, HitRadius));
        }
    }
}
