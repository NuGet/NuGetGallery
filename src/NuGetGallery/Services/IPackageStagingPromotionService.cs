// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Accepts package promotion requests from signed-in Gallery users.
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
    }
}
