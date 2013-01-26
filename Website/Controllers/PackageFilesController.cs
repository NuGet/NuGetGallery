using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet;
using NuGetGallery.Filters;
using NuGetGallery.Helpers;
using NuGetGallery.ViewModels.PackagePart;

namespace NuGetGallery
{
    public partial class PackageFilesController : Controller
    {
        private const long MaximumAllowedPackageFileSize = 3L * 1024 * 1024;		// maximum package size = 3MB
        private const int MaximumPackageContentFileSize = 100 * 1024;               // maximum package content file to return before trimming = 100K
        private const int MaximumImageFileSize = 2 * 1204 * 1024;                   // maximum image size = 2MB

        private readonly IPackageService _packageService;
        private readonly IPackageFileService _packageFileService;
        private readonly IPackageCacheService _cacheService;

        public PackageFilesController(IPackageService packageService, IPackageFileService packageFileService, IPackageCacheService cacheService)
        {
            _packageService = packageService;
            _packageFileService = packageFileService;
            _cacheService = cacheService;
        }

        [RequireRemoteHttps]
        public async Task<ActionResult> Contents(string id, string version)
        {
            Package package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            if (!String.IsNullOrEmpty(package.ExternalPackageUrl))
            {
                return View("ExternalPackage", package);
            }

            if (package.PackageFileSize > MaximumAllowedPackageFileSize)
            {
                return View("PackageTooBig", package);
            }

            IPackage packageFile = await NuGetGallery.Helpers.PackageHelper.GetPackageFromCacheOrDownloadIt(package, _cacheService, _packageFileService);

            PackageItem rootFolder = PathToTreeConverter.Convert(packageFile.GetFiles());
            var viewModel = new PackageContentsViewModel(packageFile, package.PackageRegistration.Owners, rootFolder);
            return View(viewModel);
        }

        [RequireRemoteHttps]
        [ActionName("View")]
        [CompressFilter]
        [CacheFilter(Duration = 60 * 60 * 24)]      // cache one day
        public async Task<ActionResult> ShowFileContent(string id, string version, string filePath)
        {
            IPackageFile packageFile = await GetPackageFile(id, version, filePath);
            if (packageFile == null) 
            {
                return HttpNotFound();
            }

            // treat image files specially
            if (FileHelper.IsImageFile(packageFile.Path))
            {
                // don't allow image file bigger than 2MB
                Stream imageStream = packageFile.GetStream();
                if (imageStream.Length <= MaximumImageFileSize)
                {
                    return new ImageResult(imageStream, FileHelper.GetImageMimeType(packageFile.Path));
                }
                else
                {
                    imageStream.Close();
                    return HttpNotFound();
                }
            }

            var result = new ContentResult
            {
                ContentEncoding = System.Text.Encoding.UTF8,
                ContentType = "text/plain"
            };

            if (FileHelper.IsBinaryFile(packageFile.Path))
            {
                result.Content = "*** The requested file is a binary file. ***";
            }
            else
            {
                using (var stream = packageFile.GetStream())
                {
                    result.Content = FileHelper.ReadTextTruncated(stream, maxLength: MaximumPackageContentFileSize);
                }
            }

            HttpContext.Response.AddHeader("X-Content-Type-Options", "nosniff");
            return result;
        }

        private async Task<IPackageFile> GetPackageFile(string id, string version, string filePath)
        {
            if (String.IsNullOrEmpty(filePath))
            {
                return null;
            }

            Package package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null || package.PackageFileSize > MaximumAllowedPackageFileSize)
            {
                return null;
            }

            filePath = filePath.Replace('/', Path.DirectorySeparatorChar);

            IPackage packageWithContents = await NuGetGallery.Helpers.PackageHelper.GetPackageFromCacheOrDownloadIt(package, _cacheService, _packageFileService);

            IPackageFile packageFile = packageWithContents.GetFiles()
                                                          .FirstOrDefault(p => p.Path.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            return packageFile;
        }
    }
}