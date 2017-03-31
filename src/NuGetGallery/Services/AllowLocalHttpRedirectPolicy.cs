// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    /// <summary>
    /// Redirect policy that allows potentially unsafe redirects to localhost URLs
    /// (for debugging purposes only, not to be used in production).
    /// </summary>
    public class AllowLocalHttpRedirectPolicy : ISourceDestinationRedirectPolicy
    {
        public bool IsAllowed(Uri sourceUrl, Uri destinationUrl)
        {
            return sourceUrl.Scheme == Uri.UriSchemeHttp
                || (sourceUrl.Scheme == Uri.UriSchemeHttps
                    && (destinationUrl.Scheme == Uri.UriSchemeHttps || IsLocalhost(destinationUrl))
                   );
        }

        private static bool IsLocalhost(Uri url)
        {
            // technically, any address in 127.0.0.1/8 subnet is "localhost",
            // but it's not widely used and complicates the check, so not checking
            // it here.
            return url.Host == "127.0.0.1" || url.Host == "localhost" || url.Host == "[::1]";
        }
    }
}