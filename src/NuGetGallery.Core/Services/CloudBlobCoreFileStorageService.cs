// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using NuGetGallery.Diagnostics;

namespace NuGetGallery
{
    public class CloudBlobCoreFileStorageService : ICoreFileStorageService
    {
        /// <summary>
        /// This is the maximum duration for <see cref="CopyFileAsync(string, string, string, string)"/> to poll,
        /// waiting for a package copy to complete. The value picked today is based off of the maximum duration we wait
        /// when uploading files to Azure China blob storage. Note that in cases when the copy source and destination
        /// are in the same container, the copy completed immediately and no polling is necessary.
        /// </summary>
        private static readonly TimeSpan MaxCopyDuration = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan CopyPollFrequency = TimeSpan.FromMilliseconds(500);

        private static readonly HashSet<string> KnownPublicFolders = new HashSet<string> {
            CoreConstants.PackagesFolderName,
            CoreConstants.PackageBackupsFolderName,
            CoreConstants.DownloadsFolderName
        };

        private static readonly HashSet<string> KnownPrivateFolders = new HashSet<string> {
            CoreConstants.ContentFolderName,
            CoreConstants.UploadsFolderName,
            CoreConstants.PackageReadMesFolderName,
            CoreConstants.ValidationFolderName,
            CoreConstants.UserCertificatesFolderName
        };

        protected readonly ICloudBlobClient _client;
        protected readonly IDiagnosticsSource _trace;
        protected readonly ConcurrentDictionary<string, ICloudBlobContainer> _containers = new ConcurrentDictionary<string, ICloudBlobContainer>();

        public CloudBlobCoreFileStorageService(ICloudBlobClient client, IDiagnosticsService diagnosticsService)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _trace = diagnosticsService?.SafeGetSource(nameof(CloudBlobCoreFileStorageService)) ?? throw new ArgumentNullException(nameof(diagnosticsService));
        }

        public async Task DeleteFileAsync(string folderName, string fileName)
        {
            ICloudBlobContainer container = await GetContainerAsync(folderName);
            var blob = container.GetBlobReference(fileName);
            await blob.DeleteIfExistsAsync();
        }

        public async Task<bool> FileExistsAsync(string folderName, string fileName)
        {
            ICloudBlobContainer container = await GetContainerAsync(folderName);
            var blob = container.GetBlobReference(fileName);
            return await blob.ExistsAsync();
        }

        public async Task<Stream> GetFileAsync(string folderName, string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            return (await GetBlobContentAsync(folderName, fileName)).Data;
        }

