// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
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
        private const string SearchSideBySideFlightName = GalleryPrefix + "SearchSideBySide";
        private const string GitHubUsageFlightName = GalleryPrefix + "GitHubUsage";

        private const string PackagesAtomFeedFeatureName = GalleryPrefix + "PackagesAtomFeed";

        private const string ManageDeprecationFeatureName = GalleryPrefix + "ManageDeprecation";
        private const string ManageDeprecationForManyVersionsFeatureName = GalleryPrefix + "ManageDeprecationMany";
        private const string ODataReadOnlyDatabaseFeatureName = GalleryPrefix + "ODataReadOnlyDatabase";

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

        /// <summary>
        /// The number of versions a package needs to have before it should be flighted using <see cref="ManageDeprecationForManyVersionsFeatureName"/> instead of <see cref="ManageDeprecationFeatureName"/>.
        /// </summary>
        private const int _manageDeprecationForManyVersionsThreshold = 500;

        public bool IsManageDeprecationEnabled(User user, PackageRegistration registration)
        {
            var isEnabled = _client.IsEnabled(ManageDeprecationFeatureName, user, defaultValue: false);
            return registration.Packages.Count() > _manageDeprecationForManyVersionsThreshold
                ? _client.IsEnabled(ManageDeprecationForManyVersionsFeatureName, user, defaultValue: isEnabled)
                : isEnabled;
        }

        public bool AreEmbeddedIconsEnabled(User user)
        {
            return _client.IsEnabled(EmbeddedIconFlightName, user, defaultValue: false);
        }

        private bool IsEnabled(string flight, User user, bool defaultValue)
        {
            return _client.IsEnabled(flight, user, defaultValue);
        }

        public bool IsODataDatabaseReadOnlyEnabled()
        {
            return _client.IsEnabled(ODataReadOnlyDatabaseFeatureName, defaultValue: false);
        }

        public bool IsSearchSideBySideEnabled(User user)
        {
            return _client.IsEnabled(SearchSideBySideFlightName, user, defaultValue: false);
        }

        public bool IsGitHubUsageEnabled(User user)
        {
            return _client.IsEnabled(GitHubUsageFlightName, user, defaultValue: false);
        }
    }
}