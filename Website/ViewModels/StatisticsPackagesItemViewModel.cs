
namespace NuGetGallery
{
    public class StatisticsPackagesItemViewModel
    {
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public string Client { get; set; }
        public string Operation { get; set; }
        public int Downloads { get; set; }
    }
}