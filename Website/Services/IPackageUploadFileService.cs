using NuGet;

namespace NuGetGallery
{
    public interface IPackageUploadFileService
    {
        void DeleteUploadedFile(User user);
        ZipPackage GetUploadedFile(User user);
        void SaveUploadedFile(User user, IPackageMetadata package);
    }
}