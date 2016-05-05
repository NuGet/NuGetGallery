// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Indexing
{
    public class DownloadsByVersion
    {
        private IDictionary<string, int> _downloadsByVersion =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private int? total;

        /// <summary>
        /// The total count of downloads across all versionss
        /// </summary>
        /// <remarks>
        /// This is thread safe as long as set is not being called from multiple threads
        /// </remarks>
        public int Total
        {
            get
            {
                if (total.HasValue)
                {
                    return total.Value;
                }

                total = _downloadsByVersion.Values.Sum();

                return total.Value;
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

            set
            {
                _downloadsByVersion[version] = value;
                total = null;
            }
        }
    }
}
