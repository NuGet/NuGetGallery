
namespace NuGetGallery
{
    public class GallerySetting : IEntity
    {
        public int Key { get; set; }
        public int? DownloadStatsLastAggregatedId { get; set; }
        public string SmtpHost { get; set; }
        public string SmtpUsername { get; set; }
        public string SmtpPassword { get; set; }
        public int? SmtpPort { get; set; }
        public bool UseSmtp { get; set; }
        public string GalleryOwnerName { get; set; }
        public string GalleryOwnerEmail { get; set; }
        public bool ConfirmEmailAddresses { get; set; }
    }
}