using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet;
using NuGetGallery.Filters;
using NuGetGallery.Helpers;
using NuGetGallery.ViewModels.PackagePart;

namespace NuGetGallery.Controllers
{
    public partial class PackageFilesController : Controller
    {
        private const long MaximumAllowedPackageFileSize = 3L * 1024 * 1024;		// maximum package size = 3MB
        private const int MaximumPackageContentFileSize = 16 * 1024;                // maximum package content file to return before trimming = 16K

        private readonly IPackageService _packageSvc;
        private readonly IPackageFileService _packageFileSvc;
        private readonly ICacheService _cacheSvc;

        public PackageFilesController(IPackageService packageSvc, IPackageFileService packageFileSvc, ICacheService cacheSvc)
        {
            _packageSvc = packageSvc;
            _packageFileSvc = packageFileSvc;
            _cacheSvc = cacheSvc;
        }

        public async Task<ActionResult> Contents(string id, string version)
        {
            Package package = _packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            if (package.PackageFileSize > MaximumAllowedPackageFileSize)
            {
                return View("PackageTooBig");
            }

            IPackage packageFile = await NuGetGallery.Helpers.PackageHelper.GetPackageFromCacheOrDownloadIt(package, _cacheSvc, _packageFileSvc);

            PackageItem rootFolder = PathToTreeConverter.Convert(packageFile.GetFiles());
            var viewModel = new PackageContentsViewModel(packageFile, rootFolder);
            return View(viewModel);
        }

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
                return new ImageResult(packageFile.GetStream(), FileHelper.GetImageMimeType(packageFile.Path));
            }

            var result = new ContentResult
            {
                ContentEncoding = System.Text.Encoding.UTF8,
                ContentType = "text/plain"
            };

            if (FileHelper.IsBinaryFile(packageFile.Path))
            {
                result.Content = "The requested file is a binary file.";
            }
            else
            {
                using (var stream = packageFile.GetStream())
                {
                    result.Content = FileHelper.ReadTextTruncated(stream, maxLength: MaximumPackageContentFileSize);
                }
            }

            return result;
        }

        [ActionName("Download")]
        public async Task<ActionResult> DownloadFileContent(string id, string version, string filePath)
        {
            IPackageFile packageFile = await GetPackageFile(id, version, filePath);
            if (packageFile == null)
            {
                return HttpNotFound();
            }

            return File(packageFile.GetStream(), "application/octet-stream", Path.GetFileName(packageFile.Path));
        }

        private async Task<IPackageFile> GetPackageFile(string id, string version, string filePath)
        {
            if (String.IsNullOrEmpty(filePath))
            {
                return null;
            }

            Package package = _packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null || package.PackageFileSize > MaximumAllowedPackageFileSize)
            {
                return null;
            }

            filePath = filePath.Replace('/', Path.DirectorySeparatorChar);

            IPackage packageWithContents = await NuGetGallery.Helpers.PackageHelper.GetPackageFromCacheOrDownloadIt(package, _cacheSvc, _packageFileSvc);

            IPackageFile packageFile = packageWithContents.GetFiles()
                                                          .FirstOrDefault(p => p.Path.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            return packageFile;
        }
    }
}
