// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// Identifies whether a staging group was deleted or protected by an accepted promotion.
    /// </summary>
    public enum StagingGroupDeletionResultType
    {
        Deleted,
        Conflict,
    }

    /// <summary>
    /// Reports group deletion and the number of affected current artifacts.
    /// </summary>
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
