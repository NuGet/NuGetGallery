// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Storage.Blobs;

namespace NuGetGallery
{
    public static class ConnectionStringExtensions
    {
        public static Uri GetBlobEndpointFromConnectionString(string connectionString)
        {
            var tempClient = new BlobServiceClient(connectionString);
            // if _storageConnectionString has SAS token, Uri will contain SAS signature, we need to strip it
            return new Uri(tempClient.Uri.GetLeftPart(UriPartial.Path));
        }
    }
}
