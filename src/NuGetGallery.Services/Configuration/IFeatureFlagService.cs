﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IFeatureFlagService
    {
        /// <summary>
        /// Whether account deletes are performed asychronously or not.
        /// If true, account deletes will be attempted to be performed asychronously
        /// and fall back to old method if the async delete fails
        /// </summary>
        bool IsAsyncAccountDeleteEnabled();

        /// <summary>
        /// Whether account deletes requested by user are handled automatically or not.
        /// If true, user account deletes will be deleted automatically if possible
        /// and send alert mail to user if not.
        /// </summary>
        bool IsSelfServiceAccountDeleteEnabled();

        /// <summary>
        /// Whether typosquatting detection is enabled on package uploads. If true, new packages
        /// cannot have an id that is similar to existing packages' ids.
        /// </summary>
        bool IsTyposquattingEnabled();

        /// <summary>
        /// Whether typosquatting detection is enabled for a specific user's package uploads. If true,
        /// new packages from this user cannot have an id that is similar to existing packages' ids.
        /// </summary>
        /// <param name="user"></param>
        bool IsTyposquattingEnabled(User user);

        /// <summary>
        /// Whether the packages Atom feed is enabled. If true, users can subscribe to new package versions using a
        /// feed reader on a per package ID basis.
        /// </summary>
        bool IsPackagesAtomFeedEnabled();

        /// <summary>
        /// Whether or not the user can manage their package's deprecation state.
        /// </summary>
        bool IsManageDeprecationEnabled(User user, PackageRegistration registration);

        /// <summary>
        /// Whether or not the user can manage their package's deprecation state.
        /// </summary>
        bool IsManageDeprecationEnabled(User user, IEnumerable<Package> allVersions);

        /// <summary>
        /// Whether or not the user can manage their package's deprecation state through the API.
        /// </summary>
        bool IsManageDeprecationApiEnabled(User user);

        /// <summary>
        /// Whether the user is allowed to publish packages with an embedded icon.
        /// </summary>
        bool AreEmbeddedIconsEnabled(User user);

        /// <summary>
        /// Whether the icons are assumed to be present in flat container.
        /// </summary>
        /// <returns></returns>
        bool IsForceFlatContainerIconsEnabled();

        /// <summary>
        /// Whether the user is able to access the search side-by-side experiment.
        /// </summary>
        bool IsSearchSideBySideEnabled(User user);

        /// <summary>
        /// Whether a user can see the "GitHub Usage" section in a package's display page as well
        /// as the added "GitHub Usage count" in the "Statistics" section
        /// </summary>
        /// <param name="user">The user to test for the Flight</param>
        /// <returns>Whether or not the Flight is enabled for the user</returns>
        bool IsGitHubUsageEnabled(User user);

        /// <summary>
        /// Whether a user can see the "Advanced Search" panel in the packages list view as well as use the 
        /// advanced search request parameters
        /// </summary>
        /// <param name="user">The user to test for the Flight</param>
        /// <returns>Whether or not the Flight is enabled for the user</returns>
        bool IsAdvancedSearchEnabled(User user);

        /// <summary>
        /// Whether a user can see the "Package Dependents" section in a package's display page. Also, no
        /// data is gathered from the database
        /// </summary>
        /// <param name="user">The user to test for the Flight</param>
        /// <returns>Whether or not the Flight is enabled for the user</returns>
        bool IsPackageDependentsEnabled(User user);

        /// <summary>
        /// Whether the OData controllers use the read-only replica.
        /// </summary>
        bool IsODataDatabaseReadOnlyEnabled();

        /// <summary>
        /// Whether the user can participate in A/B tests.
        /// </summary>
        bool IsABTestingEnabled(User user);

        /// <summary>
        /// Whether using the preview search to hijack OData queries is enabled.
        /// </summary>
        bool IsPreviewHijackEnabled();

        /// <summary>
        /// Whether Gravatar images should be proxied.
        /// </summary>
        bool IsGravatarProxyEnabled();

        /// <summary>
        /// Whether the "en.gravatar.com" subdomain should be used to proxy Gravatar images.
        /// This is ignored if <see cref="IsGravatarProxyEnabled"/> is <see langword="false"/>.
        /// </summary>
        bool ProxyGravatarEnSubdomain();

        /// <summary>
        /// Whether or not to check the content object service for OData cache durations.
        /// </summary>
        bool AreDynamicODataCacheDurationsEnabled();

        /// <summary>
        /// Whether the qualified users should be shown the dialog box to enable multi-factor authentication
        /// </summary>
        bool IsShowEnable2FADialogEnabled();

        /// <summary>
        /// Whether we should get feedback from the users when they dismiss enabling multi-factor authentication
        /// </summary>
        bool IsGet2FADismissFeedbackEnabled();

        /// <summary>
        /// Whether we should enable the Usabilla feedback button on every page.
        /// </summary>
        bool IsUsabillaButtonEnabledOnEveryPage();

        /// <summary>
        /// Whether the user is able to see or manage the package renames information.
        /// </summary>
        bool IsPackageRenamesEnabled(User user);

        /// <summary>
        /// Whether the user is able to publish the package with an embedded readme file.
        /// </summary>
        bool AreEmbeddedReadmesEnabled(User user);

        /// <summary>
        /// Whether the /Packages() endpoint is enabled for the V1 OData API.
        /// </summary>
        bool IsODataV1GetAllEnabled();

        /// <summary>
        /// Whether the /Packages(Id=,Version=) endpoint is enabled for non-hijacked queries for the V1 OData API.
        /// </summary>
        bool IsODataV1GetSpecificNonHijackedEnabled();

        /// <summary>
        /// Whether the /FindPackagesById() endpoint is enabled for non-hijacked queries for the V1 OData API.
        /// </summary>
        bool IsODataV1FindPackagesByIdNonHijackedEnabled();

        /// <summary>
        /// Whether the /Search() endpoint is enabled for non-hijacked queries for the V1 OData API.
        /// </summary>
        bool IsODataV1SearchNonHijackedEnabled();

        /// <summary>
        /// Whether the /Packages() endpoint is enabled for non-hijacked queries for the V2 OData API.
        /// </summary>
        bool IsODataV2GetAllNonHijackedEnabled();

        /// <summary>
        /// Whether the /Packages(Id=,Version=) endpoint is enabled for non-hijacked queries for the V2 OData API.
        /// </summary>
        bool IsODataV2GetSpecificNonHijackedEnabled();

        /// <summary>
        /// Whether the /FindPackagesById() endpoint is enabled for non-hijacked queries for the V2 OData API.
        /// </summary>
        bool IsODataV2FindPackagesByIdNonHijackedEnabled();

        /// <summary>
        /// Whether the /Search() endpoint is enabled for non-hijacked queries for the V2 OData API.
        /// </summary>
        bool IsODataV2SearchNonHijackedEnabled();
    }
}
