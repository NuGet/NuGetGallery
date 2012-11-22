using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Web.Mvc;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Shared.Protocol;

namespace NuGetGallery
{
    public class CloudBlobFileStorageService : IFileStorageService
    {
        private readonly ICloudBlobClient _client;
        private readonly IConfiguration _configuration;
        private readonly ConcurrentDictionary<string, ICloudBlobContainer> _containers = new ConcurrentDictionary<string, ICloudBlobContainer>();
        private bool _containersSetup;

        public CloudBlobFileStorageService(ICloudBlobClient client, IConfiguration configuration)
        {
            _client = client;
            _configuration = configuration;
        }

        public async Task<ActionResult> CreateDownloadFileActionResultAsync(string folderName, string fileName)
        {
            ICloudBlobContainer container = await GetContainer(folderName);
            var blob = await container.GetBlobReferenceAsync(fileName);

            var redirectUri = ConstructRedirectUri(blob.Uri);
            return new RedirectResult(redirectUri.OriginalString, false);
        }

        public async Task DeleteFileAsync(string folderName, string fileName)
        {
            ICloudBlobContainer container = await GetContainer(folderName);
            var blob = await container.GetBlobReferenceAsync(fileName);
            await blob.DeleteIfExistsAsync();
        }

        public async Task<bool> FileExistsAsync(string folderName, string fileName)
        {
            ICloudBlobContainer container = await GetContainer(folderName);
            var blob = await container.GetBlobReferenceAsync(fileName);
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

            ICloudBlobContainer container = await GetContainer(folderName);
            var blob = await container.GetBlobReferenceAsync(fileName);
            var stream = new MemoryStream();
            try
            {
                await blob.DownloadToStreamAsync(stream);
            }
            catch (TestableStorageClientException ex)
            {
                stream.Dispose();

                if (ex.ErrorCode == StorageErrorCodeStrings.ResourceNotFound)
                {
                    return null;
                }
                else
                {
                    throw;
                }
            }

            stream.Position = 0;
            return stream;
        }

        public async Task SaveFileAsync(string folderName, string fileName, Stream packageFile)
        {
            ICloudBlobContainer container = await GetContainer(folderName);
            var blob = await container.GetBlobReferenceAsync(fileName);
            await blob.DeleteIfExistsAsync();
            await blob.UploadFromStreamAsync(packageFile);
            blob.Properties.ContentType = GetContentType(folderName);
            await blob.SetPropertiesAsync();
        }

        private async Task<ICloudBlobContainer> GetContainer(string folderName)
        {
            if (!_containersSetup)
            {
                _containersSetup = true;

                Task packagesTask = PrepareContainer(Constants.PackagesFolderName, isPublic: true);
                Task downloadsTask = PrepareContainer(Constants.DownloadsFolderName, isPublic: true);
                Task uploadsTask = PrepareContainer(Constants.UploadsFolderName, isPublic: false);

                await Task.WhenAll(packagesTask, downloadsTask, uploadsTask);
            }
            
            return _containers[folderName];
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

        private async Task PrepareContainer(string folderName, bool isPublic)
        {
            var container = _client.GetContainerReference(folderName);
            await container.CreateIfNotExistAsync();
            await container.SetPermissionsAsync(
                new BlobContainerPermissions { 
                    PublicAccess = isPublic ? BlobContainerPublicAccessType.Blob : BlobContainerPublicAccessType.Off
                });

            _containers[folderName] = container;
        }

        private Uri ConstructRedirectUri(Uri blobUri)
        {
            if (!String.IsNullOrEmpty(_configuration.AzureCdnHost))
            {
                // If a Cdn is specified, convert the blob url to an Azure Cdn url.
                var builder = new UriBuilder(blobUri.Scheme, _configuration.AzureCdnHost);
                builder.Path = blobUri.AbsolutePath;
                builder.Query = blobUri.Query;

                return builder.Uri;
            }

            return blobUri;
        }
    }
}