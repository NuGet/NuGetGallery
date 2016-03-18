// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Services.Gallery;
using NuGet.Services.Gallery.Entities;

namespace NuGetGallery
{
    public interface IAutomaticallyCuratePackageCommand
    {
        Task ExecuteAsync(
            Package galleryPackage,
            PackageArchiveReader nugetPackage,
            bool commitChanges);
    }

    public class AutomaticallyCuratePackageCommand : AppCommand, IAutomaticallyCuratePackageCommand
    {
        private readonly List<IAutomaticPackageCurator> _curators;

        public AutomaticallyCuratePackageCommand(IEnumerable<IAutomaticPackageCurator> curators, IEntitiesContext entities)
            : base(entities)
        {
            _curators = curators.ToList();
        }

        public async Task ExecuteAsync(Package galleryPackage, PackageArchiveReader nugetPackage, bool commitChanges)
        {
            foreach (var curator in _curators)
            {
                await curator.CurateAsync(galleryPackage, nugetPackage, commitChanges: commitChanges);
            }
        }
    }
}