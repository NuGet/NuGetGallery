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
            return IsGitHubUri(uri) || IsCodeplexUri(uri) || IsMicrosoftUri(uri);
        }

        public static bool IsGitHubUri(this Uri uri)
        {
            return uri.IsInDomain("github.com") ||
                   uri.Host.EndsWith(".github.io", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCodeplexUri(this Uri uri)
        {
            return uri.IsInDomain("codeplex.com");
        }

        private static bool IsMicrosoftUri(this Uri uri)
        {
            return uri.IsInDomain("microsoft.com") ||
                   uri.IsInDomain("asp.net") || 
                   uri.IsInDomain("msdn.com");
        }

        private static bool IsInDomain(this Uri uri, string domain)
        {
            return uri.Host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Host, domain, StringComparison.OrdinalIgnoreCase);
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