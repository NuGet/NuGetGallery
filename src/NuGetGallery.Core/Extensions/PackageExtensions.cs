// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace NuGetGallery.Extensions
{
    public static class PackageExtensions
    {
        /// <summary>
        /// Get the latest(last) uploaded symbols package for the given package.
        /// </summary>
        /// <param name="package"><see cref="Package"/> for which latest symbol package is to be retrieved.</param>
        /// <returns>The latest symbol package if present, null otherwise</returns>
        public static SymbolPackage LatestSymbolPackage(this Package package)
        {
            return package
                .SymbolPackages?
                .OrderByDescending(sp => sp.Created)
                .FirstOrDefault();
        }

        /// <summary>
        /// Get the latest(last) uploaded symbols package for the given package, if it is <see cref="PackageStatus.Available"/>.
        /// </summary>
        /// <param name="package"><see cref="Package"/> for which latest available symbol package is to be retrieved.</param>
        /// <returns>The latest symbol package if its status is <see cref="PackageStatus.Available"/>, null otherwise</returns>
        public static SymbolPackage LatestAvailableSymbolPackage(this Package package)
        {
            var latestSymbolPackage = package.LatestSymbolPackage();
            if (latestSymbolPackage == null || latestSymbolPackage.StatusKey != PackageStatus.Available)
            {
                return null;
            }
            else
            {
                return latestSymbolPackage;
            }
        }
    }
}