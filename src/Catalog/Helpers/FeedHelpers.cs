// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    /// <summary>
    /// Helper methods for accessing and parsing the gallery's V2 feed.
    /// </summary>
    public static class FeedHelpers
    {
        /// <summary>
        /// Creates an HttpClient for reading the feed.
        /// </summary>
        public static HttpClient CreateHttpClient(Func<HttpMessageHandler> handlerFunc)
        {
            var handler = (handlerFunc != null) ? handlerFunc() : new WebRequestHandler { AllowPipelining = true };
            return new HttpClient(handler);
        }
    }
}