using System.IO;
using NuGet;

namespace NuGetGallery
{
    public interface IPackageUploadFileService
    {
        void DeleteUploadedFile(User user);
        
        ZipPackage GetUploadedFile(User user);
        
        void SaveUploadedFile(
            int userKey,
            Stream packageFileStream);
    }
}