using System.IO;
using System.Web.Mvc;

namespace NuGetGallery
{
    public interface IPackageFileService
    {
        /// <summary>
        /// Creates an ActionResult that allows a third-party client to download the nupkg for the package.
        /// </summary>
        ActionResult CreateDownloadPackageActionResult(Package package);

        /// <summary>
        /// Deletes the nupkg from the file storage.
        /// </summary>
        void DeletePackageFile(string id, string version);

        /// <summary>
        /// Saves the contents of the package represented by the stream into the file storage.
        /// </summary>
        void SavePackageFile(Package package, Stream packageFile);
        
        /// <summary>
        /// Downloads the package from the file storage and reads it into a Stream.
        /// </summary>
        Stream DownloadPackageFile(Package package);
    }
}