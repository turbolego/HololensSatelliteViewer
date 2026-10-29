using System;

namespace HololensSatelliteViewer.Services
{
    /// <summary>
    /// Shared astrodynamics math helpers used by both Sgp4Service and OrbitService.
    /// Keeping these in one place ensures the two services never drift out of sync.
    /// </summary>
    internal static class AstroMath
    {
        private const double TwoPi = 2.0 * Math.PI;

        /// <summary>
        /// Greenwich Mean Sidereal Time in radians (Vallado 2013, eq. 3-45).
        /// Valid to ~0.1 arcsec for dates within a few decades of J2000.
        /// </summary>
        /// <param name="utcTime">Moment in UTC.</param>
        /// <returns>GMST in radians, normalised to [0, 2π).</returns>
        internal static double ComputeGmst(DateTime utcTime)
        {
            int    Y  = utcTime.Year;
            int    M  = utcTime.Month;
            int    D  = utcTime.Day;
            double UT = utcTime.Hour + utcTime.Minute / 60.0
                        + utcTime.Second / 3600.0
                        + utcTime.Millisecond / 3_600_000.0;

            double jd = 367.0 * Y
                       - Math.Floor(7.0 * (Y + Math.Floor((M + 9.0) / 12.0)) / 4.0)
                       + Math.Floor(275.0 * M / 9.0)
                       + D + 1_721_013.5
                       + UT / 24.0;

            const double J2000 = 2_451_545.0;
            double T = (jd - J2000) / 36_525.0;

            double gmstDeg = 280.460_618_37
                           + 360.985_647_366_29 * (jd - J2000)
                           + 0.000_387_933      * T * T
                           - T * T * T / 38_710_000.0;

            gmstDeg = gmstDeg % 360.0;
            if (gmstDeg < 0.0) gmstDeg += 360.0;

            return gmstDeg * Math.PI / 180.0;
        }
    }
}
