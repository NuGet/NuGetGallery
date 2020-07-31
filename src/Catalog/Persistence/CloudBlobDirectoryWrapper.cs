// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class CloudBlobDirectoryWrapper : ICloudBlobDirectory
    {
        private readonly CloudBlobDirectory _blobDirectory;

        public ICloudBlockBlobClient ServiceClient => new CloudBlockBlobClientWrapper(_blobDirectory.ServiceClient);
        public CloudBlobContainer Container => _blobDirectory.Container;
        public Uri Uri => _blobDirectory.Uri;

        public CloudBlobDirectoryWrapper(CloudBlobDirectory blobDirectory)
        {
            _blobDirectory = blobDirectory ?? throw new ArgumentNullException(nameof(blobDirectory));
        }

        public CloudBlockBlob GetBlockBlobReference(string blobName)
        {
            return _blobDirectory.GetBlockBlobReference(blobName);
        }

        public async Task<IEnumerable<IListBlobItem>> ListBlobsAsync(CancellationToken cancellationToken)
        {
            return await _blobDirectory.ListBlobsAsync(cancellationToken);
        }
    }
}