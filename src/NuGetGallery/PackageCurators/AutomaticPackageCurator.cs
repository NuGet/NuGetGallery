// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Packaging;

namespace NuGetGallery
{
    public abstract class AutomaticPackageCurator : IAutomaticPackageCurator
    {
        protected ICuratedFeedService CuratedFeedService { get; private set; }

        public AutomaticPackageCurator(ICuratedFeedService curatedFeedService)
        {
            CuratedFeedService = curatedFeedService;
        }

        public abstract void Curate(
            Package galleryPackage,
            PackageReader nugetPackage,
            bool commitChanges);
        
        protected static bool DependenciesAreCurated(Package galleryPackage, CuratedFeed curatedFeed)
        {
            if (!galleryPackage.Dependencies.AnySafe())
            {
                return true;
            }

            return galleryPackage.Dependencies.All(
                d => curatedFeed.Packages
                    .Where(p => p.Included)
                    .Any(p => p.PackageRegistration.Id.Equals(d.Id, StringComparison.OrdinalIgnoreCase)));
        }
    }
}