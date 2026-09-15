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
        /// Finds a staging group for an authorized owner.
        /// </summary>
        /// <param name="stagingOwner">The authorized staging owner.</param>
        /// <param name="groupId">The owner-scoped group ID.</param>
        /// <returns>The staging group, or <see langword="null"/> when it does not exist.</returns>
        StagingGroup FindStagingGroup(User stagingOwner, string groupId);

        /// <summary>
        /// Creates an owner-scoped staging group.
        /// </summary>
        /// <param name="stagingOwner">The authorized staging owner.</param>
        /// <param name="groupId">The immutable owner-scoped group ID.</param>
        /// <param name="name">The optional group display name. The group ID is used when omitted.</param>
        /// <returns>The staging group creation result.</returns>
        Task<CreateStagingGroupResult> CreateStagingGroupAsync(User stagingOwner, string groupId, string name);

        /// <summary>
        /// Renames an owner-scoped staging group.
        /// </summary>
        /// <param name="stagingOwner">The authorized staging owner.</param>
        /// <param name="groupId">The immutable owner-scoped group ID.</param>
        /// <param name="name">The new group display name.</param>
        /// <returns>The renamed group, or <see langword="null"/> when it does not exist.</returns>
        Task<StagingGroup> RenameStagingGroupAsync(User stagingOwner, string groupId, string name);

        /// <summary>
        /// Deletes an owner-scoped staging group and its current staged package members.
        /// </summary>
        /// <param name="stagingOwner">The authorized staging owner.</param>
        /// <param name="group">The staging group to delete.</param>
        /// <returns>The group deletion result.</returns>
        Task<StagingGroupDeletionResult> DeleteStagingGroupAsync(User stagingOwner, StagingGroup group);

        /// <summary>
        /// Adds or moves a staged package identity to an owner-scoped group.
        /// </summary>
        /// <param name="stagingOwner">The authorized staging owner.</param>
        /// <param name="group">The target staging group.</param>
        /// <param name="stagedPackage">The current staged package attempt whose identity should move.</param>
        /// <returns>The membership update result.</returns>
        Task<StagingGroupMembershipResult> AddPackageToStagingGroupAsync(User stagingOwner, StagingGroup group, StagedPackage stagedPackage);

        /// <summary>
        /// Removes a staged package identity from an owner-scoped group.
        /// </summary>
        /// <param name="stagingOwner">The authorized staging owner.</param>
        /// <param name="stagedPackage">The current staged package attempt whose identity should become ungrouped.</param>
        /// <returns>The membership update result.</returns>
        Task<StagingGroupMembershipResult> RemovePackageFromStagingGroupAsync(User stagingOwner, StagedPackage stagedPackage);

        /// <summary>
        /// Gets staging group summaries for an authorized owner.
        /// </summary>
        /// <param name="stagingOwner">The authorized staging owner.</param>
        /// <returns>The staging groups and current package attempts owned by the owner.</returns>
        IReadOnlyList<StagingGroupSummary> GetStagingGroupSummaries(User stagingOwner);
    }

    public enum StagingGroupMembershipResult
    {
        Updated,
        Unchanged,
        Conflict,
    }

    public enum StagingGroupDeletionResultType
    {
        Deleted,
        Conflict,
    }

    public sealed class StagingGroupDeletionResult
    {
        private StagingGroupDeletionResult(StagingGroupDeletionResultType type, int affectedPackageCount)
        {
            Type = type;
            AffectedPackageCount = affectedPackageCount;
        }

        public StagingGroupDeletionResultType Type { get; }

        public int AffectedPackageCount { get; }

        public static StagingGroupDeletionResult Deleted(int affectedPackageCount)
        {
            return new StagingGroupDeletionResult(StagingGroupDeletionResultType.Deleted, affectedPackageCount);
        }

        public static StagingGroupDeletionResult Conflict(int affectedPackageCount)
        {
            return new StagingGroupDeletionResult(StagingGroupDeletionResultType.Conflict, affectedPackageCount);
        }
    }
}
