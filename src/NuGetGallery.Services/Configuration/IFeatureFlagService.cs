// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IFeatureFlagService
    {
        /// <summary>
        /// Whether downloads.v1.json hould be pulled from primary or secondary location.
        /// If true, the secondary location will be used to download downloads.v1.json.
        /// </summary>
        bool IsAlternateStatisticsSourceEnabled();

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
        /// Whether or not a package owner can view vulnerability advisory information on their package.
        /// </summary>
        bool IsDisplayVulnerabilitiesEnabled();

        /// <summary>
        /// Whether or not a package owner can view vulnerability advisory information on the Manage Packages page.
        /// </summary>
        bool IsManagePackagesVulnerabilitiesEnabled();

        /// <summary>
        /// Whether or not a fuget.org link is visible on a package's details page.
        /// </summary>
        bool IsDisplayFuGetLinksEnabled();

        /// <summary>
        /// Whether or not a dndocs.com link is visible on a package's details page.
        /// </summary>
        bool IsDisplayDNDocsLinksEnabled();

        /// <summary>
        /// Whether or not a nugettrends.com link is visible on a package's details page.
        /// </summary>
        bool IsDisplayNuGetTrendsLinksEnabled();

        /// <summary>
        /// Whether or not a nuget.info (NuGet Package Explorer) link is visible on a package's details page.
        /// </summary>
        bool IsDisplayNuGetPackageExplorerLinkEnabled();

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
        /// Whether the user is able to see or manage the package renames information.
        /// </summary>
        bool IsPackageRenamesEnabled(User user);

        /// <summary>
        /// Whether we're using pattern set based TFM determination on ingested packages
        /// </summary>
        bool ArePatternSetTfmHeuristicsEnabled();

        /// <summary>
        /// Whether the user is able to publish the package with an embedded readme file.
        /// </summary>
        bool AreEmbeddedReadmesEnabled(User user);

        /// <summary>
        /// Whether the /Packages() endpoint is enabled for the V1 OData API.
        /// </summary>
        bool IsODataV1GetAllEnabled();

        /// <summary>
        /// Whether the /Packages()/$count endpoint is enabled for the V1 OData API.
        /// </summary>
        bool IsODataV1GetAllCountEnabled();

        /// <summary>
        /// Whether the /Packages(Id=,Version=) endpoint is enabled for non-hijacked queries for the V1 OData API.
        /// </summary>
        bool IsODataV1GetSpecificNonHijackedEnabled();

        /// <summary>
        /// Whether the /FindPackagesById() endpoint is enabled for non-hijacked queries for the V1 OData API.
        /// </summary>
        bool IsODataV1FindPackagesByIdNonHijackedEnabled();

        /// <summary>
        /// Whether the /FindPackagesById()/$count endpoint is enabled for non-hijacked queries for the V1 OData API.
        /// </summary>
        bool IsODataV1FindPackagesByIdCountNonHijackedEnabled();

        /// <summary>
        /// Whether the /Search() endpoint is enabled for non-hijacked queries for the V1 OData API.
        /// </summary>
        bool IsODataV1SearchNonHijackedEnabled();

        /// <summary>
        /// Whether the /Search()/$count endpoint is enabled for non-hijacked queries for the V1 OData API.
        /// </summary>
        bool IsODataV1SearchCountNonHijackedEnabled();

        /// <summary>
        /// Whether the /Packages() endpoint is enabled for non-hijacked queries for the V2 OData API.
        /// </summary>
        bool IsODataV2GetAllNonHijackedEnabled();

        /// <summary>
        /// Whether the /Packages()/$count endpoint is enabled for non-hijacked queries for the V2 OData API.
        /// </summary>
        bool IsODataV2GetAllCountNonHijackedEnabled();

        /// <summary>
        /// Whether the /Packages(Id=,Version=) endpoint is enabled for non-hijacked queries for the V2 OData API.
        /// </summary>
        bool IsODataV2GetSpecificNonHijackedEnabled();

        /// <summary>
        /// Whether the /FindPackagesById() endpoint is enabled for non-hijacked queries for the V2 OData API.
        /// </summary>
        bool IsODataV2FindPackagesByIdNonHijackedEnabled();

        /// <summary>
        /// Whether the /FindPackagesById()/$count endpoint is enabled for non-hijacked queries for the V2 OData API.
        /// </summary>
        bool IsODataV2FindPackagesByIdCountNonHijackedEnabled();

        /// <summary>
        /// Whether the /Search() endpoint is enabled for non-hijacked queries for the V2 OData API.
        /// </summary>
        bool IsODataV2SearchNonHijackedEnabled();

        /// <summary>
        /// Whether rendering licence Markdown content to HTML is enabled
        /// </summary>
        bool IsLicenseMdRenderingEnabled(User user);

        /// <summary>
        /// Whether the /Search()/$count endpoint is enabled for non-hijacked queries for the V2 OData API.
        /// </summary>
        bool IsODataV2SearchCountNonHijackedEnabled();

        /// <summary>
        /// Whether the online safety changes to the report abuse form have been enabled
        /// </summary>
        bool IsShowReportAbuseSafetyChangesEnabled();

        /// <summary>
        /// Whether online safety categories are available to content owned by at least one Microsoft Entra ID-authenticated account
        /// </summary>
        bool IsAllowAadContentSafetyReportsEnabled();

        /// <summary>
        /// Whether rendering Markdown content to HTML using Markdig is enabled
        /// </summary>
        bool IsMarkdigMdRenderingEnabled();

        /// <summary>
        /// Whether rendering Markdown fenced code with syntax highlighting
        /// </summary>
        bool IsMarkdigMdSyntaxHighlightEnabled();

        /// <summary>
        /// Whether the new warning of the verfiy metadata when upload package is enabled.
        /// </summary>
        bool IsDisplayUploadWarningV2Enabled(User user);

        /// <summary>
        /// Whether the new warning of the missing readme is displayed to package authors
        /// </summary>
        bool IsDisplayPackageReadmeWarningEnabled(User user);

        /// <summary>
        /// Whether or not the user can delete a package through the API.
        /// </summary>
        bool IsDeletePackageApiEnabled(User user);

        /// <summary>
        /// Whether the allowlist is enabled for checking the image sources
        /// </summary>
        bool IsImageAllowlistEnabled();

        /// <summary>
        /// Whether or not display the banner on nuget.org
        /// </summary>
        bool IsDisplayBannerEnabled();

        /// <summary>
        /// Whether or not display target framework badges and table on nuget.org
        /// </summary>
        bool IsDisplayTargetFrameworkEnabled(User user);

        /// <summary>
        /// Whether or not to compute backend operations for target framework. This flag is overridden by <see cref="IsDisplayTargetFrameworkEnabled"/> if that flag is true.
        /// </summary>
        bool IsComputeTargetFrameworkEnabled();

        /// <summary>
        /// Whether or not recent packages has no index applied to block search engine indexing.
        /// </summary>
        bool IsRecentPackagesNoIndexEnabled();

        /// <summary>
        /// Whether or not to enforce 2FA for new external account link or replacement.
        /// </summary>
        bool IsNewAccount2FAEnforcementEnabled();

        /// <summary>
        /// Whether or not NuGet.org password login is supported. NuGet.org accounts in the <see cref="LoginDiscontinuationConfiguration.ExceptionsForEmailAddresses"/> will always be supported.
        /// </summary>
        bool IsNuGetAccountPasswordLoginEnabled();

        /// <summary>
        /// Whether or not to allow filtering by frameworks on NuGet.org search
        /// </summary>
        bool IsFrameworkFilteringEnabled(User user);

        /// <summary>
        /// Whether or not to display TFM badges in search results
        /// </summary>
        bool IsDisplayTfmBadgesEnabled(User user);

        /// <summary>
        /// Whether or not to allow filtering by frameworks on NuGet.org search
        /// </summary>
        bool IsAdvancedFrameworkFilteringEnabled(User user);
    }
}
