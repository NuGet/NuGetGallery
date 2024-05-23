// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery
{
    internal class CloudBlobPropertiesWrapper : ICloudBlobProperties
    {
        private readonly BlobProperties _blobProperties;

        public CloudBlobPropertiesWrapper(BlobProperties blobProperties)
        {
            _blobProperties = blobProperties ?? throw new ArgumentNullException(nameof(blobProperties));
        }

        public DateTimeOffset? LastModified => _blobProperties.LastModified;

        public long Length => _blobProperties.Length;

        public string ContentType
        {
            get => _blobProperties.ContentType;
            set => _blobProperties.ContentType = value;
        }
        public string CacheControl
        {
            get => _blobProperties.CacheControl;
            set => _blobProperties.CacheControl = value;
        }
        public string ContentMD5
        {
            get => _blobProperties.ContentMD5;
            set => _blobProperties.ContentMD5 = value;
        }
    }
}
