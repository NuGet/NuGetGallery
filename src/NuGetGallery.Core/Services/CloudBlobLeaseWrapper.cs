// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure;
using Azure.Storage.Blobs.Models;

namespace NuGetGallery
{
    public class CloudBlobLeaseWrapper : ICloudBlobLease
    {
        private BlobLease _blobLease;

        internal CloudBlobLeaseWrapper(BlobLease blobLease)
        {
            _blobLease = blobLease ?? throw new ArgumentNullException(nameof(blobLease));
        }

        public ETag ETag => _blobLease.ETag;

        public DateTimeOffset LastModified => _blobLease.LastModified;

        public string LeaseId => _blobLease.LeaseId;

        public int? LeaseTime => _blobLease.LeaseTime;
    }
}
