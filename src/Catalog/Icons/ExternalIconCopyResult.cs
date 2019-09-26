// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog.Icons
{
    public class ExternalIconCopyResult
    {
        public static ExternalIconCopyResult Success(Uri sourceUrl, Uri storageUrl)
        {
            return new ExternalIconCopyResult
            {
                SourceUrl = sourceUrl,
                StorageUrl = storageUrl,
            };
        }

        public static ExternalIconCopyResult Fail(Uri sourceUrl)
        {
            return new ExternalIconCopyResult
            {
                SourceUrl = sourceUrl,
                StorageUrl = null
            };
        }

        public Uri SourceUrl { get; set; }
        public Uri StorageUrl { get; set; }
        public bool IsCopySucceeded => StorageUrl != null;
    }
}
