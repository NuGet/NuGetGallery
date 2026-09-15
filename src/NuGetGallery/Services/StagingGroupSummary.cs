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
}
