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
using NuGetGallery.Diagnostics;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace NuGetGallery
{
    public class CloudBlobCoreFileStorageService : ICoreFileStorageService
    {
        /// <summary>
        /// This is the maximum duration for <see cref="CopyFileAsync(ISimpleCloudBlob, string, string, IAccessCondition)"/> to poll,
        /// waiting for a package copy to complete. The value picked today is based off of the maximum duration we wait
        /// when uploading files to Azure China blob storage. Note that in cases when the copy source and destination
        /// are in the same container, the copy completed immediately and no polling is necessary.
        /// </summary>
        private static readonly TimeSpan MaxCopyDuration = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan CopyPollFrequency = TimeSpan.FromMilliseconds(500);

        protected readonly ICloudBlobClient _client;
        protected readonly IDiagnosticsSource _trace;
        protected readonly ICloudBlobContainerInformationProvider _cloudBlobFolderInformationProvider;
        protected readonly ConcurrentDictionary<string, ICloudBlobContainer> _containers = new ConcurrentDictionary<string, ICloudBlobContainer>();

        public CloudBlobCoreFileStorageService(
            ICloudBlobClient client,
            IDiagnosticsService diagnosticsService,
            ICloudBlobContainerInformationProvider cloudBlobFolderInformationProvider)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _trace = diagnosticsService?.SafeGetSource(nameof(CloudBlobCoreFileStorageService)) ?? throw new ArgumentNullException(nameof(diagnosticsService));
            _cloudBlobFolderInformationProvider = cloudBlobFolderInformationProvider ?? throw new ArgumentNullException(nameof(cloudBlobFolderInformationProvider));
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
            if (string.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            return (await GetBlobContentAsync(folderName, fileName)).Data;
        }

        public async Task<IFileReference> GetFileReferenceAsync(string folderName, string fileName, string ifNoneMatch = null)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var result = await GetBlobContentAsync(folderName, fileName, ifNoneMatch);
            if (result.StatusCode == HttpStatusCode.NotModified)
            {
                return CloudFileReference.NotModified(ifNoneMatch);
            }
            else if (result.StatusCode == HttpStatusCode.OK)
            {
                return CloudFileReference.Modified(result.Data, result.ETag);
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

            if (!await srcBlob.ExistsAsync())
            {
                _trace.TraceEvent(
                    LogLevel.Warning,
                    eventId: 0,
                    message: $"Before calling FetchAttributesAsync(), the source blob '{srcBlob.Name}' does not exist.");
            }

            // Determine the source blob etag.
            await srcBlob.FetchAttributesAsync();
            var srcAccessCondition = AccessConditionWrapper.GenerateIfMatchCondition(srcBlob.ETag);

            // Check if the destination blob already exists and fetch attributes.
            if (await destBlob.ExistsAsync())
            {
                var sourceBlobMetadata = srcBlob.Metadata;
                await destBlob.FetchAttributesAsync();
                var destinationBlobMetadata = destBlob.Metadata;
                if (destBlob.CopyState?.Status == CloudBlobCopyStatus.Failed)
                {
                    // If the last copy failed, allow this copy to occur no matter what the caller's destination
                    // condition is. This is because the source blob is preferable over a failed copy. We use the etag
                    // of the failed blob to avoid inadvertently replacing a blob that is now valid (i.e. has a
                    // successful copy status).
                    _trace.TraceEvent(
                        LogLevel.Information,
                        eventId: 0,
                        message: $"Destination blob '{destFolderName}/{destFileName}' already exists but has a " +
                        $"failed copy status. This blob will be replaced if the etag matches '{destBlob.ETag}'.");

                    destAccessCondition = AccessConditionWrapper.GenerateIfMatchCondition(destBlob.ETag);
                }
                else if (sourceBlobMetadata != null && destinationBlobMetadata != null)
                {
                    var sourceBlobHasSha512Hash = sourceBlobMetadata.TryGetValue(CoreConstants.Sha512HashAlgorithmId, out var sourceBlobSha512Hash);
                    var destinationBlobHasSha512Hash = destinationBlobMetadata.TryGetValue(CoreConstants.Sha512HashAlgorithmId, out var destinationBlobSha512Hash);
                    if (!sourceBlobHasSha512Hash)
                    {
                        _trace.TraceEvent(
                           LogLevel.Information,
                           eventId: 0,
                           message: $"Source blob ('{srcBlob.Uri.ToString()}') doesn't have the Sha512 hash.");
                    }
                    if (!destinationBlobHasSha512Hash)
                    {
                        _trace.TraceEvent(
                           LogLevel.Information,
                           eventId: 0,
                           message: $"Destination blob ('{destBlob.Uri.ToString()}') doesn't have the Sha512 hash.");
                    }
                    if (sourceBlobHasSha512Hash && destinationBlobHasSha512Hash && sourceBlobSha512Hash == destinationBlobSha512Hash && srcBlob.Properties.Length == destBlob.Properties.Length)
                    {
                        // If the blob Sha512 hash is the same and the length is the same, no-op the copy.
                        _trace.TraceEvent(
                            LogLevel.Information,
                            eventId: 0,
                            message: $"Destination blob '{destFolderName}/{destFileName}' already has Sha512 hash " +
                            $"'{destinationBlobSha512Hash}' and length '{destBlob.Properties.Length}'. The copy " +
                            "will be skipped.");

                        return srcBlob.ETag;
                    }
                }
            }

            _trace.TraceEvent(
                LogLevel.Information,
                eventId: 0,
                message: $"Copying of source blob '{srcBlob.Uri}' to '{destFolderName}/{destFileName}' with source " +
                $"access condition {Log(srcAccessCondition)} and destination access condition " +
                $"{Log(destAccessCondition)}.");

            // Start the server-side copy and wait for it to complete. If "If-None-Match: *" was specified and the
            // destination already exists, HTTP 409 is thrown. If "If-Match: ETAG" was specified and the destination
            // has changed, HTTP 412 is thrown.
            try
            {
                await destBlob.StartCopyAsync(
                    srcBlob,
                    srcAccessCondition,
                    destAccessCondition);
            }
            catch (CloudBlobConflictException ex)
            {
                throw new FileAlreadyExistsException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "There is already a blob with name {0} in container {1}.",
                        destFileName,
                        destFolderName),
                    ex.InnerException);
            }

            var stopwatch = Stopwatch.StartNew();
            while (destBlob.CopyState.Status == CloudBlobCopyStatus.Pending
                   && stopwatch.Elapsed < MaxCopyDuration)
            {
                if (!await destBlob.ExistsAsync())
                {
                    _trace.TraceEvent(
                        LogLevel.Warning,
                        eventId: 0,
                        message: $"Before calling FetchAttributesAsync(), the destination blob '{destBlob.Name}' does not exist.");
                }

                await destBlob.FetchAttributesAsync();
                await Task.Delay(CopyPollFrequency);
            }

            if (destBlob.CopyState.Status == CloudBlobCopyStatus.Pending)
            {
                throw new TimeoutException($"Waiting for the blob copy operation to complete timed out after {MaxCopyDuration.TotalSeconds} seconds.");
            }
            else if (destBlob.CopyState.Status != CloudBlobCopyStatus.Success)
            {
                throw new CloudBlobStorageException($"The blob copy operation had copy status {destBlob.CopyState.Status} ({destBlob.CopyState.StatusDescription}).");
            }

            return srcBlob.ETag;
        }

        private static string Log(IAccessCondition accessCondition)
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

        public Task SaveFileAsync(string folderName, string fileName, Stream file, bool overwrite = true)
        {
            var contentType = GetContentType(folderName);
            return SaveFileAsync(folderName, fileName, contentType, file, overwrite);
        }

        public async Task SaveFileAsync(string folderName, string fileName, string contentType, Stream file, bool overwrite = true)
        {
            if (contentType == null)
            {
                throw new ArgumentNullException(nameof(contentType));
            }

            ICloudBlobContainer container = await GetContainerAsync(folderName);
            var blob = container.GetBlobReference(fileName);

            try
            {
                await blob.UploadFromStreamAsync(file, overwrite);
            }
            catch (CloudBlobConflictException ex)
            {
                throw new FileAlreadyExistsException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "There is already a blob with name {0} in container {1}.",
                        fileName,
                        folderName),
                    ex.InnerException);
            }

            blob.Properties.ContentType = contentType;
            blob.Properties.CacheControl = GetCacheControl(folderName);
            await blob.SetPropertiesAsync();
        }

        public async Task SaveFileAsync(string folderName, string fileName, Stream file, IAccessCondition accessConditions)
        {
            ICloudBlobContainer container = await GetContainerAsync(folderName);
            var blob = container.GetBlobReference(fileName);

            accessConditions = accessConditions ?? AccessConditionWrapper.GenerateIfNotExistsCondition();

            try
            {
                await blob.UploadFromStreamAsync(file, accessConditions);
            }
            catch (CloudBlobConflictException ex)
            {
                throw new FileAlreadyExistsException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "There is already a blob with name {0} in container {1}.",
                        fileName,
                        folderName),
                    ex.InnerException);
            }

            blob.Properties.ContentType = GetContentType(folderName);
            await blob.SetPropertiesAsync();
        }

        public async Task<Uri> GetFileUriAsync(string folderName, string fileName)
        {
            var blob = await GetBlobForUriAsync(folderName, fileName);

            return blob.Uri;
        }

        public async Task<Uri> GetPrivilegedFileUriAsync(
            string folderName,
            string fileName,
            FileUriPermissions permissions,
            DateTimeOffset endOfAccess)
        {
            if (endOfAccess < DateTimeOffset.UtcNow)
            {
                throw new ArgumentOutOfRangeException(nameof(endOfAccess), $"{nameof(endOfAccess)} is in the past");
            }

            var blob = await GetBlobForUriAsync(folderName, fileName);
            string sas = await blob.GetSharedAccessSignature(permissions, endOfAccess);

            return new Uri(blob.Uri, sas);
        }

        public async Task<Uri> GetFileReadUriAsync(string folderName, string fileName, DateTimeOffset? endOfAccess)
        {
            var blob = await GetBlobForUriAsync(folderName, fileName);

            if (IsPublicContainer(folderName))
            {
                return blob.Uri;
            }

            if (!endOfAccess.HasValue)
            {
                throw new ArgumentNullException(nameof(endOfAccess), $"{nameof(endOfAccess)} must not be null for non-public containers");
            }

            if (endOfAccess < DateTimeOffset.UtcNow)
            {
                throw new ArgumentOutOfRangeException(nameof(endOfAccess), $"{nameof(endOfAccess)} is in the past");
            }

            string sas = await blob.GetSharedAccessSignature(FileUriPermissions.Read, endOfAccess.Value);

            return new Uri(blob.Uri, sas);
        }

        /// <summary>
        /// Asynchronously sets blob metadata.
        /// </summary>
        /// <param name="folderName">The folder (container) name.</param>
        /// <param name="fileName">The blob file name.</param>
        /// <param name="updateMetadataAsync">A function which updates a metadata dictionary and returns <c>true</c>
        /// for changes to be persisted or <c>false</c> for changes to be discarded.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task SetMetadataAsync(
            string folderName,
            string fileName,
            Func<Lazy<Task<Stream>>, IDictionary<string, string>, Task<bool>> updateMetadataAsync)
        {
            if (folderName == null)
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            if (updateMetadataAsync == null)
            {
                throw new ArgumentNullException(nameof(updateMetadataAsync));
            }

            var container = await GetContainerAsync(folderName);
            var blob = container.GetBlobReference(fileName);

            await blob.FetchAttributesAsync();

            var lazyStream = new Lazy<Task<Stream>>(() => GetFileAsync(folderName, fileName));
            var wasUpdated = await updateMetadataAsync(lazyStream, blob.Metadata);

            if (wasUpdated)
            {
                var accessCondition = AccessConditionWrapper.GenerateIfMatchCondition(blob.ETag);
                await blob.SetMetadataAsync(accessCondition);
            }
        }

        /// <summary>
        /// Asynchronously sets blob properties.
        /// </summary>
        /// <param name="folderName">The folder (container) name.</param>
        /// <param name="fileName">The blob file name.</param>
        /// <param name="updatePropertiesAsync">A function which updates blob properties and returns <c>true</c>
        /// for changes to be persisted or <c>false</c> for changes to be discarded.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task SetPropertiesAsync(
            string folderName,
            string fileName,
            Func<Lazy<Task<Stream>>, ICloudBlobProperties, Task<bool>> updatePropertiesAsync)
        {
            if (folderName == null)
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            if (updatePropertiesAsync == null)
            {
                throw new ArgumentNullException(nameof(updatePropertiesAsync));
            }

            var container = await GetContainerAsync(folderName);
            var blob = container.GetBlobReference(fileName);

            await blob.FetchAttributesAsync();

            var lazyStream = new Lazy<Task<Stream>>(() => GetFileAsync(folderName, fileName));
            var wasUpdated = await updatePropertiesAsync(lazyStream, blob.Properties);

            if (wasUpdated)
            {
                var accessCondition = AccessConditionWrapper.GenerateIfMatchCondition(blob.ETag);
                await blob.SetPropertiesAsync(accessCondition);
            }
        }

        public async Task<string> GetETagOrNullAsync(
            string folderName,
            string fileName)
        {
            folderName = folderName ?? throw new ArgumentNullException(nameof(folderName));
            fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));

            var container = await GetContainerAsync(folderName);
            var blob = container.GetBlobReference(fileName);
            try
            {
                await blob.FetchAttributesAsync();
                return blob.ETag;
            }
            // In case that the blob does not exist return null.
            catch (CloudBlobStorageException)
            {
                return null;
            }
        }

        private async Task<ISimpleCloudBlob> GetBlobForUriAsync(string folderName, string fileName)
        {
            folderName = folderName ?? throw new ArgumentNullException(nameof(folderName));
            fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            
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
            return _cloudBlobFolderInformationProvider.IsPublicContainer(folderName);
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
                        AccessConditionWrapper.GenerateIfNoneMatchCondition(ifNoneMatch));
            }
            catch (CloudBlobNotModifiedException)
            {
                stream.Dispose();
                return new StorageResult(HttpStatusCode.NotModified, null, blob.ETag);
            }
            catch (CloudBlobNotFoundException)
            {
                stream.Dispose();
                return new StorageResult(HttpStatusCode.NotFound, null, blob.ETag);
            }

            stream.Position = 0;
            return new StorageResult(HttpStatusCode.OK, stream, blob.ETag);
        }

        private string GetContentType(string folderName)
        {
            return _cloudBlobFolderInformationProvider.GetContentType(folderName);
        }

        private string GetCacheControl(string folderName)
        {
            return _cloudBlobFolderInformationProvider.GetCacheControl(folderName);
        }

        private async Task<ICloudBlobContainer> PrepareContainer(string folderName, bool isPublic)
        {
            var container = _client.GetContainerReference(folderName);
            await container.CreateIfNotExistAsync(isPublic);

            return container;
        }

        private struct StorageResult
        {
            public HttpStatusCode StatusCode { get; }
            public Stream Data { get; }
            public string ETag { get; }

            public StorageResult(HttpStatusCode statusCode, Stream data, string etag)
            {
                StatusCode = statusCode;
                Data = data;
                ETag = etag;
            }
        }
    }
}