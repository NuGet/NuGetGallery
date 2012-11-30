using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;
using Lucene.Net.Index;
using Lucene.Net.Search;
using System;
using System.Collections.Generic;
using System.IO;

namespace NuGetGallery
{
    internal class AnalysisHelper
    {
        // This is a simplified query builder which works for single Terms and single Phrases
        // Returns null, TermQuery, or PhraseQuery
        public static Lucene.Net.Search.Query GetFieldQuery(Analyzer analyzer, string field, string queryText)
        {
            TokenStream stream = analyzer.TokenStream(field, new StringReader(queryText));
            TokenFilter filter = new CachingTokenFilter(stream);
            filter.Reset();

            // This attribute way of getting token properties sucks, but it's the non-obsolete one.
            var attr1 = (TermAttribute)filter.GetAttribute(typeof(TermAttribute));
            var attr2 = (PositionIncrementAttribute)filter.GetAttribute(typeof(PositionIncrementAttribute));
            Func<string> getText = () => attr1 != null ? attr1.Term() : null;
            Func<int> getPositionIncrement = () => attr2 != null ? attr2.GetPositionIncrement() : 1;

            // 0 tokens?
            if (!filter.IncrementToken())
            {
                return null;
            }

            // 1 token?
            string token1 = getText();
            int position = 0;
            if (!filter.IncrementToken())
            {
                return new TermQuery(new Term(field, token1));
            }

            // many tokens - handle first token
            PhraseQuery ret = new PhraseQuery();
            ret.Add(new Term(field, token1));

            do
            {
                // handle rest of tokens
                string tokenNext = getText();
                position += getPositionIncrement();
                ret.Add(new Term(field, tokenNext), position);
            }
            while (filter.IncrementToken());

            return ret;
        }

        public static Lucene.Net.Search.Query GetMultiFieldQuery(Analyzer analyzer, IEnumerable<string> fields, string queryText)
        {
            BooleanQuery ret = new BooleanQuery();
            foreach (var field in fields)
            {
                var q = GetFieldQuery(analyzer, field, queryText);
                ret.Add(new BooleanClause(q, BooleanClause.Occur.SHOULD));
            }
            return ret;
        }
    }
}
