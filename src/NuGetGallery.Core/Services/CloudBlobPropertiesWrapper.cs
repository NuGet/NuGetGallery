// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    internal class CloudBlobPropertiesWrapper : ICloudBlobProperties
    {
        private readonly CloudBlobWrapper _blob;

        public CloudBlobPropertiesWrapper(CloudBlobWrapper cloudBlobWrapper)
        {
            _blob = cloudBlobWrapper ?? throw new ArgumentNullException(nameof(cloudBlobWrapper));
        }

        public DateTimeOffset? LastModified => _blob._blobProperties.LastModifiedUtc;

        public long Length => _blob._blobProperties.Length;

        public string ContentType
        {
            get => _blob._blobHeaders.ContentType;
            set => _blob._blobHeaders.ContentType = value;
        }

        public string ContentEncoding
        {
            get => _blob._blobHeaders.ContentEncoding;
            set => _blob._blobHeaders.ContentEncoding = value;
        }

        public string CacheControl
        {
            get => _blob._blobHeaders.CacheControl;
            set => _blob._blobHeaders.CacheControl = value;
        }

        public string ContentMD5
        {
            get => ToHexString(_blob._blobHeaders.ContentHash);
        }

        private static string ToHexString(byte[] array)
        {
            return BitConverter.ToString(array).Replace("-", "").ToLowerInvariant();
        }
    }
}
