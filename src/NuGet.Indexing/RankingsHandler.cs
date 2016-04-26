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
        private readonly IDictionary<string, int> _rankings;

        private IDictionary<string, Ranking[]> _rankingTuples;
        public RankingResult Result { get; private set; }

        public RankingsHandler(IDictionary<string, int> rankings)
        {
            _rankings = rankings;
        }

        public void Begin(IndexReader indexReader)
        {
            _rankingTuples = new Dictionary<string, Ranking[]>();

            if (indexReader.GetSequentialSubReaders() != null)
            {
                foreach (SegmentReader segmentReader in indexReader.GetSequentialSubReaders())
                {
                    _rankingTuples.Add(segmentReader.SegmentName, new Ranking[segmentReader.MaxDoc]);
                }
            }
            else
            {
                _rankingTuples.Add(string.Empty, new Ranking[indexReader.MaxDoc]);
            }
        }

        public void End(IndexReader indexReader)
        {
            Result = new RankingResult(_rankings.Count, _rankingTuples);
        }

        public void Process(IndexReader indexReader, string readerName, int documentNumber, Document document, string id, NuGetVersion version)
        {
            _rankingTuples[readerName][documentNumber] = GetRanking(_rankings, id);
        }

        public static Ranking GetRanking(IDictionary<string, int> rankings, string id)
        {
            int rank = 0;
            if (string.IsNullOrEmpty(id) || !rankings.TryGetValue(id, out rank))
            {
                return null;
            }

            return new Ranking { Id = String.Intern(id), Rank = rank };
        }

        public class RankingResult
        {
            public RankingResult(int count, IDictionary<string, Ranking[]> documentRankings)
            {
                Count = count;
                DocumentRankings = documentRankings;
            }

            public int Count { get; private set; }
            public IDictionary<string, Ranking[]> DocumentRankings { get; private set; }
        }

        public class Ranking
        {
            public string Id { get; set; }
            public int Rank { get; set; }
        }
    }
}