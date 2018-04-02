// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery;

namespace NuGet.Jobs.Validation.Storage
{
    public class SimpleCloudBlobProvider : ISimpleCloudBlobProvider
    {
        public ISimpleCloudBlob GetBlobFromUrl(string blobUrl)
        {
            var blobUri = new Uri(blobUrl);
            var sasToken = blobUri.Query;
            var blobUriBuilder = new UriBuilder(blobUri) { Query = null };

            CloudBlockBlob innerBlob;
            if (string.IsNullOrEmpty(sasToken))
            {
                innerBlob = new CloudBlockBlob(blobUriBuilder.Uri);
            }
            else
            {
                innerBlob = new CloudBlockBlob(
                    blobUriBuilder.Uri,
                    new StorageCredentials(sasToken));
            }

            return new CloudBlobWrapper(innerBlob);
        }
    }
}
