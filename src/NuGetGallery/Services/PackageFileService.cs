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

        public Task BeginCopyPackageFileToHashedAsync(string id, string normalizedVersion, string hash)
        {
            var fileName1 = BuildFileName(id, normalizedVersion);
            var fileName2 = BuildFileName(id, normalizedVersion, hash);

            return _fileStorageService.BeginCopyAsync(
                Constants.PackagesFolderName, fileName1, 
                Constants.PackagesFolderName, fileName2);
        }

        public Task EndCopyPackageFileToHashedAsync(string id, string normalizedVersion, string hash)
        {
            var fileName2 = BuildFileName(id, normalizedVersion, hash);
            return _fileStorageService.WaitForCopyCompleteAsync(
                Constants.PackagesFolderName, fileName2);
        }

        public UriOrStream GetDownloadUriOrStream(Package package)
        {
            var fileName = BuildFileName(package.PackageRegistration.Id, package.Version);
            return _fileStorageService.GetDownloadUriOrStream(Constants.PackagesFolderName, fileName);
        }

        public UriOrStream GetDownloadUriOrStream(string id, string version)
        {
            var fileName = BuildFileName(id, version);
            return _fileStorageService.GetDownloadUriOrStream(Constants.PackagesFolderName, fileName);
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
            return SavePackageFileAsync(package.PackageRegistration.Id, package.Version, packageFile);
        }

        public Task SavePackageFileAsync(string packageId, string version, Stream packageFile)
        {
            var fileName = BuildFileName(packageId, version);
            return _fileStorageService.SaveFileAsync(Constants.PackagesFolderName, fileName, packageFile, Constants.PackageContentType);
        }

        public Task UploadFromFileAsync(string packageId, string version, string path)
        {
            var fileName = BuildFileName(packageId, version);
            return _fileStorageService.UploadFromFileAsync(Constants.PackagesFolderName, fileName, path, Constants.PackageContentType);
        }

        public Task<Stream> DownloadPackageFileAsync(Package package)
        {
            return DownloadPackageFileAsync(package.PackageRegistration.Id, package.Version);
        }

        public async Task<Stream> DownloadPackageFileAsync(string packageId, string version)
        {
            var fileName = BuildFileName(packageId, version);
            return (await _fileStorageService.GetFileAsync(Constants.PackagesFolderName, fileName));
        }

        public Task DownloadToFileAsync(string packageId, string version, string downloadedPackageFilePath)
        {
            var fileName = BuildFileName(packageId, version);
            return _fileStorageService.DownloadToFileAsync(Constants.PackagesFolderName, fileName, downloadedPackageFilePath);
        }

        public bool PackageFileExists(string packageId, string version)
        {
            var fileName = BuildFileName(packageId, version);
            return _fileStorageService.FileExistsAsync(Constants.PackagesFolderName, fileName).Result;
        }

        public bool PackageFileExists(string packageId, string normalizedVersion, string hash)
        {
            var fileName = BuildFileName(packageId, normalizedVersion, hash);
            return _fileStorageService.FileExistsAsync(Constants.PackagesFolderName, fileName).Result;
        }

        private static string BuildFileName(string id, string version)
        {
            return FileConventions.GetPackageFileName(id, version);
        }

        private static string BuildFileName(string id, string version, string hash)
        {
            return FileConventions.GetPackageFileNameHash(id, version, hash);
        }
    }
}