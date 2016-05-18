// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using NuGet.Versioning;

namespace NuGet.Indexing
{
    public class SegmentToMainReaderMappingHandler : IIndexReaderProcessorHandler
    {
        private readonly Dictionary<string, int[]> _mapping = new Dictionary<string, int[]>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int[]> Result
        {
            get
            {
                return _mapping;
            }
        }

        public bool SkipDeletes => true;

        public void Begin(IndexReader indexReader)
        {
            if (indexReader.GetSequentialSubReaders() != null)
            {
                foreach (SegmentReader segmentReader in indexReader.GetSequentialSubReaders())
                {
                    _mapping[segmentReader.SegmentName] = new int[segmentReader.MaxDoc];
                }
            }
            else
            {
                _mapping[string.Empty] = new int[indexReader.MaxDoc];
            }
        }

        public void End(IndexReader indexReader)
        {
        }

        public void Process(IndexReader indexReader,
            string readerName,
            int perSegmentDocumentNumber,
            int perIndexDocumentNumber,
            Document document,
            string id,
            NuGetVersion version)
        {
            _mapping[readerName][perSegmentDocumentNumber] = perIndexDocumentNumber;
        }
    }
}
