// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using NuGetGallery;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public sealed class AzureCloudBlockBlob : ICloudBlockBlob
    {
        private readonly BlockBlobClient _blockBlobClient;

        /// <summary>
        /// The Base64 encoded MD5 hash of the blob's content
        /// </summary>
        public async Task<string> GetContentMD5Async(CancellationToken cancellationToken)
        {
            BlobProperties properties = await FetchAttributesAsync(cancellationToken);
            return properties.ContentHash != null ? Convert.ToBase64String(properties.ContentHash) : null;
        }

        public async Task<string> GetETagAsync(CancellationToken cancellationToken)
        {
            BlobProperties properties = await FetchAttributesAsync(cancellationToken);
            return properties.ETag.ToString();
        }

        public async Task<long> GetLengthAsync(CancellationToken cancellationToken)
        {
            BlobProperties properties = await FetchAttributesAsync(cancellationToken);
            return properties.ContentLength;
        }

        public Uri Uri => _blockBlobClient.Uri;

        public AzureCloudBlockBlob(BlockBlobClient blockBlobClient)
        {
            _blockBlobClient = blockBlobClient ?? throw new ArgumentNullException(nameof(blockBlobClient));
        }

        public async Task<bool> ExistsAsync(CancellationToken cancellationToken)
        {
            return await _blockBlobClient.ExistsAsync(cancellationToken);
        }

        public async Task<BlobProperties> FetchAttributesAsync(CancellationToken cancellationToken)
        {
            BlobProperties properties = await _blockBlobClient.GetPropertiesAsync(
                conditions: null,
                cancellationToken: cancellationToken);
            return properties;
        }

        public async Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(CancellationToken cancellationToken)
        {
            BlobProperties properties = await FetchAttributesAsync(cancellationToken);
            return new ReadOnlyDictionary<string, string>(properties.Metadata);
        }

        public async Task<Stream> GetStreamAsync(CancellationToken cancellationToken)
        {
            Stream response = await _blockBlobClient
            .OpenReadAsync(
                    new BlobOpenReadOptions(allowModifications: false), //a Azure.RequestFailedException will be thrown if the blob is modified, while it is being read from.
                    cancellationToken: cancellationToken);
            return response;
        }
    }
}
