using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NuGet;

namespace NuGetGallery
{
    public class NuGetExeDownloaderService : INuGetExeDownloaderService
    {
        private static readonly TimeSpan _exeRefreshInterval = TimeSpan.FromDays(1);
        private static readonly Lazy<string> _defaultNuGetExePath = new Lazy<string>(() => Path.Combine(HttpRuntime.AppDomainAppPath, "App_Data", "NuGet.exe"));
        private static readonly object fileLock = new object();
        private readonly IPackageService packageSvc;
        private readonly IPackageFileService packageFileSvc;
        private readonly IFileSystemService fileSystem;
        private string _nugetExePath;

        public NuGetExeDownloaderService(
            IPackageService packageSvc,
            IPackageFileService packageFileSvc,
            IFileSystemService fileSystem)
        {
            this.packageSvc = packageSvc;
            this.packageFileSvc = packageFileSvc;
            this.fileSystem = fileSystem;
        }

        public string NuGetExePath
        {
            get { return _nugetExePath ?? _defaultNuGetExePath.Value; }
            set { _nugetExePath = value; }
        }

        public ActionResult CreateNuGetExeDownloadActionnResult()
        {
            EnsureNuGetExe();
            var result = new FilePathResult(NuGetExePath, Constants.OctetStreamContentType)
                         {
                             FileDownloadName = "NuGet.exe"
                         };
            return result;
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
            if (fileSystem.FileExists(NuGetExePath) && (DateTime.UtcNow - fileSystem.GetCreationTimeUtc(NuGetExePath)) < _exeRefreshInterval)
            {
                // Ensure the file exists and it is recent enough.
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

            using (Stream fileStream = fileSystem.OpenWrite(NuGetExePath),
                          packageFileStream = executable.GetStream())
            {
                packageFileStream.CopyTo(fileStream);
            }
        }
    }
}