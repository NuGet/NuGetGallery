using System.IO;
using NuGet;

namespace NuGetGallery
{
    public interface IUploadFileService
    {
        void DeleteUploadFile(int userKey);
        
        ZipPackage GetUploadFile(User user);
        
        void SaveUploadFile(
            int userKey,
            Stream packageFileStream);
    }
}