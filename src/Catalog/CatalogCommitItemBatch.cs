// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.Metadata.Catalog
{
    /// <summary>
    /// Represents a group of <see cref="CatalogCommitItem" />.
    /// Items may span multiple commits but are grouped on common criteria (e.g.:  lower-cased package ID).
    /// </summary>
    public sealed class CatalogCommitItemBatch
    {
        /// <summary>
        /// Initializes a <see cref="CatalogCommitItemBatch" /> instance.
        /// </summary>
        /// <param name="items">An enumerable of <see cref="CatalogCommitItem" />.  Items may span multiple commits.</param>
        /// <param name="key">A unique key for all items in a batch.  This is used for parallelization and may be
        /// <c>null</c> if parallelization is not used.</param>
        /// <param name="commitTimestamp">The commit timestamp to use for this batch. If <c>null</c>, the minimum of
        /// all item commit timestamps will be used.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="items" /> is either <c>null</c> or empty.</exception>
        public CatalogCommitItemBatch(
            IEnumerable<CatalogCommitItem> items,
            string key = null,
            DateTime? commitTimestamp = null)
        {
            if (items == null || !items.Any())
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(items));
            }

            var list = items.ToList();

            CommitTimeStamp = commitTimestamp ?? list.Min(item => item.CommitTimeStamp);
            Key = key;

            list.Sort();

            Items = list;
        }

        public DateTime CommitTimeStamp { get; }
        public IReadOnlyList<CatalogCommitItem> Items { get; }
        public string Key { get; }
    }
}