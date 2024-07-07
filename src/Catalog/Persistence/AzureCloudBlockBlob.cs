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
        private BlobProperties _properties;

        /// <summary>
        /// The Base64 encoded MD5 hash of the blob's content
        /// </summary>
        public async Task<string> GetContentMD5Async(CancellationToken cancellationToken)
        {
            await FetchAttributesAsync(cancellationToken);
            return _properties.ContentHash != null ? Convert.ToBase64String(_properties.ContentHash) : null;
        }

        public async Task<string> GetETagAsync(CancellationToken cancellationToken)
        {
            await FetchAttributesAsync(cancellationToken);
            return _properties.ETag.ToString();
        }

        public async Task<long> GetLengthAsync(CancellationToken cancellationToken)
        {
            await FetchAttributesAsync(cancellationToken);
            return _properties.ContentLength;
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

        public async Task FetchAttributesAsync(CancellationToken cancellationToken)
        {
            _properties = await _blockBlobClient.GetPropertiesAsync(
                conditions: null,
                cancellationToken: cancellationToken);
        }

        public async Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(CancellationToken cancellationToken)
        {
            await FetchAttributesAsync(cancellationToken);
            return new ReadOnlyDictionary<string, string>(_properties.Metadata);
        }

        public async Task<Stream> GetStreamAsync(CancellationToken cancellationToken)
        {
            Stream response = await _blockBlobClient
            .OpenReadAsync(
                    new BlobOpenReadOptions(allowModifications: false), //a Azure.RequestFailedException will be thrown if the blob is modified, while it is being read from.
                    cancellationToken: cancellationToken);
            return response;
        }

        public async Task SetPropertiesAsync(IAccessCondition accessCondition, BlobRequestConditions blobRequestConditions, CloudBlobLocationMode? cloudBlobLocationMode)
        {
            throw new NotImplementedException();
        }
    }
}
