using System.IO;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace NuGetGallery
{
    public interface IFileStorageService
    {
        Task<ActionResult> CreateDownloadFileActionResultAsync(string folderName, string fileName);

        Task DeleteFileAsync(string folderName, string fileName);

        Task<bool> FileExistsAsync(string folderName, string fileName);

        Task<Stream> GetFileAsync(string folderName, string fileName);

        Task SaveFileAsync(string folderName, string fileName, Stream packageFile);
    }
}