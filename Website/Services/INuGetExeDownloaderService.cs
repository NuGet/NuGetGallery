using System.Web.Mvc;
using NuGet;

namespace NuGetGallery
{
    public interface INuGetExeDownloaderService
    {
        ActionResult CreateNuGetExeDownloadActionResult();

        void UpdateExecutable(IPackage package);
    }
}