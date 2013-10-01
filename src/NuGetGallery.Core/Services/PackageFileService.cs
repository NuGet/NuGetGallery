using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class PackageFileService : IPackageFileService, IOpsPackageFileService
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
                CoreConstants.PackagesFolderName, fileName1, 
                CoreConstants.PackagesFolderName, fileName2);
        }

        public Task EndCopyPackageFileToHashedAsync(string id, string normalizedVersion, string hash)
        {
            var fileName2 = BuildFileName(id, normalizedVersion, hash);
            return _fileStorageService.WaitForCopyCompleteAsync(
                CoreConstants.PackagesFolderName, fileName2);
        }

        public UriOrStream GetDownloadUriOrStream(Package package)
        {
            var fileName = BuildFileName(package.PackageRegistration.Id, package.Version);
            return _fileStorageService.GetDownloadUriOrStream(CoreConstants.PackagesFolderName, fileName);
        }

        public UriOrStream GetDownloadUriOrStream(string id, string version)
        {
            var fileName = BuildFileName(id, version);
            return _fileStorageService.GetDownloadUriOrStream(CoreConstants.PackagesFolderName, fileName);
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
            return _fileStorageService.DeleteFileAsync(CoreConstants.PackagesFolderName, fileName);
        }

        public async Task SavePackageFileAsync(Package package, Stream packageFile)
        {
            await SavePackageFileAsync(package.PackageRegistration.Id, package.GetNormalizedVersion(), packageFile);
            packageFile.Seek(0, SeekOrigin.Begin);
            await SavePackageFileAsync(package.PackageRegistration.Id, package.GetNormalizedVersion(), package.Hash, packageFile);
        }

        public Task SavePackageFileAsync(string packageId, string version, Stream packageFile)
        {
            var fileName = BuildFileName(packageId, version);
            return _fileStorageService.SaveFileAsync(CoreConstants.PackagesFolderName, fileName, packageFile, CoreConstants.PackageContentType);
        }

        public Task SavePackageFileAsync(string packageId, string version, string hash, Stream packageFile)
        {
            var fileName = BuildFileName(packageId, version, hash);
            return _fileStorageService.SaveFileAsync(CoreConstants.PackagesFolderName, fileName, packageFile, CoreConstants.PackageContentType);
        }

        public Task UploadFromFileAsync(string packageId, string version, string path)
        {
            var fileName = BuildFileName(packageId, version);
            return _fileStorageService.UploadFromFileAsync(CoreConstants.PackagesFolderName, fileName, path, CoreConstants.PackageContentType);
        }

        public Task<Stream> DownloadPackageFileAsync(Package package)
        {
            return DownloadPackageFileAsync(package.PackageRegistration.Id, package.Version);
        }

        public async Task<Stream> DownloadPackageFileAsync(string packageId, string version)
        {
            var fileName = BuildFileName(packageId, version);
            return (await _fileStorageService.GetFileAsync(CoreConstants.PackagesFolderName, fileName));
        }

        public Task DownloadToFileAsync(string packageId, string version, string downloadedPackageFilePath)
        {
            var fileName = BuildFileName(packageId, version);
            return _fileStorageService.DownloadToFileAsync(CoreConstants.PackagesFolderName, fileName, downloadedPackageFilePath);
        }

        public bool PackageFileExists(string packageId, string version)
        {
            var fileName = BuildFileName(packageId, version);
            return _fileStorageService.FileExistsAsync(CoreConstants.PackagesFolderName, fileName).Result;
        }

        public bool PackageFileExists(string packageId, string normalizedVersion, string hash)
        {
            var fileName = BuildFileName(packageId, normalizedVersion, hash);
            return _fileStorageService.FileExistsAsync(CoreConstants.PackagesFolderName, fileName).Result;
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