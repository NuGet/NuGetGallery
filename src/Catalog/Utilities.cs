// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog
{
    public static class Utilities
    {
        public static Uri GetNugetCacheBustingUri(Uri originalUri)
        {
            return GetNugetCacheBustingUri(originalUri, DateTime.UtcNow.ToString());
        }

        /// <summary>
        /// Adding the timestamp to the URI as a query string ensures a cache miss with the CDN
        /// </summary>
        /// <returns>Returns a URI which ensures a cache miss with the CDN</returns>
        public static Uri GetNugetCacheBustingUri(Uri originalUri, string timestamp)
        {
            var uriBuilder = new UriBuilder(originalUri);
            uriBuilder.Query = "nuget-cache=" + timestamp;
            return uriBuilder.Uri;
        }
    }
}
