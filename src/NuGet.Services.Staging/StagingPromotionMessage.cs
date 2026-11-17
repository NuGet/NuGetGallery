// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Staging
{
    /// <summary>
    /// Identifies an accepted staging promotion command.
    /// </summary>
    public class StagingPromotionMessage
    {
        private StagingPromotionMessage(Guid promotionId, StagingPromotionTargetType targetType, int targetKey)
        {
            PromotionId = promotionId;
            TargetType = targetType;
            TargetKey = targetKey;
        }

        /// <summary>
        /// Gets the active promotion identifier.
        /// </summary>
        public Guid PromotionId { get; }

        /// <summary>
        /// Gets the kind of staging row targeted by the command.
        /// </summary>
        public StagingPromotionTargetType TargetType { get; }

        /// <summary>
        /// Gets the primary key from the staging table identified by <see cref="TargetType"/>.
        /// </summary>
        public int TargetKey { get; }

        /// <summary>
        /// Creates a group root promotion message.
        /// </summary>
        /// <param name="promotionId">The active promotion identifier.</param>
        /// <param name="stagingGroupKey">The staging group key.</param>
        /// <returns>The group root message.</returns>
        public static StagingPromotionMessage ForGroup(Guid promotionId, int stagingGroupKey)
        {
            ValidateArguments(promotionId, stagingGroupKey, nameof(stagingGroupKey));

            return new StagingPromotionMessage(promotionId, StagingPromotionTargetType.StagingGroup, stagingGroupKey);
        }

        /// <summary>
        /// Creates a package item promotion message.
        /// </summary>
        /// <param name="promotionId">The active promotion identifier.</param>
        /// <param name="stagedPackageKey">The staged package attempt key.</param>
        /// <returns>The package item message.</returns>
        public static StagingPromotionMessage ForPackage(Guid promotionId, int stagedPackageKey)
        {
            ValidateArguments(promotionId, stagedPackageKey, nameof(stagedPackageKey));

            return new StagingPromotionMessage(promotionId, StagingPromotionTargetType.StagedPackage, stagedPackageKey);
        }

        /// <summary>
        /// Creates a staged symbol package promotion message.
        /// </summary>
        /// <param name="promotionId">The active promotion identifier.</param>
        /// <param name="stagedSymbolPackageKey">The exact staged symbol package attempt key.</param>
        /// <returns>The staged symbol package promotion message.</returns>
        public static StagingPromotionMessage ForSymbolPackage(Guid promotionId, int stagedSymbolPackageKey)
        {
            ValidateArguments(promotionId, stagedSymbolPackageKey, nameof(stagedSymbolPackageKey));

            return new StagingPromotionMessage(promotionId, StagingPromotionTargetType.StagedSymbolPackage, stagedSymbolPackageKey);
        }

        private static void ValidateArguments(Guid promotionId, int targetKey, string targetKeyParameterName)
        {
            if (promotionId == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(promotionId));
            }

            if (targetKey <= 0)
            {
                throw new ArgumentOutOfRangeException(targetKeyParameterName);
            }
        }
    }

    /// <summary>
    /// Identifies the staging table addressed by <see cref="StagingPromotionMessage.TargetKey"/>.
    /// </summary>
    public enum StagingPromotionTargetType
    {
        /// <summary>
        /// <see cref="StagingPromotionMessage.TargetKey"/> is a StagingGroups key.
        /// </summary>
        StagingGroup = 1,

        /// <summary>
        /// <see cref="StagingPromotionMessage.TargetKey"/> is a StagedPackages key.
        /// </summary>
        StagedPackage = 2,

        /// <summary>
        /// <see cref="StagingPromotionMessage.TargetKey"/> is a StagedSymbolPackages key.
        /// </summary>
        StagedSymbolPackage = 3,
    }
}
