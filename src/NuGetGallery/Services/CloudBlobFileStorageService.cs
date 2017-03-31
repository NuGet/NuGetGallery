// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using NuGetGallery.Configuration;
using System.Diagnostics;

namespace NuGetGallery
{
    public class CloudBlobFileStorageService : IFileStorageService
    {
        private readonly ICloudBlobClient _client;
        private readonly IAppConfiguration _configuration;
        private readonly ConcurrentDictionary<string, ICloudBlobContainer> _containers = new ConcurrentDictionary<string, ICloudBlobContainer>();
        private readonly ISourceDestinationRedirectPolicy _redirectPolicy;

        public CloudBlobFileStorageService(ICloudBlobClient client, IAppConfiguration configuration, ISourceDestinationRedirectPolicy redirectPolicy)
        {
            _client = client;
            _configuration = configuration;
            _redirectPolicy = redirectPolicy;
        }

        public async Task<ActionResult> CreateDownloadFileActionResultAsync(Uri requestUrl, string folderName, string fileName)
        {
            ICloudBlobContainer container = await GetContainer(folderName);
            var blob = container.GetBlobReference(fileName);

            var redirectUri = GetRedirectUri(requestUrl, blob.Uri);
            return new RedirectResult(redirectUri.OriginalString, false);
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

        public async Task<bool> IsAvailableAsync()
        {
            var container = await GetContainer(Constants.PackagesFolderName);
            return await container.ExistsAsync();
        }

        private async Task<ICloudBlobContainer> GetContainer(string folderName)
        {
            ICloudBlobContainer container;
            if (_containers.TryGetValue(folderName, out container))
            {
                return container;
            }

            Task<ICloudBlobContainer> creationTask;
            switch (folderName)
            {
                case Constants.PackagesFolderName:
                case Constants.PackageBackupsFolderName:
                case Constants.DownloadsFolderName:
                    creationTask = PrepareContainer(folderName, isPublic: true);
                    break;

                case Constants.ContentFolderName:
                case Constants.UploadsFolderName:
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
                case Constants.PackagesFolderName:
                case Constants.PackageBackupsFolderName:
                case Constants.UploadsFolderName:
                    return Constants.PackageContentType;

                case Constants.DownloadsFolderName:
                    return Constants.OctetStreamContentType;

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

        internal async Task<ActionResult> CreateDownloadFileActionResult(
            HttpContextBase httpContext,
            string folderName,
            string fileName)
        {
            var container = await GetContainer(folderName);
            var blob = container.GetBlobReference(fileName);

            var redirectUri = GetRedirectUri(httpContext.Request.Url, blob.Uri);
            return new RedirectResult(redirectUri.OriginalString, false);
        }

        internal Uri GetRedirectUri(Uri requestUrl, Uri blobUri)
        {
            if (!_redirectPolicy.IsAllowed(requestUrl, blobUri))
            {
                Trace.TraceInformation("Redirect from {0} to {1} was not allowed", requestUrl, blobUri);
                throw new InvalidOperationException("Unsafe redirects are not allowed");
            }

            var host = string.IsNullOrEmpty(_configuration.AzureCdnHost)
                ? blobUri.Host 
                : _configuration.AzureCdnHost;

            // When a blob query string is passed, that one always wins.
            // This will only happen on private NuGet gallery instances,
            // not on NuGet.org.
            // When no blob query string is passed, we forward the request
            // URI's query string to the CDN. See https://github.com/NuGet/NuGetGallery/issues/3168
            // and related PR's.
            var queryString = !string.IsNullOrEmpty(blobUri.Query)
                ? blobUri.Query
                : requestUrl.Query;

            if (!string.IsNullOrEmpty(queryString))
            {
                queryString = queryString.TrimStart('?');
            }

            var urlBuilder = new UriBuilder(requestUrl.Scheme, host)
            {
                Path = blobUri.LocalPath,
                Query = queryString
            };

            return urlBuilder.Uri;
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
