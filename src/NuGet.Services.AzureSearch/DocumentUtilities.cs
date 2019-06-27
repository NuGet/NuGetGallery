// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace NuGet.Services.AzureSearch
{
    public static class DocumentUtilities
    {
        public static string GetSearchFilterString(SearchFilters searchFilters)
        {
            return searchFilters.ToString();
        }

        public static string GetSearchDocumentKey(string packageId, SearchFilters searchFilters)
        {
            var lowerId = packageId.ToLowerInvariant();
            var encodedId = EncodeKey(lowerId);
            return $"{encodedId}-{GetSearchFilterString(searchFilters)}";
        }

        public static string GetHijackDocumentKey(string packageId, string normalizedVersion)
        {
            var lowerId = packageId.ToLowerInvariant();
            var lowerVersion = normalizedVersion.ToLowerInvariant();
            return EncodeKey($"{lowerId}/{lowerVersion}");
        }

        public static double GetDownloadScore(double totalDownloadCount)
        {
            // This score ranges from 0 to less than 100, assuming that the most downloaded
            // package has less than 500 million downloads. This scoring function increases
            // quickly at first and then becomes approximately linear near the upper bound.
            return Math.Sqrt(totalDownloadCount) / 220;
        }

        private static string EncodeKey(string rawKey)
        {
            // First, encode the raw value for uniqueness.
            var bytes = Encoding.UTF8.GetBytes(rawKey);
            var unique = HttpServerUtility.UrlTokenEncode(bytes);

            // Then, prepend a string as close to the raw key as possible, for readability.
            var readable = ReplaceUnsafeKeyCharacters(rawKey).TrimStart('_');

            return readable.Length > 0 ? $"{readable}-{unique}" : unique;
        }

        private static string ReplaceUnsafeKeyCharacters(string input)
        {
            return Regex.Replace(
                input,
                "[^A-Za-z0-9-_]", // Remove equal sign as well, since it's ugly.
                "_",
                RegexOptions.None,
                TimeSpan.FromSeconds(30));
        }
    }
}
