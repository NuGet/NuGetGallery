using System;
using System.IO;

namespace NuGetGallery
{
    public class PackageUploadFileService : IPackageUploadFileService
    {
        readonly IFileStorageService fileStorageService;
        
        public PackageUploadFileService(IFileStorageService fileStorageService)
        {
            this.fileStorageService = fileStorageService;
        }
        
        public void DeleteUploadedFile(User user)
        {
            throw new Exception();
        }

        public NuGet.ZipPackage GetUploadedFile(User user)
        {
            throw new Exception();
        }

        public void SaveUploadedFile(
            int userKey,
            Stream packageFileStream)
        {
            if (userKey < 1)
                throw new ArgumentException("A user key is required.", "userKey");
            if (packageFileStream == null)
                throw new ArgumentNullException("packageFileStream");

            var packageUploadFileName = string.Format(Const.PackageUploadFileNameTemplate, userKey, Const.PackageFileExtension);

            fileStorageService.SaveFile(Const.PackageUploadsFolderName, packageUploadFileName, packageFileStream);
        }
    }
}