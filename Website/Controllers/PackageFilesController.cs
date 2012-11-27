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
    public partial class PackageFilesController : AsyncController
    {
        private const long MaximumAllowedPackageFileSize = 3L * 1024 * 1024;		// maximum package size = 3MB

        private readonly IPackageService packageSvc;
        private readonly IPackageFileService packageFileSvc;
        private readonly ICacheService cacheSvc;

        public PackageFilesController(IPackageService packageSvc, IPackageFileService packageFileSvc, ICacheService cacheSvc)
        {
            this.packageSvc = packageSvc;
            this.packageFileSvc = packageFileSvc;
            this.cacheSvc = cacheSvc;
        }

        public void ContentsAsync(string id, string version)
        {
            Package package = packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                AsyncManager.Parameters["packageFile"] = null;
                return;
            }

            if (package.PackageFileSize > MaximumAllowedPackageFileSize)
            {
                AsyncManager.Parameters["fileTooBig"] = true;
                return;
            }

            AsyncManager.OutstandingOperations.Increment();
            Task<IPackage> downloadTask = NuGetGallery.Helpers.PackageHelper.GetPackageFromCacheOrDownloadIt(package, cacheSvc, packageFileSvc);
            downloadTask.ContinueWith(
                task =>
                {
                    if (!task.IsFaulted && !task.IsCanceled)
                    {
                        AsyncManager.Parameters["packageFile"] = task.Result;
                    }

                    AsyncManager.OutstandingOperations.Decrement();
                });
        }

        public ActionResult ContentsCompleted(bool fileTooBig, IPackage packageFile)
        {
            if (fileTooBig)
            {
                return View("PackageTooBig");
            }

            if (packageFile == null)
            {
                return HttpNotFound();
            }

            PackageItem rootFolder = PathToTreeConverter.Convert(packageFile.GetFiles());
            var viewModel = new PackageContentsViewModel(packageFile, rootFolder);
            return View(viewModel);
        }

        [ActionName("View")]
        [CompressFilter]
        [CacheFilter(Duration = 60 * 60 * 24)]      // cache one day
        public void ShowFileContentAsync(string id, string version, string filePath)
        {
            AsyncManager.OutstandingOperations.Increment();
            TryGetPackageFile(id, version, filePath).ContinueWith(
                task =>
                {
                    if (!task.IsFaulted && !task.IsCanceled)
                    {
                        AsyncManager.Parameters["packageFile"] = task.Result;
                    }

                    AsyncManager.OutstandingOperations.Decrement();
                });
        }

        public ActionResult ShowFileContentCompleted(IPackageFile packageFile)
        {
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
                    result.Content = FileHelper.ReadTextTruncated(stream, maxLength: 8 * 1024);     // read maximum 8K
                }
            }

            return result;
        }

        [ActionName("Download")]
        public void DownloadFileContentAsync(string id, string version, string filePath)
        {
            AsyncManager.OutstandingOperations.Increment();
            TryGetPackageFile(id, version, filePath).ContinueWith(
                task =>
                {
                    if (!task.IsFaulted && !task.IsCanceled)
                    {
                        AsyncManager.Parameters["packageFile"] = task.Result;
                    }

                    AsyncManager.OutstandingOperations.Decrement();
                });
        }

        public ActionResult DownloadFileContentCompleted(IPackageFile packageFile)
        {
            if (packageFile == null) 
            {
                return HttpNotFound();
            }
            else 
            {
                return File(packageFile.GetStream(), "application/octet-stream", Path.GetFileName(packageFile.Path));
            }
        }

        private async Task<IPackageFile> TryGetPackageFile(string id, string version, string filePath)
        {
            if (String.IsNullOrEmpty(filePath))
            {
                return null;
            }

            Package package = packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null || package.PackageFileSize > MaximumAllowedPackageFileSize)
            {
                return null;
            }

            filePath = filePath.Replace('/', Path.DirectorySeparatorChar);

            IPackage packageWithContents = await NuGetGallery.Helpers.PackageHelper.GetPackageFromCacheOrDownloadIt(package, cacheSvc, packageFileSvc);

            IPackageFile packageFile = packageWithContents.GetFiles()
                                                          .FirstOrDefault(p => p.Path.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            return packageFile;
        }
    }
}