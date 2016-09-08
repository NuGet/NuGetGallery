﻿// Copyright (c) .NET Foundation. All rights reserved.
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
        private readonly IGalleryConfigurationService _configService;
        private readonly IFileSystemService _fileSystemService;

        public FileSystemFileStorageService(IGalleryConfigurationService configService, IFileSystemService fileSystemService)
        {
            _configService = configService;
            _fileSystemService = fileSystemService;
        }

        public async Task<ActionResult> CreateDownloadFileActionResultAsync(Uri requestUrl, string folderName, string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = BuildPath((await _configService.GetCurrent()).FileStorageDirectory, folderName, fileName);
            if (!_fileSystemService.FileExists(path))
            {
                return new HttpNotFoundResult();
            }

            var result = new FilePathResult(path, GetContentType(folderName))
            {
                FileDownloadName = new FileInfo(fileName).Name
            };

            return result;
        }

        public async Task DeleteFileAsync(string folderName, string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = BuildPath((await _configService.GetCurrent()).FileStorageDirectory, folderName, fileName);
            if (_fileSystemService.FileExists(path))
            {
                _fileSystemService.DeleteFile(path);
            }

            return;
        }

        public async Task<bool> FileExistsAsync(string folderName, string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = BuildPath((await _configService.GetCurrent()).FileStorageDirectory, folderName, fileName);
            bool fileExists = _fileSystemService.FileExists(path);

            return fileExists;
        }

        public async Task<Stream> GetFileAsync(string folderName, string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = BuildPath((await _configService.GetCurrent()).FileStorageDirectory, folderName, fileName);

            Stream fileStream = _fileSystemService.FileExists(path) ? _fileSystemService.OpenRead(path) : null;
            return fileStream;
        }

        public async Task<IFileReference> GetFileReferenceAsync(string folderName, string fileName, string ifNoneMatch = null)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = BuildPath((await _configService.GetCurrent()).FileStorageDirectory, folderName, fileName);

            // Get the last modified date of the file and use that as the ContentID
            var file = new FileInfo(path);
            return file.Exists ? new LocalFileReference(file) : null;
        }

        public async Task SaveFileAsync(string folderName, string fileName, Stream packageFile)
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

            if (!_fileSystemService.DirectoryExists((await _configService.GetCurrent()).FileStorageDirectory))
            {
                _fileSystemService.CreateDirectory((await _configService.GetCurrent()).FileStorageDirectory);
            }

            var filePath = BuildPath((await _configService.GetCurrent()).FileStorageDirectory, folderName, fileName);
            var folderPath = Path.GetDirectoryName(filePath);
            if (_fileSystemService.FileExists(filePath))
            {
                _fileSystemService.DeleteFile(filePath);
            }
            else
            {
                _fileSystemService.CreateDirectory(folderPath);
            }

            using (var file = _fileSystemService.OpenWrite(filePath))
            {
                packageFile.CopyTo(file);
            }

            return;
        }

        public async Task<bool> IsAvailableAsync()
        {
            return Directory.Exists((await _configService.GetCurrent()).FileStorageDirectory);
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