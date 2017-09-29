// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;

namespace NuGetGallery
{
    public class CloudBlobCoreFileStorageService : ICoreFileStorageService
    {
        protected readonly ICloudBlobClient _client;
        protected readonly ConcurrentDictionary<string, ICloudBlobContainer> _containers = new ConcurrentDictionary<string, ICloudBlobContainer>();

        public CloudBlobCoreFileStorageService(ICloudBlobClient client)
        {
            _client = client;
        }

        public async Task DeleteFileAsync(string folderName, string fileName)
        {
            ICloudBlobContainer container = await GetContainer(folderName);
            var blob = container.GetBlobReference(fileName);
            await blob.DeleteIfExistsAsync();
        }

        public async Task<bool> FileExistsAsync(string folderName, string fileName)
        {
            ICloudBlobContainer container = await GetContainer(folderName);
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

            ICloudBlobContainer container = await GetContainer(folderName);
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

        public async Task SaveFileAsync(string folderName, string fileName, Stream packageFile, bool overwrite = true)
        {
            ICloudBlobContainer container = await GetContainer(folderName);
            var blob = container.GetBlobReference(fileName);

            try
            {
                await blob.UploadFromStreamAsync(packageFile, overwrite);
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int?)HttpStatusCode.Conflict)
            {
                throw new InvalidOperationException(
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

        public async Task<Uri> GetFileReadUriAsync(string folderName, string fileName, DateTimeOffset? endOfAccess)
        {
            folderName = folderName ?? throw new ArgumentNullException(nameof(folderName));
            fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            if (endOfAccess.HasValue && endOfAccess < DateTimeOffset.UtcNow)
            {
                throw new ArgumentOutOfRangeException(nameof(endOfAccess), $"{nameof(endOfAccess)} is in the past");
            }
            ICloudBlobContainer container = await GetContainer(folderName);
            var blob = container.GetBlobReference(fileName);
            return new Uri(blob.Uri, blob.GetSharedReadSignature(endOfAccess));
        }

        protected async Task<ICloudBlobContainer> GetContainer(string folderName)
        {
            ICloudBlobContainer container;
            if (_containers.TryGetValue(folderName, out container))
            {
                return container;
            }

            Task<ICloudBlobContainer> creationTask;
            switch (folderName)
            {
                case CoreConstants.PackagesFolderName:
                case CoreConstants.PackageBackupsFolderName:
                case CoreConstants.DownloadsFolderName:
                    creationTask = PrepareContainer(folderName, isPublic: true);
                    break;

                case CoreConstants.ContentFolderName:
                case CoreConstants.UploadsFolderName:
                case CoreConstants.PackageReadMesFolderName:
                case CoreConstants.ValidationFolderName:
                    creationTask = PrepareContainer(folderName, isPublic: false);
                    break;

                default:
                    throw new InvalidOperationException(
                        String.Format(CultureInfo.CurrentCulture, "The folder name {0} is not supported.", folderName));
            }

            container = await creationTask;
            _containers[folderName] = container;
            return container;
        }

        private async Task<StorageResult> GetBlobContentAsync(string folderName, string fileName, string ifNoneMatch = null)
        {
            ICloudBlobContainer container = await GetContainer(folderName);

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
