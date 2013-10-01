using System;
using System.IO;
using System.Threading.Tasks;

namespace NuGetGallery
{
    /// <remarks>
    /// This interace is intended to be used from ops tasks only
    /// </remarks>
    public interface IOpsPackageFileService : IPackageFileService
    {
        /// <summary>
        /// Saves the contents of the package represented by the stream into the file storage.
        /// </summary>
        Task SavePackageFileAsync(string id, string normalizedVersion, Stream packageFile);

        /// <summary>
        /// Saves the contents of the package represented by the stream into the file storage.
        /// </summary>
        Task SavePackageFileAsync(string packageId, string normalizedVersion, string hash, Stream packageFile);

        bool PackageFileExists(string packageId, string normalizedVersion);

        bool PackageFileExists(string packageId, string normalizedVersion, string hash);

        Task BeginCopyPackageFileToHashedAsync(string packageId, string normalizedVersion, string hash);
        Task EndCopyPackageFileToHashedAsync(string packageId, string normalizedVersion, string hash);

        Task DownloadToFileAsync(string packageId, string normalizedVersion, string downloadedPackageFilePath);
        Task UploadFromFileAsync(string packageId, string normalizedVersion, string uploadedPackageFilePath);
    }
}