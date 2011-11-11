using System;
using System.IO;

namespace NuGetGallery
{
    public class UploadFileService : IUploadFileService
    {
        readonly IFileStorageService fileStorageService;
        
        public UploadFileService(IFileStorageService fileStorageService)
        {
            this.fileStorageService = fileStorageService;
        }
        
        public void DeleteUploadFile(int userKey)
        {
            if (userKey < 1)
                throw new ArgumentException("A user key is required.", "userKey");

            var uploadFileName = string.Format(Const.UploadFileNameTemplate, userKey, Const.PackageFileExtension);

            fileStorageService.DeleteFile(Const.UploadsFolderName, uploadFileName);
        }

        public NuGet.ZipPackage GetUploadFile(User user)
        {
            throw new Exception();
        }

        public void SaveUploadFile(
            int userKey,
            Stream uploadFileStream)
        {
            if (userKey < 1)
                throw new ArgumentException("A user key is required.", "userKey");
            if (uploadFileStream == null)
                throw new ArgumentNullException("packageFileStream");

            var packageUploadFileName = string.Format(Const.UploadFileNameTemplate, userKey, Const.PackageFileExtension);

            fileStorageService.SaveFile(Const.UploadsFolderName, packageUploadFileName, uploadFileStream);
        }
    }
}