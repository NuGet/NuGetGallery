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
        readonly IDictionary<string, OpenBitSet> _bitSetLookup;

        public OpenBitSetLookupFilter(IDictionary<string, OpenBitSet> bitSetLookup)
        {
            _bitSetLookup = bitSetLookup;
        }

        public override DocIdSet GetDocIdSet(IndexReader reader)
        {
            SegmentReader segmentReader = reader as SegmentReader;

            string readerName = (segmentReader != null) ? segmentReader.SegmentName : string.Empty;

            OpenBitSet docIdSet;
            if (_bitSetLookup.TryGetValue(readerName, out docIdSet))
            {
                return docIdSet;
            }
            return null;
        }
    }
}
