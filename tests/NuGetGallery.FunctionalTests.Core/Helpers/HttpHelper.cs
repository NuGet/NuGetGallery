// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGetGallery.FunctionalTests
{
    /// <summary>
    /// General HTTP utility methods for functional tests.
    /// </summary>
    public static class HttpHelper
    {
        /// <summary>
        /// Sends an HTTP request and manually follows any redirect response.
        /// .NET 9+ unconditionally blocks HTTPS-to-HTTP redirect downgrades in SocketsHttpHandler.
        /// </summary>
        public static async Task<HttpResponseMessage> SendFollowingRedirectsAsync(
            HttpClient client, HttpRequestMessage request)
        {
            var response = await client.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.Found ||
                response.StatusCode == HttpStatusCode.MovedPermanently)
            {
                var redirectUri = response.Headers.Location;
                response.Dispose();
                response = await client.GetAsync(redirectUri);
            }
            return response;
        }
    }
}
