// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    /// <summary>
    /// Maps packages that transfer their popularity away to the
    /// set of packages receiving the popularity.
    /// </summary>
    public class PopularityTransferData : IReadOnlyDictionary<string, SortedSet<string>>
    {
        private readonly SortedDictionary<string, SortedSet<string>> _transfers =
            new SortedDictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);

        public void AddTransfer(string fromId, string toId)
        {
            if (!_transfers.TryGetValue(fromId, out var toIds))
            {
                toIds = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                _transfers.Add(fromId, toIds);
            }

            toIds.Add(toId);
        }

        public SortedSet<string> this[string key] => _transfers[key];
        public IEnumerable<string> Keys => _transfers.Keys;
        public IEnumerable<SortedSet<string>> Values => _transfers.Values;
        public int Count => _transfers.Count;
        public bool ContainsKey(string key) => _transfers.ContainsKey(key);
        public IEnumerator<KeyValuePair<string, SortedSet<string>>> GetEnumerator() => _transfers.GetEnumerator();
        public bool TryGetValue(string key, out SortedSet<string> value) => _transfers.TryGetValue(key, out value);
        IEnumerator IEnumerable.GetEnumerator() => _transfers.GetEnumerator();
    }
}
