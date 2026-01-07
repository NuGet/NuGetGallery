// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Options;
using NuGet.Services;
using NuGet.Versioning;

namespace NuGet.Jobs.Catalog2Registration
{
    public class RegistrationUrlBuilder
    {
        private readonly IOptionsSnapshot<Catalog2RegistrationConfiguration> _options;
        private readonly string _legacyBaseUrl;
        private readonly string _gzippedBaseUrl;
        private readonly string _semver2BaseUrl;

        public RegistrationUrlBuilder(IOptionsSnapshot<Catalog2RegistrationConfiguration> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));

            _legacyBaseUrl = EnsureTrailingSlash(_options.Value.LegacyBaseUrl);
            _gzippedBaseUrl = EnsureTrailingSlash(_options.Value.GzippedBaseUrl);
            _semver2BaseUrl = EnsureTrailingSlash(_options.Value.SemVer2BaseUrl);
        }

        public string GetIndexPath(string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            return $"{Uri.EscapeUriString(id.ToLowerInvariant())}/index.json";
        }

        public string GetIndexUrl(HiveType hive, string id)
        {
            return GetBaseUrl(hive) + GetIndexPath(id);
        }

        public string GetInlinedPageUrl(HiveType hive, string id, NuGetVersion lower, NuGetVersion upper)
        {
            return GetIndexUrl(hive, id) + "#" + GetPageFragment(lower, upper);
        }

        public string ConvertHive(HiveType fromHive, HiveType toHive, string url)
        {
            var path = ConvertToPath(fromHive, url);
            return GetBaseUrl(toHive) + path;
        }

        public string ConvertToPath(HiveType hive, string url)
        {
            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            var baseUrl = GetBaseUrl(hive);
            Guard.Assert(url.StartsWith(baseUrl), $"URL '{url}' does not start with expected base URL '{baseUrl}'.");
            return url.Substring(baseUrl.Length);
        }

        public string GetPageUrl(HiveType hive, string id, NuGetVersion lower, NuGetVersion upper)
        {
            return GetBaseUrl(hive) + GetPagePath(id, lower, upper);
        }

        public string GetPagePath(string id, NuGetVersion lower, NuGetVersion upper)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            return $"{Uri.EscapeUriString(id.ToLowerInvariant())}/{GetPageFragment(lower, upper)}.json";
        }

        private static string GetPageFragment(NuGetVersion lower, NuGetVersion upper)
        {
            if (lower == null)
            {
                throw new ArgumentNullException(nameof(lower));
            }

            if (upper == null)
            {
                throw new ArgumentNullException(nameof(upper));
            }

            return
                $"page/" +
                $"{Uri.EscapeUriString(lower.ToNormalizedString().ToLowerInvariant())}/" +
                $"{Uri.EscapeUriString(upper.ToNormalizedString().ToLowerInvariant())}";
        }

        public string GetLeafPath(string id, NuGetVersion version)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            return
                $"{Uri.EscapeUriString(id.ToLowerInvariant())}/" +
                $"{Uri.EscapeUriString(version.ToNormalizedString().ToLowerInvariant())}.json";
        }

        public string GetLeafUrl(HiveType hive, string id, NuGetVersion version)
        {
            return GetBaseUrl(hive) + GetLeafPath(id, version);
        }

        private string GetBaseUrl(HiveType hive)
        {
            switch (hive)
            {
                case HiveType.Legacy:
                    return _legacyBaseUrl;
                case HiveType.Gzipped:
                    return _gzippedBaseUrl;
                case HiveType.SemVer2:
                    return _semver2BaseUrl;
                default:
                    throw new NotImplementedException($"The hive type '{hive}' does not have a configured base URL.");
            }
        }

        private static string EnsureTrailingSlash(string baseUrl)
        {
            if (baseUrl == null)
            {
                throw new ArgumentNullException(nameof(baseUrl));
            }

            return baseUrl.TrimEnd('/') + '/';
        }
    }
}
