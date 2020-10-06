using System.Web.Mvc;

namespace NuGetGallery
{
    public class PackageListSearchViewModel
    {
        [AllowHtml]
        public string Q { get; set; }
        public int Page { get; set; }
        public bool? Prerel { get; set; }
        public string SortBy { get; set; }
        public string PackageType { get; set; }
        public bool? TestData { get; set; }
    }
}