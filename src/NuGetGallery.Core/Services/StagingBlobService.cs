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
        private static readonly TimeSpan ReadAccessDuration = TimeSpan.FromMinutes(10);

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

        public async Task<Uri> GetPackageReadUriAsync(string packagePath, string packageETag)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                throw new ArgumentNullException(nameof(packagePath));
            }

            if (string.IsNullOrWhiteSpace(packageETag))
            {
                throw new ArgumentNullException(nameof(packageETag));
            }

            var currentETag = await _fileStorageService.GetETagOrNullAsync(CoreConstants.Folders.StagingFolderName, packagePath);
            if (!string.Equals(currentETag, packageETag, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The staged package blob '{packagePath}' has changed or no longer exists.");
            }

            return await _fileStorageService.GetFileReadUriAsync(
                CoreConstants.Folders.StagingFolderName,
                packagePath,
                DateTimeOffset.UtcNow.Add(ReadAccessDuration));
        }

        public async Task<StagingFileReference> CopyPackageFileToStagingAsync(string packageId, string normalizedVersion, Uri packageFileUri)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (string.IsNullOrWhiteSpace(normalizedVersion))
            {
                throw new ArgumentNullException(nameof(normalizedVersion));
            }

            if (packageFileUri == null)
            {
                throw new ArgumentNullException(nameof(packageFileUri));
            }

            var path = GeneratePackagePath(packageId, normalizedVersion, Guid.NewGuid());
            await _fileStorageService.CopyFileAsync(
                packageFileUri,
                CoreConstants.Folders.StagingFolderName,
                path,
                AccessConditionWrapper.GenerateIfNotExistsCondition());

            var etag = await _fileStorageService.GetETagOrNullAsync(CoreConstants.Folders.StagingFolderName, path);
            if (etag == null)
            {
                throw new InvalidOperationException($"The staged package blob '{path}' was not found after it was copied.");
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
