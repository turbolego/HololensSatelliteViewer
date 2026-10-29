using System;
using System.Globalization;
using HololensSatelliteViewer.Models;

namespace HololensSatelliteViewer.Services
{
    public class Sgp4Service
    {
        private const double TwoPi       = 2.0 * Math.PI;
        private const double MinutesPerDay = 1440.0;
        private const double EarthRadiusKm = 6378.137;
        private const double Xke          = 0.0743669161;   // sqrt(GM) in Earth-radii^3/min^2
        private const double J2           = 0.00108262998905;

        public Satellite Propagate(TleRecord tle, DateTime utcTime)
        {
            if (tle == null)  throw new ArgumentNullException(nameof(tle));
            if (string.IsNullOrEmpty(tle.Line1) || tle.Line1.Length < 69)
                throw new ArgumentException("TLE Line1 must be at least 69 characters.", nameof(tle));
            if (string.IsNullOrEmpty(tle.Line2) || tle.Line2.Length < 69)
                throw new ArgumentException("TLE Line2 must be at least 69 characters.", nameof(tle));

            var el = ParseTle(tle.Line1, tle.Line2);
            var minutesSinceEpoch = (utcTime - el.Epoch).TotalMinutes;

            // ── Kepler / simplified SGP4 ─────────────────────────────────────
            // n is in rad/min, a is in Earth radii (dimensionless)
            var n = el.MeanMotion * TwoPi / MinutesPerDay;
            var a = Math.Pow(Xke / n, 2.0 / 3.0);

            // First-order J2 perturbation
            var delta = 1.5 * J2 * (3.0 * el.CosI * el.CosI - 1.0)
                        / Math.Pow(1.0 - el.Eccentricity * el.Eccentricity, 1.5);
            var nPrime = n / (1.0 + delta);
            var aPrime = a * (1.0 - delta);

            // Mean anomaly at epoch + propagated (rad)
            var M = (el.MeanAnomaly + nPrime * minutesSinceEpoch) % TwoPi;
            if (M < 0) M += TwoPi;

            // Solve Kepler's equation  M = E - e*sin(E)
            var E = SolveKepler(M, el.Eccentricity);

            var cosE = Math.Cos(E);
            var sinE = Math.Sin(E);

            // True anomaly
            var nu = 2.0 * Math.Atan2(
                Math.Sqrt(1.0 + el.Eccentricity) * sinE,
                Math.Sqrt(1.0 - el.Eccentricity) * (cosE - el.Eccentricity));

            // Orbital radius in Earth radii
            var r = aPrime * (1.0 - el.Eccentricity * cosE);

            // Position in orbital plane (Earth radii)
            var u    = el.ArgumentOfPerigee + nu;
            var xOrb = r * Math.Cos(u);
            var yOrb = r * Math.Sin(u);

            // Rotate to ECI (still in Earth radii at this point)
            var cosRaan = Math.Cos(el.Raan);
            var sinRaan = Math.Sin(el.Raan);
            var cosI    = el.CosI;
            var sinI    = Math.Sin(Math.Acos(cosI));

            var xEciRe = xOrb * cosRaan - yOrb * cosI * sinRaan;
            var yEciRe = xOrb * sinRaan + yOrb * cosI * cosRaan;
            var zEciRe = yOrb * sinI;

            // ── UNITS FIX: convert Earth radii → km ─────────────────────────
            // The Kepler solver produces r in Earth radii (a dimensionless ~1.065
            // for LEO). OrbitService computes the observer in km, so we must match.
            var xEci = xEciRe * EarthRadiusKm;
            var yEci = yEciRe * EarthRadiusKm;
            var zEci = zEciRe * EarthRadiusKm;

            // Geodetic latitude (ECI Z component gives geocentric lat directly)
            var lat = Math.Atan2(zEci, Math.Sqrt(xEci * xEci + yEci * yEci));

            // ── Geodetic longitude: subtract GMST to go from ECI → ECEF ─────
            // ECI X-axis points at the vernal equinox; ECEF X-axis points at
            // Greenwich. The angle between them at a given moment is GMST.
            var gmst   = AstroMath.ComputeGmst(utcTime);
            var lonEci = Math.Atan2(yEci, xEci);
            var lon    = lonEci - gmst;
            // Normalise to [-π, π]
            while (lon >  Math.PI) lon -= TwoPi;
            while (lon < -Math.PI) lon += TwoPi;

            var altitudeKm = Math.Sqrt(xEci * xEci + yEci * yEci + zEci * zEci)
                             - EarthRadiusKm;

            // Approximate orbital speed at this radius (vis-viva, circular approx)
            var velocity = Math.Sqrt(398600.4418 / (r * EarthRadiusKm));

            return new Satellite
            {
                Name             = tle.Name,
                NoradId          = tle.NoradId,
                Latitude         = lat * 180.0 / Math.PI,
                Longitude        = lon * 180.0 / Math.PI,
                AltitudeKm       = altitudeKm,
                VelocityKmPerSec = velocity,
                Timestamp        = utcTime,
                // Hand ECI km coordinates to OrbitService for topocentric transform
                X = xEci,
                Y = yEci,
                Z = zEci
            };
        }

