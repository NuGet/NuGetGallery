// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    internal class CloudBlobCopyState : ICloudBlobCopyState
    {
        private readonly CloudBlobWrapper _blob;

        public CloudBlobCopyState(CloudBlobWrapper blob)
        {
            _blob = blob ?? throw new ArgumentNullException(nameof(blob));
        }
        public CloudBlobCopyStatus Status => CloudWrapperHelpers.GetBlobCopyStatus(_blob.BlobProperties?.CopyStatus);

        public string StatusDescription => _blob.BlobProperties?.CopyStatusDescription;
    }
}
