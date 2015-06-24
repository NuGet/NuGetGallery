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
    public class LatestListedHandler : IIndexReaderProcessorHandler
    {
        IDictionary<string, OpenBitSet> _openBitSetLookup;
        IDictionary<string, Tuple<NuGetVersion, string, int>> _lookup;

        bool _includeUnlisted;
        bool _includePrerelease;

        public LatestListedHandler(bool includeUnlisted, bool includePrerelease)
        {
            _includeUnlisted = includeUnlisted;
            _includePrerelease = includePrerelease;
        }

        public Filter Result { get; private set; }

        public void Begin(IndexReader indexReader)
        {
            _openBitSetLookup = new Dictionary<string, OpenBitSet>();
            _lookup = new Dictionary<string, Tuple<NuGetVersion, string, int>>();

            if (indexReader.GetSequentialSubReaders() != null)
            {
                foreach (SegmentReader segmentReader in indexReader.GetSequentialSubReaders())
                {
                    _openBitSetLookup.Add(segmentReader.SegmentName, new OpenBitSet());
                }
            }
            else
            {
                _openBitSetLookup.Add(string.Empty, new OpenBitSet());
            }
        }

        public void End(IndexReader indexReader)
        {
            foreach (Tuple<NuGetVersion, string, int> entry in _lookup.Values)
            {
                string readerName = entry.Item2;
                int readerDocumentId = entry.Item3;

                _openBitSetLookup[readerName].Set(readerDocumentId);
            }

            Result = new OpenBitSetLookupFilter(_openBitSetLookup);
        }

        public void Process(IndexReader indexReader, string readerName, int n, Document document, string id, NuGetVersion version)
        {
            if (id == null || version == null)
            {
                return;
            }

            bool isListed = GetListed(document);

            Update(isListed, readerName, n, id, version);
        }

        void Update(bool isListed, string readerName, int n, string id, NuGetVersion version)
        {
            if (isListed || _includeUnlisted)
            {
                if (!version.IsPrerelease || _includePrerelease)
                {
                    Tuple<NuGetVersion, string, int> existingVersion;
                    if (_lookup.TryGetValue(id, out existingVersion))
                    {
                        if (version > existingVersion.Item1)
                        {
                            _lookup[id] = Tuple.Create(version, readerName, n);
                        }
                    }
                    else
                    {
                        _lookup.Add(id, Tuple.Create(version, readerName, n));
                    }
                }
            }
        }

        static bool GetListed(Document document)
        {
            string listed = document.Get("Listed");
            return (listed == null) ? false : listed.Equals("true", StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
