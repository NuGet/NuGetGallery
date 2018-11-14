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
        /// <param name="commitTimeStamp">A commit timestamp relevant to <paramref name="items" />.
        /// For example, the minimum or maximum commit timestamp amongst all <paramref name="items" />,
        /// depending on the <see cref="CommitCollector" />.</param>
        /// <param name="key">A unique key for all items in a batch.  This is used for parallelization and may be
        /// <c>null</c> if parallelization is not used.</param>
        /// <param name="items">An enumerable of <see cref="CatalogCommitItem" />.  Items may span multiple commits.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="items" /> is either <c>null</c> or empty.</exception>
        public CatalogCommitItemBatch(DateTime commitTimeStamp, string key, IEnumerable<CatalogCommitItem> items)
        {
            if (items == null || !items.Any())
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(items));
            }

            CommitTimeStamp = commitTimeStamp;
            Key = key;

            var list = items.ToList();

            list.Sort();

            Items = list;
        }

        public DateTime CommitTimeStamp { get; }
        public IReadOnlyList<CatalogCommitItem> Items { get; }
        public string Key { get; }
    }
}