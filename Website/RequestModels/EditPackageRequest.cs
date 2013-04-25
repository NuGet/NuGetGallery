
namespace NuGetGallery
{
    public class EditPackageRequest
    {
        public EditPackageRegistrationRequest EditPackageRegistrationRequest { get; set; }
        public EditPackageVersionRequest EditPackageVersionRequest { get; set; }

        public string PackageTitle { get; set; }

        public string Version { get; set; }
    }
}