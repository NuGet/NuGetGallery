// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Represents a staging group and its current package and symbol attempts.
    /// </summary>
    public class StagingGroupSummary
    {
        /// <summary>
        /// Initializes a staging group summary.
        /// </summary>
        /// <param name="group">The staging group.</param>
        /// <param name="packages">The group's current package attempts.</param>
        /// <param name="symbols">The group's current symbol attempts.</param>
        public StagingGroupSummary(StagingGroup group, IReadOnlyList<StagedPackage> packages, IReadOnlyList<StagedSymbolPackage> symbols = null)
        {
            Group = group ?? throw new ArgumentNullException(nameof(group));
            Packages = packages ?? throw new ArgumentNullException(nameof(packages));
            Symbols = symbols ?? Array.Empty<StagedSymbolPackage>();
        }

        /// <summary>
        /// Gets the staging group.
        /// </summary>
        public StagingGroup Group { get; }

        /// <summary>
        /// Gets the group's current package attempts.
        /// </summary>
        public IReadOnlyList<StagedPackage> Packages { get; }

        /// <summary>
        /// Gets the group's current symbol attempts.
        /// </summary>
        public IReadOnlyList<StagedSymbolPackage> Symbols { get; }
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
    /// Represents one page of a staging group's current package and symbol attempts.
    /// </summary>
    public class StagingGroupPackagePage
    {
        /// <summary>
        /// Initializes a page of staging group package attempts.
        /// </summary>
        /// <param name="group">The staging group.</param>
        /// <param name="items">The package attempts on the requested page.</param>
        /// <param name="totalCount">The total number of current package and symbol attempts in the group.</param>
        /// <param name="allPackagesReady">Whether every current package and symbol attempt in the group is eligible for promotion.</param>
        /// <param name="symbols">The symbol attempts on the requested artifact page.</param>
        /// <param name="symbolCount">The total number of current symbol attempts in the group.</param>
        public StagingGroupPackagePage(
            StagingGroup group,
            IReadOnlyList<StagedPackage> items,
            int totalCount,
            bool allPackagesReady,
            IReadOnlyList<StagedSymbolPackage> symbols = null,
            int symbolCount = 0)
        {
            Group = group ?? throw new ArgumentNullException(nameof(group));
            Items = items ?? throw new ArgumentNullException(nameof(items));
            if (totalCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalCount));
            }

            TotalCount = totalCount;
            AllPackagesReady = allPackagesReady;
            Symbols = symbols ?? Array.Empty<StagedSymbolPackage>();
            if (symbolCount < 0 || symbolCount > totalCount)
            {
                throw new ArgumentOutOfRangeException(nameof(symbolCount));
            }

            SymbolCount = symbolCount;
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
        /// Gets the total number of current package and symbol attempts in the group.
        /// </summary>
        public int TotalCount { get; }

        /// <summary>
        /// Gets whether every current package and symbol attempt in the group is eligible for promotion.
        /// </summary>
        public bool AllPackagesReady { get; }

        /// <summary>
        /// Gets the symbol attempts on the requested artifact page.
        /// </summary>
        public IReadOnlyList<StagedSymbolPackage> Symbols { get; }

        /// <summary>
        /// Gets the total number of current symbol attempts in the group.
        /// </summary>
        public int SymbolCount { get; }
    }
}
