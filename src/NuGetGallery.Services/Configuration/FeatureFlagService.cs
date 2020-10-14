﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGet.Services.FeatureFlags;
using NuGetGallery.Features;

namespace NuGetGallery
{
    public class FeatureFlagService : IFeatureFlagService
    {
        private const string GalleryPrefix = "NuGetGallery.";

        private const string ABTestingFlightName = GalleryPrefix + "ABTesting";
        private const string AsyncAccountDeleteFeatureName = GalleryPrefix + "AsyncAccountDelete";
        private const string SelfServiceAccountDeleteFeatureName = GalleryPrefix + "SelfServiceAccountDelete";
        private const string EmbeddedIconFlightName = GalleryPrefix + "EmbeddedIcons";
        private const string ForceFlatContainerIconsFeatureName = GalleryPrefix + "ForceFlatContainerIcons";
        private const string GitHubUsageFlightName = GalleryPrefix + "GitHubUsage";
        private const string AdvancedSearchFlightName = GalleryPrefix + "AdvancedSearch";
        private const string PackageDependentsFlightName = GalleryPrefix + "PackageDependents";
        private const string ManageDeprecationFeatureName = GalleryPrefix + "ManageDeprecation";
        private const string ManageDeprecationForManyVersionsFeatureName = GalleryPrefix + "ManageDeprecationMany";
        private const string ManageDeprecationApiFeatureName = GalleryPrefix + "ManageDeprecationApi";
        private const string ODataReadOnlyDatabaseFeatureName = GalleryPrefix + "ODataReadOnlyDatabase";
        private const string PackagesAtomFeedFeatureName = GalleryPrefix + "PackagesAtomFeed";
        private const string SearchSideBySideFlightName = GalleryPrefix + "SearchSideBySide";
        private const string TyposquattingFeatureName = GalleryPrefix + "Typosquatting";
        private const string TyposquattingFlightName = GalleryPrefix + "TyposquattingFlight";
        private const string PreviewHijackFeatureName = GalleryPrefix + "PreviewHijack";
        private const string GravatarProxyFeatureName = GalleryPrefix + "GravatarProxy";
        private const string GravatarProxyEnSubdomainFeatureName = GalleryPrefix + "GravatarProxyEnSubdomain";
        private const string ODataCacheDurationsFeatureName = GalleryPrefix + "ODataCacheDurations";
        private const string ShowEnable2FADialog = GalleryPrefix + "ShowEnable2FADialog";
        private const string Get2FADismissFeedback = GalleryPrefix + "Get2FADismissFeedback";
        private const string UsabillaOnEveryPageFeatureName = GalleryPrefix + "UsabillaEveryPage";
        private const string PackageRenamesFeatureName = GalleryPrefix + "PackageRenames";
        private const string EmbeddedReadmeFlightName = GalleryPrefix + "EmbeddedReadmes";
        private const string LicenseMdRenderingFlightName = GalleryPrefix + "LicenseMdRendering";

        private const string ODataV1GetAllNonHijackedFeatureName = GalleryPrefix + "ODataV1GetAllNonHijacked";
        private const string ODataV1GetAllCountNonHijackedFeatureName = GalleryPrefix + "ODataV1GetAllCountNonHijacked";
        private const string ODataV1GetSpecificNonHijackedFeatureName = GalleryPrefix + "ODataV1GetSpecificNonHijacked";
        private const string ODataV1FindPackagesByIdNonHijackedFeatureName = GalleryPrefix + "ODataV1FindPackagesByIdNonHijacked";
        private const string ODataV1FindPackagesByIdCountNonHijackedFeatureName = GalleryPrefix + "ODataV1FindPackagesByIdCountNonHijacked";
        private const string ODataV1SearchNonHijackedFeatureName = GalleryPrefix + "ODataV1SearchNonHijacked";
        private const string ODataV1SearchCountNonHijackedFeatureName = GalleryPrefix + "ODataV1SearchCountNonHijacked";

        private const string ODataV2GetAllNonHijackedFeatureName = GalleryPrefix + "ODataV2GetAllNonHijacked";
        private const string ODataV2GetAllCountNonHijackedFeatureName = GalleryPrefix + "ODataV2GetAllCountNonHijacked";
        private const string ODataV2GetSpecificNonHijackedFeatureName = GalleryPrefix + "ODataV2GetSpecificNonHijacked";
        private const string ODataV2FindPackagesByIdNonHijackedFeatureName = GalleryPrefix + "ODataV2FindPackagesByIdNonHijacked";
        private const string ODataV2FindPackagesByIdCountNonHijackedFeatureName = GalleryPrefix + "ODataV2FindPackagesByIdCountNonHijacked";
        private const string ODataV2SearchNonHijackedFeatureName = GalleryPrefix + "ODataV2SearchNonHijacked";
        private const string ODataV2SearchCountNonHijackedFeatureName = GalleryPrefix + "ODataV2SearchCountNonHijacked";

        private readonly IFeatureFlagClient _client;

