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

        static string BuildFileName(int userKey)
        {
            return string.Format(Const.UploadFileNameTemplate, userKey, Const.PackageFileExtension);
        }

        public void DeleteUploadFile(int userKey)
        {
            if (userKey < 1)
                throw new ArgumentException("A user key is required.", "userKey");

            var uploadFileName = BuildFileName(userKey);

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

            var uploadFileName = BuildFileName(userKey);

            fileStorageService.SaveFile(Const.UploadsFolderName, uploadFileName, uploadFileStream);
        }
    }
}