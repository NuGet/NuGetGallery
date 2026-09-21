// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGet.Services.Staging.Promotion
{
    /// <summary>
    /// Coordinates successful package completion within an active staging group promotion.
    /// </summary>
    public interface IStagingGroupPromotionService
    {
        /// <summary>
        /// Marks a grouped package attempt as successful and completes the group when all active members have succeeded.
        /// </summary>
        /// <param name="stagedPackage">The successfully published staged package.</param>
        /// <remarks>
        /// The caller owns the database transaction and commits these changes with the package publication state.
        /// </remarks>
        void CompletePackage(StagedPackage stagedPackage);
    }
}
