// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Storage.Blobs.Models;

namespace NuGetGallery
{
    internal class CloudBlobReadOnlyProperties
    {
        public DateTime LastModifiedUtc { get; }
        public long Length { get; }
        public bool IsSnapshot { get; }
        public CopyStatus? CopyStatus { get; }
        public string CopyStatusDescription { get; }

        public CloudBlobReadOnlyProperties(BlobProperties blobProperties, bool isSnapshot = false)
        {
            LastModifiedUtc = blobProperties.LastModified.UtcDateTime;
            Length = blobProperties.ContentLength;
            IsSnapshot = isSnapshot;
            CopyStatus = blobProperties.BlobCopyStatus;
            CopyStatusDescription = blobProperties.CopyStatusDescription;
        }

        public CloudBlobReadOnlyProperties(BlobItem blobItem)
        {
            LastModifiedUtc = blobItem.Properties?.LastModified?.UtcDateTime ?? DateTime.MinValue;
            Length = blobItem.Properties?.ContentLength ?? 0;
            IsSnapshot = blobItem.Snapshot != null;
            CopyStatus = null;
            CopyStatusDescription = null;
        }
    }
}
