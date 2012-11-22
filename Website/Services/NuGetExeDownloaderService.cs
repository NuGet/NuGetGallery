using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet;

namespace NuGetGallery
{
    public class NuGetExeDownloaderService : INuGetExeDownloaderService
    {
        private static readonly object fileLock = new object();
        private readonly IFileStorageService _fileStorageSvc;
        private readonly IPackageFileService _packageFileSvc;
        private readonly IPackageService _packageSvc;

        public NuGetExeDownloaderService(
            IPackageService packageSvc,
            IPackageFileService packageFileSvc,
            IFileStorageService fileStorageSvc)
        {
            _packageSvc = packageSvc;
            _packageFileSvc = packageFileSvc;
            _fileStorageSvc = fileStorageSvc;
        }

        public async Task<ActionResult> CreateNuGetExeDownloadActionResultAsync()
        {
            await EnsureNuGetExe();
            return await _fileStorageSvc.CreateDownloadFileActionResultAsync(Constants.DownloadsFolderName, "nuget.exe");
        }

        public Task UpdateExecutableAsync(IPackage zipPackage)
        {
            return ExtractNuGetExe(zipPackage);
        }

        private async Task EnsureNuGetExe()
        {
            if (await _fileStorageSvc.FileExistsAsync(Constants.DownloadsFolderName, "nuget.exe"))
            {
                // Ensure the file exists on blob storage.
                return;
            }

            var package = _packageSvc.FindPackageByIdAndVersion("NuGet.CommandLine", version: null, allowPrerelease: false);
            if (package == null)
            {
                throw new InvalidOperationException("Unable to find NuGet.CommandLine.");
            }

            using (Stream packageStream = await _packageFileSvc.DownloadPackageFileAsync(package))
            {
                var zipPackage = new ZipPackage(packageStream);
                await ExtractNuGetExe(zipPackage);
            }
        }

        private Task ExtractNuGetExe(IPackage package)
        {
            lock (fileLock)
            {
                var executable = package.GetFiles("tools")
                                        .First(f => f.Path.Equals(@"tools\NuGet.exe", StringComparison.OrdinalIgnoreCase));

                using (Stream packageFileStream = executable.GetStream())
                {
                    return _fileStorageSvc.SaveFileAsync(Constants.DownloadsFolderName, "nuget.exe", packageFileStream);
                }
            }
        }
    }
}