        public FeatureFlagService(IFeatureFlagClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public bool IsAsyncAccountDeleteEnabled()
        {
            return _client.IsEnabled(AsyncAccountDeleteFeatureName, defaultValue: false);
        }

        public bool IsSelfServiceAccountDeleteEnabled()
        {
            return _client.IsEnabled(SelfServiceAccountDeleteFeatureName, defaultValue: false);
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
            if (registration == null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            return IsManageDeprecationEnabled(user, registration.Packages);
        }

        public bool IsManageDeprecationEnabled(User user, IEnumerable<Package> allVersions)
        {
            if (allVersions == null)
            {
                throw new ArgumentNullException(nameof(allVersions));
            }

            if (!_client.IsEnabled(ManageDeprecationFeatureName, user, defaultValue: false))
            {
                return false;
            }

            return allVersions.Count() < _manageDeprecationForManyVersionsThreshold
                || _client.IsEnabled(ManageDeprecationForManyVersionsFeatureName, user, defaultValue: true);
        }

        public bool IsManageDeprecationApiEnabled(User user)
        {
            return _client.IsEnabled(ManageDeprecationApiFeatureName, user, defaultValue: false);
        }

        public bool AreEmbeddedIconsEnabled(User user)
        {
            return _client.IsEnabled(EmbeddedIconFlightName, user, defaultValue: false);
        }

        public bool IsForceFlatContainerIconsEnabled()
        {
            return _client.IsEnabled(ForceFlatContainerIconsFeatureName, defaultValue: false);
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

        public bool IsAdvancedSearchEnabled(User user)
        {
            return _client.IsEnabled(AdvancedSearchFlightName, user, defaultValue: false);
        }

        public bool IsPackageDependentsEnabled(User user)
        {
            return _client.IsEnabled(PackageDependentsFlightName, user, defaultValue: false);
        }

        public bool IsABTestingEnabled(User user)
        {
            return _client.IsEnabled(ABTestingFlightName, user, defaultValue: false);
        }

        public bool IsPreviewHijackEnabled()
        {
            return _client.IsEnabled(PreviewHijackFeatureName, defaultValue: false);
        }

        public bool IsGravatarProxyEnabled()
        {
            return _client.IsEnabled(GravatarProxyFeatureName, defaultValue: false);
        }

        public bool ProxyGravatarEnSubdomain()
        {
            return _client.IsEnabled(GravatarProxyEnSubdomainFeatureName, defaultValue: false);
        }

        public bool AreDynamicODataCacheDurationsEnabled()
        {
            return _client.IsEnabled(ODataCacheDurationsFeatureName, defaultValue: false);
        }

        public bool IsShowEnable2FADialogEnabled()
        {
            return _client.IsEnabled(ShowEnable2FADialog, defaultValue: false);
        }

        public bool IsGet2FADismissFeedbackEnabled()
        {
            return _client.IsEnabled(Get2FADismissFeedback, defaultValue: false);
        }

        public bool IsUsabillaButtonEnabledOnEveryPage()
        {
            return _client.IsEnabled(UsabillaOnEveryPageFeatureName, defaultValue: false);
        }

        public bool IsPackageRenamesEnabled(User user)
        {
            return _client.IsEnabled(PackageRenamesFeatureName, user, defaultValue: false);
        }

        public bool AreEmbeddedReadmesEnabled(User user)
        {
            return _client.IsEnabled(EmbeddedReadmeFlightName, user, defaultValue: false);
        }

        public bool IsODataV1GetAllEnabled()
        {
            return _client.IsEnabled(ODataV1GetAllNonHijackedFeatureName, defaultValue: true);
        }

        public bool IsODataV1GetAllCountEnabled()
        {
            return _client.IsEnabled(ODataV1GetAllCountNonHijackedFeatureName, defaultValue: true);
        }

        public bool IsODataV1GetSpecificNonHijackedEnabled()
        {
            return _client.IsEnabled(ODataV1GetSpecificNonHijackedFeatureName, defaultValue: true);
        }

        public bool IsODataV1FindPackagesByIdNonHijackedEnabled()
        {
            return _client.IsEnabled(ODataV1FindPackagesByIdNonHijackedFeatureName, defaultValue: true);
        }

        public bool IsODataV1FindPackagesByIdCountNonHijackedEnabled()
        {
            return _client.IsEnabled(ODataV1FindPackagesByIdCountNonHijackedFeatureName, defaultValue: true);
        }

        public bool IsODataV1SearchNonHijackedEnabled()
        {
            return _client.IsEnabled(ODataV1SearchNonHijackedFeatureName, defaultValue: true);
        }

        public bool IsODataV1SearchCountNonHijackedEnabled()
        {
            return _client.IsEnabled(ODataV1SearchCountNonHijackedFeatureName, defaultValue: true);
        }

        public bool IsODataV2GetAllNonHijackedEnabled()
        {
            return _client.IsEnabled(ODataV2GetAllNonHijackedFeatureName, defaultValue: true);
        }

        public bool IsODataV2GetAllCountNonHijackedEnabled()
        {
            return _client.IsEnabled(ODataV2GetAllCountNonHijackedFeatureName, defaultValue: true);
        }

        public bool IsODataV2GetSpecificNonHijackedEnabled()
        {
            return _client.IsEnabled(ODataV2GetSpecificNonHijackedFeatureName, defaultValue: true);
        }

        public bool IsODataV2FindPackagesByIdNonHijackedEnabled()
        {
            return _client.IsEnabled(ODataV2FindPackagesByIdNonHijackedFeatureName, defaultValue: true);
        }

        public bool IsODataV2FindPackagesByIdCountNonHijackedEnabled()
        {
            return _client.IsEnabled(ODataV2FindPackagesByIdCountNonHijackedFeatureName, defaultValue: true);
        }

        public bool IsODataV2SearchNonHijackedEnabled()
        {
            return _client.IsEnabled(ODataV2SearchNonHijackedFeatureName, defaultValue: true);
        }

        public bool IsLicenseMdRenderingEnabled(User user)
        {
            return _client.IsEnabled(LicenseMdRenderingFlightName, user, defaultValue: false);
        }

        public bool IsODataV2SearchCountNonHijackedEnabled()
        {
            return _client.IsEnabled(ODataV2SearchCountNonHijackedFeatureName, defaultValue: true);
        }
    }
}