// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using NuGet.Versioning;

namespace NuGet.Indexing
{
    public class RankingsHandler : IIndexReaderProcessorHandler
    {
        private readonly IReadOnlyDictionary<string, int> _rankings;
        private RankingBySegment _rankingBySegmentReaderName;

        public RankingResult Result { get; private set; }

        public RankingsHandler(IReadOnlyDictionary<string, int> rankings)
        {
            if (rankings == null)
            {
                throw new ArgumentNullException(nameof(rankings));
            }

            _rankings = rankings;
        }

        public bool SkipDeletes => true;

        public void Begin(IndexReader indexReader)
        {
            _rankingBySegmentReaderName = new RankingBySegment();

            if (indexReader.GetSequentialSubReaders() != null)
            {
                foreach (SegmentReader segmentReader in indexReader.GetSequentialSubReaders())
                {
                    _rankingBySegmentReaderName[segmentReader.SegmentName] = new Ranking[segmentReader.MaxDoc];
                }
            }
            else
            {
                _rankingBySegmentReaderName[string.Empty] = new Ranking[indexReader.MaxDoc];
            }
        }

        public void End(IndexReader indexReader)
        {
            Result = new RankingResult(_rankings.Count, _rankingBySegmentReaderName);
        }

        public void Process(IndexReader indexReader,
            string readerName,
            int perSegmentDocumentNumber,
            int perIndexDocumentNumber,
            Document document,
            string id,
            NuGetVersion version)
        {
            _rankingBySegmentReaderName[readerName][perSegmentDocumentNumber] = GetRanking(_rankings, id);
        }

        public static Ranking GetRanking(IReadOnlyDictionary<string, int> rankings, string id)
        {
            if (rankings == null)
            {
                throw new ArgumentNullException(nameof(rankings));
            }

            int rank = 0;
            if (string.IsNullOrEmpty(id) || !rankings.TryGetValue(id, out rank))
            {
                return null;
            }

            return new Ranking { Id = string.Intern(id), Rank = rank };
        }
    }
}