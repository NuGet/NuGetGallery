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

        private readonly bool _includeUnlisted;
        private readonly bool _includePrerelease;
        private readonly bool _includeSemVer2;

        public LatestListedHandler(bool includeUnlisted, bool includePrerelease, bool includeSemVer2)
        {
            _includeUnlisted = includeUnlisted;
            _includePrerelease = includePrerelease;
            _includeSemVer2 = includeSemVer2;
        }

        public Filter Result { get; private set; }

        public bool SkipDeletes => true;

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

        public void Process(IndexReader indexReader,
            string readerName,
            int perSegmentDocumentNumber,
            int perIndexDocumentNumber,
            Document document,
            string id,
            NuGetVersion version)
        {
            if (id == null || version == null)
            {
                return;
            }

            bool isListed = GetListed(document);
            bool isSemVer2 = IsSemVer2(document);

            Update(isListed, isSemVer2, readerName, perSegmentDocumentNumber, id, version);
        }

        private void Update(bool isListed,
            bool isSemVer2,
            string readerName,
            int n,
            string id,
            NuGetVersion version)
        {
            if ((!_includeSemVer2 && isSemVer2) ||
                (!_includeUnlisted && isListed) ||
                (!_includePrerelease && version.IsPrerelease))
            {
                return;
            }

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

        internal static bool GetListed(Document document)
        {
            string listed = document.Get("Listed");
            return (listed == null) ? false : listed.Equals("true", StringComparison.InvariantCultureIgnoreCase);
        }

        internal static bool IsSemVer2(Document document)
        {
            string semVerLevel = document.Get("SemVerLevel");
            return (semVerLevel == null) ? false : semVerLevel.Equals("2");
        }
    }
}
