using System.IO;
using NuGet;

namespace NuGetGallery
{
    public interface IUploadFileService
    {
        void DeleteUploadFile(int userKey);
        
        Stream GetUploadFile(int userKey);
        
        void SaveUploadFile(
            int userKey,
            Stream packageFileStream);
    }
}