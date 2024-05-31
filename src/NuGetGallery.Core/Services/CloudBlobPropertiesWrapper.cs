// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Storage.Blobs.Specialized;

namespace NuGetGallery
{
    internal class CloudBlobPropertiesWrapper : ICloudBlobProperties
    {
        private readonly BlockBlobClient _blob;

        public CloudBlobPropertiesWrapper(BlockBlobClient blob)
        {
            _blob = blob ?? throw new ArgumentNullException(nameof(blob));
        }

        public DateTimeOffset? LastModified => _blob.Properties.LastModified;

        public long Length => _blob.Properties.Length;

        public string ContentType
        {
            get => _blob.Properties.ContentType;
            set => _blob.Properties.ContentType = value;
        }

        public string ContentEncoding
        {
            get => _blob.Properties.ContentEncoding;
            set => _blob.Properties.ContentEncoding = value;
        }

        public string CacheControl
        {
            get => _blob.Properties.CacheControl;
            set => _blob.Properties.CacheControl = value;
        }

        public string ContentMD5
        {
            get => _blob.Properties.ContentMD5;
            set => _blob.Properties.ContentMD5 = value;
        }
    }
}
