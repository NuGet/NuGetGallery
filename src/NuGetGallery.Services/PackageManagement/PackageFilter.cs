// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Services.Helpers;

namespace NuGetGallery.Services.PackageManagement
{
    public class PackageFilter : IPackageFilter
    {
        private readonly IPackageService _packageService;

        public PackageFilter(IPackageService packageService)
        {
            _packageService = packageService;
        }
            
        /// <inheritdoc />
        public Package GetFiltered(IReadOnlyCollection<Package> packages, PackageFilterContext context)
        {
            var version = context.Version;
            if (version == null)
            {
                if (LatestPackageRouteVerifier.IsLatestRoute(context.RouteBase, out var preRelease))
                {
                    return _packageService.FilterLatestPackageBySuffix(packages, null, preRelease);
                }
            }
            else
            {
                if (version.Equals(LatestPackageRouteVerifier.SupportedRoutes.AbsoluteLatestUrlString, StringComparison.InvariantCultureIgnoreCase))
                {
                    // The user is looking for the absolute latest version and not an exact version.
                    return packages.FirstOrDefault(p => p.IsLatestSemVer2);
                }
                if (LatestPackageRouteVerifier.IsLatestRoute(context.RouteBase, out var preRelease))
                {
                    return _packageService.FilterLatestPackageBySuffix(packages, version, preRelease);
                }
                else
                {
                    return _packageService.FilterExactPackage(packages, version);
                }
            }

            return _packageService.FilterLatestPackage(packages, SemVerLevelKey.SemVer2, allowPrerelease: true);
        }
    }
}