using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Services.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public static class PackageExtensions
    {
        public static string ToShortNameOrNull(this NuGetFramework frameworkName)
        {
            if (frameworkName == null)
            {
                return null;
            }

            var shortFolderName = frameworkName.GetShortFolderName();

            // If the shortFolderName is "any", we want to return null to preserve NuGet.Core
            // compatibility in the V2 feed.
            if (String.Equals(shortFolderName, "any", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return shortFolderName;
        }

        public static string Flatten(this IEnumerable<PackageDependencyGroup> dependencyGroups)
        {
            return FlattenDependencies(
                AsPackageDependencyEnumerable(dependencyGroups).ToList());
        }

        public static string Flatten(this IEnumerable<PackageType> packageTypes)
        {
            return String.Join("|", packageTypes.Select(d => String.Format(CultureInfo.InvariantCulture, "{0}:{1}", d.Name, d.Version)));
        }

        public static string Flatten(this ICollection<PackageDependency> dependencies)
        {
            return
                FlattenDependencies(
                    dependencies.Select(
                        d => new { d.Id, VersionSpec = d.VersionSpec.ToStringSafe(), TargetFramework = d.TargetFramework.ToStringSafe() }));
        }

        public static IEnumerable<PackageDependency> AsPackageDependencyEnumerable(this IEnumerable<PackageDependencyGroup> dependencyGroups)
        {
            foreach (var dependencyGroup in dependencyGroups)
            {
                if (!dependencyGroup.Packages.Any())
                {
                    yield return new PackageDependency
                    {
                        Id = null,
                        VersionSpec = null,
                        TargetFramework = dependencyGroup.TargetFramework.ToShortNameOrNull()
                    };
                }
                else
                {
                    foreach (var dependency in dependencyGroup.Packages.Select(
                        d => new { d.Id, d.VersionRange, dependencyGroup.TargetFramework }))
                    {
                        yield return new PackageDependency
                        {
                            Id = dependency.Id,
                            VersionSpec = dependency.VersionRange?.ToString(),
                            TargetFramework = dependency.TargetFramework.ToShortNameOrNull()
                        };
                    }
                }
            }
        }

        public static IEnumerable<PackageType> AsPackageTypeEnumerable(this IEnumerable<NuGet.Packaging.Core.PackageType> packageTypes)
        {
            foreach (var packageType in packageTypes)
            {
                yield return new PackageType
                {
                    Name = packageType.Name,
                    Version = packageType.Version.ToString()
                };
            }

        }

        private static string FlattenDependencies(IEnumerable<dynamic> dependencies)
        {
            return String.Join(
                "|", dependencies.Select(d => String.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2}", d.Id, d.VersionSpec, d.TargetFramework)));
        }
    }
}
