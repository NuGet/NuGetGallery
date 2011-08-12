using System.IO;
using System.Web.Mvc;

namespace NuGetGallery {
    public interface IPackageFileService {
        void SavePackageFile(Package package, Stream packageFile);
        ActionResult CreateDownloadPackageResult(Package package);
    }
}