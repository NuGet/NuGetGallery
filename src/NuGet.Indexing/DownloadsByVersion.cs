// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Indexing
{
    public class DownloadsByVersion
    {
        private readonly IDictionary<string, int> _downloadsByVersion =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private int _total;

        /// <summary>
        /// The total count of downloads across all versions
        /// </summary>
        /// <remarks>
        /// This is thread safe as long as set is not being called from multiple threads
        /// </remarks>
        public int Total
        {
            get
            {
                return _total;
            }
        }

        public int this[string version]
        {
            get
            {
                int count = 0;

                _downloadsByVersion.TryGetValue(version, out count);

                return count;
            }

            // Set is only ever called when the auxiliary data is reloaded, which is only ever done on one thread per instance.
            set
            {
                int oldValue;
                _downloadsByVersion.TryGetValue(version, out oldValue);
                _downloadsByVersion[version] = value;

                _total = _total + value - oldValue;
            }
        }
    }
}
