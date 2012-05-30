using System;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using NuGet;

namespace NuGetGallery
{
    public class NuGetExeDownloaderService : INuGetExeDownloaderService
    {
        private static readonly object fileLock = new object();
        private readonly IPackageService packageSvc;
        private readonly IPackageFileService packageFileSvc;
        private readonly IFileStorageService fileStorageSvc;

        public NuGetExeDownloaderService(
            IPackageService packageSvc,
            IPackageFileService packageFileSvc,
            IFileStorageService fileStorageSvc)
        {
            this.packageSvc = packageSvc;
            this.packageFileSvc = packageFileSvc;
            this.fileStorageSvc = fileStorageSvc;
        }

        public ActionResult CreateNuGetExeDownloadActionResult()
        {
            EnsureNuGetExe();
            return fileStorageSvc.CreateDownloadFileActionResult(Constants.DownloadsFolderName, "nuget.exe");
        }

        public void UpdateExecutable(IPackage zipPackage)
        {
            lock (fileLock)
            {
                ExtractNuGetExe(zipPackage);
            }
        }

        private void EnsureNuGetExe()
        {
            if (fileStorageSvc.FileExists(Constants.DownloadsFolderName, "nuget.exe"))
            {
                // Ensure the file exists on blob storage.
                return;
            }

            lock (fileLock)
            {
                var package = packageSvc.FindPackageByIdAndVersion("NuGet.CommandLine", version: null, allowPrerelease: false);
                if (package == null)
                {
                    throw new InvalidOperationException("Unable to find NuGet.CommandLine.");
                }

                using (var packageStream = packageFileSvc.DownloadPackageFile(package))
                {
                    var zipPackage = new ZipPackage(packageStream);
                    ExtractNuGetExe(zipPackage);
                }
            }
        }

        private void ExtractNuGetExe(IPackage package)
        {
            var executable = package.GetFiles("tools")
                                       .First(f => f.Path.Equals(@"tools\NuGet.exe", StringComparison.OrdinalIgnoreCase));

            using (Stream packageFileStream = executable.GetStream())
            {
                fileStorageSvc.SaveFile(Constants.DownloadsFolderName, "nuget.exe", packageFileStream);
            }
        }
    }
}