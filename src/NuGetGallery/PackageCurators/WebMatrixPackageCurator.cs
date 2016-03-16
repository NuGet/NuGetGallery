// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Services.Gallery;
using NuGet.Services.Gallery.Entities;
using NuGet.Versioning;

namespace NuGetGallery
{
    public class WebMatrixPackageCurator : AutomaticPackageCurator
    {
        public WebMatrixPackageCurator(ICuratedFeedService curatedFeedService)
            : base(curatedFeedService)
        {
        }

        public override async Task CurateAsync(
            Package galleryPackage,
            PackageArchiveReader nugetPackage,
            bool commitChanges)
        {
            var curatedFeed = CuratedFeedService.GetFeedByName("webmatrix", includePackages: true);
            if (curatedFeed == null)
            {
                return;
            }

            var shouldBeIncluded = ShouldCuratePackage(curatedFeed, galleryPackage, nugetPackage);
            if (shouldBeIncluded)
            {
                await CuratedFeedService.CreatedCuratedPackageAsync(
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
            PackageArchiveReader packageArchiveReader)
        {
            var nuspec = packageArchiveReader.GetNuspecReader();

            return
                // Must have min client version of null or <= 2.2
                (nuspec.GetMinClientVersion() == null || nuspec.GetMinClientVersion() <= new NuGetVersion(2, 2, 0)) &&

                // Must be latest stable
                galleryPackage.IsLatestStable &&

                // Must support net40
                SupportsNet40(galleryPackage) &&

                (
                    // Must have AspNetWebPages tag
                    ContainsAspNetWebPagesTag(galleryPackage) ||

                    // OR: Must not contain powershell or T4
                    DoesNotContainUnsupportedFiles(packageArchiveReader)
                ) &&

                // Dependencies on the gallery must be curated
                DependenciesAreCurated(galleryPackage, curatedFeed);
        }

        private static bool ContainsAspNetWebPagesTag(Package galleryPackage)
        {
            return galleryPackage.Tags != null &&
                galleryPackage.Tags.ToLowerInvariant().Contains("aspnetwebpages");
        }

        private static bool SupportsNet40(Package galleryPackage)
        {
            var net40Fx = new NuGetFramework(".NETFramework", new Version(4, 0));
            return (galleryPackage.SupportedFrameworks.Count == 0) ||
                (from fx in galleryPackage.SupportedFrameworks
                 let fxName = NuGetFramework.Parse(fx.TargetFramework)
                 where fxName == net40Fx
                 select fx)
                .Any();
        }

        private static bool DoesNotContainUnsupportedFiles(PackageArchiveReader nugetPackage)
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