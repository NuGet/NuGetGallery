// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Function;

namespace NuGet.Indexing
{
    public class DownloadsBoostedQuery : CustomScoreQuery
    {
        const double BaseBoostConstant = 10.0;

        private readonly double _baseBoost;
        private readonly Downloads _downloads;
        private readonly RankingResult _ranking;
        private readonly IReadOnlyDictionary<string, int[]> _docIdMapping;
        private readonly QueryBoostingContext _context;

        public Query Query { get; }

        public DownloadsBoostedQuery(Query query,
            IReadOnlyDictionary<string, int[]> docIdMapping,
            Downloads downloads,
            RankingResult ranking,
            QueryBoostingContext context,
            double baseBoost = BaseBoostConstant)
            : base(query)
        {
            _docIdMapping = docIdMapping;
            _downloads = downloads;
            _baseBoost = baseBoost;
            _ranking = ranking;
            _context = context;

            Query = query;
        }

        protected override CustomScoreProvider GetCustomScoreProvider(IndexReader reader)
        {
            return new DownloadsScoreProvider(reader, _docIdMapping, _downloads, _ranking, _context, _baseBoost);
        }

        public override string ToString()
        {
            return "downloads(" + Query.ToString() + ")";
        }
    }
}