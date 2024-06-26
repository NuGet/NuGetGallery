// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Storage.Blobs.Models;

namespace NuGetGallery
{
    internal class CloudBlobPropertiesWrapper : ICloudBlobProperties
    {
        private readonly CloudBlobWrapper _blob;

        public CloudBlobPropertiesWrapper(CloudBlobWrapper cloudBlobWrapper)
        {
            _blob = cloudBlobWrapper ?? throw new ArgumentNullException(nameof(cloudBlobWrapper));
        }

        public DateTimeOffset? LastModified => _blob.BlobProperties.LastModifiedUtc;

        public long Length => _blob.BlobProperties.Length;

        public string ContentType
        {
            get => _blob.BlobHeaders.ContentType;
            set => SafeHeaders.ContentType = value;
        }

        public string ContentEncoding
        {
            get => _blob.BlobHeaders.ContentEncoding;
            set => SafeHeaders.ContentEncoding = value;
        }

        public string CacheControl
        {
            get => _blob.BlobHeaders.CacheControl;
            set => SafeHeaders.CacheControl = value;
        }

        public string ContentMD5
        {
            get => ToBase64String(_blob.BlobHeaders.ContentHash);
        }

        private BlobHttpHeaders SafeHeaders
        {
            get
            {
                if (_blob.BlobHeaders == null)
                {
                    _blob.BlobHeaders = new BlobHttpHeaders();
                }
                return _blob.BlobHeaders;
            }
        }

        private static string ToBase64String(byte[] array)
        {
            if (array == null)
            {
                return null;
            }
            return Convert.ToBase64String(array);
        }
    }
}
