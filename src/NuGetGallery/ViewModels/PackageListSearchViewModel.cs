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
        public string Frameworks { get; set; }
        public string Tfms { get; set; }
        public bool? IncludeComputedFrameworks { get; set; }
        public string FrameworkFilterMode { get; set; }
        public string PackageType { get; set; }
        public bool? TestData { get; set; }
    }
}