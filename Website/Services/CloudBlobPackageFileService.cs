using System.IO;
using System.Web.Mvc;
using Microsoft.WindowsAzure.StorageClient;

namespace NuGetGallery
{
    public class CloudBlobPackageFileService : IPackageFileService
    {
        ICloudBlobClient blobClient;
        ICloudBlobContainer blobContainer;

        public CloudBlobPackageFileService(ICloudBlobClient client)
        {
            this.blobClient = client;

            blobContainer = blobClient.GetContainerReference("packages");
            blobContainer.CreateIfNotExist();
            blobContainer.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
        }

        static string BuildPackageUriPart(Package package)
        {
            return BuildPackageUriPart(package.PackageRegistration.Id, package.Version);
        }

        static string BuildPackageUriPart(string id, string version)
        {
            return string.Format(Const.PackageFileSavePathTemplate, id, version, Const.PackageFileExtension);
        }

        public ActionResult CreateDownloadPackageResult(Package package)
        {
            var blob = blobContainer.GetBlobReference(BuildPackageUriPart(package));
            return new RedirectResult(blob.Uri.ToString(), false);
        }

        public void DeletePackageFile(
            string id,
            string version)
        {
            var blob = blobContainer.GetBlobReference(BuildPackageUriPart(id, version));
            blob.DeleteIfExists();
        }

        public void SavePackageFile(
            Package package,
            Stream packageFile)
        {
            var blob = blobContainer.GetBlobReference(BuildPackageUriPart(package));
            blob.DeleteIfExists();
            blob.UploadFromStream(packageFile);
        }
    }
}