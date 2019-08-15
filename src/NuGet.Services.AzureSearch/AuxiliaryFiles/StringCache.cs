// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Threading;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public class StringCache
    {
        /// <summary>
        /// Maintain a lookup of strings for de-duping. We maintain the original case for de-duping purposes by using
        /// the default string comparer. As of July of 2019 in PROD, maintaining original case of version
        /// string adds less than 0.3% extra strings. De-duping version strings in a case-sensitive manner removes
        /// 87.0% of the string allocations. Intuitively this means most people use the same case of a given version
        /// string and a lot of people use the same versions strings (common ones are 1.0.0, 1.0.1, 1.0.2, 1.1.0, etc).
        /// </summary>
        private readonly ConcurrentDictionary<string, string> _values = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// Keep track of the number of requests for a string. This is the number of times <see cref="Dedupe(string)"/>
        /// has been called.
        /// </summary>
        private int _requestCount = 0;

        /// <summary>
        /// Keep track of the number of string de-duped, i.e. "cache hits".
        /// </summary>
        private int _hitCount = 0;

        /// <summary>
        /// Keep track of the number of characters in the cache.
        /// </summary>
        private long _charCount = 0;

        public int StringCount => _values.Count;
        public int RequestCount => _requestCount;
        public int HitCount => _hitCount;
        public long CharCount => _charCount;

        public string Dedupe(string value)
        {
            Interlocked.Increment(ref _requestCount);

            if (value == null)
            {
                return null;
            }

            // Inspired by:
            // https://devblogs.microsoft.com/pfxteam/building-a-custom-getoradd-method-for-concurrentdictionarytkeytvalue/
            while (true)
            {
                if (_values.TryGetValue(value, out var existingValue))
                {
                    Interlocked.Increment(ref _hitCount);
                    return existingValue;
                }

                if (_values.TryAdd(value, value))
                {
                    Interlocked.Add(ref _charCount, value.Length);
                    return value;
                }
            }
        }

        /// <summary>
        /// Resets <see cref="RequestCount"/> and <see cref="HitCount"/> back to zero.
        /// </summary>
        public void ResetCounts()
        {
            _requestCount = 0;
            _hitCount = 0;
        }
    }
}

