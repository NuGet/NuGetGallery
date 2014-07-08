using System;
using System.Collections.Generic;
using NuGet;

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
        public string Language { get; set; }

        public IEnumerable<NuGet.PackageDependencySet> DependencySets { get; set; }
        public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies { get; set; }
    }
}