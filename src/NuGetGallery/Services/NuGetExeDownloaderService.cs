using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class NuGetExeDownloaderService : INuGetExeDownloaderService
    {
        private const int MaxNuGetExeFileSize = 10 * 1024 * 1024;
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

        public async Task<ActionResult> CreateNuGetExeDownloadActionResultAsync(Uri requestUrl)
        {
            await EnsureNuGetExe();
            return await _fileStorageService.CreateDownloadFileActionResultAsync(requestUrl, Constants.DownloadsFolderName, "nuget.exe");
        }

        public Task UpdateExecutableAsync(INupkg nupkg)
        {
            return ExtractNuGetExe(nupkg);
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
                var nupkg = new Nupkg(packageStream, leaveOpen: true);
                await ExtractNuGetExe(nupkg);
            }
        }

        private Task ExtractNuGetExe(INupkg package)
        {
            using (Stream nugetExeStream = package.GetSizeVerifiedFileStream(@"tools\NuGet.exe", MaxNuGetExeFileSize))
            {
                return _fileStorageService.SaveFileAsync(Constants.DownloadsFolderName, "nuget.exe", nugetExeStream);
            }
        }
    }
}
