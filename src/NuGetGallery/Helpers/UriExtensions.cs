// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web;

namespace NuGetGallery
{
    public static class UriExtensions
    {
        public static bool IsHttpsProtocol(this Uri uri)
        {
            return uri.Scheme == Uri.UriSchemeHttps;
        }

        public static bool IsHttpProtocol(this Uri uri)
        {
            return uri.Scheme == Uri.UriSchemeHttp;
        }

        public static bool IsGitProtocol(this Uri uri)
        {
            return uri.Scheme == GalleryConstants.GitRepository;
        }

        public static bool IsDomainWithHttpsSupport(this Uri uri)
        {
            return IsGitHubUri(uri) || IsGitHubPagerUri(uri) || IsCodeplexUri(uri) || IsMicrosoftUri(uri);
        }

        public static bool IsGitHubUri(this Uri uri)
        {
            return string.Equals(uri.Host, "www.github.com", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsGitHubPagerUri(this Uri uri)
        {
            return uri.Authority.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase) ||
                   uri.Authority.EndsWith(".github.io", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCodeplexUri(this Uri uri)
        {
            return uri.Authority.EndsWith(".codeplex.com", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uri.Authority, "codeplex.com", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMicrosoftUri(this Uri uri)
        {
            return uri.Authority.EndsWith(".microsoft.com", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Authority, "microsoft.com", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Authority, "www.asp.net", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Authority, "asp.net", StringComparison.OrdinalIgnoreCase) ||
                   uri.Authority.EndsWith(".asp.net", StringComparison.OrdinalIgnoreCase) ||
                   uri.Authority.EndsWith(".msdn.com", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Authority, "msdn.com", StringComparison.OrdinalIgnoreCase);
        }

        public static Uri ToHttps(this Uri uri)
        {
            var uriBuilder = new UriBuilder(uri);
            uriBuilder.Scheme = Uri.UriSchemeHttps;
            uriBuilder.Port = -1;

            return uriBuilder.Uri;
        }

        public static string AppendQueryStringToRelativeUri(string relativeUrl, IReadOnlyCollection<KeyValuePair<string, string>> queryStringCollection)
        {
            var tempUri = new Uri("http://www.nuget.org/");
            var builder = new UriBuilder(new Uri(tempUri, relativeUrl));
            var query = HttpUtility.ParseQueryString(builder.Query);
            foreach (var pair in queryStringCollection)
            {
                query[pair.Key] = pair.Value;
            }

            builder.Query = query.ToString();
            return builder.Uri.PathAndQuery;
        }
    }
}