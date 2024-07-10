// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Blobs.Models;
using NuGet.Protocol;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    /// <summary>
    /// <see cref="Storage"/> implementation using v12 <see cref="Azure.Storage.Blobs"/> Sdk.
    /// See <see href="https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/storage/Azure.Storage.Blobs/AzureStorageNetMigrationV12.md">Migration Guide: From Microsoft.Azure.Storage.Blob to Azure.Storage.Blobs</see>
    /// </summary>
    public class AzureStorageBlobs : Storage
    {
        private readonly bool _compressContent;
        private readonly IBlobContainerClientWrapper _blobContainer;
        private readonly IThrottle _throttle;

        public AzureStorageBlobs(
            IBlobContainerClientWrapper blobContainer,
            bool compressContent,
            IThrottle throttle) : base(blobContainer.GetUri())
        {
            _blobContainer = blobContainer ?? throw new ArgumentNullException(nameof(blobContainer));
            _compressContent = compressContent;
            _throttle = throttle ?? NullThrottle.Instance;
        }

        public override bool Exists(string fileName)
        {
            throw new NotImplementedException();
        }

        public override Task<IEnumerable<StorageListItem>> ListAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        protected override Task OnCopyAsync(Uri sourceUri, IStorage destinationStorage, Uri destinationUri, IReadOnlyDictionary<string, string> destinationProperties, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        protected override Task OnDeleteAsync(Uri resourceUri, DeleteRequestOptions deleteRequestOptions, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        protected override async Task<StorageContent> OnLoadAsync(Uri resourceUri, CancellationToken cancellationToken)
        {
            string name = GetName(resourceUri).TrimStart('/');

            var blob = _blobContainer.GetBlockBlobClient(name);
            var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);

            await _throttle.WaitAsync();
            try
            {
                string content;

                using (var originalStream = new MemoryStream())
                {
                    await blob.DownloadToAsync(originalStream, cancellationToken);
                    originalStream.Seek(0, SeekOrigin.Begin);

                    if (properties.Value.ContentEncoding == "gzip")
                    {
                        using (var uncompressedStream = new GZipStream(originalStream, CompressionMode.Decompress))
                        {
                            using (var reader = new StreamReader(uncompressedStream))
                            {
                                content = await reader.ReadToEndAsync();
                            }
                        }
                    }
                    else
                    {
                        using (var reader = new StreamReader(originalStream))
                        {
                            content = await reader.ReadToEndAsync();
                        }
                    }
                }

                return new StringStorageContentWithETag(content, properties.Value.ETag.ToString());
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                if (Verbose)
                {
                    Trace.WriteLine(string.Format("Can't load '{0}'. Blob doesn't exist", resourceUri));
                }

                return null;
            }
            finally
            {
                _throttle.Release();
            }
        }

        protected override async Task OnSaveAsync(Uri resourceUri, StorageContent content, CancellationToken cancellationToken)
        {
            string name = GetName(resourceUri);

            var blob = _blobContainer.GetBlockBlobClient(name);
            var headers = new BlobHttpHeaders
            {
                ContentType = content.ContentType,
                CacheControl = content.CacheControl
            };

            if (_compressContent)
            {
                headers.ContentEncoding = "gzip";

                using (var stream = content.GetContentStream())
                {
                    var destinationStream = new MemoryStream();

                    using (var compressionStream = new GZipStream(destinationStream, CompressionMode.Compress, true))
                    {
                        await stream.CopyToAsync(compressionStream);
                    }

                    destinationStream.Seek(0, SeekOrigin.Begin);
                    await blob.UploadAsync(destinationStream, options: new BlobUploadOptions() { HttpHeaders = headers }, cancellationToken);

                    Trace.WriteLine(string.Format("Saved compressed blob {0} to container {1}", blob.Uri.ToString(), blob.BlobContainerName));
                }
            }
            else
            {
                using (var stream = content.GetContentStream())
                {
                    await blob.UploadAsync(stream, options: new BlobUploadOptions() { HttpHeaders = headers }, cancellationToken);
                }

                Trace.WriteLine(string.Format("Saved uncompressed blob {0} to container {1}", blob.Uri.ToString(), blob.BlobContainerName));
            }

            await TryTakeBlobSnapshotAsync(blob);
        }

        private async Task<bool> TryTakeBlobSnapshotAsync(BlockBlobClient blob)
        {
            if (blob == null)
            {
                return false;
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (_blobContainer.HasOnlyOriginalSnapshot(blob.Name))
                {
                    var response = await blob.CreateSnapshotAsync();
                    stopwatch.Stop();
                    Trace.WriteLine($"SnapshotCreated:milliseconds={stopwatch.ElapsedMilliseconds}:{blob.Uri.ToString()}:{response?.Value.Snapshot}");
                }
                return true;
            }
            catch (RequestFailedException e)
            {
                stopwatch.Stop();
                Trace.WriteLine($"EXCEPTION:milliseconds={stopwatch.ElapsedMilliseconds}:CreateSnapshot: Failed to take the snapshot for blob {blob.Uri.ToString()}. Exception{e.ToString()}");
                return false;
            }
        }
    }
}
