using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Mvc;
using Microsoft.WindowsAzure.StorageClient;

namespace NuGetGallery
{
    public class CloudBlobFileStorageService : IFileStorageService
    {
        ICloudBlobClient client;
        IDictionary<string, ICloudBlobContainer> containers = new Dictionary<string, ICloudBlobContainer>();

        public CloudBlobFileStorageService(ICloudBlobClient client)
        {
            this.client = client;

            PrepareContainer(Const.PackagesFolderName, true);
            PrepareContainer(Const.UploadsFolderName, false);
        }

        public ActionResult CreateDownloadFileActionResult(
            string folderName, 
            string fileName)
        {
            var container = GetContainer(folderName);
            var blob = container.GetBlobReference(fileName);
            return new RedirectResult(blob.Uri.ToString(), false);
        }

        public void DeleteFile(
            string folderName, 
            string fileName)
        {
            var container = GetContainer(folderName);
            var blob = container.GetBlobReference(fileName);
            blob.DeleteIfExists();
        }

        ICloudBlobContainer GetContainer(string folderName)
        {
            return containers[folderName];
        }

        public Stream GetFile(
            string folderName, 
            string fileName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
                throw new ArgumentNullException("folderName");
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException("fileName");
            
            var container = GetContainer(folderName);
            var blob = container.GetBlobReference(fileName);
            var stream = new MemoryStream();
            try
            {
                blob.DownloadToStream(stream);
            }
            catch (TestableStorageClientException ex)
            {
                if (ex.ErrorCode == StorageErrorCode.ResourceNotFound)
                {
                    stream.Dispose();
                    return null;
                }
                else
                    throw;
            }
            return stream;
        }

        void PrepareContainer(
            string folderName,
            bool isPublic)
        {
            var container = client.GetContainerReference(folderName);
            container.CreateIfNotExist();

            if (isPublic)
                container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
            else
                container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Off });

            containers.Add(folderName, container);
        }

        public void SaveFile(
            string folderName, 
            string fileName, 
            Stream fileStream)
        {
            var container = GetContainer(folderName);
            var blob = container.GetBlobReference(fileName);
            blob.DeleteIfExists();
            blob.UploadFromStream(fileStream);
        }
    }
}