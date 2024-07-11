// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGetGallery;

namespace NuGet.Jobs.Validation.Storage
{
    public class SimpleCloudBlobProvider : ISimpleCloudBlobProvider
    {
        private readonly ICloudBlobClient _blobClient;

        public SimpleCloudBlobProvider(ICloudBlobClient blobClient)
        {
            _blobClient = blobClient ?? throw new ArgumentNullException(nameof(blobClient));
        }

        public ISimpleCloudBlob GetBlobFromUrl(string blobUrl)
        {
            return _blobClient.GetBlobFromUri(new Uri(blobUrl));
        }
    }
}
