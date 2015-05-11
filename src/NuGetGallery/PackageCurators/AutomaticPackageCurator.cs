// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Linq;
using System.Web.Mvc;
using Ninject;
using NuGet;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public abstract class AutomaticPackageCurator : IAutomaticPackageCurator
    {
        public abstract void Curate(
            Package galleryPackage,
            INupkg nugetPackage,
            bool commitChanges);

        protected virtual T GetService<T>()
        {
            return Container.Kernel.TryGet<T>();
        }

        protected static bool DependenciesAreCurated(Package galleryPackage, CuratedFeed curatedFeed)
        {
            if (galleryPackage.Dependencies.IsEmpty())
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