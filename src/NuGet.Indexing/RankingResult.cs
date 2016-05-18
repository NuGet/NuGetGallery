// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Indexing
{
    public class RankingResult
    {
        public RankingResult(int count, RankingBySegment documentRankings)
        {
            Count = count;
            DocumentRankings = documentRankings;
        }

        public int Count { get; }

        public RankingBySegment DocumentRankings { get; }
    }
}
