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
            var filters = new Dictionary<string, Filter>(StringComparer.OrdinalIgnoreCase);

            foreach (var feed in feeds)
            {
                filters.Add(feed.Key, CreateFilter(reader, feed.Value));
            }

            return filters;
        }

        static OpenBitSet CreateOpenBitSet(IndexReader reader, HashSet<string> feedIds)
        {
            var openBitSet = new OpenBitSet();

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

                if (feedIds.Contains(id))
                {
                    openBitSet.Set(n);
                }
            }

            return openBitSet;
        }

        static Filter CreateFilter(IndexReader reader, HashSet<string> feedIds)
        {
            var bitSetLookup = new Dictionary<string, OpenBitSet>(StringComparer.OrdinalIgnoreCase);

            foreach (SegmentReader segmentReader in reader.GetSequentialSubReaders())
            {
                bitSetLookup.Add(segmentReader.SegmentName, CreateOpenBitSet(segmentReader, feedIds));
            }

            return new OpenBitSetLookupFilter(bitSetLookup);
        }
    }
}
