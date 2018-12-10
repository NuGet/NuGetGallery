// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using NuGet.Common;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Contains common file name generation logic used by many storage abstractions
    /// </summary>
    public static class FileNameHelper
    {
        public  static string BuildFileName(Package package, string format, string extension)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (package.PackageRegistration == null ||
                string.IsNullOrWhiteSpace(package.PackageRegistration.Id) ||
                (string.IsNullOrWhiteSpace(package.NormalizedVersion) && string.IsNullOrWhiteSpace(package.Version)))
            {
                throw new ArgumentException(CoreStrings.PackageIsMissingRequiredData, nameof(package));
            }

            return BuildFileName(
                package.PackageRegistration.Id,
                string.IsNullOrEmpty(package.NormalizedVersion) ?
                    NuGetVersionFormatter.Normalize(package.Version) :
                    package.NormalizedVersion, format, extension);
        }

        public static string BuildFileName(string id, string version, string pathTemplate, string extension)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            // Note: packages should be saved and retrieved in blob storage using the lower case version of their filename because
            // a) package IDs can and did change case over time
            // b) blob storage is case sensitive
            // c) we don't want to hit the database just to look up the right case
            // and remember - version can contain letters too.
            return string.Format(
                CultureInfo.InvariantCulture,
                pathTemplate,
                id.ToLowerInvariant(),
                version.ToLowerInvariant(),
                extension);
        }

        /// <summary>
        /// Enforces the correct file separators for passing paths to work with zip file entries.
        /// </summary>
        /// <remarks>
        /// When client packs the nupkg, it enforces all zip file entries to use forward slashes 
        /// and relative paths.
        /// At the same time, paths in nuspec can contain backslashes and start with dot. This
        /// method fixes the separators so those paths can be used to retrieve files from zip
        /// archive.
        /// </remarks>
        /// <param name="fileName">File name to fix.</param>
        /// <returns>File path with proper path separators.</returns>
        public static string GetZipEntryPath(string filePath)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            return PathUtility.StripLeadingDirectorySeparators(filePath);
        }
    }
}
