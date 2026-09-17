// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Represents a staging group and its current package attempts.
    /// </summary>
    public class StagingGroupSummary
    {
        /// <summary>
        /// Initializes a staging group summary.
        /// </summary>
        /// <param name="group">The staging group.</param>
        /// <param name="packages">The group's current package attempts.</param>
        public StagingGroupSummary(StagingGroup group, IReadOnlyList<StagedPackage> packages)
        {
            Group = group ?? throw new ArgumentNullException(nameof(group));
            Packages = packages ?? throw new ArgumentNullException(nameof(packages));
        }

        /// <summary>
        /// Gets the staging group.
        /// </summary>
        public StagingGroup Group { get; }

        /// <summary>
        /// Gets the group's current package attempts.
        /// </summary>
        public IReadOnlyList<StagedPackage> Packages { get; }
    }

    /// <summary>
    /// Represents one page of staging group summaries and the total number of matching groups.
    /// </summary>
    public class StagingGroupSummaryPage
    {
        /// <summary>
        /// Initializes a page of staging group summaries.
        /// </summary>
        /// <param name="items">The summaries on the requested page.</param>
        /// <param name="totalCount">The total number of matching groups.</param>
        public StagingGroupSummaryPage(IReadOnlyList<StagingGroupSummary> items, int totalCount)
        {
            Items = items ?? throw new ArgumentNullException(nameof(items));
            if (totalCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalCount));
            }

            TotalCount = totalCount;
        }

        /// <summary>
        /// Gets the summaries on the requested page.
        /// </summary>
        public IReadOnlyList<StagingGroupSummary> Items { get; }

        /// <summary>
        /// Gets the total number of matching groups.
        /// </summary>
        public int TotalCount { get; }
    }

    /// <summary>
    /// Represents one page of a staging group's current package attempts.
    /// </summary>
    public class StagingGroupPackagePage
    {
        /// <summary>
        /// Initializes a page of staging group package attempts.
        /// </summary>
        /// <param name="group">The staging group.</param>
        /// <param name="items">The package attempts on the requested page.</param>
        /// <param name="totalCount">The total number of current package attempts in the group.</param>
        /// <param name="allPackagesReady">Whether every current package attempt in the group is ready.</param>
        public StagingGroupPackagePage(
            StagingGroup group,
            IReadOnlyList<StagedPackage> items,
            int totalCount,
            bool allPackagesReady)
        {
            Group = group ?? throw new ArgumentNullException(nameof(group));
            Items = items ?? throw new ArgumentNullException(nameof(items));
            if (totalCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalCount));
            }

            TotalCount = totalCount;
            AllPackagesReady = allPackagesReady;
        }

        /// <summary>
        /// Gets the staging group.
        /// </summary>
        public StagingGroup Group { get; }

        /// <summary>
        /// Gets the current package attempts on the requested page.
        /// </summary>
        public IReadOnlyList<StagedPackage> Items { get; }

        /// <summary>
        /// Gets the total number of current package attempts in the group.
        /// </summary>
        public int TotalCount { get; }

        /// <summary>
        /// Gets whether every current package attempt in the group is ready.
        /// </summary>
        public bool AllPackagesReady { get; }
    }
}
