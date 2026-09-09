// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class FileSystemFileStorageService : ICoreFileStorageService
    {
        private readonly string _fileStorageDirectory;
        private readonly IFileSystemService _fileSystemService;

        public FileSystemFileStorageService(string fileStorageDirectory, IFileSystemService fileSystemService)
        {
            _fileStorageDirectory = fileStorageDirectory;
            _fileSystemService = fileSystemService;
        }

        public Task<DownloadFileResult> CreateDownloadFileResultAsync(Uri requestUrl, string folderName, string fileName, string versionParameter)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = BuildPath(_fileStorageDirectory, folderName, fileName);
            if (!_fileSystemService.FileExists(path))
            {
                return Task.FromResult(DownloadFileResult.NotFound());
            }

            return Task.FromResult(
                DownloadFileResult.LocalFile(
                    path,
                    GetContentType(folderName),
                    new FileInfo(fileName).Name));
        }

        public Task DeleteFileAsync(string folderName, string fileName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = BuildPath(_fileStorageDirectory, folderName, fileName);
            if (_fileSystemService.FileExists(path))
            {
                _fileSystemService.DeleteFile(path);
            }

            return Task.FromResult(0);
        }

        public Task<bool> FileExistsAsync(string folderName, string fileName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = BuildPath(_fileStorageDirectory, folderName, fileName);
            bool fileExists = _fileSystemService.FileExists(path);

            return Task.FromResult(fileExists);
        }

        public Task<Stream> GetFileAsync(string folderName, string fileName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = BuildPath(_fileStorageDirectory, folderName, fileName);

            Stream fileStream = _fileSystemService.FileExists(path) ? _fileSystemService.OpenRead(path) : null;
            return Task.FromResult(fileStream);
        }

        public Task<IFileReference> GetFileReferenceAsync(string folderName, string fileName, string ifNoneMatch = null)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = BuildPath(_fileStorageDirectory, folderName, fileName);

            // Get the last modified date of the file and use that as the ContentID
            var file = new FileInfo(path);
            return Task.FromResult<IFileReference>(file.Exists ? new LocalFileReference(file) : null);
        }

        public Task SaveFileAsync(string folderName, string fileName, string contentType, Stream file, bool overwrite = true)
        {
            // file system does not support content type, so we'll simply ignore it
            return SaveFileAsync(folderName, fileName, file, overwrite);
        }

        public Task SaveFileAsync(string folderName, string fileName, Stream packageFile, bool overwrite = true)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            if (packageFile == null)
            {
                throw new ArgumentNullException(nameof(packageFile));
            }

            var filePath = BuildPath(_fileStorageDirectory, folderName, fileName);

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
                throw new FileAlreadyExistsException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "There is already a file with name {0} in folder {1}.",
                        fileName,
                        folderName),
                    ex);
            }

            return Task.FromResult(0);
        }

        public async Task SaveFileAsync(string folderName, string fileName, Stream file, IAccessCondition condition)
        {
            await SaveFileAsync(folderName, fileName, file);
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

            var srcFilePath = BuildPath(_fileStorageDirectory, srcFolderName, srcFileName);
            var destFilePath = BuildPath(_fileStorageDirectory, destFolderName, destFileName);

            _fileSystemService.CreateDirectory(Path.GetDirectoryName(destFilePath));

            try
            {
                _fileSystemService.Copy(srcFilePath, destFilePath, overwrite: false);
            }
            catch (IOException e)
            {
                throw new FileAlreadyExistsException("Could not copy because destination file already exists", e);
            }

            return Task.FromResult<string>(null);
        }

        public virtual Task<bool> IsAvailableAsync()
        {
            return Task.FromResult(Directory.Exists(_fileStorageDirectory));
        }

        public Task<Uri> GetFileUriAsync(string folderName, string fileName)
        {
            /// Not implemented for the same reason as <see cref="GetFileReadUriAsync(string, string, DateTimeOffset?)"/>.
            throw new NotImplementedException();
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

        public Task<Uri> GetPrivilegedFileUriAsync(string folderName, string fileName, FileUriPermissions permissions, DateTimeOffset endOfAccess)
        {
            /// Not implemented for the same reason as <see cref="GetFileReadUriAsync(string, string, DateTimeOffset?)"/>.
            throw new NotImplementedException();
        }

        public Task<Uri> GetPrivilegedFileUriWithDelegationSasAsync(string folderName, string fileName, FileUriPermissions permissions, DateTimeOffset endOfAccess)
        {
            throw new NotImplementedException();
        }

        public Task SetMetadataAsync(
            string folderName,
            string fileName,
            Func<Lazy<Task<Stream>>, IDictionary<string, string>, Task<bool>> updateMetadataAsync)
        {
            return Task.CompletedTask;
        }

        public Task SetPropertiesAsync(
            string folderName,
            string fileName,
            Func<Lazy<Task<Stream>>, ICloudBlobProperties, Task<bool>> updatePropertiesAsync)
        {
            return Task.CompletedTask;
        }

        private static string BuildPath(string fileStorageDirectory, string folderName, string fileName)
        {
            return Path.Combine(fileStorageDirectory, folderName, fileName);
        }

        public Task<string> GetETagOrNullAsync(
           string folderName,
           string fileName)
        {
            throw new NotImplementedException(nameof(GetETagOrNullAsync));
        }

        private static string GetContentType(string folderName)
        {
            switch (folderName)
            {
                case CoreConstants.Folders.PackagesFolderName:
                case CoreConstants.Folders.SymbolPackagesFolderName:
                    return CoreConstants.PackageContentType;

                default:
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.CurrentCulture, "The folder name {0} is not supported.", folderName));
            }
        }
    }
}
