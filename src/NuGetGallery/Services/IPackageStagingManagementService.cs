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
        /// Gets staged packages visible to the API credential.
        /// </summary>
        /// <param name="currentUser">The user associated with the staging credential.</param>
        /// <param name="scopes">The scopes granted to the staging credential.</param>
        /// <returns>The current staged package statuses visible to the credential.</returns>
        IReadOnlyList<PackageStagingStatus> GetPackages(User currentUser, IEnumerable<Scope> scopes);

        /// <summary>
        /// Gets an owner-visible staged package.
        /// </summary>
        /// <param name="currentUser">The user associated with the staging credential.</param>
        /// <param name="scopes">The scopes granted to the staging credential.</param>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version.</param>
        /// <returns>The staged package status, or <see langword="null"/> when the package is not visible to the caller.</returns>
        PackageStagingStatus GetPackageStatus(User currentUser, IEnumerable<Scope> scopes, string id, string version);

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
        /// Gets the enabled staging owners manageable by the user.
        /// </summary>
        /// <param name="currentUser">The user requesting the staging owners.</param>
        /// <returns>The enabled staging owners.</returns>
        IReadOnlyList<User> GetStagingOwners(User currentUser);

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
        /// Deletes an authorized current staged package attempt while retaining its reserved package row.
        /// </summary>
        /// <param name="stagedPackage">The authorized current staged package attempt.</param>
        Task DeletePackageAsync(StagedPackage stagedPackage);

        /// <summary>
        /// Gets staged packages owned by the user or an enabled organization the user belongs to.
        /// </summary>
        /// <param name="currentUser">The user requesting the staged packages.</param>
        /// <returns>The owner-visible staged packages.</returns>
        IReadOnlyList<StagedPackage> GetStagedPackages(User currentUser);

        /// <summary>
        /// Gets staging groups owned by the user or an enabled organization the user belongs to.
        /// </summary>
        /// <param name="currentUser">The user requesting the staging groups.</param>
        /// <returns>The owner-visible staging groups.</returns>
        IReadOnlyList<StagingGroup> GetStagingGroups(User currentUser);

        /// <summary>
        /// Finds an owner-visible staging group.
        /// </summary>
        /// <param name="currentUser">The user requesting the staging group.</param>
        /// <param name="owner">The group owner's username.</param>
        /// <param name="groupId">The owner-scoped group ID.</param>
        /// <returns>The staging group, or <see langword="null"/> when it does not exist or is not visible.</returns>
        StagingGroup FindStagingGroup(User currentUser, string owner, string groupId);

        /// <summary>
        /// Creates an owner-scoped staging group.
        /// </summary>
        /// <param name="currentUser">The user creating the staging group.</param>
        /// <param name="owner">The staging owner's username.</param>
        /// <param name="groupId">The immutable owner-scoped group ID.</param>
        /// <param name="name">The optional group display name. The group ID is used when omitted.</param>
        /// <returns>The staging group creation result.</returns>
        Task<CreateStagingGroupResult> CreateStagingGroupAsync(User currentUser, string owner, string groupId, string name);

        /// <summary>
        /// Creates a staging group for the owner scoped to an API credential.
        /// </summary>
        /// <param name="currentUser">The user associated with the staging credential.</param>
        /// <param name="scopes">The scopes granted to the staging credential.</param>
        /// <param name="groupId">The immutable owner-scoped group ID.</param>
        /// <param name="name">The group display name.</param>
        /// <returns>The staging group creation result.</returns>
        Task<CreateStagingGroupResult> CreateStagingGroupWithApiKeyAsync(User currentUser, IEnumerable<Scope> scopes, string groupId, string name);

        /// <summary>
        /// Gets staging group summaries visible to an API credential.
        /// </summary>
        /// <param name="currentUser">The user associated with the staging credential.</param>
        /// <param name="scopes">The scopes granted to the staging credential.</param>
        /// <returns>The staging groups and current package attempts owned by the credential's enabled owner, or <see langword="null"/> when the owner is unavailable.</returns>
        IReadOnlyList<StagingGroupSummary> GetStagingGroupSummariesWithApiKey(User currentUser, IEnumerable<Scope> scopes);
    }
}
