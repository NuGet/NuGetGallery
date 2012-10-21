using System;
using System.Globalization;
using System.IO;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class FileSystemFileStorageService : IFileStorageService
    {
        private readonly IConfiguration _configuration;
        private readonly IFileSystemService _fileSystemSvc;

        public FileSystemFileStorageService(
            IConfiguration configuration,
            IFileSystemService fileSystemSvc)
        {
            _configuration = configuration;
            _fileSystemSvc = fileSystemSvc;
        }

        public ActionResult CreateDownloadFileActionResult(
            string folderName,
            string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException("folderName");
            }
            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            var path = BuildPath(_configuration.FileStorageDirectory, folderName, fileName);
            if (!_fileSystemSvc.FileExists(path))
            {
                return new HttpNotFoundResult();
            }

            var result = new FilePathResult(path, GetContentType(folderName));
            result.FileDownloadName = new FileInfo(fileName).Name;
            return result;
        }

        public void DeleteFile(
            string folderName,
            string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException("folderName");
            }
            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            var path = BuildPath(_configuration.FileStorageDirectory, folderName, fileName);
            if (_fileSystemSvc.FileExists(path))
            {
                _fileSystemSvc.DeleteFile(path);
            }
        }

        public bool FileExists(
            string folderName,
            string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException("folderName");
            }
            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            var path = BuildPath(_configuration.FileStorageDirectory, folderName, fileName);
            return _fileSystemSvc.FileExists(path);
        }

        public Stream GetFile(
            string folderName,
            string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException("folderName");
            }
            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            var path = BuildPath(_configuration.FileStorageDirectory, folderName, fileName);
            if (_fileSystemSvc.FileExists(path))
            {
                return _fileSystemSvc.OpenRead(path);
            }
            else
            {
                return null;
            }
        }

        public void SaveFile(
            string folderName,
            string fileName,
            Stream packageFile)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException("folderName");
            }
            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }
            if (packageFile == null)
            {
                throw new ArgumentNullException("packageFile");
            }

            if (!_fileSystemSvc.DirectoryExists(_configuration.FileStorageDirectory))
            {
                _fileSystemSvc.CreateDirectory(_configuration.FileStorageDirectory);
            }

            var folderPath = Path.Combine(_configuration.FileStorageDirectory, folderName);
            if (!_fileSystemSvc.DirectoryExists(folderPath))
            {
                _fileSystemSvc.CreateDirectory(folderPath);
            }

            var filePath = BuildPath(_configuration.FileStorageDirectory, folderName, fileName);
            using (var file = _fileSystemSvc.OpenWrite(filePath))
            {
                packageFile.CopyTo(file);
            }
        }

        private static string BuildPath(
            string fileStorageDirectory,
            string folderName,
            string fileName)
        {
            return Path.Combine(fileStorageDirectory, folderName, fileName);
        }

        private static string GetContentType(string folderName)
        {
            switch (folderName)
            {
                case Constants.PackagesFolderName:
                    return Constants.PackageContentType;
                case Constants.DownloadsFolderName:
                    return Constants.OctetStreamContentType;
                default:
                    throw new InvalidOperationException(
                        String.Format(CultureInfo.CurrentCulture, "The folder name {0} is not supported.", folderName));
            }
        }
    }
}