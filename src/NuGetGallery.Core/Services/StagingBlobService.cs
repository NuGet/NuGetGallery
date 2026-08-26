// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class StagingBlobService : IStagingBlobService
    {
        private static readonly TimeSpan MaxCopyDuration = TimeSpan.FromMinutes(10);

        private static readonly TimeSpan CopyPollFrequency = TimeSpan.FromMilliseconds(500);

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

            string contentHash;
            using (var hashAlgorithm = SHA512.Create())
            {
                contentHash = Convert.ToBase64String(hashAlgorithm.ComputeHash(packageFile));
            }

            var length = packageFile.Length;
            var path = GeneratePackagePath(packageId, normalizedVersion, Guid.NewGuid());
            packageFile.Position = 0;

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

            return new StagingFileReference(path, etag, length, contentHash);
        }

        public async Task CopyStagedPackageToValidationSetAsync(
            string packagePath,
            string packageETag,
            ICloudBlobClient validationStorageClient,
            string validationSetPackageFileName)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                throw new ArgumentNullException(nameof(packagePath));
            }

            if (string.IsNullOrWhiteSpace(packageETag))
            {
                throw new ArgumentNullException(nameof(packageETag));
            }

            if (validationStorageClient == null)
            {
                throw new ArgumentNullException(nameof(validationStorageClient));
            }

            if (string.IsNullOrWhiteSpace(validationSetPackageFileName))
            {
                throw new ArgumentNullException(nameof(validationSetPackageFileName));
            }

            var destinationContainer = validationStorageClient.GetContainerReference(CoreConstants.Folders.ValidationFolderName);
            var destinationBlob = destinationContainer.GetBlobReference(validationSetPackageFileName);

            IAccessCondition destinationAccessCondition = null;
            if (await destinationBlob.ExistsAsync())
            {
                await destinationBlob.FetchAttributesAsync();

                if (destinationBlob.CopyState.Status == CloudBlobCopyStatus.Failed || destinationBlob.CopyState.Status == CloudBlobCopyStatus.Aborted)
                {
                    destinationAccessCondition = AccessConditionWrapper.GenerateIfMatchCondition(destinationBlob.ETag);
                }
            }
            else
            {
                destinationAccessCondition = AccessConditionWrapper.GenerateIfNotExistsCondition();
            }

            if (destinationAccessCondition != null)
            {
                var sourceUri = await _fileStorageService.GetFileReadUriAsync(CoreConstants.Folders.StagingFolderName, packagePath, DateTimeOffset.UtcNow.Add(MaxCopyDuration));
                var sourceBlob = validationStorageClient.GetBlobFromUri(sourceUri);
                await destinationBlob.StartCopyAsync(sourceBlob, AccessConditionWrapper.GenerateIfMatchCondition(packageETag), destinationAccessCondition);
            }

            var stopwatch = Stopwatch.StartNew();
            while (destinationBlob.CopyState.Status == CloudBlobCopyStatus.Pending && stopwatch.Elapsed < MaxCopyDuration)
            {
                await destinationBlob.FetchAttributesAsync();
                await Task.Delay(CopyPollFrequency);
            }

            if (destinationBlob.CopyState.Status == CloudBlobCopyStatus.Pending)
            {
                throw new TimeoutException($"Waiting for staged package copy to complete timed out after {MaxCopyDuration.TotalSeconds} seconds.");
            }

            if (destinationBlob.CopyState.Status != CloudBlobCopyStatus.Success)
            {
                throw new InvalidOperationException($"The staged package copy failed with status {destinationBlob.CopyState.Status} ({destinationBlob.CopyState.StatusDescription}).");
            }
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
