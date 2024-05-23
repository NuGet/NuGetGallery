// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery
{
    internal class CloudBlobCopyState : ICloudBlobCopyState
    {
        private readonly CloudBlockBlob _blob;

        public CloudBlobCopyState(CloudBlockBlob blob)
        {
            _blob = blob ?? throw new ArgumentNullException(nameof(blob));
        }
        public CloudBlobCopyStatus Status => CloudWrapperHelpers.GetBlobCopyStatus(_blob.CopyState.Status);

        public string StatusDescription => _blob.CopyState.StatusDescription;
    }
}
