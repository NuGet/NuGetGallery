using System.IO;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IUploadFileService
    {
        Task DeleteUploadFileAsync(int userKey);

        Task<Stream> GetUploadFileAsync(int userKey);

        Task SaveUploadFileAsync(int userKey, Stream packageFileStream);
    }
}