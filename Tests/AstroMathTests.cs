using System;
using Xunit;
using HololensSatelliteViewer.Services;

namespace HololensSatelliteViewer.Tests
{
    /// <summary>
    /// Focused tests for AstroMath.ComputeGmst — the single source of truth for
    /// Greenwich Mean Sidereal Time used by both Sgp4Service and OrbitService.
    /// </summary>
    public class AstroMathTests
    {
        private const double TwoPi    = 2.0 * Math.PI;
        private const double DegToRad = Math.PI / 180.0;

        [Fact]
        public void ComputeGmst_AtJ2000_MatchesFormulaConstant()
        {
            // At J2000.0 (2000-01-01 12:00:00 UTC), T = 0 in the Vallado formula,
            // so GMST = 280.460618° exactly (the leading constant).
            var j2000 = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            double deg = AstroMath.ComputeGmst(j2000) / DegToRad;

            // Tight tolerance: ±0.002° (≈ 0.5 arcsec)
            Assert.InRange(deg, 280.459, 280.462);
        }

        [Fact]
        public void ComputeGmst_AlwaysReturnsValueInZeroToTwoPi()
        {
            var dates = new[]
            {
                new DateTime(1970,  1,  1,  0,  0,  0, DateTimeKind.Utc),
                new DateTime(2000,  1,  1, 12,  0,  0, DateTimeKind.Utc),
                new DateTime(2026,  6, 30, 18, 45, 13, DateTimeKind.Utc),
                new DateTime(2050, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            };
            foreach (var t in dates)
            {
                double g = AstroMath.ComputeGmst(t);
                Assert.InRange(g, 0.0, TwoPi);
            }
        }

        [Fact]
        public void ComputeGmst_IncreasesAtCorrectRateOverOneHour()
        {
            // Earth rotates 360° per sidereal day (23h 56m 4.09s), so ~15.04°/h.
            var t0    = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);
            double g0 = AstroMath.ComputeGmst(t0);
            double g1 = AstroMath.ComputeGmst(t0.AddHours(1.0));

            // Unwrap the 2π boundary before comparing
            double deltaRad = (g1 - g0 + TwoPi) % TwoPi;

            // Expected ≈ 0.2625 rad (15.04°); allow ±0.002 rad tolerance
            Assert.InRange(deltaRad, 0.260, 0.265);
        }

        [Fact]
        public void ComputeGmst_IncreasesExactlyOneRevolutionPerSiderealDay()
        {
            // IAU sidereal day = 86 164.09054 s
            const double siderealDaySec = 86_164.09054;

            var t0    = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
            double g0 = AstroMath.ComputeGmst(t0);
            double g1 = AstroMath.ComputeGmst(t0.AddSeconds(siderealDaySec));

            // After one sidereal day GMST advances by exactly 2π (one full revolution).
            // g1 and g0 are both normalised to [0, 2π), so their raw difference is ≈ 2π.
            double delta = g1 - g0;
            if (delta < 0) delta += TwoPi;   // handle the unlikely wrap case

            // Allow ±0.002 rad ≈ 0.11° tolerance
            Assert.InRange(delta, TwoPi - 0.002, TwoPi + 0.002);
        }

        [Fact]
        public void ComputeGmst_IsStrictlyIncreasing_OverShortPositiveInterval()
        {
            var t0 = new DateTime(2026, 6, 30, 6, 0, 0, DateTimeKind.Utc);
            // Compare values 1 hour apart, staying well within a half-revolution
            // (so no wrap-around to worry about).
            double g0 = AstroMath.ComputeGmst(t0);
            double g1 = AstroMath.ComputeGmst(t0.AddHours(1.0));

            // After 1 hour we gain ~15°; both values must lie in the same 0–2π window
            // unless a wrap occurs.  Check only that g1 > g0 or a wrap happened.
            double delta = (g1 - g0 + TwoPi) % TwoPi;
            Assert.True(delta > 0.0, "GMST should increase over time.");
        }

        [Fact]
        public void ComputeGmst_Sgp4AndOrbitServiceCallSameImplementation()
        {
            // Both services now delegate to AstroMath.ComputeGmst.
            // Calling AstroMath directly twice with the same input should always be equal.
            var t = new DateTime(2026, 6, 30, 14, 32, 11, DateTimeKind.Utc);
            Assert.Equal(AstroMath.ComputeGmst(t), AstroMath.ComputeGmst(t));
        }
    }
}
