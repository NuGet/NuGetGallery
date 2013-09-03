namespace NuGetGallery
{
    public class VerifyPackageRequest
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string LicenseUrl { get; set; }

        public bool Listed { get; set; }
        public EditPackageVersionRequest Edit { get; set; }
    }
}