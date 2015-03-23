using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;
using NuGet.Versioning;
using System;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public static class LatestVersionFilter
    {
        public static Filter Create(IndexReader indexReader, bool prerelease)
        {
            IDictionary<string, OpenBitSet> openBitSetLookup = new Dictionary<string, OpenBitSet>();

            foreach (SegmentReader segmentReader in indexReader.GetSequentialSubReaders())
            {
                openBitSetLookup.Add(segmentReader.SegmentName, new OpenBitSet());
            }

            IDictionary<string, Tuple<NuGetVersion, string, int>> lookup = MakeLatestVersionLookup(indexReader, prerelease);

            foreach (Tuple<NuGetVersion, string, int> entry in lookup.Values)
            {
                openBitSetLookup[entry.Item2].Set(entry.Item3);
            }

            return new OpenBitSetLookupFilter(openBitSetLookup);
        }

        static string GetId(Document document)
        {
            string id = document.Get("Id");
            string ns = document.Get("Namespace");
            return (ns == null) ? id : string.Format("{0}/{1}", ns, id);
        }

        static IDictionary<string, Tuple<NuGetVersion, string, int>> MakeLatestVersionLookup(IndexReader indexReader, bool prerelease)
        {
            IDictionary<string, Tuple<NuGetVersion, string, int>> lookup = new Dictionary<string, Tuple<NuGetVersion, string, int>>();

            foreach (SegmentReader segmentReader in indexReader.GetSequentialSubReaders())
            {
                for (int n = 0; n < segmentReader.MaxDoc; n++)
                {
                    if (indexReader.IsDeleted(n))
                    {
                        continue;
                    }

                    Document document = segmentReader.Document(n);

                    NuGetVersion currentVersion = NuGetVersion.Parse(document.Get("Version"));

                    if (!currentVersion.IsPrerelease || prerelease)
                    {
                        string id = GetId(document);

                        Tuple<NuGetVersion, string, int> existingVersion;
                        if (lookup.TryGetValue(id, out existingVersion))
                        {
                            if (currentVersion > existingVersion.Item1)
                            {
                                lookup[id] = Tuple.Create(currentVersion, segmentReader.SegmentName, n);
                            }
                        }
                        else
                        {
                            lookup.Add(id, Tuple.Create(currentVersion, segmentReader.SegmentName, n));
                        }
                    }
                }
            }

            return lookup;
        }
    }
}
