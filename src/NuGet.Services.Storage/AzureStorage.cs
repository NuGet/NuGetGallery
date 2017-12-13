// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGet.Services.Storage
{
    public class AzureStorage : Storage
    {
        private readonly ILogger<AzureStorage> _logger;
        private readonly CloudBlobDirectory _directory;

        public AzureStorage(CloudStorageAccount account, string containerName, string path, Uri baseAddress, ILogger<AzureStorage> logger)
            : this(account.CreateCloudBlobClient().GetContainerReference(containerName).GetDirectoryReference(path), baseAddress, logger)
        {
        }

        private AzureStorage(CloudBlobDirectory directory, Uri baseAddress, ILogger<AzureStorage> logger)
            : base(baseAddress ?? GetDirectoryUri(directory), logger)
        {
            _logger = logger;
            _directory = directory;

            if (_directory.Container.CreateIfNotExists())
            {
                _directory.Container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

                if (Verbose)
                {
                    _logger.LogInformation("Created {ContainerName} publish container", _directory.Container.Name);
                }
            }

            ResetStatistics();
        }

        public bool CompressContent
        {
            get;
            set;
        }

        static Uri GetDirectoryUri(CloudBlobDirectory directory)
        {
            Uri uri = new UriBuilder(directory.Uri)
            {
                Scheme = "http",
                Port = 80
            }.Uri;

            return uri;
        }

        //Blob exists
        public override bool Exists(string fileName)
        {
            var packageRegistrationUri = ResolveUri(fileName);
            var blobName = GetName(packageRegistrationUri);

            var blob = _directory.GetBlockBlobReference(blobName);

            if (blob.Exists())
            {
                return true;
            }
            if (Verbose)
            {
                _logger.LogInformation("The blob {BlobUri} does not exist.", packageRegistrationUri);
            }
            return false;
        }

        public override async Task<bool> ExistsAsync(string fileName, CancellationToken cancellationToken)
        {
            var packageRegistrationUri = ResolveUri(fileName);
            var blobName = GetName(packageRegistrationUri);

            var blob = _directory.GetBlockBlobReference(blobName);

            if (await blob.ExistsAsync(cancellationToken))
            {
                return true;
            }
            if (Verbose)
            {
                _logger.LogInformation("The blob {BlobUri} does not exist.", packageRegistrationUri);
            }
            return false;
        }

        public override async Task<IEnumerable<StorageListItem>> List(CancellationToken cancellationToken)
        {
            var files = await _directory.ListBlobsAsync(cancellationToken);

            return files.Select(GetStorageListItem).AsEnumerable();
        }

        private StorageListItem GetStorageListItem(IListBlobItem listBlobItem)
        {
            var lastModified = (listBlobItem as CloudBlockBlob)?.Properties.LastModified?.UtcDateTime;

            return new StorageListItem(listBlobItem.Uri, lastModified);
        }

        //  save
        protected override async Task OnSave(Uri resourceUri, StorageContent content, bool overwrite, CancellationToken cancellationToken)
        {
            string name = GetName(resourceUri);

            CloudBlockBlob blob = _directory.GetBlockBlobReference(name);
            blob.Properties.ContentType = content.ContentType;
            blob.Properties.CacheControl = content.CacheControl;

            var accessCondition = overwrite ? null : AccessCondition.GenerateIfNotExistsCondition();

            if (CompressContent)
            {
                blob.Properties.ContentEncoding = "gzip";
                using (Stream stream = content.GetContentStream())
                {
                    MemoryStream destinationStream = new MemoryStream();

                    using (GZipStream compressionStream = new GZipStream(destinationStream, CompressionMode.Compress, true))
                    {
                        await stream.CopyToAsync(compressionStream);
                    }

                    destinationStream.Seek(0, SeekOrigin.Begin);

                    await blob.UploadFromStreamAsync(
                        destinationStream,
                        accessCondition,
                        options: null,
                        operationContext: null,
                        cancellationToken: cancellationToken);

                    _logger.LogInformation("Saved compressed blob {BlobUri} to container {ContainerName}", blob.Uri.ToString(), _directory.Container.Name);
                }
            }
            else
            {
                using (Stream stream = content.GetContentStream())
                {
                    await blob.UploadFromStreamAsync(
                        stream,
                        accessCondition,
                        options: null,
                        operationContext: null,
                        cancellationToken: cancellationToken);

                    _logger.LogInformation("Saved uncompressed blob {BlobUri} to container {ContainerName}", blob.Uri.ToString(), _directory.Container.Name);
                }
            }
        }

        //  load
        protected override async Task<StorageContent> OnLoad(Uri resourceUri, CancellationToken cancellationToken)
        {
            // the Azure SDK will treat a starting / as an absolute URL,
            // while we may be working in a subdirectory of a storage container
            // trim the starting slash to treat it as a relative path
            string name = GetName(resourceUri).TrimStart('/');

            CloudBlockBlob blob = _directory.GetBlockBlobReference(name);

            if (blob.Exists())
            {
                MemoryStream originalStream = new MemoryStream();
                await blob.DownloadToStreamAsync(originalStream, cancellationToken);

                originalStream.Position = 0;
                
                MemoryStream content;

                if (blob.Properties.ContentEncoding == "gzip")
                {
                    using (var uncompressedStream = new GZipStream(originalStream, CompressionMode.Decompress))
                    {
                        content = new MemoryStream();

                        await uncompressedStream.CopyToAsync(content);
                    }

                    content.Position = 0;
                }
                else
                {
                    content = originalStream;
                }

                return new StreamStorageContent(content);
            }

            if (Verbose)
            {
                _logger.LogInformation("Can't load {BlobUri}. Blob doesn't exist", resourceUri);
            }

            return null;
        }

        //  delete
        protected override async Task OnDelete(Uri resourceUri, CancellationToken cancellationToken)
        {
            string name = GetName(resourceUri);

            CloudBlockBlob blob = _directory.GetBlockBlobReference(name);

            await blob.DeleteAsync(cancellationToken);
        }
    }
}
