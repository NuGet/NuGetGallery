// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Search;
using Xunit;

namespace NuGetGallery.Infrastructure
{
    public class AnalysisHelperFacts
    {
        [Fact]
        public void UnderstandsPhraseQueries()
        {
            var query = DoAnalysis(new NuGetSearchTerm {Field = "Description", TermOrPhrase = "package manager"});
            Assert.IsType<PhraseQuery>(query);
        }

        [Fact]
        public void UnderstandsTermQueries()
        {
            var query = DoAnalysis(new NuGetSearchTerm {Field = "Description", TermOrPhrase = "preeminent"});
            Assert.IsType<TermQuery>(query);
        }

        [Fact]
        public void UnderstandsEmptyQueries()
        {
            var query = DoAnalysis(new NuGetSearchTerm {Field = "Description", TermOrPhrase = ""});
            Assert.IsType<BooleanQuery>(query);
            Assert.Empty(((BooleanQuery)query).Clauses);
        }

        private Query DoAnalysis(NuGetSearchTerm st)
        {
            return AnalysisHelper.GetFieldQuery(new StandardAnalyzer(LuceneCommon.LuceneVersion), st.Field, st.TermOrPhrase);
        }
    }
}
