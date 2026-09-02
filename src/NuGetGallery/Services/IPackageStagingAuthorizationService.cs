// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    /// <summary>
    /// Authorizes users and API credentials to manage private staged packages.
    /// </summary>
    public interface IPackageStagingAuthorizationService
    {
        /// <summary>
        /// Determines whether a signed-in user can manage a staged package.
        /// </summary>
        /// <param name="currentUser">The user requesting access.</param>
        /// <param name="stagedPackage">The staged package attempt.</param>
        /// <returns><see langword="true"/> when the user can manage the staged package.</returns>
        bool CanManage(User currentUser, StagedPackage stagedPackage);

        /// <summary>
        /// Determines whether an API credential can manage a staged package.
        /// </summary>
        /// <param name="currentUser">The user associated with the API credential.</param>
        /// <param name="scopes">The scopes granted to the API credential.</param>
        /// <param name="stagedPackage">The staged package attempt.</param>
        /// <returns><see langword="true"/> when the credential can manage the staged package.</returns>
        bool CanManageWithApiKey(User currentUser, IEnumerable<Scope> scopes, StagedPackage stagedPackage);
    }
}
