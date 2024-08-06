// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web;

namespace NuGetGallery
{
    public static class UriExtensions
    {
        private static string ExternalLinkAnchorTagFormat = $"<a href=\"{{1}}\" target=\"_blank\">{{0}}</a>";

        public static string ToEncodedUrlStringOrNull(this Uri uri)
        {
            if (uri == null)
            {
                return null;
            }

            return uri.AbsoluteUri;
        }

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
            return uri.Scheme == ServicesConstants.GitRepository;
        }

        public static bool IsDomainWithHttpsSupport(this Uri uri)
        {
            return IsGitHubUri(uri) || 
                   IsGitHubPagerUri(uri) || 
                   IsCodeplexUri(uri) || 
                   IsMicrosoftUri(uri) || 
                   IsNuGetUri(uri);
        }

        public static bool IsGitHubUri(this Uri uri)
        {
            return string.Equals(uri.Host, "www.github.com", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGitHubPagerUri(this Uri uri)
        {
            return uri.Authority.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase) ||
                   uri.Authority.EndsWith(".github.io", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCodeplexUri(this Uri uri)
        {
            return uri.IsInDomain("codeplex.com");
        }

        private static bool IsMicrosoftUri(this Uri uri)
        {
            return uri.IsInDomain("microsoft.com") ||
                   uri.IsInDomain("asp.net") || 
                   uri.IsInDomain("msdn.com") ||
                   uri.IsInDomain("odata.org") ||
                   string.Equals(uri.Authority, "aka.ms", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Authority, "www.mono-project.com", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNuGetUri(this Uri uri)
        {
            return uri.IsInDomain("nuget.org") ||
                   uri.IsInDomain("nugettest.org");
        }

        private static bool IsInDomain(this Uri uri, string domain)
        {
            return uri.Authority.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Authority, domain, StringComparison.OrdinalIgnoreCase);
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

        public static Uri AppendPathToUri(this Uri uri, string pathToAppend, string queryString = null)
        {
            var builder = new UriBuilder(uri);
            builder.Path = builder.Path.TrimEnd('/') + "/" + pathToAppend.TrimStart('/');
            if (!string.IsNullOrEmpty(queryString))
            {
                builder.Query = queryString;
            }
            return builder.Uri;
    }

        public static string GetExternalUrlAnchorTag(string data, string link)
        {
            return string.Format(ExternalLinkAnchorTagFormat, data, link);
        }
    }
}