// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Accepts package and staging group promotion requests from signed-in Gallery users.
    /// </summary>
    public interface IPackageStagingPromotionService
    {
        /// <summary>
        /// Attempts to begin promotion of a staged package.
        /// </summary>
        /// <param name="currentUser">The user requesting promotion.</param>
        /// <param name="stagedPackage">The staged package attempt to promote.</param>
        /// <returns>The result of accepting the promotion request.</returns>
        Task<PackageStagingPromotionResult> PromotePackageAsync(User currentUser, StagedPackage stagedPackage);

        /// <summary>
        /// Resends an individual promotion that is still in progress and has exceeded the wait threshold.
        /// </summary>
        /// <param name="currentUser">The user requesting the resend.</param>
        /// <param name="stagedPackage">The staged package attempt being promoted.</param>
        /// <returns>The result of the resend request.</returns>
        Task<PackageStagingPromotionResult> ResendPackageAsync(User currentUser, StagedPackage stagedPackage);

        /// <summary>
        /// Attempts to begin promotion of every current package in a staging group.
        /// </summary>
        /// <param name="currentUser">The user requesting promotion.</param>
        /// <param name="group">The staging group to promote.</param>
        /// <returns>The result of accepting the promotion request.</returns>
        Task<StagingGroupPromotionResult> PromoteGroupAsync(User currentUser, StagingGroup group);

        /// <summary>
        /// Resends work for an active group promotion that has exceeded the wait threshold.
        /// </summary>
        /// <param name="currentUser">The user requesting the resend.</param>
        /// <param name="group">The staging group being promoted.</param>
        /// <returns>The result of the resend request.</returns>
        Task<StagingGroupPromotionResult> ResendGroupAsync(User currentUser, StagingGroup group);
    }

    /// <summary>
    /// Describes whether a package promotion request was accepted.
    /// </summary>
    public enum PackageStagingPromotionResult
    {
        /// <summary>
        /// The promotion request was accepted for asynchronous processing.
        /// </summary>
        Accepted,

        /// <summary>
        /// The user cannot promote the staged package.
        /// </summary>
        Unauthorized,

        /// <summary>
        /// The staged package is not ready for promotion.
        /// </summary>
        NotReady,

        /// <summary>
        /// The staged package belongs to a group and must be promoted with that group.
        /// </summary>
        Grouped,

        /// <summary>
        /// The staged package changed while promotion was being accepted.
        /// </summary>
        Conflict,

        /// <summary>
        /// The promotion is active, but its message could not be confirmed as sent; it can be retried now.
        /// </summary>
        DispatchFailed,
    }

    /// <summary>
    /// Describes whether a staging group promotion request was accepted.
    /// </summary>
    public enum StagingGroupPromotionResult
    {
        /// <summary>
        /// The promotion request was accepted for asynchronous processing.
        /// </summary>
        Accepted,

        /// <summary>
        /// The user cannot promote every package in the staging group.
        /// </summary>
        Unauthorized,

        /// <summary>
        /// The staging group has no current package members.
        /// </summary>
        Empty,

        /// <summary>
        /// At least one current package is not ready for promotion.
        /// </summary>
        NotReady,

        /// <summary>
        /// The staging group or one of its packages changed while promotion was being accepted.
        /// </summary>
        Conflict,

        /// <summary>
        /// The promotion is active, but its message could not be confirmed as sent; it can be retried now.
        /// </summary>
        DispatchFailed,
    }
}
