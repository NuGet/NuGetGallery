// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Dnx
{
    internal static class DnxConstants
    {
        internal const string ApplicationOctetStreamContentType = "application/octet-stream";
        internal const string DefaultCacheControl = "max-age=120";

        internal static readonly IReadOnlyDictionary<string, string> RequiredBlobProperties = new Dictionary<string, string>()
        {
            { StorageConstants.CacheControl, DefaultCacheControl },
            { StorageConstants.ContentType, ApplicationOctetStreamContentType }
        };
    }
}