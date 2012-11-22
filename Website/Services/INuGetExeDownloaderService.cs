using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet;

namespace NuGetGallery
{
    public interface INuGetExeDownloaderService
    {
        Task<ActionResult> CreateNuGetExeDownloadActionResultAsync();

        Task UpdateExecutableAsync(IPackage package);
    }
}