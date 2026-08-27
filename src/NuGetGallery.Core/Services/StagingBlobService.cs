// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class StagingBlobService : IStagingBlobService
    {
        private readonly ICoreFileStorageService _fileStorageService;

        public StagingBlobService(ICoreFileStorageService fileStorageService)
        {
            _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
        }

        public async Task<StagingFileReference> SavePackageFileAsync(string packageId, string normalizedVersion, Stream packageFile)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (string.IsNullOrWhiteSpace(normalizedVersion))
            {
                throw new ArgumentNullException(nameof(normalizedVersion));
            }

            if (packageFile == null)
            {
                throw new ArgumentNullException(nameof(packageFile));
            }

            if (!packageFile.CanRead || !packageFile.CanSeek || packageFile.Position != 0)
            {
                throw new ArgumentException("The package stream must be readable, seekable, and positioned at the beginning.", nameof(packageFile));
            }

            var path = GeneratePackagePath(packageId, normalizedVersion, Guid.NewGuid());
            await _fileStorageService.SaveFileAsync(
                CoreConstants.Folders.StagingFolderName,
                path,
                CoreConstants.PackageContentType,
                packageFile,
                overwrite: false);

            var etag = await _fileStorageService.GetETagOrNullAsync(CoreConstants.Folders.StagingFolderName, path);
            if (etag == null)
            {
                throw new InvalidOperationException($"The staged package blob '{path}' was not found after it was saved.");
            }

            return new StagingFileReference(path, etag);
        }

        internal static string GeneratePackagePath(string packageId, string normalizedVersion, Guid fileId)
        {
            if (packageId.IndexOf('/') >= 0)
            {
                throw new ArgumentException("The package ID cannot contain a slash.", nameof(packageId));
            }

            if (normalizedVersion.IndexOf('/') >= 0)
            {
                throw new ArgumentException("The package version cannot contain a slash.", nameof(normalizedVersion));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1}/{2:N}{3}",
                packageId.ToLowerInvariant(),
                normalizedVersion.ToLowerInvariant(),
                fileId,
                CoreConstants.NuGetPackageFileExtension);
        }
    }
}
