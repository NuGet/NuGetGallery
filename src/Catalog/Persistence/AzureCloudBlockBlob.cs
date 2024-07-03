// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using NuGetGallery;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public sealed class AzureCloudBlockBlob : ICloudBlockBlob
    {
        private readonly BlobClient _blobClient;
        private BlobProperties _properties;

        /// <summary>
        /// The Base64 encoded MD5 hash of the blob's content
        /// </summary>
        public string ContentMD5
        {
            get => _properties.ContentHash != null ? Convert.ToBase64String(_properties.ContentHash) : null;
            //set => _properties.ContentHash = value != null ? Convert.FromBase64String(value) : null;
        }

        public string ETag => _properties.ETag.ToString();
        public long Length => _properties.ContentLength;
        public Uri Uri => _blobClient.Uri;

        public AzureCloudBlockBlob(BlobClient blobClient)
        {
            _blobClient = blobClient ?? throw new ArgumentNullException(nameof(blobClient));
        }

        public async Task<bool> ExistsAsync(CancellationToken cancellationToken)
        {
            return await _blobClient.ExistsAsync(cancellationToken);
        }

        public async Task FetchAttributesAsync(CancellationToken cancellationToken)
        {
            _properties = await _blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        }

        public async Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(CancellationToken cancellationToken)
        {
            await FetchAttributesAsync(cancellationToken);
            return new ReadOnlyDictionary<string, string>(_properties.Metadata);
        }

        public async Task<Stream> GetStreamAsync(CancellationToken cancellationToken)
        {
            // Should we use one of the wrappers here?
            Stream response = await _blobClient.OpenReadAsync(new BlobOpenReadOptions(false), cancellationToken);
            return response;
        }

        public async Task SetPropertiesAsync(IAccessCondition accessCondition, BlobRequestConditions blobRequestConditions, CloudBlobLocationMode? cloudBlobLocationMode)
        {
            // ??? Not sure how accessCondition is used here and first parameter is null
            await _blobClient.SetHttpHeadersAsync(null, blobRequestConditions);
            await _blobClient.SetMetadataAsync(null, blobRequestConditions);
        }
    }
}

