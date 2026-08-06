// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class StagingBlobReference
    {
        public StagingBlobReference(
            string blobPath,
            string etag,
            string contentHash,
            long contentLength,
            StagingBlobType blobType)
        {
            BlobPath = blobPath ?? throw new ArgumentNullException(nameof(blobPath));
            ETag = etag ?? throw new ArgumentNullException(nameof(etag));
            ContentHash = contentHash ?? throw new ArgumentNullException(nameof(contentHash));
            ContentLength = contentLength;
            BlobType = blobType;
        }

        public string BlobPath { get; }

        public string ETag { get; }

        public string ContentHash { get; }

        public long ContentLength { get; }

        public StagingBlobType BlobType { get; }
    }
}
