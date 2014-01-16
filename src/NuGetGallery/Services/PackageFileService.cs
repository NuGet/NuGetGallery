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

        public Task<ActionResult> CreateDownloadPackageActionResultAsync(Uri requestUrl, Package package)
        {
            var fileName = BuildFileName(package);
            return _fileStorageService.CreateDownloadFileActionResultAsync(requestUrl, Constants.PackagesFolderName, fileName);
        }

        public Task<ActionResult> CreateDownloadPackageActionResultAsync(Uri requestUrl, string id, string version)
        {
            var fileName = BuildFileName(id, version);
            return _fileStorageService.CreateDownloadFileActionResultAsync(requestUrl, Constants.PackagesFolderName, fileName);
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

        public async Task<Stream> DownloadPackageFileAsync(Package package)
        {
            var fileName = BuildFileName(package);
            return (await _fileStorageService.GetFileAsync(Constants.PackagesFolderName, fileName));
        }

        private static string BuildFileName(string id, string version)
        {
            if (id == null)
            {
                throw new ArgumentNullException("id");
            }
            
            if (version == null)
            {
                throw new ArgumentNullException("version");
            }

            // Note: packages should be saved and retrieved in blob storage using the lower case version of their filename because
            // a) package IDs can and did change case over time
            // b) blob storage is case sensitive
            // c) we don't want to hit the database just to look up the right case
            // and remember - version can contain letters too.
            return String.Format(
                CultureInfo.InvariantCulture,
                Constants.PackageFileSavePathTemplate,
                id.ToLowerInvariant(),
                version.ToLowerInvariant(),
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
                (String.IsNullOrWhiteSpace(package.NormalizedVersion) && String.IsNullOrWhiteSpace(package.Version)))
            {
                throw new ArgumentException("The package is missing required data.", "package");
            }

            return BuildFileName(
                package.PackageRegistration.Id, 
                String.IsNullOrEmpty(package.NormalizedVersion) ?
                    SemanticVersionExtensions.Normalize(package.Version) :
                    package.NormalizedVersion);
        }
    }
}