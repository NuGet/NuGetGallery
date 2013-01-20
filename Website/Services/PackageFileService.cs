using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class PackageFileService : IPackageFileService
    {
        private readonly IFileStorageService _fileStorageService;

        public PackageFileService(IFileStorageService fileStorageService)
        {
            _fileStorageService = fileStorageService;
        }

        public Task<ActionResult> CreateDownloadPackageActionResultAsync(Package package)
        {
            var fileName = BuildFileName(package);
            return _fileStorageService.CreateDownloadFileActionResultAsync(Constants.PackagesFolderName, fileName);
        }

        public Task DeletePackageFileAsync(string id, string version)
        {
            if (String.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException("id");
            }

            if (String.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentNullException("version");
            }

            var fileName = BuildFileName(id, version);
            return _fileStorageService.DeleteFileAsync(Constants.PackagesFolderName, fileName);
        }

        public Task SavePackageFileAsync(Package package, Stream packageFile)
        {
            if (packageFile == null)
            {
                throw new ArgumentNullException("packageFile");
            }

            var fileName = BuildFileName(package);
            return _fileStorageService.SaveFileAsync(Constants.PackagesFolderName, fileName, packageFile);
        }

        public Task<Stream> DownloadPackageFileAsync(Package package)
        {
            var fileName = BuildFileName(package);
            return _fileStorageService.GetFileAsync(Constants.PackagesFolderName, fileName);
        }

        private static string BuildFileName(string id, string version)
        {
            return String.Format(
                CultureInfo.InvariantCulture,
                Constants.PackageFileSavePathTemplate,
                id,
                version,
                Constants.NuGetPackageFileExtension);
        }

        private static string BuildFileName(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            if (package.PackageRegistration == null || 
                String.IsNullOrWhiteSpace(package.PackageRegistration.Id) || 
                String.IsNullOrWhiteSpace(package.Version))
            {
                throw new ArgumentException("The package is missing required data.", "package");
            }

            return BuildFileName(package.PackageRegistration.Id, package.Version);
        }
    }
}