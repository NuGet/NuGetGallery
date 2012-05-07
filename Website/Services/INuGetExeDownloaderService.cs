using System.Web.Mvc;
using NuGet;

namespace NuGetGallery
{
    public interface INuGetExeDownloaderService
    {
        ActionResult CreateNuGetExeDownloadActionnResult();

        void UpdateExecutable(IPackage package);
    }
}