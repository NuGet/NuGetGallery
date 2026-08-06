// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class StagingBlobService : IStagingBlobService
    {
        public const string ArtifactTypeMetadataKey = "NuGetArtifactType";
        public const string FormatVersionMetadataKey = "StagingFormatVersion";
        public const string FormatVersion = "1";

        private static readonly TimeSpan MaxCopyDuration = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan CopyPollFrequency = TimeSpan.FromMilliseconds(500);
        private static readonly Regex BlobPathPattern = new Regex(
            @"\Av1/\d{4}/\d{2}/\d{2}/[0-9a-f]{32}\.(nupkg|snupkg)\z",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private readonly ICloudBlobClient _client;
        private readonly Lazy<Task<ICloudBlobContainer>> _container;

        public StagingBlobService(ICloudBlobClient client, bool initializeContainer = false)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _container = new Lazy<Task<ICloudBlobContainer>>(() => InitializeContainerAsync(initializeContainer));
        }

        private Task<ICloudBlobContainer> GetContainerAsync()
        {
            return _container.Value;
        }

        private async Task<ICloudBlobContainer> InitializeContainerAsync(bool initializeContainer)
        {
            var container = _client.GetContainerReference(CoreConstants.Folders.StagingFolderName);
            if (initializeContainer)
            {
                await container.CreateIfNotExistAsync(enablePublicAccess: false);
            }

            return container;
        }

        public async Task<StagingBlobReference> CreateAsync(Stream content, StagingBlobType blobType)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (!content.CanRead || !content.CanSeek)
            {
                throw new ArgumentException("The staging blob stream must be readable and seekable.", nameof(content));
            }

            if (content.Position != 0)
            {
                throw new ArgumentException("The staging blob stream must be positioned at the beginning.", nameof(content));
            }

            string contentHash;
            using (var hashAlgorithm = SHA512.Create())
            {
                contentHash = Convert.ToBase64String(hashAlgorithm.ComputeHash(content));
            }

            var contentLength = content.Length;
            content.Position = 0;

            var blobPath = CreateBlobPath(blobType, DateTime.UtcNow, Guid.NewGuid());
            var container = await GetContainerAsync();
            var blob = container.GetBlobReference(blobPath);

            blob.Properties.ContentType = CoreConstants.PackageContentType;
            blob.Properties.CacheControl = null;
            blob.Metadata[CoreConstants.Sha512HashAlgorithmId] = contentHash;
            blob.Metadata[ArtifactTypeMetadataKey] = GetArtifactType(blobType);
            blob.Metadata[FormatVersionMetadataKey] = FormatVersion;

            try
            {
                await blob.UploadFromStreamAsync(
                    content,
                    AccessConditionWrapper.GenerateIfNotExistsCondition());
            }
            catch (CloudBlobStorageException ex) when (
                ex is CloudBlobConflictException
                || ex is CloudBlobPreconditionFailedException)
            {
                throw new FileAlreadyExistsException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "There is already a staging blob with path {0}.",
                        blobPath),
                    ex);
            }

            await blob.FetchAttributesAsync();

            var result = new StagingBlobReference(
                blobPath,
                blob.ETag,
                contentHash,
                contentLength,
                blobType);

            ValidateBlob(blob, result);
            return result;
        }

        public async Task<Stream> OpenReadAsync(StagingBlobReference blob)
        {
            if (blob == null)
            {
                throw new ArgumentNullException(nameof(blob));
            }

            var storedBlob = await GetValidatedBlobAsync(blob);
            return await storedBlob.OpenReadAsync(AccessConditionWrapper.GenerateIfMatchCondition(blob.ETag));
        }

        public async Task CopyAsync(
            StagingBlobReference source,
            string destinationFolderName,
            string destinationFileName,
            IAccessCondition destinationAccessCondition)
        {
            // TODO: Accept an explicit destination storage client before staged validation is implemented. Staging
            // uses package storage, while validation working files may use a separate storage account.
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (string.IsNullOrWhiteSpace(destinationFolderName))
            {
                throw new ArgumentNullException(nameof(destinationFolderName));
            }

            if (string.IsNullOrWhiteSpace(destinationFileName))
            {
                throw new ArgumentNullException(nameof(destinationFileName));
            }

            var sourceBlob = await GetValidatedBlobAsync(source);
            var destinationContainer = _client.GetContainerReference(destinationFolderName);
            var destinationBlob = destinationContainer.GetBlobReference(destinationFileName);

            await destinationBlob.StartCopyAsync(
                sourceBlob,
                AccessConditionWrapper.GenerateIfMatchCondition(source.ETag),
                destinationAccessCondition ?? AccessConditionWrapper.GenerateIfNotExistsCondition());

            var started = DateTime.UtcNow;
            while (destinationBlob.CopyState.Status == CloudBlobCopyStatus.Pending
                && DateTime.UtcNow - started < MaxCopyDuration)
            {
                await Task.Delay(CopyPollFrequency);
                await destinationBlob.FetchAttributesAsync();
            }

            if (destinationBlob.CopyState.Status != CloudBlobCopyStatus.Success)
            {
                throw new CloudBlobStorageException(
                    $"The staging blob copy did not succeed. Copy status: {destinationBlob.CopyState.Status}.");
            }

            ValidateContent(destinationBlob, source);
        }

        public async Task DeleteAsync(string blobPath, string expectedETag)
        {
            ValidateBlobPath(blobPath);

            if (string.IsNullOrWhiteSpace(expectedETag))
            {
                throw new ArgumentNullException(nameof(expectedETag));
            }

            var container = await GetContainerAsync();
            var blob = container.GetBlobReference(blobPath);
            await blob.DeleteIfExistsAsync(AccessConditionWrapper.GenerateIfMatchCondition(expectedETag));
        }

        internal static string CreateBlobPath(StagingBlobType blobType, DateTime createdUtc, Guid blobId)
        {
            if (createdUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("The staging blob creation time must be UTC.", nameof(createdUtc));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "v1/{0:yyyy/MM/dd}/{1:N}.{2}",
                createdUtc,
                blobId,
                GetArtifactType(blobType));
        }

        private async Task<ISimpleCloudBlob> GetValidatedBlobAsync(StagingBlobReference blob)
        {
            ValidateBlobPath(blob.BlobPath);

            var container = await GetContainerAsync();
            var storedBlob = container.GetBlobReference(blob.BlobPath);
            await storedBlob.FetchAttributesAsync();
            ValidateBlob(storedBlob, blob);
            return storedBlob;
        }

        private static void ValidateBlob(ISimpleCloudBlob blob, StagingBlobReference expected)
        {
            var expectedExtension = "." + GetArtifactType(expected.BlobType);
            if (!expected.BlobPath.EndsWith(expectedExtension, StringComparison.Ordinal))
            {
                throw new StagingBlobIntegrityException(
                    $"The staging blob path does not match its artifact type: {expected.BlobPath}.");
            }

            if (!string.Equals(blob.ETag, expected.ETag, StringComparison.Ordinal))
            {
                throw new StagingBlobIntegrityException(
                    $"The staging blob ETag does not match the expected value: {expected.BlobPath}.");
            }

            ValidateContent(blob, expected);
        }

        private static void ValidateContent(ISimpleCloudBlob blob, StagingBlobReference expected)
        {
            if (blob.Properties.Length != expected.ContentLength
                || !string.Equals(blob.Properties.ContentType, CoreConstants.PackageContentType, StringComparison.Ordinal)
                || !string.IsNullOrEmpty(blob.Properties.CacheControl)
                || !TryGetMetadata(blob, CoreConstants.Sha512HashAlgorithmId, expected.ContentHash)
                || !TryGetMetadata(blob, ArtifactTypeMetadataKey, GetArtifactType(expected.BlobType))
                || !TryGetMetadata(blob, FormatVersionMetadataKey, FormatVersion))
            {
                throw new StagingBlobIntegrityException(
                    $"The staging blob properties do not match the expected values: {expected.BlobPath}.");
            }
        }

        private static bool TryGetMetadata(ISimpleCloudBlob blob, string key, string expectedValue)
        {
            return blob.Metadata != null
                && blob.Metadata.TryGetValue(key, out var actualValue)
                && string.Equals(actualValue, expectedValue, StringComparison.Ordinal);
        }

        private static void ValidateBlobPath(string blobPath)
        {
            if (string.IsNullOrWhiteSpace(blobPath))
            {
                throw new ArgumentNullException(nameof(blobPath));
            }

            if (!BlobPathPattern.IsMatch(blobPath))
            {
                throw new ArgumentException("The staging blob path is invalid.", nameof(blobPath));
            }
        }

        private static string GetArtifactType(StagingBlobType blobType)
        {
            switch (blobType)
            {
                case StagingBlobType.Nupkg:
                    return "nupkg";
                case StagingBlobType.Snupkg:
                    return "snupkg";
                default:
                    throw new ArgumentOutOfRangeException(nameof(blobType));
            }
        }
    }
}
