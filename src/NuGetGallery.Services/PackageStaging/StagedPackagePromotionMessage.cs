// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    /// <summary>
    /// Identifies an accepted staged package promotion.
    /// </summary>
    public class StagedPackagePromotionMessage
    {
        /// <summary>
        /// Creates a staged package promotion message.
        /// </summary>
        /// <param name="promotionId">The active promotion identifier.</param>
        /// <param name="stagedPackageKey">The staged package attempt key.</param>
        public StagedPackagePromotionMessage(Guid promotionId, int stagedPackageKey)
        {
            if (promotionId == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(promotionId));
            }

            if (stagedPackageKey <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stagedPackageKey));
            }

            PromotionId = promotionId;
            StagedPackageKey = stagedPackageKey;
        }

        /// <summary>
        /// Gets the active promotion identifier.
        /// </summary>
        public Guid PromotionId { get; }

        /// <summary>
        /// Gets the staged package attempt key.
        /// </summary>
        public int StagedPackageKey { get; }
    }
}
