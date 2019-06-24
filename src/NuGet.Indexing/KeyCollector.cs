// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Search;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public class KeyCollector : Collector
    {
        private int[] _keys;
        private int[] _checksums;
        private IList<DocumentKey> _pairs;

        public KeyCollector(IList<DocumentKey> pairs)
        {
            _pairs = pairs;
        }

        public override bool AcceptsDocsOutOfOrder
        {
            get { return true; }
        }

        public override void Collect(int docID)
        {
            _pairs.Add(new DocumentKey(_keys[docID], docID, _checksums[docID]));
        }

        public override void SetNextReader(Lucene.Net.Index.IndexReader reader, int docBase)
        {
            _keys = FieldCache_Fields.DEFAULT.GetInts(reader, "Key");
            _checksums = FieldCache_Fields.DEFAULT.GetInts(reader, "Checksum");
        }

        public override void SetScorer(Scorer scorer)
        {
        }
    }

    public class DocumentKey
    {
        public int PackageKey { get; private set; }
        public int DocumentId { get; private set; }
        public int Checksum { get; private set; }

        public DocumentKey(int packageKey, int documentId, int checksum)
        {
            PackageKey = packageKey;
            DocumentId = documentId;
            Checksum = checksum;
        }
    }
}
