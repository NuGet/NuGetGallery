// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Web.Mvc;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class FileSystemFileStorageService : IFileStorageService
    {
        private readonly IAppConfiguration _configuration;
        private readonly IFileSystemService _fileSystemService;

        public FileSystemFileStorageService(IAppConfiguration configuration, IFileSystemService fileSystemService)
        {
            _configuration = configuration;
            _fileSystemService = fileSystemService;
        }

        public Task<ActionResult> CreateDownloadFileActionResultAsync(Uri requestUrl, string folderName, string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = BuildPath(_configuration.FileStorageDirectory, folderName, fileName);
            if (!_fileSystemService.FileExists(path))
            {
                return Task.FromResult<ActionResult>(new HttpNotFoundResult());
            }

            var result = new FilePathResult(path, GetContentType(folderName))
            {
                FileDownloadName = new FileInfo(fileName).Name
            };

            return Task.FromResult<ActionResult>(result);
        }

        public Task DeleteFileAsync(string folderName, string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = BuildPath(_configuration.FileStorageDirectory, folderName, fileName);
            if (_fileSystemService.FileExists(path))
            {
                _fileSystemService.DeleteFile(path);
            }

            return Task.FromResult(0);
        }

        public Task<bool> FileExistsAsync(string folderName, string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = BuildPath(_configuration.FileStorageDirectory, folderName, fileName);
            bool fileExists = _fileSystemService.FileExists(path);

            return Task.FromResult(fileExists);
        }

        public Task<Stream> GetPackageFileAsync(string folderName, string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = BuildPath(_configuration.FileStorageDirectory, folderName, fileName);

            Stream fileStream = _fileSystemService.FileExists(path) ? _fileSystemService.OpenRead(path) : null;
            return Task.FromResult(fileStream);
        }

        //Gets the README file for the Package from File Storage
        public Task<Stream> GetReadmeFileAsync(string folderName, string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = BuildPath(_configuration.FileStorageDirectory, folderName, fileName);

            Stream fileStream = _fileSystemService.FileExists(path) ? _fileSystemService.OpenRead(path) : null;
            return Task.FromResult(fileStream);
        }

        public Task<IFileReference> GetFileReferenceAsync(string folderName, string fileName, string ifNoneMatch = null)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = BuildPath(_configuration.FileStorageDirectory, folderName, fileName);

            // Get the last modified date of the file and use that as the ContentID
            var file = new FileInfo(path);
            return Task.FromResult<IFileReference>(file.Exists ? new LocalFileReference(file) : null);
        }

        public Task SaveFileAsync(string folderName, string fileName, Stream packageFile, bool overwrite = true)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            if (packageFile == null)
            {
                throw new ArgumentNullException(nameof(packageFile));
            }

            var filePath = BuildPath(_configuration.FileStorageDirectory, folderName, fileName);

            var dirPath = Path.GetDirectoryName(filePath);

            _fileSystemService.CreateDirectory(dirPath);
                        
            try
            {
                using (var file = _fileSystemService.OpenWrite(filePath, overwrite))
                {
                    packageFile.CopyTo(file);
                }
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException(
                    String.Format(
                        CultureInfo.CurrentCulture,
                        "There is already a file with name {0} in folder {1}.",
                        fileName,
                        folderName),
                    ex);
            }

            return Task.FromResult(0);
        }

        public Task<bool> IsAvailableAsync()
        {
            return Task.FromResult(Directory.Exists(_configuration.FileStorageDirectory));
        }

        private static string BuildPath(string fileStorageDirectory, string folderName, string fileName)
        {
            // Resolve the file storage directory
            fileStorageDirectory = ResolvePath(fileStorageDirectory);

            return Path.Combine(fileStorageDirectory, folderName, fileName);
        }

        public static string ResolvePath(string fileStorageDirectory)
        {
            if (fileStorageDirectory.StartsWith("~/", StringComparison.OrdinalIgnoreCase) && HostingEnvironment.IsHosted)
            {
                fileStorageDirectory = HostingEnvironment.MapPath(fileStorageDirectory);
            }
            return fileStorageDirectory;
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