using System;

namespace HololensSatelliteViewer.Services
{
    public static class LocationOverrideStore
    {
        private static readonly object SyncRoot = new object();
        private static bool hasOverride;
        private static double latitude;
        private static double longitude;
        private static int generation;

        public static void Set(double newLatitude, double newLongitude)
        {
            double validatedLatitude;
            double validatedLongitude;
            if (!LocationSettingsModel.TryParseCoordinateString(
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0},{1}",
                    newLatitude,
                    newLongitude),
                out validatedLatitude,
                out validatedLongitude))
            {
                throw new ArgumentOutOfRangeException(nameof(newLatitude), "Coordinates must be within valid latitude and longitude ranges.");
            }

            lock (SyncRoot)
            {
                latitude = validatedLatitude;
                longitude = validatedLongitude;
                hasOverride = true;
                generation++;
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                if (!hasOverride)
                {
                    return;
                }

                hasOverride = false;
                latitude = 0.0;
                longitude = 0.0;
                generation++;
            }
        }

        public static bool TryGet(out double currentLatitude, out double currentLongitude)
        {
            lock (SyncRoot)
            {
                currentLatitude = latitude;
                currentLongitude = longitude;
                return hasOverride;
            }
        }

        public static int GetGeneration()
        {
            lock (SyncRoot)
            {
                return generation;
            }
        }
    }
}
