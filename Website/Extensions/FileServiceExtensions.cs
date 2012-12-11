using System.IO;
using System.Threading.Tasks;
using NuGet;

namespace NuGetGallery
{
    public static class FileServiceExtensions
    {
        public static Task SavePackageFileAsync(this IPackageFileService packageFileService, Package package, IPackage nugetPackage)
        {
            using (Stream stream = nugetPackage.GetStream())
            {
                return packageFileService.SavePackageFileAsync(package, stream);
            }
        }

        public static Task SaveUploadFileAsync(this IUploadFileService uploadFileService, int userKey, IPackage nugetPackage)
        {
            using (var stream = nugetPackage.GetStream())
            {
                return uploadFileService.SaveUploadFileAsync(userKey, stream);
            }
        }
    }
}