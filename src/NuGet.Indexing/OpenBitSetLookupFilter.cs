// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;

namespace NuGet.Indexing
{
    public class OpenBitSetLookupFilter : Filter
    {
        readonly IDictionary<string, OpenBitSet> _bitSetLookup;

        public OpenBitSetLookupFilter(IDictionary<string, OpenBitSet> bitSetLookup)
        {
            _bitSetLookup = bitSetLookup;
        }

        public override DocIdSet GetDocIdSet(IndexReader segmentReader)
        {
            string segmentName = ((SegmentReader)segmentReader).SegmentName;

            OpenBitSet docIdSet;
            if (_bitSetLookup.TryGetValue(segmentName, out docIdSet))
            {
                return docIdSet;
            }
            return null;
        }
    }
}
