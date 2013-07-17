namespace NuGetGallery
{
    // These guys are no longer referenced by code, but they are still referenced by AggregateStatistics.sql, so need to be part of the data model.
    public class GallerySetting : IEntity
    {
        public int? DownloadStatsLastAggregatedId { get; set; }
        public long? TotalDownloadCount { get; set; }
        public int Key { get; set; }
        public string LastLicenseReport { get; set; }
    }
}