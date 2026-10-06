// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    /// <summary>
    /// Represents one page of current staged artifacts visible to an API credential.
    /// </summary>
    /// <typeparam name="T">The artifact type.</typeparam>
    public class StagingArtifactPage<T>
    {
        /// <summary>
        /// Initializes a page of artifacts and its total matching count.
        /// </summary>
        /// <param name="items">The artifacts on this page.</param>
        /// <param name="totalCount">The total number of visible artifacts.</param>
        public StagingArtifactPage(IReadOnlyList<T> items, int totalCount)
        {
            Items = items ?? throw new ArgumentNullException(nameof(items));
            if (totalCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalCount));
            }

            TotalCount = totalCount;
        }

        /// <summary>
        /// Gets the artifacts on this page.
        /// </summary>
        public IReadOnlyList<T> Items { get; }

        /// <summary>
        /// Gets the total number of visible artifacts.
        /// </summary>
        public int TotalCount { get; }
    }
}
