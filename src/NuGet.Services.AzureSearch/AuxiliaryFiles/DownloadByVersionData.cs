// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public class DownloadByVersionData : IReadOnlyDictionary<string, long>
    {
        private readonly SortedDictionary<string, long> _versions
            = new SortedDictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        public long Total { get; private set; }

        public long GetDownloadCount(string version)
        {
            if (!_versions.TryGetValue(version, out var downloads))
            {
                return 0;
            }

            return downloads;
        }

        public void SetDownloadCount(string version, long downloads)
        {
            if (downloads < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(downloads), "The download count must not be negative.");
            }

            if (_versions.TryGetValue(version, out var existingDownloads))
            {
                // Remove the previous version so that the latest case is retained. Versions are case insensitive but
                // we should try to respect the latest intent.
                _versions.Remove(version);
            }
            else
            {
                existingDownloads = 0;
            }

            Total += downloads - existingDownloads;

            // Only store the download count if the value is not zero.
            if (downloads != 0)
            {
                _versions.Add(version, downloads);
            }
        }

        public IEnumerable<string> Keys => _versions.Keys;
        public IEnumerable<long> Values => _versions.Values;
        public int Count => _versions.Count;
        public long this[string key] => _versions[key];
        public IEnumerator<KeyValuePair<string, long>> GetEnumerator() => _versions.GetEnumerator();
        public bool TryGetValue(string key, out long value) => _versions.TryGetValue(key, out value);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public bool ContainsKey(string key) => _versions.ContainsKey(key);
    }
}

