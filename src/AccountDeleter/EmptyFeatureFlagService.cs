// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using System;
using System.Collections.Generic;

namespace NuGetGallery.AccountDeleter
{
    public class EmptyFeatureFlagService : IFeatureFlagService
    {
        public bool AreDynamicODataCacheDurationsEnabled()
        {
            throw new NotImplementedException();
        }

        public bool AreEmbeddedIconsEnabled(User user)
        {
            throw new NotImplementedException();
        }

        public bool AreEmbeddedReadmesEnabled(User user)
        {
            throw new NotImplementedException();
        }

        public bool ArePatternSetTfmHeuristicsEnabled()
        {
            //Until this feature is enabled by default, we will assume it is turned off here.
            //This is the only used feature flag that was added.
            return false;
        }

        public bool IsABTestingEnabled(User user)
        {
            throw new NotImplementedException();
        }

        public bool IsAdvancedSearchEnabled(User user)
        {
            throw new NotImplementedException();
        }

        public bool IsAlternateStatisticsSourceEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsAsyncAccountDeleteEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsComputeTargetFrameworkEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsDeletePackageApiEnabled(User user)
        {
            throw new NotImplementedException();
        }

        public bool IsDisplayBannerEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsDisplayFuGetLinksEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsDisplayDNDocsLinksEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsDisplayNuGetPackageExplorerLinkEnabled()
        {
            throw new NotImplementedException();
        }
        public bool IsDisplayNuGetTrendsLinksEnabled()
        {
            throw new NotImplementedException();
        }
        public bool IsDisplayTargetFrameworkEnabled(User user)
        {
            throw new NotImplementedException();
        }

        public bool IsDisplayVulnerabilitiesEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsNuGetAccountPasswordLoginEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsForceFlatContainerIconsEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsGet2FADismissFeedbackEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsGitHubUsageEnabled(User user)
        {
            throw new NotImplementedException();
        }

        public bool IsGravatarProxyEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsImageAllowlistEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsLicenseMdRenderingEnabled(User user)
        {
            throw new NotImplementedException();
        }

        public bool IsManageDeprecationApiEnabled(User user)
        {
            throw new NotImplementedException();
        }

        public bool IsManageDeprecationEnabled(User user, PackageRegistration registration)
        {
            throw new NotImplementedException();
        }

        public bool IsManageDeprecationEnabled(User user, IEnumerable<Package> allVersions)
        {
            throw new NotImplementedException();
        }

        public bool IsManagePackagesVulnerabilitiesEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsMarkdigMdRenderingEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsMarkdigMdSyntaxHighlightEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsNewAccount2FAEnforcementEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsODataDatabaseReadOnlyEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsODataV1FindPackagesByIdCountNonHijackedEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsODataV1FindPackagesByIdNonHijackedEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsODataV1GetAllCountEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsODataV1GetAllEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsODataV1GetSpecificNonHijackedEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsODataV1SearchCountNonHijackedEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsODataV1SearchNonHijackedEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsODataV2FindPackagesByIdCountNonHijackedEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsODataV2FindPackagesByIdNonHijackedEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsODataV2GetAllCountNonHijackedEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsODataV2GetAllNonHijackedEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsODataV2GetSpecificNonHijackedEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsODataV2SearchCountNonHijackedEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsODataV2SearchNonHijackedEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsPackageDependentsEnabled(User user)
        {
            throw new NotImplementedException();
        }

        public bool IsPackageRenamesEnabled(User user)
        {
            throw new NotImplementedException();
        }

        public bool IsPackagesAtomFeedEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsPreviewHijackEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsRecentPackagesNoIndexEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsSearchSideBySideEnabled(User user)
        {
            throw new NotImplementedException();
        }

        public bool IsSelfServiceAccountDeleteEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsShowEnable2FADialogEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsShowReportAbuseSafetyChangesEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsAllowAadContentSafetyReportsEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsTyposquattingEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsTyposquattingEnabled(User user)
        {
            throw new NotImplementedException();
        }

        public bool ProxyGravatarEnSubdomain()
        {
            throw new NotImplementedException();
        }

        public bool IsDisplayUploadWarningV2Enabled(User user)
        {
            throw new NotImplementedException();
        }

        public bool IsDisplayPackageReadmeWarningEnabled(User user)
        {
            throw new NotImplementedException();
        }

        public bool IsFrameworkFilteringEnabled(User user)
        {
            throw new NotImplementedException();
        }

        public bool IsDisplayTfmBadgesEnabled(User user)
        {
            throw new NotImplementedException();
        }

        public bool IsAdvancedFrameworkFilteringEnabled(User user)
        {
            throw new NotImplementedException();
        }
    }
}
