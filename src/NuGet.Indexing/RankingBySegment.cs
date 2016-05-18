// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGet.Indexing
{
    public class RankingBySegment
    {
        private Dictionary<string, Ranking[]> _mapping = new Dictionary<string, Ranking[]>();

        public Ranking[] this[string segmentReaderName]
        {
            get
            {
                return _mapping[segmentReaderName];
            }

            set
            {
                _mapping[segmentReaderName] = value;
            }
        }

        public IEnumerable<Ranking> Flatten()
        {
            return _mapping
                .SelectMany(r => r.Value)
                .Where(r => r != null)
                .OrderBy(r => r.Id);
        }
    }
}
