// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using NuGet.Services.FeatureFlags;
using NuGetGallery.Features;

namespace NuGetGallery
{
    public class FeatureFlagService : IFeatureFlagService
    {
        private const string GalleryPrefix = "NuGetGallery.";

        // Typosquatting detection
        private const string TyposquattingFeatureName = GalleryPrefix + "Typosquatting";
        private const string TyposquattingFlightName = GalleryPrefix + "TyposquattingFlight";
        private const string EmbeddedIconFlightName = GalleryPrefix + "EmbeddedIcons";

        private const string PackagesAtomFeedFeatureName = GalleryPrefix + "PackagesAtomFeed";

        private const string ManageDeprecationFeatureName = GalleryPrefix + "ManageDeprecation";
        private const string SearchCircuitBreakerFeatureName = GalleryPrefix + "SearchCircuitBreaker";

        private readonly IFeatureFlagClient _client;

        public FeatureFlagService(IFeatureFlagClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public bool IsTyposquattingEnabled()
        {
            return _client.IsEnabled(TyposquattingFeatureName, defaultValue: false);
        }

        public bool IsTyposquattingEnabled(User user)
        {
            return _client.IsEnabled(TyposquattingFlightName, user, defaultValue: false);
        }

        public bool IsPackagesAtomFeedEnabled()
        {
            return _client.IsEnabled(PackagesAtomFeedFeatureName, defaultValue: false);
        }

        public bool IsManageDeprecationEnabled(User user)
        {
            return _client.IsEnabled(ManageDeprecationFeatureName, user, defaultValue: false);
        }

        public bool IsEmbeddedIconsEnabled(User user)
        {
            return _client.IsEnabled(EmbeddedIconFlightName, user, defaultValue: false);
        }

        private bool IsEnabled(string flight, User user, bool defaultValue)
        {
            return _client.IsEnabled(flight, user, defaultValue);
        }

        public bool IsSearchCircuitBreakerEnabled()
        {
            return _client.IsEnabled(SearchCircuitBreakerFeatureName, defaultValue: false);
        }
    }
}