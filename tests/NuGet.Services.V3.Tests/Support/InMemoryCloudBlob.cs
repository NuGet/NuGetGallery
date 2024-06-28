// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGetGallery;

namespace NuGet.Services
{
    public class InMemoryCloudBlob : ISimpleCloudBlob
    {
        private static int _nextETag = 0;
        private readonly object _lock = new object();
        private string _etag;

        public InMemoryCloudBlob()
        {
        }

        public InMemoryCloudBlob(string content)
        {
            Bytes = Encoding.ASCII.GetBytes(content);
            Exists = true;
            _etag = Interlocked.Increment(ref _nextETag).ToString();
        }

        public ICloudBlobProperties Properties { get; } = Mock.Of<ICloudBlobProperties>();
        public IDictionary<string, string> Metadata => throw new NotImplementedException();
        public ICloudBlobCopyState CopyState => throw new NotImplementedException();
        public Uri Uri => throw new NotImplementedException();
        public string Name => throw new NotImplementedException();
        public DateTime LastModifiedUtc { get; private set; } = DateTime.UtcNow;
        public bool IsSnapshot => throw new NotImplementedException();

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
            lock (_lock)
            {
                Exists = false;
            }

            return Task.CompletedTask;
        }

        public Task<string> DownloadTextIfExistsAsync()
        {
            throw new NotImplementedException();
        }

        public Task DownloadToStreamAsync(Stream target)
        {
            throw new NotImplementedException();
        }

        public Task DownloadToStreamAsync(Stream target, IAccessCondition accessCondition)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ExistsAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(Exists);
            }
        }

        public Task FetchAttributesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<bool> FetchAttributesIfExistsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<string> GetSharedAccessSignature(FileUriPermissions permissions, DateTimeOffset endOfAccess)
        {
            throw new NotImplementedException();
        }

        public async Task<Stream> OpenReadAsync(IAccessCondition accessCondition)
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
                    throw new CloudBlobNotFoundException(null);
                }

                return new MemoryStream(Bytes);
            }
        }

        public Task<Stream> OpenReadIfExistsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<Stream> OpenReadStreamAsync(TimeSpan serverTimeout, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<Stream> OpenWriteAsync(IAccessCondition accessCondition, string contentType)
        {
            await Task.Yield();

            return new RecordingStream(bytes =>
            {
                UploadFromBytes(bytes, accessCondition);
            });
        }

        public Task SetMetadataAsync(IAccessCondition accessCondition)
        {
            throw new NotImplementedException();
        }

        public Task SetPropertiesAsync()
        {
            throw new NotImplementedException();
        }

        public Task SetPropertiesAsync(IAccessCondition accessCondition)
        {
            throw new NotImplementedException();
        }

        public Task SnapshotAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task StartCopyAsync(ISimpleCloudBlob source, IAccessCondition sourceAccessCondition, IAccessCondition destAccessCondition)
        {
            throw new NotImplementedException();
        }

        public Task UploadFromStreamAsync(Stream source, bool overwrite)
        {
            throw new NotImplementedException();
        }

        public async Task UploadFromStreamAsync(Stream source, IAccessCondition accessCondition)
        {
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
            var newBytes = buffer.ToArray();

            UploadFromBytes(newBytes, accessCondition);
        }

        private void UploadFromBytes(byte[] newBytes, IAccessCondition accessCondition)
        {
            var newETag = Interlocked.Increment(ref _nextETag).ToString();

            lock (_lock)
            {
                if (Exists)
                {
                    if (accessCondition.IfMatchETag != null && accessCondition.IfMatchETag != ETag)
                    {
                        throw new InvalidOperationException("The If-Match condition failed because it does not match the current etag.");
                    }

                    if (accessCondition.IfNoneMatchETag == "*")
                    {
                        throw new InvalidOperationException("The If-None-Match condition failed because the blob exists.");
                    }
                }
                else
                {
                    if (accessCondition.IfMatchETag != null)
                    {
                        throw new InvalidOperationException("The If-Match condition failed because the file does not exist.");
                    }
                }

                _etag = newETag;
                Bytes = newBytes;
                Exists = true;
                LastModifiedUtc = DateTime.UtcNow;
            }
        }
    }
}
