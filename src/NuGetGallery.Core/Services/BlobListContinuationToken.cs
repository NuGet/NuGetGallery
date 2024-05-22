// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery
{
    internal class BlobListContinuationToken : IBlobListContinuationToken
    {
        public BlobListContinuationToken(BlobContinuationToken continuationToken)
        {
            ContinuationToken = continuationToken;
        }
        public BlobContinuationToken ContinuationToken { get; }
    }
}
