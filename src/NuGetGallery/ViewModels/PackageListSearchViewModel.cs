using System.Web.Mvc;

namespace NuGetGallery
{
    public class PackageListSearchViewModel
    {
        [AllowHtml]
        public string Q { get; set; }
        public int Page { get; set; }
    }
}