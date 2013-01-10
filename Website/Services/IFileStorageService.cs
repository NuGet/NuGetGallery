using System.IO;
using System.Web.Mvc;

namespace NuGetGallery
{
    public interface IFileStorageService
    {
        ActionResult CreateDownloadFileActionResult(
            string folderName,
            string fileName);

        void DeleteFile(
            string folderName,
            string fileName);

        bool FileExists(
            string folderName,
            string fileName);

        /// <returns>the filestream, or null if the file does not exist.</returns>
        Stream GetFile(
            string folderName,
            string fileName);

        void SaveFile(
            string folderName,
            string fileName,
            Stream packageFile);
    }
}
