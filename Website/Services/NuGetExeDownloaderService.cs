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
        private readonly IFileStorageService _fileStorageService;
        private readonly IPackageFileService _packageFileService;
        private readonly IPackageService _packageService;

        public NuGetExeDownloaderService(
            IPackageService packageService,
            IPackageFileService packageFileService,
            IFileStorageService fileStorageService)
        {
            _packageService = packageService;
            _packageFileService = packageFileService;
            _fileStorageService = fileStorageService;
        }

        public async Task<ActionResult> CreateNuGetExeDownloadActionResultAsync()
        {
            await EnsureNuGetExe();
            return await _fileStorageService.CreateDownloadFileActionResultAsync(Constants.DownloadsFolderName, "nuget.exe");
        }

        public Task UpdateExecutableAsync(IPackage zipPackage)
        {
            return ExtractNuGetExe(zipPackage);
        }

        private async Task EnsureNuGetExe()
        {
            if (await _fileStorageService.FileExistsAsync(Constants.DownloadsFolderName, "nuget.exe"))
            {
                // Ensure the file exists on blob storage.
                return;
            }

            var package = _packageService.FindPackageByIdAndVersion("NuGet.CommandLine", version: null, allowPrerelease: false);
            if (package == null)
            {
                throw new InvalidOperationException("Unable to find NuGet.CommandLine.");
            }

            using (Stream packageStream = await _packageFileService.DownloadPackageFileAsync(package))
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
                    return _fileStorageService.SaveFileAsync(Constants.DownloadsFolderName, "nuget.exe", packageFileStream);
                }
            }
        }
    }
}