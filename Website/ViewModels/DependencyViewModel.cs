using System;
using NuGet;

namespace NuGetGallery
{
    public class DependencyViewModel
    {
        public DependencyViewModel(PackageDependency dependency)
            : this(dependency.Id, dependency.VersionSpec)
        {
        }

        public DependencyViewModel(string id, string versionSpec)
        {
            Id = id;
            if (!String.IsNullOrEmpty(versionSpec))
            {
                VersionSpec = VersionUtility.PrettyPrint(VersionUtility.ParseVersionSpec(versionSpec));
            }
        }

        public string Id { get; private set; }
        public string VersionSpec { get; private set; }
    }
}