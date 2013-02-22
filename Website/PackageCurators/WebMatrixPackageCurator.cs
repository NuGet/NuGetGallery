﻿using System.IO;
using System.Linq;
using NuGet;

namespace NuGetGallery
{
    public class WebMatrixPackageCurator : AutomaticPackageCurator
    {
        public override void Curate(
            Package galleryPackage,
            IPackage nugetPackage,
            bool commitChanges)
        {
            var curatedFeed = GetService<ICuratedFeedByNameQuery>().Execute("webmatrix", includePackages: true);
            if (curatedFeed == null)
            {
                return;
            }

            if (!galleryPackage.IsLatestStable)
            {
                return;
            }

            var shouldBeIncluded = galleryPackage.Tags != null && galleryPackage.Tags.ToLowerInvariant().Contains("aspnetwebpages");

            if (!shouldBeIncluded)
            {
                shouldBeIncluded = true;
                foreach (var file in nugetPackage.GetFiles())
                {
                    var fi = new FileInfo(file.Path);
                    if (fi.Extension == ".ps1" || fi.Extension == ".t4")
                    {
                        shouldBeIncluded = false;
                        break;
                    }
                }
            }

            if (shouldBeIncluded && DependenciesAreCurated(galleryPackage, curatedFeed))
            {
                GetService<ICreateCuratedPackageCommand>().Execute(
                    curatedFeed,
                    galleryPackage.PackageRegistration,
                    automaticallyCurated: true,
                    commitChanges: commitChanges);
            }
        }
    }
}