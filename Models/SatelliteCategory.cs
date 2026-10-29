namespace HololensSatelliteViewer.Models
{
    /// <summary>
    /// Classifies a tracked object by its orbital / operational type.
    /// The numeric values are stable — Satellite = 0 is the default so that
    /// fallback TLEs (whose Category is never explicitly set) are treated as
    /// ordinary satellites.
    /// </summary>
    public enum SatelliteCategory
    {
        Satellite   = 0,   // operational / visual satellites (GROUP=visual)
        Geostation  = 1,   // geostationary satellites       (GROUP=geo)
        Debris      = 2    // tracked debris / rocket bodies  (NAME=DEB)
    }
}
