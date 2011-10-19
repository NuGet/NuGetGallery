
namespace NuGetGallery
{
    public class GallerySetting : IEntity
    {
        public int Key { get; set; }
        public int? DownloadStatsLastAggregatedId { get; set; }
        public int? SmtpPort { get; set; }
        public string SmtpUsername { get; set; }
        public string SmtpHost { get; set; }
    }
}