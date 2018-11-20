// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public sealed class AzureCloudBlockBlob : ICloudBlockBlob
    {
        private readonly CloudBlockBlob _blob;

        /// <summary>
        /// The Base64 encoded MD5 hash of the blob's content
        /// </summary>
        public string ContentMD5
        {
            get => _blob.Properties.ContentMD5;
            set => _blob.Properties.ContentMD5 = value;
        }

        public string ETag => _blob.Properties.ETag;
        public long Length => _blob.Properties.Length;
        public Uri Uri => _blob.Uri;

        public AzureCloudBlockBlob(CloudBlockBlob blob)
        {
            _blob = blob ?? throw new ArgumentNullException(nameof(blob));
        }

        public Task<bool> ExistsAsync(CancellationToken cancellationToken)
        {
            return _blob.ExistsAsync();
        }

        public async Task FetchAttributesAsync(CancellationToken cancellationToken)
        {
            await _blob.FetchAttributesAsync(cancellationToken);
        }

        public Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                new ReadOnlyDictionary<string, string>(_blob.Metadata));
        }

        public async Task<Stream> GetStreamAsync(CancellationToken cancellationToken)
        {
            return await _blob.OpenReadAsync(cancellationToken);
        }

        public async Task SetPropertiesAsync(AccessCondition accessCondition, BlobRequestOptions options, OperationContext operationContext)
        {
            await _blob.SetPropertiesAsync(accessCondition, options, operationContext);
        }
    }
}