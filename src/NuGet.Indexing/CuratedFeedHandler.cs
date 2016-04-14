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
    public class CuratedFeedHandler : IIndexReaderProcessorHandler
    {
        IDictionary<string, IDictionary<string, OpenBitSet>> _bitSetLookup;
        IDictionary<string, HashSet<string>> _feeds;

        public CuratedFeedHandler(IDictionary<string, HashSet<string>> feeds)
        {
            _feeds = feeds;
        }

        public IDictionary<string, Filter> Result { get; private set; }

        public void Begin(IndexReader indexReader)
        {
            _bitSetLookup = new Dictionary<string, IDictionary<string, OpenBitSet>>(StringComparer.OrdinalIgnoreCase);

            if (indexReader.GetSequentialSubReaders() != null)
            {
                foreach (var key in _feeds.Keys)
                {
                    _bitSetLookup[key] = new Dictionary<string, OpenBitSet>();

                    foreach (SegmentReader segmentReader in indexReader.GetSequentialSubReaders())
                    {
                        _bitSetLookup[key][segmentReader.SegmentName] = new OpenBitSet();
                    }
                }
            }
            else
            {
                foreach (var key in _feeds.Keys)
                {
                    _bitSetLookup[key] = new Dictionary<string, OpenBitSet>();
                    _bitSetLookup[key][string.Empty] = new OpenBitSet();
                }
            }
        }

        public void End(IndexReader indexReader)
        {
            Result = new Dictionary<string, Filter>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in _feeds.Keys)
            {
                Result[key] = new OpenBitSetLookupFilter(_bitSetLookup[key]);
            }
        }

        public void Process(IndexReader indexReader, string readerName, int documentNumber, Document document, string id, NuGetVersion version)
        {
            foreach (var feed in _feeds)
            {
                if (feed.Value.Contains(id))
                {
                    _bitSetLookup[feed.Key][readerName].Set(documentNumber);
                }
            }
        }
    }
}
