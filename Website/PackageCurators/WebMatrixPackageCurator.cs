using System.IO;
using System.Linq;
using NuGet;

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
                    automaticallyCurated: true,
                    commitChanges: commitChanges);
            }
        }

        internal static bool ShouldCuratePackage(
            CuratedFeed curatedFeed, 
            Package galleryPackage,
            INupkg nugetPackage)
        {
            if (!galleryPackage.IsLatestStable)
            {
                return false;
            }

            bool shouldBeIncluded = galleryPackage.Tags != null && 
                galleryPackage.Tags.ToLowerInvariant().Contains("aspnetwebpages");

            if (!shouldBeIncluded)
            {
                shouldBeIncluded = true;
                foreach (var filePath in nugetPackage.GetFiles())
                {
                    var fi = new FileInfo(filePath);
                    if (fi.Extension == ".ps1" || fi.Extension == ".t4")
                    {
                        return false;
                    }
                }
            }

            if (!shouldBeIncluded)
            {
                return false;
            }

            return DependenciesAreCurated(galleryPackage, curatedFeed);
        }
    }
}