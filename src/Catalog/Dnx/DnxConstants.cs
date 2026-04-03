// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Dnx
{
    public static class DnxConstants
    {
        internal const string ApplicationOctetStreamContentType = "application/octet-stream";
        internal const string DefaultCacheControl = "max-age=120";

        internal static readonly IReadOnlyDictionary<string, string> RequiredBlobProperties = new Dictionary<string, string>()
        {
            { StorageConstants.CacheControl, DefaultCacheControl },
            { StorageConstants.ContentType, ApplicationOctetStreamContentType }
        };

        // Default Cache Control of Package Version Index (at Storage)
        public const string DefaultCacheControlOfPackageVersionIndex = "max-age=10";

        // Cache Duration of Package Version Index (at CDN)
        public static readonly TimeSpan CacheDurationOfPackageVersionIndex = TimeSpan.FromSeconds(60);

        // Front Cursor with Updates
        // (MaxNumberOfUpdatesToKeepOfFrontCursor - 1) * MinIntervalBetweenTwoUpdatesOfFrontCursor > CacheDurationOfPackageVersionIndex
        public const int MaxNumberOfUpdatesToKeepOfFrontCursor = 31;
        public static readonly TimeSpan MinIntervalBetweenTwoUpdatesOfFrontCursor = TimeSpan.FromSeconds(60);
    }
}
