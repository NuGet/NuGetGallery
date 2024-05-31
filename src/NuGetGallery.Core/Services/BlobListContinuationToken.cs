// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    internal class BlobListContinuationToken : IBlobListContinuationToken
    {
        public BlobListContinuationToken(string continuationToken)
        {
            ContinuationToken = continuationToken;
        }
        public string ContinuationToken { get; }
    }
}
