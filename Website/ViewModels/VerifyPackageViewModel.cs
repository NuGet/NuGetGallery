
namespace NuGetGallery
{
    public class VerifyPackageViewModel : IPackageVersionModel
    {
        public string Id { get; set; }
        public string Version { get; set; }

        public string Authors { get; set; }
        public string Description { get; set; }
        public string LicenseUrl { get; set; }
        public string ProjectUrl { get; set; }
        public bool RequiresLicenseAcceptance { get; set; }
        public string Summary { get; set; }
        public string Tags { get; set; }
        public string Title { get; set; }
        public bool Listed { get; set; }
    }
}