        public async Task<IFileReference> GetFileReferenceAsync(string folderName, string fileName, string ifNoneMatch = null)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            ICloudBlobContainer container = await GetContainerAsync(folderName);
            var blob = container.GetBlobReference(fileName);
            var result = await GetBlobContentAsync(folderName, fileName, ifNoneMatch);
            if (result.StatusCode == HttpStatusCode.NotModified)
            {
                return CloudFileReference.NotModified(ifNoneMatch);
            }
            else if (result.StatusCode == HttpStatusCode.OK)
            {
                if (await blob.ExistsAsync())
                {
                    await blob.FetchAttributesAsync();
                }
                return CloudFileReference.Modified(blob, result.Data);
            }
            else
            {
                // Not found
                return null;
            }
        }

        public Task CopyFileAsync(
            Uri srcUri,
            string destFolderName,
            string destFileName,
            IAccessCondition destAccessCondition)
        {
            if (srcUri == null)
            {
                throw new ArgumentNullException(nameof(srcUri));
            }

            var srcBlob = _client.GetBlobFromUri(srcUri);

            return CopyFileAsync(srcBlob, destFolderName, destFileName, destAccessCondition);
        }

        public async Task<string> CopyFileAsync(
            string srcFolderName,
            string srcFileName,
            string destFolderName,
            string destFileName,
            IAccessCondition destAccessCondition)
        {
            if (srcFolderName == null)
            {
                throw new ArgumentNullException(nameof(srcFolderName));
            }

            if (srcFileName == null)
            {
                throw new ArgumentNullException(nameof(srcFileName));
            }

            var srcContainer = await GetContainerAsync(srcFolderName);
            var srcBlob = srcContainer.GetBlobReference(srcFileName);

            return await CopyFileAsync(srcBlob, destFolderName, destFileName, destAccessCondition);
        }

        private async Task<string> CopyFileAsync(
            ISimpleCloudBlob srcBlob,
            string destFolderName,
            string destFileName,
            IAccessCondition destAccessCondition)
        {
            if (destFolderName == null)
            {
                throw new ArgumentNullException(nameof(destFolderName));
            }

            if (destFileName == null)
            {
                throw new ArgumentNullException(nameof(destFileName));
            }

            var destContainer = await GetContainerAsync(destFolderName);
            var destBlob = destContainer.GetBlobReference(destFileName);
            destAccessCondition = destAccessCondition ?? AccessConditionWrapper.GenerateIfNotExistsCondition();
            var mappedDestAccessCondition = new AccessCondition
            {
                IfNoneMatchETag = destAccessCondition.IfNoneMatchETag,
                IfMatchETag = destAccessCondition.IfMatchETag,
            };

            // Determine the source blob etag.
            await srcBlob.FetchAttributesAsync();
            var srcAccessCondition = AccessCondition.GenerateIfMatchCondition(srcBlob.ETag);

            // Check if the destination blob already exists and fetch attributes.
            if (await destBlob.ExistsAsync())
            {
                if (destBlob.CopyState?.Status == CopyStatus.Failed)
                {
                    // If the last copy failed, allow this copy to occur no matter what the caller's destination
                    // condition is. This is because the source blob is preferable over a failed copy. We use the etag
                    // of the failed blob to avoid inadvertently replacing a blob that is now valid (i.e. has a
                    // successful copy status).
                    _trace.TraceEvent(
                        TraceEventType.Information,
                        id: 0,
                        message: $"Destination blob '{destFolderName}/{destFileName}' already exists but has a " +
                        $"failed copy status. This blob will be replaced if the etag matches '{destBlob.ETag}'.");

                    mappedDestAccessCondition = AccessCondition.GenerateIfMatchCondition(destBlob.ETag);
                }
                else if ((srcBlob.Properties.ContentMD5 != null
                     && srcBlob.Properties.ContentMD5 == destBlob.Properties.ContentMD5
                     && srcBlob.Properties.Length == destBlob.Properties.Length))
                {
                    // If the blob hash is the same and the length is the same, no-op the copy.
                    _trace.TraceEvent(
                        TraceEventType.Information,
                        id: 0,
                        message: $"Destination blob '{destFolderName}/{destFileName}' already has hash " +
                        $"'{destBlob.Properties.ContentMD5}' and length '{destBlob.Properties.Length}'. The copy " +
                        $"will be skipped.");

                    return srcBlob.ETag;
                }
            }

            _trace.TraceEvent(
                TraceEventType.Information,
                id: 0,
                message: $"Copying of source blob '{srcBlob.Uri}' to '{destFolderName}/{destFileName}' with source " +
                $"access condition {Log(srcAccessCondition)} and destination access condition " +
                $"{Log(mappedDestAccessCondition)}.");

            // Start the server-side copy and wait for it to complete. If "If-None-Match: *" was specified and the
            // destination already exists, HTTP 409 is thrown. If "If-Match: ETAG" was specified and the destination
            // has changed, HTTP 412 is thrown.
            try
            {
                await destBlob.StartCopyAsync(
                    srcBlob,
                    srcAccessCondition,
                    mappedDestAccessCondition);
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int?)HttpStatusCode.Conflict)
            {
                throw new FileAlreadyExistsException(
                    String.Format(
                        CultureInfo.CurrentCulture,
                        "There is already a blob with name {0} in container {1}.",
                        destFileName,
                        destFolderName),
                    ex);
            }

            var stopwatch = Stopwatch.StartNew();
            while (destBlob.CopyState.Status == CopyStatus.Pending
                   && stopwatch.Elapsed < MaxCopyDuration)
            {
                await destBlob.FetchAttributesAsync();
                await Task.Delay(CopyPollFrequency);
            }

            if (destBlob.CopyState.Status == CopyStatus.Pending)
            {
                throw new TimeoutException($"Waiting for the blob copy operation to complete timed out after {MaxCopyDuration.TotalSeconds} seconds.");
            }
            else if (destBlob.CopyState.Status != CopyStatus.Success)
            {
                throw new StorageException($"The blob copy operation had copy status {destBlob.CopyState.Status} ({destBlob.CopyState.StatusDescription}).");
            }

            return srcBlob.ETag;
        }

        private static string Log(AccessCondition accessCondition)
        {
            if (accessCondition?.IfMatchETag != null)
            {
                return $"'If-Match: {accessCondition.IfMatchETag}'";
            }
            else if (accessCondition?.IfNoneMatchETag != null)
            {
                return $"'If-None-Match: {accessCondition.IfNoneMatchETag}'";
            }

            return "(none)";
        }

        public async Task SaveFileAsync(string folderName, string fileName, Stream packageFile, bool overwrite = true)
        {
            ICloudBlobContainer container = await GetContainerAsync(folderName);
            var blob = container.GetBlobReference(fileName);

            try
            {
                await blob.UploadFromStreamAsync(packageFile, overwrite);
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int?)HttpStatusCode.Conflict)
            {
                throw new FileAlreadyExistsException(
                    String.Format(
                        CultureInfo.CurrentCulture,
                        "There is already a blob with name {0} in container {1}.",
                        fileName,
                        folderName),
                    ex);
            }

            blob.Properties.ContentType = GetContentType(folderName);
            await blob.SetPropertiesAsync();
        }

        public async Task<Uri> GetPriviledgedFileUriAsync(
            string folderName,
            string fileName,
            FileUriPermissions permissions,
            DateTimeOffset endOfAccess)
        {
            var blob = await GetBlobForUriAsync(folderName, fileName, endOfAccess);

            return new Uri(
                blob.Uri,
                blob.GetSharedAccessSignature(MapFileUriPermissions(permissions), endOfAccess));
        }

        public async Task<Uri> GetFileReadUriAsync(string folderName, string fileName, DateTimeOffset? endOfAccess)
        {
            var blob = await GetBlobForUriAsync(folderName, fileName, endOfAccess);

            if (IsPublicContainer(folderName))
            {
                return blob.Uri;
            }

            return new Uri(
                blob.Uri,
                blob.GetSharedAccessSignature(SharedAccessBlobPermissions.Read, endOfAccess));
        }

        private static SharedAccessBlobPermissions MapFileUriPermissions(FileUriPermissions permissions)
        {
            return (SharedAccessBlobPermissions)permissions;
        }

        private async Task<ISimpleCloudBlob> GetBlobForUriAsync(string folderName, string fileName, DateTimeOffset? endOfAccess)
        {
            folderName = folderName ?? throw new ArgumentNullException(nameof(folderName));
            fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            if (endOfAccess.HasValue && endOfAccess < DateTimeOffset.UtcNow)
            {
                throw new ArgumentOutOfRangeException(nameof(endOfAccess), $"{nameof(endOfAccess)} is in the past");
            }

            if (!IsPublicContainer(folderName) && endOfAccess == null)
            {
                throw new ArgumentNullException(nameof(endOfAccess), $"{nameof(endOfAccess)} must not be null for non-public containers");
            }

            ICloudBlobContainer container = await GetContainerAsync(folderName);

            return container.GetBlobReference(fileName);
        }

        protected async Task<ICloudBlobContainer> GetContainerAsync(string folderName)
        {
            ICloudBlobContainer container;
            if (_containers.TryGetValue(folderName, out container))
            {
                return container;
            }

            container = await PrepareContainer(folderName, IsPublicContainer(folderName));
            _containers[folderName] = container;
            return container;
        }

        private bool IsPublicContainer(string folderName)
        {
            if (KnownPublicFolders.Contains(folderName))
            {
                return true;
            }

            if (KnownPrivateFolders.Contains(folderName))
            {
                return false;
            }

            throw new InvalidOperationException(
                String.Format(CultureInfo.CurrentCulture, "The folder name {0} is not supported.", folderName));
        }

        private async Task<StorageResult> GetBlobContentAsync(string folderName, string fileName, string ifNoneMatch = null)
        {
            ICloudBlobContainer container = await GetContainerAsync(folderName);

            var blob = container.GetBlobReference(fileName);

            var stream = new MemoryStream();
            try
            {
                await blob.DownloadToStreamAsync(
                    stream, 
                    accessCondition: 
                        ifNoneMatch == null ? 
                        null : 
                        AccessCondition.GenerateIfNoneMatchCondition(ifNoneMatch));
            }
            catch (StorageException ex)
            {
                stream.Dispose();

                if (ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.NotModified)
                {
                    return new StorageResult(HttpStatusCode.NotModified, null);
                }
                else if (ex.RequestInformation.ExtendedErrorInformation.ErrorCode == BlobErrorCodeStrings.BlobNotFound)
                {
                    return new StorageResult(HttpStatusCode.NotFound, null);
                }

                throw;
            }
            catch (TestableStorageClientException ex)
            {
                // This is for unit test only, because we can't construct an 
                // StorageException object with the required ErrorCode
                stream.Dispose();

                if (ex.ErrorCode == BlobErrorCodeStrings.BlobNotFound)
                {
                    return new StorageResult(HttpStatusCode.NotFound, null);
                }

                throw;
            }

            stream.Position = 0;
            return new StorageResult(HttpStatusCode.OK, stream);
        }

        private static string GetContentType(string folderName)
        {
            switch (folderName)
            {
                case CoreConstants.PackagesFolderName:
                case CoreConstants.PackageBackupsFolderName:
                case CoreConstants.UploadsFolderName:
                case CoreConstants.ValidationFolderName:
                    return CoreConstants.PackageContentType;

                case CoreConstants.DownloadsFolderName:
                    return CoreConstants.OctetStreamContentType;

                case CoreConstants.PackageReadMesFolderName:
                    return CoreConstants.TextContentType;

                default:
                    throw new InvalidOperationException(
                        String.Format(CultureInfo.CurrentCulture, "The folder name {0} is not supported.", folderName));
            }
        }

        private async Task<ICloudBlobContainer> PrepareContainer(string folderName, bool isPublic)
        {
            var container = _client.GetContainerReference(folderName);
            await container.CreateIfNotExistAsync();
            await container.SetPermissionsAsync(
                new BlobContainerPermissions
                {
                    PublicAccess = isPublic ? BlobContainerPublicAccessType.Blob : BlobContainerPublicAccessType.Off
                });

            return container;
        }

        private struct StorageResult
        {
            private HttpStatusCode _statusCode;
            private Stream _data;

            public HttpStatusCode StatusCode { get { return _statusCode; } }
            public Stream Data { get { return _data; } }

            public StorageResult(HttpStatusCode statusCode, Stream data)
            {
                _statusCode = statusCode;
                _data = data;
            }
        }
    }
}
