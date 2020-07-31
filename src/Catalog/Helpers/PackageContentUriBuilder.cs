// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    /// <summary>
    /// Utility class to validate package content URL format strings.
    /// </summary>
    public class PackageContentUriBuilder
    {
        public const string IdLowerPlaceholderString = "{id-lower}";
        public const string VersionLowerPlaceholderString = "{version-lower}";

        private const string NuPkgExtension = ".nupkg";
        private readonly string _packageContentUrlFormat;

        public PackageContentUriBuilder(string packageContentUrlFormat)
        {
            _packageContentUrlFormat = packageContentUrlFormat ?? throw new ArgumentNullException(nameof(packageContentUrlFormat));

            if (!_packageContentUrlFormat.Contains(IdLowerPlaceholderString) || !_packageContentUrlFormat.Contains(VersionLowerPlaceholderString))
            {
                throw new ArgumentException(
                    $"The package content URL format must contain the following placeholders to be valid: {IdLowerPlaceholderString}, {VersionLowerPlaceholderString}. " +
                    "(e.g. https://storageaccountname.blob.core.windows.net/packages/{id-lower}.{version-lower}.nupkg)",
                    nameof(packageContentUrlFormat));
            }
            else if (!_packageContentUrlFormat.EndsWith(NuPkgExtension))
            {
                throw new ArgumentException(
                    $"The package content URL format must point to files with the {NuPkgExtension} extension.",
                    nameof(packageContentUrlFormat));
            }
        }

        public Uri Build(string packageId, string normalizedPackageVersion)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (normalizedPackageVersion == null)
            {
                throw new ArgumentNullException(nameof(normalizedPackageVersion));
            }

            return new Uri(
                _packageContentUrlFormat
                .Replace(IdLowerPlaceholderString, packageId.ToLowerInvariant())
                .Replace(VersionLowerPlaceholderString, normalizedPackageVersion.ToLowerInvariant()));
        }
    }
}