using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;
using NuGet.Versioning;
using System;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public class Compatibility
    {
        class MatchingDoc
        {
            public NuGetVersion Version { get; set; }
            public string SegmentName { get; set; }
            public int Doc { get; set; }
        }

        class MatchingDocsEntry
        {
            public IDictionary<string, MatchingDoc> MatchingDocs { get; private set; }
            public IDictionary<string, MatchingDoc> MatchingDocsPre { get; private set; }

            public MatchingDocsEntry()
            {
                MatchingDocs = new Dictionary<string, MatchingDoc>();
                MatchingDocsPre = new Dictionary<string, MatchingDoc>();
            }
        }

        public class BitSetsLookupEntry
        {
            public IDictionary<string, OpenBitSet> MatchingDocs { get; private set; }
            public IDictionary<string, OpenBitSet> MatchingDocsPre { get; private set; }

            public BitSetsLookupEntry()
            {
                MatchingDocs = new Dictionary<string, OpenBitSet>();
                MatchingDocsPre = new Dictionary<string, OpenBitSet>();
            }
        }

        public static Tuple<IDictionary<string, Filter>, IDictionary<string, Filter>> Warm(IndexReader reader, IDictionary<string, ISet<string>> frameworkCompatibility)
        {
            frameworkCompatibility["any"] = null;

            IDictionary<string, Compatibility.BitSetsLookupEntry> bitSets = CreateBitSetsLookup(reader, frameworkCompatibility);

            IDictionary<string, Filter> frameworkFilters = new Dictionary<string, Filter>();
            IDictionary<string, Filter> includePrereleaseFrameworkFilters = new Dictionary<string, Filter>();

            foreach (string frameworkName in frameworkCompatibility.Keys)
            {
                frameworkFilters.Add(frameworkName, new CachingWrapperFilter(new OpenBitSetLookupFilter(bitSets[frameworkName].MatchingDocs)));
                includePrereleaseFrameworkFilters.Add(frameworkName, new CachingWrapperFilter(new OpenBitSetLookupFilter(bitSets[frameworkName].MatchingDocsPre)));
            }

            return Tuple.Create(frameworkFilters, includePrereleaseFrameworkFilters);
        }

        public static IDictionary<string, BitSetsLookupEntry> CreateBitSetsLookup(IndexReader reader, IDictionary<string, ISet<string>> frameworkCompatibility)
        {
            //  This is a two step process because we first need to calculate the highest version across the whole data set (i.e. across every segment)

            //  STEP 1. Create a lookup table of compatible documents (identified by SegmentName and Doc) per entry in the framework compatibility table
            //  (The result include separate structures for release-only and including pre-release.)

            IDictionary<string, MatchingDocsEntry> matchingDocsLookup = new Dictionary<string, MatchingDocsEntry>();

            foreach (string key in frameworkCompatibility.Keys)
            {
                matchingDocsLookup[key] = new MatchingDocsEntry();
            }

            foreach (SegmentReader segmentReader in reader.GetSequentialSubReaders())
            {
                UpdateMatchingDocs(matchingDocsLookup, segmentReader, frameworkCompatibility);
            }

            //  STEP 2. From the globally created MatchingDocsLookup table we create per-segment lookups 

            IDictionary<string, BitSetsLookupEntry> bitSetsLookup = new Dictionary<string, BitSetsLookupEntry>();

            foreach (string key in frameworkCompatibility.Keys)
            {
                BitSetsLookupEntry newBitSetsLookupEntry = new BitSetsLookupEntry();

                foreach (SegmentReader segmentReader in reader.GetSequentialSubReaders())
                {
                    newBitSetsLookupEntry.MatchingDocs.Add(segmentReader.SegmentName, new OpenBitSet());
                    newBitSetsLookupEntry.MatchingDocsPre.Add(segmentReader.SegmentName, new OpenBitSet());
                }

                bitSetsLookup[key] = newBitSetsLookupEntry;
            }

            foreach (KeyValuePair<string, MatchingDocsEntry> entry in matchingDocsLookup)
            {
                foreach (MatchingDoc matchingDoc in entry.Value.MatchingDocs.Values)
                {
                    bitSetsLookup[entry.Key].MatchingDocs[matchingDoc.SegmentName].Set(matchingDoc.Doc);
                }
                foreach (MatchingDoc matchingDocPre in entry.Value.MatchingDocsPre.Values)
                {
                    bitSetsLookup[entry.Key].MatchingDocsPre[matchingDocPre.SegmentName].Set(matchingDocPre.Doc);
                }
            }

            return bitSetsLookup;
        }

        static void UpdateMatchingDocs(IDictionary<string, MatchingDocsEntry> matchingDocsLookup, SegmentReader reader, IDictionary<string, ISet<string>> frameworkCompatibility)
        {
            for (int doc = 0; doc < reader.MaxDoc; doc++)
            {
                if (reader.IsDeleted(doc))
                {
                    continue;
                }

                Document document = reader.Document(doc);
                string id = document.GetField("Id").StringValue.ToLowerInvariant();
                NuGetVersion version = new NuGetVersion(document.GetField("Version").StringValue);
                Field[] frameworks = document.GetFields("TargetFramework");

                foreach (KeyValuePair<string, ISet<string>> frameworkKV in frameworkCompatibility)
                {
                    bool isCompatible = false;

                    if (frameworkKV.Key == "any")
                    {
                        isCompatible = true;
                    }
                    else
                    {
                        foreach (Field frameworkField in frameworks)
                        {
                            string framework = frameworkField.StringValue;
                            if (framework == "any" || framework == "agnostic" || frameworkKV.Value.Contains(framework))
                            {
                                isCompatible = true;
                            }
                        }
                    }

                    MatchingDocsEntry entry = matchingDocsLookup[frameworkKV.Key];

                    if (isCompatible)
                    {
                        if (!version.IsPrerelease)
                        {
                            if (!entry.MatchingDocs.ContainsKey(id) || entry.MatchingDocs[id].Version < version)
                            {
                                entry.MatchingDocs[id] = new MatchingDoc { Version = version, SegmentName = reader.SegmentName, Doc = doc };
                            }
                        }

                        if (!entry.MatchingDocsPre.ContainsKey(id) || entry.MatchingDocsPre[id].Version < version)
                        {
                            entry.MatchingDocsPre[id] = new MatchingDoc { Version = version, SegmentName = reader.SegmentName, Doc = doc };
                        }
                    }
                }
            }
        }
    }
}
