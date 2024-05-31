// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Storage.Blobs.Specialized;

namespace NuGetGallery
{
    internal class CloudBlobCopyState : ICloudBlobCopyState
    {
        private readonly BlockBlobClient _blob;

        public CloudBlobCopyState(BlockBlobClient blob)
        {
            _blob = blob ?? throw new ArgumentNullException(nameof(blob));
        }
        public CloudBlobCopyStatus Status => CloudWrapperHelpers.GetBlobCopyStatus(_blob.CopyState.Status);

        public string StatusDescription => _blob.CopyState.StatusDescription;
    }
}
