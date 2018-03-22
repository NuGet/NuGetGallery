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

        public Task<Stream> GetFileAsync(string folderName, string fileName)
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

        public Task CopyFileAsync(Uri srcUri, string destFolderName, string destFileName, IAccessCondition destAccessCondition)
        {
            // We could theoretically support this by downloading the source URI to the destination path. This is not
            // needed today so this method will remain unimplemented until it is needed.
            throw new NotImplementedException();
        }

        public Task<string> CopyFileAsync(
            string srcFolderName,
            string srcFileName,
            string destFolderName,
            string destFileName,
            IAccessCondition destAccessCondition)
        {
            if (srcFolderName == null)
            {
                throw new ArgumentNullException(nameof(srcFolderName));
            }

            if (srcFileName == null)
            {
                throw new ArgumentNullException(nameof(srcFileName));
            }

            if (destFolderName == null)
            {
                throw new ArgumentNullException(nameof(destFolderName));
            }

            if (destFileName == null)
            {
                throw new ArgumentNullException(nameof(destFileName));
            }

            var srcFilePath = BuildPath(_configuration.FileStorageDirectory, srcFolderName, srcFileName);
            var destFilePath = BuildPath(_configuration.FileStorageDirectory, destFolderName, destFileName);

            _fileSystemService.CreateDirectory(Path.GetDirectoryName(destFilePath));
            
            try
            {
                _fileSystemService.Copy(srcFilePath, destFilePath, overwrite: false);
            }
            catch (IOException e)
            {
                throw new InvalidOperationException("Could not copy because destination file already exists", e);
            }

            return Task.FromResult<string>(null);
        }

        public Task<bool> IsAvailableAsync()
        {
            return Task.FromResult(Directory.Exists(_configuration.FileStorageDirectory));
        }

        public Task<Uri> GetFileReadUriAsync(string folderName, string fileName, DateTimeOffset? endOfAccess)
        {
            // technically, we would be able to generate the file:/// url here, but we don't need it right now
            // and implementation would be a bit non-trivial: System.Uri handles the "%" character in paths 
            // in a funny way: 
            // new Uri(@"c:\%41foo%20bar%25.baz")
            // produces the
            // file:///c:/Afoo%20bar%2525.baz
            // which is not particularly correct, so we'd need to work around that to have a correct implementation
            throw new NotImplementedException();
        }

        public Task<Uri> GetPriviledgedFileUriAsync(string folderName, string fileName, FileUriPermissions permissions, DateTimeOffset endOfAccess)
        {
            /// Not implemented for the same reason as <see cref="GetFileReadUriAsync(string, string, DateTimeOffset?)"/>.
            throw new NotImplementedException();
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
                case CoreConstants.PackagesFolderName:
                    return CoreConstants.PackageContentType;

                case CoreConstants.DownloadsFolderName:
                    return CoreConstants.OctetStreamContentType;

                default:
                    throw new InvalidOperationException(
                        String.Format(CultureInfo.CurrentCulture, "The folder name {0} is not supported.", folderName));
            }
        }
    }
}