using System.IO;
using System.Web.Mvc;

namespace NuGetGallery
{
    public interface IPackageFileService
    {
        ActionResult CreateDownloadPackageActionResult(Package package);
        void DeletePackageFile(string id, string version);
        void SavePackageFile(Package package, Stream packageFile);
    }
}