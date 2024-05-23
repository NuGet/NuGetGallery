// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery
{
    internal class CloudBlobPropertiesWrapper : ICloudBlobProperties
    {
        private readonly CloudBlockBlob _blob;

        public CloudBlobPropertiesWrapper(CloudBlockBlob blob)
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
