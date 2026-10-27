using System;

namespace HololensSatelliteViewer.Utilities
{
    public static class GeohashConverter
    {
        private const string Base32 = "0123456789bcdefghjkmnpqrstuvwxyz";

        public static GeohashCoordinate Decode(string geohash)
        {
            if (string.IsNullOrWhiteSpace(geohash))
            {
                throw new ArgumentException("Geohash code cannot be empty.", nameof(geohash));
            }

            bool isEvenBit = true;
            double minLatitude = -90.0;
            double maxLatitude = 90.0;
            double minLongitude = -180.0;
            double maxLongitude = 180.0;

            foreach (char character in geohash.Trim().ToLowerInvariant())
            {
                int value = Base32.IndexOf(character);
                if (value < 0)
                {
                    throw new ArgumentException(
                        string.Format("Invalid geohash character: {0}", character),
                        nameof(geohash));
                }

                for (int mask = 16; mask > 0; mask >>= 1)
                {
                    if (isEvenBit)
                    {
                        double midpoint = (minLongitude + maxLongitude) / 2.0;
                        if ((value & mask) != 0) minLongitude = midpoint;
                        else maxLongitude = midpoint;
                    }
                    else
                    {
                        double midpoint = (minLatitude + maxLatitude) / 2.0;
                        if ((value & mask) != 0) minLatitude = midpoint;
                        else maxLatitude = midpoint;
                    }

                    isEvenBit = !isEvenBit;
                }
            }

            return new GeohashCoordinate(
                (minLatitude + maxLatitude) / 2.0,
                (minLongitude + maxLongitude) / 2.0);
        }
    }

    public sealed class GeohashCoordinate
    {
        public GeohashCoordinate(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
    }
}
