// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging;

namespace NuGetGallery
{
    public interface IAutomaticallyCuratePackageCommand
    {
        void Execute(
            Package galleryPackage,
            PackageReader nugetPackage,
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

        public void Execute(Package galleryPackage, PackageReader nugetPackage, bool commitChanges)
        {
            foreach (var curator in _curators)
            {
                curator.Curate(galleryPackage, nugetPackage, commitChanges: commitChanges);
            }
        }
    }
}