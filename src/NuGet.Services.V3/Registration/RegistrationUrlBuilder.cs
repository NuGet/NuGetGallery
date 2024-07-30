// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;

namespace NuGet.Protocol.Registration
{
    public static class RegistrationUrlBuilder
    {
        /// <summary>
        /// Builds a URL to a registration index. This pattern is documented.
        /// Source: https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource#registration-pages-and-leaves
        /// </summary>
        /// <param name="baseUrl">The base URL of the registration hive. Trailing slashes will be stripped.</param>
        /// <param name="id">The package ID.</param>
        public static string GetIndexUrl(string baseUrl, string id)
        {
            var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
            var lowerId = id.ToLowerInvariant();
            return $"{normalizedBaseUrl}{lowerId}/index.json";
        }

        /// <summary>
        /// Builds a URL to a registration leaf. This is a document that is specific to a package ID and version. Note
        /// that this URL in general should be discovered using the registration index. In the case of NuGet.org, the
        /// leaf URL has a specific pattern than can be generated without a lookup.
        /// </summary>
        /// <param name="baseUrl">The base URL of the registration hive. Trailing slashes will be stripped.</param>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version. It will be parsed and normalized.</param>
        public static string GetLeafUrl(string baseUrl, string id, string version)
        {
            var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
            var lowerId = id.ToLowerInvariant();
            var parsedVersion = NuGetVersion.Parse(version);
            var lowerVersion = parsedVersion.ToNormalizedString().ToLowerInvariant();
            return $"{normalizedBaseUrl}{lowerId}/{lowerVersion}.json";
        }

        private static string NormalizeBaseUrl(string baseUrl)
        {
            return baseUrl.TrimEnd('/') + '/';
        }
    }
}
