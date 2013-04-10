using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace NuGetGallery
{
    public interface IPackageFileService
    {
        /// <summary>
        ///     Creates an ActionResult that allows a third-party client to download the nupkg for the packageRegistration.
        /// </summary>
        Task<ActionResult> CreateDownloadPackageActionResultAsync(Uri requestUrl, Package package);

        /// <summary>
        ///     Creates an ActionResult that allows a third-party client to download the nupkg for the packageRegistration.
        /// </summary>
        Task<ActionResult> CreateDownloadPackageActionResultAsync(Uri requestUrl, string unsafeId, string unsafeVersion);

        /// <summary>
        ///     Deletes the nupkg from the file storage.
        /// </summary>
        Task DeletePackageFileAsync(string id, string version);

        /// <summary>
        ///     Saves the contents of the packageRegistration represented by the stream into the file storage.
        /// </summary>
        Task SavePackageFileAsync(Package package, Stream packageFile);

        /// <summary>
        ///     Downloads the packageRegistration from the file storage and reads it into a Stream asynchronously.
        /// </summary>
        Task<Stream> DownloadPackageFileAsync(Package packge);
    }
}