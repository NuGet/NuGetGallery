// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class CorePackageFileService : ICorePackageFileService
    {
        private readonly ICoreFileStorageService _fileStorageService;

        public CorePackageFileService(ICoreFileStorageService fileStorageService)
        {
            _fileStorageService = fileStorageService;
        }

        public Task SavePackageFileAsync(Package package, Stream packageFile)
        {
            if (packageFile == null)
            {
                throw new ArgumentNullException(nameof(packageFile));
            }

            var fileName = BuildFileName(package, CoreConstants.PackageFileSavePathTemplate, CoreConstants.NuGetPackageFileExtension);
            return _fileStorageService.SaveFileAsync(CoreConstants.PackagesFolderName, fileName, packageFile, overwrite: false);
        }

        public Task<Stream> DownloadPackageFileAsync(Package package)
        {
            var fileName = BuildFileName(package, CoreConstants.PackageFileSavePathTemplate, CoreConstants.NuGetPackageFileExtension);
            return _fileStorageService.GetFileAsync(CoreConstants.PackagesFolderName, fileName);
        }

        protected static string BuildFileName(Package package, string format, string extension)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (package.PackageRegistration == null ||
                String.IsNullOrWhiteSpace(package.PackageRegistration.Id) ||
                (String.IsNullOrWhiteSpace(package.NormalizedVersion) && String.IsNullOrWhiteSpace(package.Version)))
            {
                throw new ArgumentException(CoreStrings.PackageIsMissingRequiredData, nameof(package));
            }

            return BuildFileName(
                package.PackageRegistration.Id,
                string.IsNullOrEmpty(package.NormalizedVersion) ?
                    NuGetVersionFormatter.Normalize(package.Version) :
                    package.NormalizedVersion, format, extension);
        }

        protected static string BuildFileName(string id, string version, string pathTemplate, string extension)
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
            return String.Format(
                CultureInfo.InvariantCulture,
                pathTemplate,
                id.ToLowerInvariant(),
                version.ToLowerInvariant(),
                extension);
        }
    }
}