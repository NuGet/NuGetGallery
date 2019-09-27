// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    /// <summary>
    /// Produces the icon URL based on the service configuration.
    /// </summary>
    public class ConfigurationIconFileProvider : IIconUrlProvider
    {
        private readonly IFeatureFlagService _featureFlagService;
        private readonly IIconUrlTemplateProcessor _iconUrlTemplateProcessor;

        public ConfigurationIconFileProvider(IFeatureFlagService featureFlagService, IIconUrlTemplateProcessor iconUrlTemplateProcessor)
        {
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _iconUrlTemplateProcessor = iconUrlTemplateProcessor ?? throw new ArgumentNullException(nameof(iconUrlTemplateProcessor));
        }

        public string GetIconUrlString(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            var hasExternalIconUrlSpecified = !string.IsNullOrWhiteSpace(package.IconUrl);
            if (package.HasEmbeddedIcon || (_featureFlagService.IsForceFlatContainerIconsEnabled() && hasExternalIconUrlSpecified))
            {
                // never fall back to iconUrl in this case
                return GetIconUrlFromTemplate(package);
            }
            else
            {
                if (!hasExternalIconUrlSpecified)
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

        private string GetIconUrlFromTemplate(Package package)
        {
            var iconUrl = _iconUrlTemplateProcessor.Process(package);
            if (string.IsNullOrWhiteSpace(iconUrl))
            {
                return null;
            }

            return iconUrl;
        }
    }
}
