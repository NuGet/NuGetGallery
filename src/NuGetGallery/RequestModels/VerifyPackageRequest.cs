using System;

namespace NuGetGallery
{
    public class VerifyPackageRequest
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string LicenseUrl { get; set; }

        public bool Listed { get; set; }
        public EditPackageVersionRequest Edit { get; set; }
        public Version MinClientVersion { get; set; }
        public bool DevelopmentDependency { get; set; }

        public string Language { get; set; }
    }
}