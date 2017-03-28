// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using NuGet.Versioning;

namespace NuGetGallery
{
    // Hard-coded for now, but we can easily expand to use an additional Sql table to join with 
    // when supporting additional semVerLevel's is needed.

    /// <summary>
    /// Helper class to use to determine the SemVer level of a package version.
    /// </summary>
    public static class SemVerLevelKey
    {
        private static readonly NuGetVersion _semVer2Version = NuGetVersion.Parse("2.0.0");

        /// <summary>
        /// This could either indicate being SemVer1-compliant, or non-SemVer-compliant at all (e.g. System.Versioning pattern).
        /// </summary>
        public static readonly int? Unknown = null;

        /// <summary>
        /// Indicates being SemVer2-compliant, but not SemVer1-compliant.
        /// This key corresponds to semVerLevel=2.0.0
        /// </summary>
        public static readonly int SemVer2 = 2;

        /// <summary>
        /// Identifies the SemVer-level of a package based on original version string and dependency versions.
        /// </summary>
        /// <param name="originalVersion">The package's non-normalized, original version string.</param>
        /// <param name="dependencies">The package's direct dependencies as defined in the package's manifest.</param>
        /// <returns>Returns <c>null</c> when unknown; otherwise the identified SemVer-level key.</returns>
        public static int? ForPackage(NuGetVersion originalVersion, IEnumerable<PackageDependency> dependencies)
        {
            if (originalVersion == null)
            {
                throw new ArgumentNullException(nameof(originalVersion));
            }

            if (originalVersion.IsSemVer2)
            {
                // No need to further check the dependencies: 
                // the original version already is identified to be SemVer2-compliant, but not SemVer1-compliant.
                return SemVer2;
            }

            if (dependencies != null)
            {
                foreach (var dependency in dependencies)
                {
                    // Check the package dependencies for SemVer-compliance.
                    // As soon as a SemVer2-compliant dependency version is found that is not SemVer1-compliant,
                    // this package in itself is to be identified as to have SemVerLevelKey.SemVer2.
                    var dependencyVersionRange = VersionRange.Parse(dependency.VersionSpec);

                    if ((dependencyVersionRange.MinVersion != null && dependencyVersionRange.MinVersion.IsSemVer2)
                        || (dependencyVersionRange.MaxVersion != null && dependencyVersionRange.MaxVersion.IsSemVer2))
                    {
                        return SemVer2;
                    }
                }
            }

            return Unknown;
        }

        /// <summary>
        /// Identifies the SemVer-level for a given semVerLevel version string.
        /// </summary>
        /// <param name="semVerLevel">The version string indicating the supported SemVer-level.</param>
        /// <returns>
        /// Returns <c>null</c> when unknown; otherwise the identified SemVer-level key.
        /// </returns>
        /// <remarks>
        /// Older clients don't send the semVerLevel query parameter at all, 
        /// so we default to Unknown for backwards-compatibility.
        /// </remarks>
        public static int? ForSemVerLevel(string semVerLevel)
        {
            if (semVerLevel == null)
            {
                return Unknown;
            }

            NuGetVersion parsedVersion;
            if (NuGetVersion.TryParse(semVerLevel, out parsedVersion))
            {
                return _semVer2Version <= parsedVersion ? SemVer2 : Unknown;
            }
            else
            {
                return Unknown;
            }
        }
        
        /// <summary>
        /// Indicates whether the provided SemVer-level key is compliant with the provided SemVer-level version string.
        /// </summary>
        /// <param name="semVerLevel">The SemVer-level string indicating the SemVer-level to comply with.</param>
        /// <returns><c>True</c> if compliant; otherwise <c>false</c>.</returns>
        public static Expression<Func<Package, bool>> IsPackageCompliantWithSemVerLevel(string semVerLevel)
        {
            // Note: we must return an expression that Linq to Entities is able to translate to SQL
            var parsedSemVerLevelKey = ForSemVerLevel(semVerLevel);

            if (parsedSemVerLevelKey == SemVer2)
            {
                return p => p.SemVerLevelKey == Unknown || p.SemVerLevelKey == parsedSemVerLevelKey;
            }

            return p => p.SemVerLevelKey == Unknown;
        }
    }
}