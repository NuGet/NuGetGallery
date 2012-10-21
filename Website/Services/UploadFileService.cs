using System;
using System.Globalization;
using System.IO;

namespace NuGetGallery
{
    public class UploadFileService : IUploadFileService
    {
        private readonly IFileStorageService _fileStorageService;

        public UploadFileService(IFileStorageService fileStorageService)
        {
            _fileStorageService = fileStorageService;
        }

        public void DeleteUploadFile(int userKey)
        {
            if (userKey < 1)
            {
                throw new ArgumentException("A user key is required.", "userKey");
            }

            var uploadFileName = BuildFileName(userKey);

            _fileStorageService.DeleteFile(Constants.UploadsFolderName, uploadFileName);
        }

        public Stream GetUploadFile(int userKey)
        {
            if (userKey < 1)
            {
                throw new ArgumentException("A user key is required.", "userKey");
            }

            var uploadFileName = BuildFileName(userKey);

            return _fileStorageService.GetFile(Constants.UploadsFolderName, uploadFileName);
        }

        public void SaveUploadFile(
            int userKey,
            Stream packageFileStream)
        {
            if (userKey < 1)
            {
                throw new ArgumentException("A user key is required.", "userKey");
            }
            if (packageFileStream == null)
            {
                throw new ArgumentNullException("packageFileStream");
            }

            var uploadFileName = BuildFileName(userKey);

            _fileStorageService.SaveFile(Constants.UploadsFolderName, uploadFileName, packageFileStream);
        }

        private static string BuildFileName(int userKey)
        {
            return String.Format(CultureInfo.InvariantCulture, Constants.UploadFileNameTemplate, userKey, Constants.NuGetPackageFileExtension);
        }
    }
}