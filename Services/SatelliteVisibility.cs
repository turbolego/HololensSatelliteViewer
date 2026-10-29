using HololensSatelliteViewer.Models;

namespace HololensSatelliteViewer.Services
{
    internal static class SatelliteVisibility
    {
        internal static bool IsVisible(
            Satellite satellite,
            bool showSatellites,
            bool showGeostations,
            bool showDebris)
        {
            switch (satellite.Category)
            {
                case SatelliteCategory.Geostation: return showGeostations;
                case SatelliteCategory.Debris: return showDebris;
                default: return showSatellites;
            }
        }
    }
}