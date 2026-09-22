// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGet.Services.Staging.Promotion
{
    /// <summary>
    /// Coordinates successful package completion within an active staging group promotion.
    /// </summary>
    public interface IStagingGroupPromotionService
    {
        /// <summary>
        /// Marks a grouped package attempt as successful within the caller's transaction.
        /// </summary>
        /// <param name="stagedPackage">The successfully published staged package.</param>
        void MarkPackageSucceeded(StagedPackage stagedPackage);

        /// <summary>
        /// Finalizes a group when all active package attempts have succeeded.
        /// </summary>
        /// <param name="stagingGroupKey">The staging group key.</param>
        /// <param name="promotionId">The active promotion identifier.</param>
        /// <returns>A task that represents the finalization attempt.</returns>
        Task TryFinalizeAsync(int stagingGroupKey, Guid promotionId);
    }
}
