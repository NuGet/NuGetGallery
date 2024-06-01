// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Storage.Blobs.Models;

namespace NuGetGallery
{
    internal class CloudBlobReadOnlyProperties
    {
        public DateTime LastModifiedUtc { get; }
        public string ETag { get; }
        public long Length { get; }
        public bool IsSnapshot { get; }

        public CloudBlobReadOnlyProperties(BlobProperties blobProperties, bool isSnapshot = false)
        {
            LastModifiedUtc = blobProperties?.LastModified.UtcDateTime ?? DateTime.MinValue;
            ETag = blobProperties?.ETag.ToString();
            Length = blobProperties.ContentLength;
            IsSnapshot = isSnapshot;
        }

        public CloudBlobReadOnlyProperties(BlobItem blobItem)
        {
            LastModifiedUtc = blobItem.Properties?.LastModified?.UtcDateTime ?? DateTime.MinValue;
            ETag = blobItem.Properties?.ETag?.ToString();
            Length = blobItem.Properties?.ContentLength ?? 0;
            IsSnapshot = blobItem.Snapshot != null;
        }
    }
}
