// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    /// <summary>
    /// Produces the icon URL based on the service configuration value of <see cref="IAppConfiguration.InternalIconUrlBaseAddress"/>.
    /// </summary>
    public class ConfigurationIconFileProvider : IIconUrlProvider
    {
        private readonly IAppConfiguration _configuration;

        public ConfigurationIconFileProvider(IAppConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public string GetIconUrlString(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (package.UsesIconFromFlatContainer)
            {
                if (string.IsNullOrWhiteSpace(_configuration.InternalIconUrlBaseAddress))
                {
                    return null;
                }

                // never fall back to iconUrl if UsesIconFromFlatContainer is true
                return ConstructInternalIconUrl(_configuration.InternalIconUrlBaseAddress, package.Id, package.NormalizedVersion);
            }
            else
            {
                if (_configuration.IgnoreIconUrl || string.IsNullOrWhiteSpace(package.IconUrl))
                {
                    return null;
                }

                return package.IconUrl;
            }
        }

        public Uri GetIconUrl(Package package)
        {
            var iconUrl = GetIconUrlString(package);
            if (iconUrl == null)
            {
                return null;
            }

            return new Uri(iconUrl);
        }

        private static string ConstructInternalIconUrl(string baseUrl, string packageId, string normalizedVersion)
        {
            string trailingSlash = baseUrl.EndsWith("/") ? string.Empty : "/";
            return $"{baseUrl}{trailingSlash}{packageId.ToLowerInvariant()}/{normalizedVersion.ToLowerInvariant()}/icon";
        }
    }
}
