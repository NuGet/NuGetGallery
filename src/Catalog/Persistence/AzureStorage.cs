// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class AzureStorage : Storage
    {
        private readonly CloudBlobDirectory _directory;

        public AzureStorage(CloudStorageAccount account, string containerName, string path, Uri baseAddress)
            : this(account.CreateCloudBlobClient().GetContainerReference(containerName).GetDirectoryReference(path), baseAddress)
        {
        }

        private AzureStorage(CloudBlobDirectory directory, Uri baseAddress)
            : base(baseAddress ?? GetDirectoryUri(directory))
        {
            _directory = directory;

            if (_directory.Container.CreateIfNotExists())
            {
                _directory.Container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

                if (Verbose)
                {
                    Trace.WriteLine(String.Format("Created '{0}' publish container", _directory.Container.Name));
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
            Uri packageRegistrationUri = ResolveUri(fileName);
            string blobName = GetName(packageRegistrationUri);

            CloudBlockBlob blob = _directory.GetBlockBlobReference(blobName);

            if (blob.Exists())
            {
                return true;
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

        protected override async Task OnSave(Uri resourceUri, StorageContent content, CancellationToken cancellationToken)
        {
            string name = GetName(resourceUri);

            CloudBlockBlob blob = _directory.GetBlockBlobReference(name);
            blob.Properties.ContentType = content.ContentType;
            blob.Properties.CacheControl = content.CacheControl;

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
                    await blob.UploadFromStreamAsync(destinationStream, cancellationToken);
                }
            }
            else
            {
                using (Stream stream = content.GetContentStream())
                {
                    await blob.UploadFromStreamAsync(stream, cancellationToken);
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

                originalStream.Seek(0, SeekOrigin.Begin);

                string content;

                if (blob.Properties.ContentEncoding == "gzip")
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

                return new StringStorageContent(content);
            }

            if (Verbose)
            {
                Trace.WriteLine(String.Format("Can't load '{0}'. Blob doesn't exist", resourceUri));
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
