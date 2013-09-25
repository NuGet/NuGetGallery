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

namespace NuGetGallery
{
    public class CloudBlobFileStorageService : IFileStorageService
    {
        private readonly ICloudBlobClient _client;
        private readonly IAppConfiguration _configuration;
        private readonly ConcurrentDictionary<string, CloudBlobContainer> _containers = new ConcurrentDictionary<string, CloudBlobContainer>();

        // Internal VTable Pattern
        Func<CloudBlobContainer, Task> Container_CreateIfNotExistAsync { get; set; }
        Func<CloudBlobContainer, BlobContainerPermissions, Task> Container_SetPermissionsAsync { get; set; }
        Func<CloudBlobContainer, string, ISimpleCloudBlob> Container_GetBlobReference { get; set; }

        public CloudBlobFileStorageService(ICloudBlobClient client, IAppConfiguration configuration)
        {
            _client = client;
            _configuration = configuration;

            // Internal VTable
            Container_CreateIfNotExistAsync = c =>
            {
                return Task.Factory.FromAsync<bool>(
                    c.BeginCreateIfNotExists(null, null),
                    c.EndCreateIfNotExists);
            };
            Container_SetPermissionsAsync = (c, permissions) =>
            {
                return Task.Factory.FromAsync(
                    c.BeginSetPermissions(permissions, null, null),
                    c.EndSetPermissions);
            };
            Container_GetBlobReference = (c, blobName) => new CloudBlobWrapper(c.GetBlockBlobReference(blobName));
        }

        public async Task<ActionResult> CreateDownloadFileActionResultAsync(Uri requestUrl, string folderName, string fileName)
        {
            var container = await GetContainer(folderName);
            var blob = Container_GetBlobReference(container, fileName);

            var redirectUri = GetRedirectUri(requestUrl, blob.Uri);
            return new RedirectResult(redirectUri.OriginalString, false);
        }

        public async Task DeleteFileAsync(string folderName, string fileName)
        {
            var container = await GetContainer(folderName);
            var blob = Container_GetBlobReference(container, fileName);
            await blob.DeleteIfExistsAsync();
        }

        public async Task<bool> FileExistsAsync(string folderName, string fileName)
        {
            CloudBlobContainer container = await GetContainer(folderName);
            var blob = Container_GetBlobReference(container, fileName);
            return await blob.ExistsAsync();
        }

        public async Task<Stream> GetFileAsync(string folderName, string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException("folderName");
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            return (await GetBlobContentAsync(folderName, fileName)).Data;
        }

        public async Task<IFileReference> GetFileReferenceAsync(string folderName, string fileName, string ifNoneMatch = null)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException("folderName");
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            var container = await GetContainer(folderName);
            var blob = Container_GetBlobReference(container, fileName);
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

        public async Task SaveFileAsync(string folderName, string fileName, Stream packageFile)
        {
            var container = await GetContainer(folderName);
            var blob = Container_GetBlobReference(container, fileName);
            await blob.DeleteIfExistsAsync();
            await blob.UploadFromStreamAsync(packageFile);
            blob.Properties.ContentType = GetContentType(folderName);
            await blob.SetPropertiesAsync();
        }

        private async Task<CloudBlobContainer> GetContainer(string folderName)
        {
            CloudBlobContainer container;
            if (_containers.TryGetValue(folderName, out container))
            {
                return container;
            }

            Task<CloudBlobContainer> creationTask;
            switch (folderName)
            {
                case Constants.PackagesFolderName:
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
            var container = await GetContainer(folderName);
            var blob = Container_GetBlobReference(container, fileName);

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
                case Constants.UploadsFolderName:
                    return Constants.PackageContentType;

                case Constants.DownloadsFolderName:
                    return Constants.OctetStreamContentType;

                default:
                    throw new InvalidOperationException(
                        String.Format(CultureInfo.CurrentCulture, "The folder name {0} is not supported.", folderName));
            }
        }

        private async Task<CloudBlobContainer> PrepareContainer(string folderName, bool isPublic)
        {
            var container = _client.GetContainerReference(folderName);
            await Container_CreateIfNotExistAsync(container);
            await Container_SetPermissionsAsync(container,
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
            var blob = Container_GetBlobReference(container, fileName);

            var redirectUri = GetRedirectUri(httpContext.Request.Url, blob.Uri);
            return new RedirectResult(redirectUri.OriginalString, false);
        }

        internal Uri GetRedirectUri(Uri requestUrl, Uri blobUri)
        {
            string host = String.IsNullOrEmpty(_configuration.AzureCdnHost) ? blobUri.Host : _configuration.AzureCdnHost;
            var urlBuilder = new UriBuilder(requestUrl.Scheme, host)
            {
                Path = blobUri.LocalPath,
                Query = blobUri.Query
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
