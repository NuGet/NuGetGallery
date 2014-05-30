using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public interface INuGetExeDownloaderService
    {
        Task<ActionResult> CreateNuGetExeDownloadActionResultAsync(Uri requestUrl);
    }
}