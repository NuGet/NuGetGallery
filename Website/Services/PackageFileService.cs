using System;
using System.Globalization;
using System.IO;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class PackageFileService : IPackageFileService
    {
        private readonly IFileStorageService fileStorageSvc;
        
        public PackageFileService(
            IFileStorageService fileStorageSvc)
        {
            this.fileStorageSvc = fileStorageSvc;
        }

        private static string BuildFileName(
            string id, 
            string version)
        {
            return String.Format(CultureInfo.InvariantCulture, Constants.PackageFileSavePathTemplate, id, version, Constants.NuGetPackageFileExtension);
        }

        public ActionResult CreateDownloadPackageActionResult(Package package)
        {
            if (package == null)
                throw new ArgumentNullException("package");
            if (package.PackageRegistration == null 
                || String.IsNullOrWhiteSpace(package.PackageRegistration.Id) 
                || String.IsNullOrWhiteSpace(package.Version))
                throw new ArgumentException("The package is missing required data.", "package");
            
            var fileName = BuildFileName(
                package.PackageRegistration.Id,
                package.Version);

            return fileStorageSvc.CreateDownloadFileActionResult(
                Constants.PackagesFolderName, 
                fileName);
        }

        public void DeletePackageFile(
            string id, 
            string version)
        {
            if (String.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException("id");
            if (String.IsNullOrWhiteSpace(version))
                throw new ArgumentNullException("version");

            var fileName = BuildFileName(id, version);

            fileStorageSvc.DeleteFile(
                Constants.PackagesFolderName, 
                fileName);
        }

        public void SavePackageFile(
            Package package,
            Stream packageFile)
        {
            if (package == null)
                throw new ArgumentNullException("package");
            if (packageFile == null)
                throw new ArgumentNullException("packageFile");
            if (package.PackageRegistration == null 
                || String.IsNullOrWhiteSpace(package.PackageRegistration.Id) 
                || String.IsNullOrWhiteSpace(package.Version))
                throw new ArgumentException("The package is missing required data.", "package");

            var fileName = BuildFileName(
                package.PackageRegistration.Id,
                package.Version);

            fileStorageSvc.SaveFile(
                Constants.PackagesFolderName, 
                fileName, 
                packageFile);
        }
    }
}