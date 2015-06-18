using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;
using System;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public static class CuratedFeedFilter
    {
        public static IDictionary<string, Filter> CreateFilters(IndexReader reader, IDictionary<string, HashSet<string>> feeds)
        {
            var bitSetLookup = new Dictionary<string, IDictionary<string, OpenBitSet>>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in feeds.Keys)
            {
                bitSetLookup[key] = new Dictionary<string, OpenBitSet>();

                foreach (SegmentReader segmentReader in reader.GetSequentialSubReaders())
                {
                    bitSetLookup[key][segmentReader.SegmentName] = new OpenBitSet();
                }
            }

            foreach (SegmentReader segmentReader in reader.GetSequentialSubReaders())
            {
                CreateOpenBitSets(segmentReader, feeds, bitSetLookup);
            }

            var filters = new Dictionary<string, Filter>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in feeds.Keys)
            {
                filters[key] = new OpenBitSetLookupFilter(bitSetLookup[key]);
            }

            return filters;
        }

        static void CreateOpenBitSets(SegmentReader reader, IDictionary<string, HashSet<string>> feeds, IDictionary<string, IDictionary<string, OpenBitSet>> bitSetLookup)
        {
            for (int n = 0; n < reader.MaxDoc; n++)
            {
                if (reader.IsDeleted(n))
                {
                    continue;
                }

                Document document = reader.Document(n);

                string id = document.Get("Id");

                if (id == null)
                {
                    continue;
                }

                foreach (var feed in feeds)
                {
                    if (feed.Value.Contains(id))
                    {
                        bitSetLookup[feed.Key][reader.SegmentName].Set(n);
                    }
                }
            }
        }
    }
}
