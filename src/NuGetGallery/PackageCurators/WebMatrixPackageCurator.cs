using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class WebMatrixPackageCurator : AutomaticPackageCurator
    {
        public override void Curate(
            Package galleryPackage,
            INupkg nugetPackage,
            bool commitChanges)
        {
            var curatedFeed = GetService<ICuratedFeedService>().GetFeedByName("webmatrix", includePackages: true);
            if (curatedFeed == null)
            {
                return;
            }

            var shouldBeIncluded = ShouldCuratePackage(curatedFeed, galleryPackage, nugetPackage);
            if (shouldBeIncluded)
            {
                GetService<ICuratedFeedService>().CreatedCuratedPackage(
                    curatedFeed,
                    galleryPackage.PackageRegistration,
                    included: true,
                    automaticallyCurated: true,
                    commitChanges: commitChanges);
            }
        }

        internal static bool ShouldCuratePackage(
            CuratedFeed curatedFeed, 
            Package galleryPackage,
            INupkg nugetPackage)
        {
            return 
                // Must have min client version of null or <= 2.2
                (nugetPackage.Metadata.MinClientVersion == null || nugetPackage.Metadata.MinClientVersion <= new Version(2, 2)) &&

                // Must be latest stable
                galleryPackage.IsLatestStable &&

                // Must support net40
                SupportsNet40(galleryPackage) &&

                // Dependencies on the gallery must be curated
                DependenciesAreCurated(galleryPackage, curatedFeed) &&

                (
                    // Must have AspNetWebPages tag
                    ContainsAspNetWebPagesTag(galleryPackage) ||

                    // OR: Must not contain powershell or T4
                    DoesNotContainUnsupportedFiles(nugetPackage)
                );
        }

        private static bool ContainsAspNetWebPagesTag(Package galleryPackage)
        {
            return galleryPackage.Tags != null &&
                galleryPackage.Tags.ToLowerInvariant().Contains("aspnetwebpages");
        }

        private static bool SupportsNet40(Package galleryPackage)
        {
            var net40fx = new FrameworkName(".NETFramework", new Version(4, 0));
            return (galleryPackage.SupportedFrameworks.Count == 0) ||
                (from fx in galleryPackage.SupportedFrameworks
                 let fxName = VersionUtility.ParseFrameworkName(fx.TargetFramework)
                 where fxName == net40fx
                 select fx)
                .Any();
        }

        private static bool DoesNotContainUnsupportedFiles(INupkg nugetPackage)
        {
            foreach (var filePath in nugetPackage.GetFiles())
            {
                var fi = new FileInfo(filePath);
                if (fi.Extension == ".ps1" || fi.Extension == ".t4")
                {
                    return false;
                }
            }
            return true;
        }
    }
}