// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
        /// If disabled, 
        /// </summary>
        bool IsManageDeprecationEnabled(User user, PackageRegistration registration);

        /// <summary>
        /// Whether or not the user can manage their package's deprecation state through the API.
        /// </summary>
        bool IsManageDeprecationApiEnabled(User user);

        /// <summary>
        /// Whether the user is allowed to publish packages with an embedded icon.
        /// </summary>
        bool AreEmbeddedIconsEnabled(User user);

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
    }
}
