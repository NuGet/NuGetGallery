using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Configuration;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class NuGetExeDownloaderService : INuGetExeDownloaderService
    {
        private const int MaxNuGetExeFileSize = 10 * 1024 * 1024;
        private readonly IFileStorageService _fileStorageService;
        private readonly IPackageFileService _packageFileService;
        private readonly IPackageService _packageService;
        private readonly IAppConfiguration _configuration;

        public NuGetExeDownloaderService(
            IPackageService packageService,
            IPackageFileService packageFileService,
            IFileStorageService fileStorageService,
            IAppConfiguration configuration)
        {
            _packageService = packageService;
            _packageFileService = packageFileService;
            _fileStorageService = fileStorageService;
            _configuration = configuration;
        }

        public async Task<ActionResult> CreateNuGetExeDownloadActionResultAsync(Uri requestUrl)
        {
            await EnsureNuGetExe();
            var uriOrStream = _fileStorageService.GetDownloadUriOrStream(Constants.DownloadsFolderName, "nuget.exe");
            return GetDownloadResult(requestUrl, uriOrStream, Constants.OctetStreamContentType);
        }

        internal ActionResult GetDownloadResult(Uri requestUrl, UriOrStream uriOrStream, string contentType)
        {
            if (uriOrStream.Uri != null)
            {
                if (uriOrStream.Uri.IsFile)
                {
                    var ret = new FilePathResult(uriOrStream.Uri.LocalPath, contentType);
                    ret.FileDownloadName = new FileInfo(uriOrStream.Uri.LocalPath).Name;
                    return ret;
                }
                else
                {
                    return new RedirectResult(GetRedirectUri(requestUrl, uriOrStream.Uri));
                }
            }
            else if (uriOrStream.Stream != null)
            {
                return new FileStreamResult(uriOrStream.Stream, Constants.PackageContentType);
            }
            else
            {
                return new HttpNotFoundResult();
            }
        }

        internal string GetRedirectUri(Uri requestUrl, Uri blobUri)
        {
            string host = String.IsNullOrEmpty(_configuration.AzureCdnHost) ? blobUri.Host : _configuration.AzureCdnHost;
            var urlBuilder = new UriBuilder(requestUrl.Scheme, host)
            {
                Path = blobUri.LocalPath,
                Query = blobUri.Query
            };

            return urlBuilder.Uri.AbsoluteUri;
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
                return _fileStorageService.SaveFileAsync(Constants.DownloadsFolderName, "nuget.exe", nugetExeStream, Constants.OctetStreamContentType);
            }
        }
    }
}
