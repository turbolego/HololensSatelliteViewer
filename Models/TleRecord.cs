namespace HololensSatelliteViewer.Models
{
    public class TleRecord
    {
        public string Name { get; set; }
        public string Line1 { get; set; }
        public string Line2 { get; set; }
        public int NoradId { get; set; }
        /// <summary>Set by OrbitService when loading from a category-specific feed.</summary>
        public SatelliteCategory Category { get; set; }
    }
}
