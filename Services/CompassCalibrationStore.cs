using Windows.Storage;

namespace HololensSatelliteViewer.Services
{
    public static class CompassCalibrationStore
    {
        private const string Key = "northCalibrationOffset";
        private static readonly ApplicationDataContainer LocalSettings =
            ApplicationData.Current.LocalSettings;

        public static void SaveOffset(float offsetDegrees)
        {
            LocalSettings.Values[Key] = offsetDegrees;
        }

        public static float LoadOffset()
        {
            object value;
            if (LocalSettings.Values.TryGetValue(Key, out value) && value is float)
            {
                return (float)value;
            }

            if (value is double)
            {
                return (float)(double)value;
            }

            return 0.0f;
        }
    }
}
