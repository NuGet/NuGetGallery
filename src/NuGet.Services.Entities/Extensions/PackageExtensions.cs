﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace NuGet.Services.Entities
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
        /// Returns true if there exists a symbol package which is latest and is in
        /// available state, false otherwise.
        /// </summary>
        public static bool IsLatestSymbolPackageAvailable(this Package package)
        {
            var latestSymbolPackage = package.LatestSymbolPackage();
            return latestSymbolPackage != null
                && latestSymbolPackage.StatusKey == PackageStatus.Available;
        }
    }
}