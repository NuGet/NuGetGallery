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