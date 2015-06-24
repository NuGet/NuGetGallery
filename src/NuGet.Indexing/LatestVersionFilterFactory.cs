// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;
using NuGet.Versioning;
using System;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public static class LatestVersionFilterFactory
    {
        public static Filter Create(IndexReader indexReader, bool includePrerelease, bool includeUnlisted)
        {
            IDictionary<string, OpenBitSet> openBitSetLookup = new Dictionary<string, OpenBitSet>();

            if (indexReader.GetSequentialSubReaders() != null)
            {
                foreach (SegmentReader segmentReader in indexReader.GetSequentialSubReaders())
                {
                    openBitSetLookup.Add(segmentReader.SegmentName, new OpenBitSet());
                }
            }
            else
            {
                openBitSetLookup.Add(string.Empty, new OpenBitSet());
            }

            IDictionary<string, Tuple<NuGetVersion, string, int>> lookup = MakeLatestVersionLookup(indexReader, includePrerelease, includeUnlisted);

            foreach (Tuple<NuGetVersion, string, int> entry in lookup.Values)
            {
                string readerName = entry.Item2;
                int readerDocumentId = entry.Item3;

                openBitSetLookup[readerName].Set(readerDocumentId);
            }

            return new OpenBitSetLookupFilter(openBitSetLookup);
        }

        static IDictionary<string, Tuple<NuGetVersion, string, int>> MakeLatestVersionLookup(IndexReader indexReader, bool includePrerelease, bool includeUnlisted)
        {
            IDictionary<string, Tuple<NuGetVersion, string, int>> lookup = new Dictionary<string, Tuple<NuGetVersion, string, int>>();

            if (indexReader.GetSequentialSubReaders() != null)
            {
                foreach (SegmentReader segmentReader in indexReader.GetSequentialSubReaders())
                {
                    MakeLatestVersionLookupPerReader(lookup, segmentReader, segmentReader.SegmentName, includePrerelease, includeUnlisted);
                }
            }
            else
            {
                MakeLatestVersionLookupPerReader(lookup, indexReader, string.Empty, includePrerelease, includeUnlisted);
            }

            return lookup;
        }

        static void MakeLatestVersionLookupPerReader(IDictionary<string, Tuple<NuGetVersion, string, int>> lookup, IndexReader reader, string readerName, bool includePrerelease, bool includeUnlisted)
        {
            for (int n = 0; n < reader.MaxDoc; n++)
            {
                if (reader.IsDeleted(n))
                {
                    continue;
                }

                Document document = reader.Document(n);

                NuGetVersion version = GetVersion(document);

                if (version == null)
                {
                    continue;
                }

                bool isListed = GetListed(document);

                if (isListed || includeUnlisted)
                {
                    if (!version.IsPrerelease || includePrerelease)
                    {
                        string id = GetId(document);

                        if (id == null)
                        {
                            continue;
                        }

                        Tuple<NuGetVersion, string, int> existingVersion;
                        if (lookup.TryGetValue(id, out existingVersion))
                        {
                            if (version > existingVersion.Item1)
                            {
                                lookup[id] = Tuple.Create(version, readerName, n);
                            }
                        }
                        else
                        {
                            lookup.Add(id, Tuple.Create(version, readerName, n));
                        }
                    }
                }
            }
        }

        static NuGetVersion GetVersion(Document document)
        {
            string version = document.Get("Version");
            return (version == null) ? null : new NuGetVersion(version);
        }

        static bool GetListed(Document document)
        {
            string listed = document.Get("Listed");
            return (listed == null) ? false : listed.Equals("true", StringComparison.InvariantCultureIgnoreCase);
        }

        static string GetId(Document document)
        {
            string id = document.Get("Id");
            string ns = document.Get("Namespace");
            string fullname = (ns == null) ? id : string.Format("{0}/{1}", ns, id);
            return (fullname == null) ? null : fullname.ToLowerInvariant();
        }
    }
}
