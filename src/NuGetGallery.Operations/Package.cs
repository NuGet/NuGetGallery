using System;
namespace NuGetGallery.Operations
{
    public class Package
    {
        public string Hash { get; set; }
        public string Id { get; set; }
        public int Key { get; set; }
        public string Version { get; set; }
        public string NormalizedVersion { get; set; }
        public string ExternalPackageUrl { get; set; }
        public DateTime? Created { get; set; }
    }
}
