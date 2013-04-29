using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class UploadFileService : IUploadFileService
    {
        private readonly IFileStorageService _fileStorageService;

        public UploadFileService(IFileStorageService fileStorageService)
        {
            _fileStorageService = fileStorageService;
        }

        public Task DeleteUploadFileAsync(int userKey)
        {
            if (userKey < 1)
            {
                throw new ArgumentException("A user key is required.", "userKey");
            }

            var uploadFileName = BuildFileName(userKey);

            return _fileStorageService.DeleteFileAsync(Constants.UploadsFolderName, uploadFileName);
        }

        public Task<Stream> GetUploadFileAsync(int userKey)
        {
            if (userKey < 1)
            {
                throw new ArgumentException("A user key is required.", "userKey");
            }

            // Use the trick of a private core method that actually does the async stuff to allow for sync arg contract checking
            return GetUploadFileAsyncCore(userKey);
        }

        public Task SaveUploadFileAsync(int userKey, Stream packageFileStream)
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
            return _fileStorageService.SaveFileAsync(Constants.UploadsFolderName, uploadFileName, packageFileStream);
        }

        private static string BuildFileName(int userKey)
        {
            return String.Format(CultureInfo.InvariantCulture, Constants.UploadFileNameTemplate, userKey, Constants.NuGetPackageFileExtension);
        }

        // Use the trick of a private core method that actually does the async stuff to allow for sync arg contract checking
        private async Task<Stream> GetUploadFileAsyncCore(int userKey)
        {
            var uploadFileName = BuildFileName(userKey);
            return await _fileStorageService.GetFileAsync(Constants.UploadsFolderName, uploadFileName);
        }
    }
}