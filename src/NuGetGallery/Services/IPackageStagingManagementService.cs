// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Retrieves private staged packages for authorized owners.
    /// </summary>
    public interface IPackageStagingManagementService
    {
        /// <summary>
        /// Gets an owner-visible staged package.
        /// </summary>
        /// <param name="currentUser">The user associated with the staging credential.</param>
        /// <param name="scopes">The scopes granted to the staging credential.</param>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version.</param>
        /// <returns>The staged package status, or <see langword="null"/> when the package is not visible to the caller.</returns>
        PackageStagingStatus GetPackage(User currentUser, IEnumerable<Scope> scopes, string id, string version);

        /// <summary>
        /// Gets the owner-visible status for a staged package attempt.
        /// </summary>
        /// <param name="stagedPackage">The staged package attempt.</param>
        /// <returns>The staged package status.</returns>
        PackageStagingStatus GetStatus(StagedPackage stagedPackage);

        /// <summary>
        /// Determines whether package staging is enabled for the user or an organization the user belongs to.
        /// </summary>
        /// <param name="currentUser">The user whose staging access should be checked.</param>
        /// <returns><see langword="true"/> when at least one eligible staging owner is enabled.</returns>
        bool IsEnabled(User currentUser);

        /// <summary>
        /// Finds the current attempt for a staged package.
        /// </summary>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version.</param>
        /// <returns>The current attempt, or <see langword="null"/> when the package is not staged.</returns>
        StagedPackage FindCurrentStagedPackage(string id, string version);

        /// <summary>
        /// Opens the downloadable content for an authorized staged package attempt.
        /// </summary>
        /// <param name="stagedPackage">The authorized staged package attempt.</param>
        /// <returns>The package stream.</returns>
        Task<Stream> OpenPackageContentAsync(StagedPackage stagedPackage);

        /// <summary>
        /// Changes whether the package should be listed after promotion.
        /// </summary>
        /// <param name="stagedPackage">The authorized current staged package attempt.</param>
        /// <param name="listed">Whether the package should be listed after promotion.</param>
        Task UpdateListedAsync(StagedPackage stagedPackage, bool listed);

        /// <summary>
        /// Gets staged packages owned by the user or an enabled organization the user belongs to.
        /// </summary>
        /// <param name="currentUser">The user requesting the staged packages.</param>
        /// <returns>The owner-visible staged packages.</returns>
        IReadOnlyList<StagedPackage> GetStagedPackages(User currentUser);
    }
}
