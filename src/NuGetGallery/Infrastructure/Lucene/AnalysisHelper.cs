using System.Linq;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;
using Lucene.Net.Index;
using Lucene.Net.Search;
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

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

            // This attribute way of getting token properties isn't very good, but it's the non-obsolete one.
            var attr1 = filter.GetAttribute<ITermAttribute>();
            Func<string> getText = () => attr1 != null ? attr1.Term : null;

            Func<int> getPositionIncrement;
            if (filter.HasAttribute<IPositionIncrementAttribute>())
            {
                var attr = filter.GetAttribute<IPositionIncrementAttribute>();
                getPositionIncrement = () => attr.PositionIncrement;
            }
            else
            {
                getPositionIncrement = () => 1;
            }

            // 0 tokens
            if (!filter.IncrementToken())
            {
                return new BooleanQuery();
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
            // Return empty BooleanQuery if no clauses are generated
            BooleanQuery ret = new BooleanQuery();

            // Do a full loop per-field to allow for different fields using different analyzers
            foreach (var field in fields)
            {
                var q = GetFieldQuery(analyzer, field, queryText);
                if (q is BooleanQuery)
                {
                    Debug.Assert((q as BooleanQuery).Clauses.Count == 0,
                                 "Only *empty* boolean queries should be returned by GetFieldQuery");
                    continue;
                }
                else
                {
                    Debug.Assert(q != null, "GetFieldQuery should not return null");
                    ret.Add(new BooleanClause(q, Occur.SHOULD));
                }
            }

            return ret;
        }
    }
}
