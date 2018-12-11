// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch.Integration
{
    public class InMemoryCoreFileStorageService : ICoreFileStorageService
    {
        private readonly object _lock = new object();
        private int _nextContentId = 0;

        public Dictionary<string, InMemoryFileReference> Files { get; } = new Dictionary<string, InMemoryFileReference>();


        public Task CopyFileAsync(Uri srcUri, string destFolderName, string destFileName, IAccessCondition destAccessCondition)
        {
            throw new NotImplementedException();
        }

        public Task<string> CopyFileAsync(string srcFolderName, string srcFileName, string destFolderName, string destFileName, IAccessCondition destAccessCondition)
        {
            throw new NotImplementedException();
        }

        public Task DeleteFileAsync(string folderName, string fileName)
        {
            throw new NotImplementedException();
        }

        public Task<bool> FileExistsAsync(string folderName, string fileName)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetETagOrNullAsync(string folderName, string fileName)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> GetFileAsync(string folderName, string fileName)
        {
            throw new NotImplementedException();
        }

        public Task<Uri> GetFileReadUriAsync(string folderName, string fileName, DateTimeOffset? endOfAccess)
        {
            throw new NotImplementedException();
        }

        public Task<IFileReference> GetFileReferenceAsync(string folderName, string fileName, string ifNoneMatch = null)
        {
            var fullFileName = GetFullFileName(folderName, fileName);
            lock (_lock)
            {
                if (!Files.TryGetValue(fullFileName, out var fileReference))
                {
                    return Task.FromResult<IFileReference>(null);
                }

                return Task.FromResult<IFileReference>(fileReference);
            }
        }

        private static string GetFullFileName(string folderName, string fileName)
        {
            return $"{folderName}/{fileName}";
        }

        public Task<Uri> GetPriviledgedFileUriAsync(string folderName, string fileName, FileUriPermissions permissions, DateTimeOffset endOfAccess)
        {
            throw new NotImplementedException();
        }

        public Task SaveFileAsync(string folderName, string fileName, Stream file, bool overwrite = true)
        {
            throw new NotImplementedException();
        }

        public Task SaveFileAsync(string folderName, string fileName, string contentType, Stream file, bool overwrite = true)
        {
            throw new NotImplementedException();
        }

        public async Task SaveFileAsync(string folderName, string fileName, Stream file, IAccessCondition accessCondition)
        {
            if (accessCondition.IfMatchETag == null && accessCondition.IfNoneMatchETag == null)
            {
                throw new ArgumentException($"Either {nameof(accessCondition.IfMatchETag)} or {nameof(accessCondition.IfNoneMatchETag)} must be set.");
            }

            if (accessCondition.IfMatchETag != null && accessCondition.IfNoneMatchETag != null)
            {
                throw new ArgumentException($"Exactly one of {nameof(accessCondition.IfMatchETag)} or {nameof(accessCondition.IfNoneMatchETag)} must be set, not both.");
            }

            if (accessCondition.IfNoneMatchETag != null && accessCondition.IfNoneMatchETag != "*")
            {
                throw new ArgumentException($"{nameof(accessCondition.IfNoneMatchETag)} must be set to either null or '*'.");
            }

            var fullFileName = GetFullFileName(folderName, fileName);
            var buffer = new MemoryStream();
            await file.CopyToAsync(buffer);
            var newFileReference = new InMemoryFileReference(
                contentId: Interlocked.Increment(ref _nextContentId).ToString(),
                bytes: buffer.ToArray());

            lock (_lock)
            {
                if (!Files.TryGetValue(fullFileName, out var fileReference))
                {
                    if (accessCondition.IfMatchETag != null)
                    {
                        throw new InvalidOperationException("The If-Match condition failed because the file does not exist.");
                    }
                }
                else
                {
                    if (accessCondition.IfMatchETag != fileReference.ContentId)
                    {
                        throw new InvalidOperationException("The If-Match condition failed because it does not match the current etag.");
                    }
                }

                Files[fullFileName] = newFileReference;
            }
        }

        public Task SetMetadataAsync(string folderName, string fileName, Func<Lazy<Task<Stream>>, IDictionary<string, string>, Task<bool>> updateMetadataAsync)
        {
            throw new NotImplementedException();
        }

        public Task SetPropertiesAsync(string folderName, string fileName, Func<Lazy<Task<Stream>>, BlobProperties, Task<bool>> updatePropertiesAsync)
        {
            throw new NotImplementedException();
        }
    }
}
