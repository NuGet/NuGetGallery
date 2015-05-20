// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public class OpenBitSetLookupFilter : Filter
    {
        IDictionary<string, OpenBitSet> _bitSetLookup;

        public OpenBitSetLookupFilter(IDictionary<string, OpenBitSet> bitSetLookup)
        {
            _bitSetLookup = bitSetLookup;
        }

        public override DocIdSet GetDocIdSet(IndexReader segmentReader)
        {
            string segmentName = ((SegmentReader)segmentReader).SegmentName;
            return _bitSetLookup[segmentName];
        }
    }
}
