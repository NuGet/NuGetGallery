// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
        private readonly IIconUrlTemplateProcessor _iconUrlTemplateProcessor;

        public ConfigurationIconFileProvider(IAppConfiguration configuration, IIconUrlTemplateProcessor iconUrlTemplateProcessor)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _iconUrlTemplateProcessor = iconUrlTemplateProcessor ?? throw new ArgumentNullException(nameof(iconUrlTemplateProcessor));
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
                    // never fall back to iconUrl if UsesIconFromFlatContainer is true
                    return null;
                }

                return _iconUrlTemplateProcessor.Process(package);
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
    }
}
