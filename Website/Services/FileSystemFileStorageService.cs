using System;
using System.Globalization;
using System.IO;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class FileSystemFileStorageService : IFileStorageService
    {
        readonly IConfiguration configuration;
        readonly IFileSystemService fileSystemSvc;

        public FileSystemFileStorageService(
            IConfiguration configuration,
            IFileSystemService fileSystemSvc)
        {
            this.configuration = configuration;
            this.fileSystemSvc = fileSystemSvc;
        }

        static string BuildPath(
            string fileStorageDirectory,
            string folderName,
            string fileName)
        {
            return Path.Combine(fileStorageDirectory, folderName, fileName);
        }

        public ActionResult CreateDownloadFileActionResult(
            string folderName, 
            string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
                throw new ArgumentNullException("folderName");
            if (String.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException("fileName");
            
            var path = BuildPath(configuration.FileStorageDirectory, folderName, fileName);
            if (!fileSystemSvc.FileExists(path))
                return new HttpNotFoundResult();

            var result = new FilePathResult(path, GetContentType(folderName));
            result.FileDownloadName = new FileInfo(fileName).Name;
            return result;
        }

        static string GetContentType(string folderName)
        {
            switch (folderName)
            {
                case Constants.PackagesFolderName:
                    return Constants.PackageContentType;
                default:
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, "The folder name {0} is not supported.", folderName));
            }
        }

        public void DeleteFile(
            string folderName, 
            string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
                throw new ArgumentNullException("folderName");
            if (String.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException("fileName");
            
            var path = BuildPath(configuration.FileStorageDirectory, folderName, fileName);
            if (fileSystemSvc.FileExists(path))
                fileSystemSvc.DeleteFile(path);
        }

        public Stream GetFile(
            string folderName, 
            string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
                throw new ArgumentNullException("folderName");
            if (String.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException("fileName");

            var path = BuildPath(configuration.FileStorageDirectory, folderName, fileName);
            if (fileSystemSvc.FileExists(path))
                return fileSystemSvc.OpenRead(path);
            else
                return null;
        }

        public void SaveFile(
            string folderName,
            string fileName, 
            Stream packageFile)
        {
            if (String.IsNullOrWhiteSpace(folderName))
                throw new ArgumentNullException("folderName");
            if (String.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException("fileName");
            if (packageFile == null)
                throw new ArgumentNullException("packageFile");

            if (!fileSystemSvc.DirectoryExists(configuration.FileStorageDirectory))
                fileSystemSvc.CreateDirectory(configuration.FileStorageDirectory);

            var folderPath = Path.Combine(configuration.FileStorageDirectory, folderName);
            if (!fileSystemSvc.DirectoryExists(folderPath))
                fileSystemSvc.CreateDirectory(folderPath);

            var filePath = BuildPath(configuration.FileStorageDirectory, folderName, fileName);
            using (var file = fileSystemSvc.OpenWrite(filePath))
            {
                packageFile.CopyTo(file);
            }
        }
    }
}