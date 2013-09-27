using System;
using System.IO;
using System.Threading.Tasks;
using NuGetGallery;

namespace NuGetGallery
{
    public interface IFileStorageService
    {
        /// <summary>
        /// Returns an Uri to a file, or the file stream itself - but preferably just the Uri.
        /// </summary>
        /// <remarks>
        /// May return an Uri to a resource which does not actually exist (user requests will yield 404 etc).
        /// May also return UriOrStream with Uri == null and Stream == Null if the file definitely does not exist.
        /// </remarks>
        UriOrStream GetDownloadUriOrStream(string folderName, string fileName);

        Task DeleteFileAsync(string folderName, string fileName);

        Task<bool> FileExistsAsync(string folderName, string fileName);

        Task<Stream> GetFileAsync(string folderName, string fileName);

        /// <summary>
        /// Gets a reference to a file in the storage service, which can be used to open the full file data.
        /// The file is verified to exist and be different to (newer than?) the ifNoneMatch parameter, if it is supplied.
        /// </summary>
        /// <param name="folderName">The folder containing the file to open</param>
        /// <param name="fileName">The file within that folder to open</param>
        /// <param name="ifNoneMatch">The <see cref="IFileReference.ContentId"/> value to use in an If-None-Match request</param>
        /// <returns>A <see cref="IFileReference"/> representing the file reference</returns>
        Task<IFileReference> GetFileReferenceAsync(string folderName, string fileName, string ifNoneMatch = null);

        Task SaveFileAsync(string folderName, string fileName, Stream packageFile, string contentType);

        Task DownloadToFileAsync(string folderName, string fileName, string downloadedPackageFilePath);

        Task UploadFromFileAsync(string folderName, string fileName, string path, string contentType);
    }
}
