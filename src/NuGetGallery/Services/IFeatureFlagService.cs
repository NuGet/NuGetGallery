// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IFeatureFlagService
    {
        /// <summary>
        /// Whether typosquatting detection is enabled on package uploads. If true, new packages
        /// cannot have an id that is similar to existing packages' ids.
        /// </summary>
        /// <returns></returns>
        bool IsTyposquattingEnabled();

        /// <summary>
        /// Whether typosquatting detection is enabled for a specific user's package uploads. If true,
        /// new packages from this user cannot have an id that is similar to existing packages' ids.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        bool IsTyposquattingEnabled(User user);

        /// <summary>
        /// Whether the packages Atom feed is enabled. If true, users can subscribe to new package versions using a
        /// feed reader on a per package ID basis.
        /// </summary>
        /// <returns></returns>
        bool IsPackagesAtomFeedEnabled();

        /// <summary>
        /// Whether or not users can manage their package's deprecation state.
        /// If disabled, 
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        bool IsManageDeprecationEnabled(User user);

        /// <summary>
        /// Whether or not the search is using circuit breaker.
        /// </summary>
        /// <returns></returns>
        bool IsSearchCircuitBreakerEnabled();

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
        /// <param name="user">The user to test fot the Flight</param>
        /// <returns>Whether or not the Flight is enabled for the user</returns>
        bool IsGitHubUsageEnabled(User user);
    }
}
