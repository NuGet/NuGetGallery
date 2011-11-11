using NuGet;
using System.IO;

namespace NuGetGallery
{
    public interface IPackageUploadFileService
    {
        void DeleteUploadedFile(User user);
        
        ZipPackage GetUploadedFile(User user);
        
        void SaveUploadedFile(
            int userKey,
            string packageId,
            string packageVersion,
            Stream packageFileStream);
    }
}