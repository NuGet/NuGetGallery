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