        // ── Kepler solver (Newton–Raphson) ────────────────────────────────────
        private static double SolveKepler(double M, double e)
        {
            var E = M;
            for (int i = 0; i < 15; i++)
            {
                var dE = (M - E + e * Math.Sin(E)) / (1.0 - e * Math.Cos(E));
                E += dE;
                if (Math.Abs(dE) < 1e-10) break;
            }
            return E;
        }

        // ── TLE parser ────────────────────────────────────────────────────────
        private static OrbitalElements ParseTle(string line1, string line2)
        {
            var epochYear = int.Parse(line1.Substring(18, 2), CultureInfo.InvariantCulture);
            var epochDay  = double.Parse(line1.Substring(20, 12), CultureInfo.InvariantCulture);

            // Validate epoch fields
            if (epochYear < 0 || epochYear > 99)
                throw new ArgumentException(
                    $"TLE epoch year {epochYear} out of range [0, 99].", nameof(line1));
            if (epochDay < 1.0 || epochDay > 366.99)
                throw new ArgumentException(
                    $"TLE epoch day {epochDay} out of range [1.0, 366.99].", nameof(line1));

            var fullYear = epochYear < 57 ? 2000 + epochYear : 1900 + epochYear;
            var epoch    = new DateTime(fullYear, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                           .AddDays(epochDay - 1.0);

            var incDeg   = double.Parse(line2.Substring(8,  8).Trim(), CultureInfo.InvariantCulture);
            var raanDeg  = double.Parse(line2.Substring(17, 8).Trim(), CultureInfo.InvariantCulture);
            var ecc      = double.Parse("0." + line2.Substring(26, 7).Trim(), CultureInfo.InvariantCulture);
            var aopDeg   = double.Parse(line2.Substring(34, 8).Trim(), CultureInfo.InvariantCulture);
            var maDeg    = double.Parse(line2.Substring(43, 8).Trim(), CultureInfo.InvariantCulture);
            var mm       = double.Parse(line2.Substring(52, 11).Trim(), CultureInfo.InvariantCulture);

            // Validate orbital parameters
            if (incDeg < 0.0 || incDeg > 180.0)
                throw new ArgumentException(
                    $"TLE inclination {incDeg}° out of range [0, 180].", nameof(line2));
            if (ecc < 0.0 || ecc >= 1.0)
                throw new ArgumentException(
                    $"TLE eccentricity {ecc} out of range [0, 1).", nameof(line2));
            if (mm <= 0.0)
                throw new ArgumentException(
                    $"TLE mean motion {mm} must be positive.", nameof(line2));

            var inc = incDeg * Math.PI / 180.0;

            return new OrbitalElements
            {
                Epoch             = epoch,
                Inclination       = inc,
                Raan              = raanDeg * Math.PI / 180.0,
                Eccentricity      = ecc,
                ArgumentOfPerigee = aopDeg  * Math.PI / 180.0,
                MeanAnomaly       = maDeg   * Math.PI / 180.0,
                MeanMotion        = mm,
                CosI              = Math.Cos(inc)
            };
        }

        private class OrbitalElements
        {
            public DateTime Epoch             { get; set; }
            public double   Inclination       { get; set; }
            public double   Raan              { get; set; }
            public double   Eccentricity      { get; set; }
            public double   ArgumentOfPerigee { get; set; }
            public double   MeanAnomaly       { get; set; }
            public double   MeanMotion        { get; set; }
            public double   CosI              { get; set; }
        }
    }
}
