using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HololensSatelliteViewer.Services
{
    public sealed class LocationPreset
    {
        public LocationPreset(string name, double latitude, double longitude)
        {
            Name = name;
            Latitude = latitude;
            Longitude = longitude;
        }

        public string Name { get; private set; }
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
    }

    public sealed class AirportLocation
    {
        public AirportLocation(string iata, string name, double latitude, double longitude)
        {
            Iata = iata;
            Name = name;
            Latitude = latitude;
            Longitude = longitude;
        }

        public string Iata { get; private set; }
        public string Name { get; private set; }
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
    }

    public static class LocationSettingsModel
    {
        private static readonly IList<LocationPreset> PresetList = new List<LocationPreset>
        {
            new LocationPreset("Oslo, Norway", 59.9139, 10.7522),
            new LocationPreset("London, UK", 51.5074, -0.1278),
            new LocationPreset("New York, USA", 40.7128, -74.0060),
            new LocationPreset("Tokyo, Japan", 35.6762, 139.6503),
            new LocationPreset("Sydney, Australia", -33.8688, 151.2093)
        };

        public static IList<LocationPreset> Presets
        {
            get { return PresetList; }
        }

        public static AirportLocation[] ParseAirportCatalog(IEnumerable<string> lines)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));

            var airports = new List<AirportLocation>();
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] fields = line.Split('\t');
                if (fields.Length == 4 && fields[0] == "iata")
                {
                    continue;
                }

                double latitude;
                double longitude;
                if (fields.Length != 4 ||
                    string.IsNullOrWhiteSpace(fields[0]) ||
                    string.IsNullOrWhiteSpace(fields[1]) ||
                    !double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out latitude) ||
                    !double.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out longitude) ||
                    !IsValidCoordinate(latitude, longitude))
                {
                    throw new FormatException("An airport catalog row contains invalid airport data.");
                }

                airports.Add(new AirportLocation(fields[0], fields[1], latitude, longitude));
            }

            return airports.ToArray();
        }

        public static AirportLocation[] FindThreeClosestAirports(
            IEnumerable<AirportLocation> airports,
            double latitude,
            double longitude)
        {
            if (airports == null) throw new ArgumentNullException(nameof(airports));
            if (!IsValidCoordinate(latitude, longitude))
            {
                throw new ArgumentOutOfRangeException(nameof(latitude), "Coordinates must be within valid latitude and longitude ranges.");
            }

            return airports
                .OrderBy(airport => HaversineMeters(latitude, longitude, airport.Latitude, airport.Longitude))
                .Take(3)
                .ToArray();
        }

        public static bool TryParseCoordinateString(
            string input,
            out double latitude,
            out double longitude)
        {
            latitude = 0.0;
            longitude = 0.0;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var parts = input.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 ||
                !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out latitude) ||
                !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out longitude))
            {
                return false;
            }

            return !double.IsNaN(latitude) &&
                !double.IsInfinity(latitude) &&
                !double.IsNaN(longitude) &&
                !double.IsInfinity(longitude) &&
                latitude >= -90.0 &&
                latitude <= 90.0 &&
                longitude >= -180.0 &&
                longitude <= 180.0;
        }

        public static string FormatCoordinates(double latitude, double longitude)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:F5}, {1:F5}", latitude, longitude);
        }

        public static string FormatStep(double step)
        {
            return step.ToString("0.###", CultureInfo.InvariantCulture) + "°";
        }

        public static double AdjustLatitude(double latitude, double deltaDegrees)
        {
            return Math.Round(
                Math.Max(-90.0, Math.Min(90.0, latitude + deltaDegrees)),
                4,
                MidpointRounding.AwayFromZero);
        }

        public static double AdjustLongitude(double longitude, double deltaDegrees)
        {
            double adjusted = longitude + deltaDegrees;
            while (adjusted > 180.0) adjusted -= 360.0;
            while (adjusted <= -180.0) adjusted += 360.0;
            return Math.Round(adjusted, 4, MidpointRounding.AwayFromZero);
        }

        public static bool TryDecodeGeohash(string geohash, out double latitude, out double longitude)
        {
            latitude = 0.0;
            longitude = 0.0;
            if (string.IsNullOrWhiteSpace(geohash))
            {
                return false;
            }

            try
            {
                var coordinate = Utilities.GeohashConverter.Decode(geohash);
                latitude = coordinate.Latitude;
                longitude = coordinate.Longitude;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool IsValidCoordinate(double latitude, double longitude)
        {
            return !double.IsNaN(latitude) &&
                !double.IsInfinity(latitude) &&
                !double.IsNaN(longitude) &&
                !double.IsInfinity(longitude) &&
                latitude >= -90.0 &&
                latitude <= 90.0 &&
                longitude >= -180.0 &&
                longitude <= 180.0;
        }

        private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusMeters = 6371000.0;
            double phi1 = lat1 * Math.PI / 180.0;
            double phi2 = lat2 * Math.PI / 180.0;
            double deltaPhi = (lat2 - lat1) * Math.PI / 180.0;
            double deltaLambda = (lon2 - lon1) * Math.PI / 180.0;
            double sinLatitude = Math.Sin(deltaPhi / 2.0);
            double sinLongitude = Math.Sin(deltaLambda / 2.0);
            double a = sinLatitude * sinLatitude +
                Math.Cos(phi1) * Math.Cos(phi2) * sinLongitude * sinLongitude;
            a = Math.Max(0.0, Math.Min(1.0, a));
            return earthRadiusMeters * 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        }
    }
}
