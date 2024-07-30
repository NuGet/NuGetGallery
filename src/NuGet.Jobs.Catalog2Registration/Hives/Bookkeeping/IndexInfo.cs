// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Protocol.Registration;

namespace NuGet.Jobs.Catalog2Registration
{
    /// <summary>
    /// A class that handles the bookkeeping of modifying a registration index. It holds a reference to a
    /// <see cref="RegistrationIndex"/> that can be later serialized into a blob. It also holds references to
    /// bookkeeping objects for its contained pages.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class IndexInfo
    {
        private string DebuggerDisplay => $"Index ({Items.Count} page items)";

        private readonly List<PageInfo> _items;

        private IndexInfo(RegistrationIndex index, List<PageInfo> items)
        {
            Index = index ?? throw new ArgumentNullException(nameof(index));
            _items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public static IndexInfo Existing(IHiveStorage storage, HiveType hive, RegistrationIndex index)
        {
            // Ensure the index is sorted in ascending order by lower version bound.
            var sorted = index.Items
                .Select(pageItem => new
                {
                    PageItem = pageItem,
                    PageInfo = PageInfo.Existing(pageItem, url => GetPageAsync(storage, hive, url)),
                })
                .OrderBy(x => x.PageInfo.Lower)
                .ToList();

            var items = sorted.Select(x => x.PageInfo).ToList();
            index.Items.Clear();
            index.Items.AddRange(sorted.Select(x => x.PageItem));

            return new IndexInfo(index, items);
        }

        private static async Task<RegistrationPage> GetPageAsync(IHiveStorage storage, HiveType hive, string url)
        {
            return await storage.ReadPageAsync(hive, url);
        }

        public static IndexInfo New()
        {
            var index = new RegistrationIndex
            {
                Items = new List<RegistrationPage>(),
            };

            return new IndexInfo(index, new List<PageInfo>());
        }

        public RegistrationIndex Index { get; }
        public IReadOnlyList<PageInfo> Items => _items;

        public void RemoveAt(int index)
        {
            _items.RemoveAt(index);
            Index.Items.RemoveAt(index);
        }

        public void Insert(int index, PageInfo pageInfo)
        {
            _items.Insert(index, pageInfo);
            Index.Items.Insert(index, pageInfo.PageItem);
        }
    }
}
