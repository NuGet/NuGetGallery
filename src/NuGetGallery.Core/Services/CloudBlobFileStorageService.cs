using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;

namespace NuGetGallery
{
    public class CloudBlobFileStorageService : IFileStorageService
    {
        static int[] SleepTimes = { 50, 750, 1500, 2500, 3750, 5250, 7000, 9000 };

        private readonly ICloudBlobClient _client;
        private readonly ConcurrentDictionary<string, ICloudBlobContainer> _containers = new ConcurrentDictionary<string, ICloudBlobContainer>();
        private Func<string, bool> _isPublicFolderPolicy;

        public CloudBlobFileStorageService(ICloudBlobClient client, Func<string, bool> isPublicFolderPolicy)
        {
            _client = client;
            _isPublicFolderPolicy = isPublicFolderPolicy;
        }

        public async Task DeleteFileAsync(string folderName, string fileName)
        {
            ICloudBlobContainer container = await EnsureContainer(folderName);
            var blob = container.GetBlobReference(fileName);
            await blob.DeleteIfExistsAsync();
        }

        public async Task DownloadToFileAsync(string folderName, string fileName, string downloadedPackageFilePath)
        {
            ICloudBlobContainer container = await EnsureContainer(folderName);
            var blob = container.GetBlobReference(fileName);
            await blob.DownloadToFileAsync(downloadedPackageFilePath);
        }

        public async Task UploadFromFileAsync(string folderName, string fileName, string path, string contentType)
        {
            ICloudBlobContainer container = await EnsureContainer(folderName);
            var blob = container.GetBlobReference(fileName);
            await blob.UploadFromFileAsync(path);
            blob.Properties.ContentType = "application/zip";
            await blob.SetPropertiesAsync();
        }

        public async Task<bool> FileExistsAsync(string folderName, string fileName)
        {
            ICloudBlobContainer container = await EnsureContainer(folderName);
            var blob = container.GetBlobReference(fileName);
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

            ICloudBlobContainer container = await EnsureContainer(folderName);
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

        public async Task SaveFileAsync(string folderName, string fileName, Stream packageFile, string contentType)
        {
            ICloudBlobContainer container = await EnsureContainer(folderName);
            var blob = container.GetBlobReference(fileName);
            await blob.DeleteIfExistsAsync();
            await blob.UploadFromStreamAsync(packageFile);
            blob.Properties.ContentType = contentType;
            await blob.SetPropertiesAsync();
        }

        public async Task<ICloudBlobContainer> EnsureContainer(string folderName)
        {
            ICloudBlobContainer container;
            if (_containers.TryGetValue(folderName, out container))
            {
                return container;
            }

            container = await PrepareContainer(folderName, _isPublicFolderPolicy(folderName));
            _containers[folderName] = container;
            return container;
        }

        private async Task<StorageResult> GetBlobContentAsync(string folderName, string fileName, string ifNoneMatch = null)
        {
            ICloudBlobContainer container = await EnsureContainer(folderName);

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

        public UriOrStream GetDownloadUriOrStream(
            string folderName,
            string fileName)
        {
            ICloudBlobContainer container = _client.GetContainerReference(folderName);
            var blob = container.GetBlobReference(fileName);
            return new UriOrStream(blob.Uri);
        }

        public async Task BeginCopyAsync(
            string folderName1, string fileName1, 
            string folderName2, string fileName2)
        {
            ICloudBlobContainer container1 = _client.GetContainerReference(folderName1);
            var blob1 = container1.GetBlobReference(fileName1);

            ICloudBlobContainer container2 = await EnsureContainer(folderName2);
            var blob2 = container2.GetBlobReference(fileName2);

            await blob2.StartCopyFromBlobAsync(blob1.Uri);
        }

        public async Task WaitForCopyCompleteAsync(string folderName, string fileName)
        {
            ICloudBlobContainer container = _client.GetContainerReference(folderName);
            var blob = container.GetBlobReference(fileName);

            DateTime startTime = DateTime.Now;
            TimeSpan timeout = TimeSpan.FromMinutes(10);
            int i = 0;
            while (DateTime.Now < (startTime + timeout))
            {
                if (blob.CopyState != null)
                {
                    if (blob.CopyState.Status == CopyStatus.Success)
                    {
                        return;
                    }

                    if (blob.CopyState.Status == CopyStatus.Failed)
                    {
                        string msg1 = string.Format("Blob copy failed. Src: {0}, Dst: {1}, BytesCopied: {2} ", 
                            blob.CopyState.Source, 
                            blob.Uri, 
                            blob.CopyState.BytesCopied);
                        throw new BlobCopyFailedException(msg1);
                    }
                }

                await blob.FetchAttributesAsync();

                await Task.Delay(SleepTimes[i]);
                if (i < SleepTimes.Length - 1)
                {
                    i += 1;
                }
            }

            string msg2 = string.Format("Blob copy did not complete within {0} minutes", timeout.TotalMinutes);
            throw new TimeoutException(msg2);
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
