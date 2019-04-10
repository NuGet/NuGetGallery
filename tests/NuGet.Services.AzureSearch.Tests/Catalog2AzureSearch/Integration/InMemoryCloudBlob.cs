// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch.Integration
{
    public class InMemoryCloudBlob : ISimpleCloudBlob
    {
        private static int _nextETag = 0;
        private readonly object _lock = new object();
        private string _etag;

        public BlobProperties Properties { get; } = new CloudBlockBlob(new Uri("https://example/blob")).Properties;
        public IDictionary<string, string> Metadata => throw new NotImplementedException();
        public CopyState CopyState => throw new NotImplementedException();
        public Uri Uri => throw new NotImplementedException();
        public string Name => throw new NotImplementedException();
        public DateTime LastModifiedUtc => throw new NotImplementedException();

        public string ETag
        {
            get
            {
                lock (_lock)
                {
                    return _etag;
                }
            }
        }

        public byte[] Bytes { get; private set; }
        public bool Exists { get; private set; }
        public string AsString
        {
            get
            {
                if (Bytes == null)
                {
                    return null;
                }

                return Encoding.UTF8.GetString(Bytes);
            }
        }

        public Task DeleteIfExistsAsync()
        {
            throw new NotImplementedException();
        }

        public Task DownloadToStreamAsync(Stream target)
        {
            throw new NotImplementedException();
        }

        public Task DownloadToStreamAsync(Stream target, AccessCondition accessCondition)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ExistsAsync()
        {
            throw new NotImplementedException();
        }

        public Task FetchAttributesAsync()
        {
            throw new NotImplementedException();
        }

        public string GetSharedAccessSignature(SharedAccessBlobPermissions permissions, DateTimeOffset? endOfAccess)
        {
            throw new NotImplementedException();
        }

        public async Task<Stream> OpenReadAsync(AccessCondition accessCondition)
        {
            if (accessCondition.IfMatchETag != null || accessCondition.IfNoneMatchETag != null)
            {
                throw new ArgumentException($"Both {nameof(accessCondition.IfMatchETag)} or {nameof(accessCondition.IfNoneMatchETag)} must be null.");
            }

            await Task.Yield();

            lock (_lock)
            {
                if (!Exists)
                {
                    throw new StorageException(
                        new RequestResult
                        {
                            HttpStatusCode = (int)HttpStatusCode.NotFound,
                        },
                        "Not found.",
                        inner: null);
                }

                return new MemoryStream(Bytes);
            }
        }

        public Task<Stream> OpenReadStreamAsync(TimeSpan serverTimeout, TimeSpan maxExecutionTime, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> OpenWriteAsync(AccessCondition accessCondition)
        {
            throw new NotImplementedException();
        }

        public Task SetMetadataAsync(AccessCondition accessCondition)
        {
            throw new NotImplementedException();
        }

        public Task SetPropertiesAsync()
        {
            throw new NotImplementedException();
        }

        public Task SetPropertiesAsync(AccessCondition accessCondition)
        {
            throw new NotImplementedException();
        }

        public Task StartCopyAsync(ISimpleCloudBlob source, AccessCondition sourceAccessCondition, AccessCondition destAccessCondition)
        {
            throw new NotImplementedException();
        }

        public Task UploadFromStreamAsync(Stream source, bool overwrite)
        {
            throw new NotImplementedException();
        }

        public async Task UploadFromStreamAsync(Stream source, AccessCondition accessCondition)
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

            await Task.Yield();

            var buffer = new MemoryStream();
            await source.CopyToAsync(buffer);
            var newETag = Interlocked.Increment(ref _nextETag).ToString();
            var newBytes = buffer.ToArray();

            lock (_lock)
            {
                if (!Exists)
                {
                    if (accessCondition.IfMatchETag != null)
                    {
                        throw new InvalidOperationException("The If-Match condition failed because the file does not exist.");
                    }
                }
                else
                {
                    if (accessCondition.IfMatchETag != ETag)
                    {
                        throw new InvalidOperationException("The If-Match condition failed because it does not match the current etag.");
                    }
                }

                _etag = newETag;
                Bytes = newBytes;
                Exists = true;
            }
        }
    }
}
