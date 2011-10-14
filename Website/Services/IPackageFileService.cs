using System.IO;
using System.Web.Mvc;

namespace NuGetGallery
{
    public interface IPackageFileService
    {
        void DeletePackageFile(string id, string version);
        void SavePackageFile(Package package, Stream packageFile);
        ActionResult CreateDownloadPackageResult(Package package);
    }
}