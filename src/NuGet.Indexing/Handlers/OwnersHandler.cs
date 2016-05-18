// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using NuGet.Versioning;

namespace NuGet.Indexing
{
    public class OwnersHandler : IIndexReaderProcessorHandler
    {
        private readonly IDictionary<string, HashSet<string>> _owners;

        private HashSet<string> _knownOwners;
        private IDictionary<string, IDictionary<string, DynamicDocIdSet>> _ownerTuples;

        public OwnersResult Result { get; private set; }

        public OwnersHandler(IDictionary<string, HashSet<string>> owners)
        {
            _owners = owners;
        }

        public bool SkipDeletes => true;

        public void Begin(IndexReader indexReader)
        {
            _knownOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _ownerTuples = new Dictionary<string, IDictionary<string, DynamicDocIdSet>>();

            if (indexReader.GetSequentialSubReaders() != null)
            {
                foreach (SegmentReader segmentReader in indexReader.GetSequentialSubReaders())
                {
                    _ownerTuples.Add(segmentReader.SegmentName, new Dictionary<string, DynamicDocIdSet>(StringComparer.OrdinalIgnoreCase));
                }
            }
            else
            {
                _ownerTuples.Add(string.Empty, new Dictionary<string, DynamicDocIdSet>(StringComparer.OrdinalIgnoreCase));
            }
        }

        public void End(IndexReader indexReader)
        {
            Result = new OwnersResult(_knownOwners, _owners, _ownerTuples);
        }

        public void Process(IndexReader indexReader,
            string readerName,
            int perSegmentDocumentNumber,
            int perIndexDocumentNumber,
            Document document,
            string id,
            NuGetVersion version)
        {
            HashSet<string> registrationOwners;

            if (id != null && _owners.TryGetValue(id, out registrationOwners))
            {
                foreach (string registrationOwner in registrationOwners)
                {
                    _knownOwners.Add(registrationOwner);

                    DynamicDocIdSet ownerDocIdSet;
                    if (_ownerTuples[readerName].TryGetValue(registrationOwner, out ownerDocIdSet))
                    {
                        ownerDocIdSet.DocIds.Add(perSegmentDocumentNumber);
                    }
                    else
                    {
                        ownerDocIdSet = new DynamicDocIdSet();
                        ownerDocIdSet.DocIds.Add(perSegmentDocumentNumber);

                        _ownerTuples[readerName].Add(registrationOwner, ownerDocIdSet);
                    }
                }
            }
        }
    }
}