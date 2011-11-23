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
            return String.Format(Const.UploadFileNameTemplate, userKey, Const.NuGetPackageFileExtension);
        }

        public void DeleteUploadFile(int userKey)
        {
            if (userKey < 1)
                throw new ArgumentException("A user key is required.", "userKey");

            var uploadFileName = BuildFileName(userKey);

            fileStorageService.DeleteFile(Const.UploadsFolderName, uploadFileName);
        }

        public Stream GetUploadFile(int userKey)
        {
            if (userKey < 1)
                throw new ArgumentException("A user key is required.", "userKey");

            var uploadFileName = BuildFileName(userKey);

            return fileStorageService.GetFile(Const.UploadsFolderName, uploadFileName);
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