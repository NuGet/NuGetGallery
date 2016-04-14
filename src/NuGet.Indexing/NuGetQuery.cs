// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;
using Lucene.Net.Index;
using Lucene.Net.Search;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NuGet.Indexing
{
    public static class NuGetQuery
    {
        public static Query MakeQuery(string q)
        {
            return MakeQuery(q, null);
        }

        public static Query MakeQuery(string q, NuGetIndexSearcher searcher)
        {
            var grouping = new Dictionary<string, HashSet<string>>();
            foreach (Clause clause in MakeClauses(Tokenize(q)))
            {
                HashSet<string> text;
                if (!grouping.TryGetValue(clause.Field, out text))
                {
                    text = new HashSet<string>();
                    grouping.Add(clause.Field, text);
                }
                text.Add(clause.Text);
            }

            if (grouping.Count == 0)
            {
                return new MatchAllDocsQuery();
            }

            return ConstructQuery(grouping, searcher);
        }

        // Lucene Query creation logic

        private static Query ConstructQuery(Dictionary<string, HashSet<string>> clauses, NuGetIndexSearcher searcher)
        {
            Analyzer analyzer = new PackageAnalyzer();

            List<Filter> filters = new List<Filter>();

            BooleanQuery booleanQuery = new BooleanQuery();
            foreach (var clause in clauses)
            {
                switch (clause.Key.ToLowerInvariant())
                {
                    case "id":
                        IdClause(booleanQuery, analyzer, clause.Value, Occur.MUST);
                        break;
                    case "packageid":
                        PackageIdClause(booleanQuery, analyzer, clause.Value);
                        break;
                    case "version":
                        VersionClause(booleanQuery, analyzer, clause.Value, Occur.MUST);
                        break;
                    case "title":
                        TitleClause(booleanQuery, analyzer, clause.Value, Occur.MUST);
                        break;
                    case "description":
                        DescriptionClause(booleanQuery, analyzer, clause.Value, Occur.MUST);
                        break;
                    case "tag":
                    case "tags":
                        TagClause(booleanQuery, analyzer, clause.Value, Occur.MUST);
                        break;
                    case "author":
                    case "authors":
                        AuthorClause(booleanQuery, analyzer, clause.Value, Occur.MUST);
                        break;
                    case "summary":
                        SummaryClause(booleanQuery, analyzer, clause.Value, Occur.MUST);
                        break;
                    case "owner":
                    case "owners":
                        if (searcher != null)
                        {
                            filters.AddRange(OwnerFilters(searcher.Owners, clause.Value));
                        }
                        break;
                    default:
                        AnyClause(booleanQuery, analyzer, clause.Value);
                        
                        if (searcher != null)
                        {
                            var ownerFilters = OwnerFilters(searcher.Owners, clause.Value).ToList();
                            if (ownerFilters.Any())
                            {
                                booleanQuery.Add(ConstructFilteredQuery(new MatchAllDocsQuery(), ownerFilters), Occur.SHOULD);
                            }
                        }
                        
                        break;
                }
            }

            // Determine if we have added any clauses - if not, match all docs
            Query query = booleanQuery;
            if (!booleanQuery.Clauses.Any())
            {
                query = new MatchAllDocsQuery();
            }

            // Any filters to add?
            query = ConstructFilteredQuery(query, filters);

            return query;
        }

        private static Query ConstructFilteredQuery(Query query, List<Filter> filters)
        {
            if (filters.Count == 1)
            {
                return new FilteredQuery(query, filters[0]);
            }
            else if (filters.Count > 1)
            {
                return new FilteredQuery(query, new ChainedFilter(filters.ToArray()));
            }

            return query;
        }

        private static Query ConstructClauseQuery(Analyzer analyzer, string field, IEnumerable<string> values, Occur occur = Occur.SHOULD, float queryBoost = 1.0f, float termBoost = 1.0f)
        {
            BooleanQuery query = new BooleanQuery();
            foreach (string text in values)
            {
                Query termQuery = ExecuteAnalyzer(analyzer, field, text);
                termQuery.Boost = termBoost;
                query.Add(termQuery, occur);
            }
            query.Boost = queryBoost;
            return query;
        }

        private static void IdClause(BooleanQuery query, Analyzer analyzer, IEnumerable<string> values, Occur occur)
        {
            if (occur == Occur.MUST)
            {
                BooleanQuery subQuery = new BooleanQuery();
                query.Add(subQuery, Occur.MUST);
                query = subQuery;
            }

            query.Add(ConstructClauseQuery(analyzer, "Id", values, Occur.SHOULD, 8.0f), Occur.SHOULD);
            query.Add(ConstructClauseQuery(analyzer, "ShingledId", values), Occur.SHOULD);
            query.Add(ConstructClauseQuery(analyzer, "TokenizedId", values), Occur.SHOULD);
            if (values.Count() > 1)
            {
                query.Add(ConstructClauseQuery(analyzer, "TokenizedId", values, Occur.MUST, 4.0f), Occur.SHOULD);
            }
        }

        private static void PackageIdClause(BooleanQuery query, Analyzer analyzer, IEnumerable<string> values)
        {
            query.Add(ConstructClauseQuery(analyzer, "Id", values), Occur.MUST);
        }

        private static void VersionClause(BooleanQuery query, Analyzer analyzer, IEnumerable<string> values, Occur occur)
        {
            query.Add(ConstructClauseQuery(analyzer, "Version", values), occur);
        }

        private static void TitleClause(BooleanQuery query, Analyzer analyzer, IEnumerable<string> values, Occur occur)
        {
            if (occur == Occur.MUST)
            {
                BooleanQuery subQuery = new BooleanQuery();
                query.Add(subQuery, Occur.MUST);
                query = subQuery;
            }

            query.Add(ConstructClauseQuery(analyzer, "Title", values, Occur.SHOULD), Occur.SHOULD);
            if (values.Count() > 1)
            {
                query.Add(ConstructClauseQuery(analyzer, "Title", values, Occur.MUST, 4.0f), Occur.SHOULD);
            }
        }

        private static void DescriptionClause(BooleanQuery query, Analyzer analyzer, IEnumerable<string> values, Occur occur)
        {
            query.Add(ConstructClauseQuery(analyzer, "Description", values), occur);
        }

        private static void SummaryClause(BooleanQuery query, Analyzer analyzer, IEnumerable<string> values, Occur occur)
        {
            query.Add(ConstructClauseQuery(analyzer, "Summary", values), occur);
        }

        private static void TagClause(BooleanQuery query, Analyzer analyzer, IEnumerable<string> values, Occur occur)
        {
            query.Add(ConstructClauseQuery(analyzer, "Tags", values, Occur.SHOULD, 2.0f), occur);
        }

        private static void AuthorClause(BooleanQuery query, Analyzer analyzer, IEnumerable<string> values, Occur occur)
        {
            query.Add(ConstructClauseQuery(analyzer, "Authors", values), occur);
        }

        private static IEnumerable<Filter> OwnerFilters(
            OwnersHandler.OwnersResult owners, 
            HashSet<string> value)
        {
            foreach (var owner in value)
            {
                if (owners.KnownOwners.Contains(owner)) // don't filter if we have no such owner
                {
                    yield return new OwnersFilter(owners, owner);
                }
            }
        }

        private static void AnyClause(BooleanQuery query, Analyzer analyzer, IEnumerable<string> values)
        {
            IdClause(query, analyzer, values, Occur.SHOULD);
            VersionClause(query, analyzer, values, Occur.SHOULD);
            TitleClause(query, analyzer, values, Occur.SHOULD);
            DescriptionClause(query, analyzer, values, Occur.SHOULD);
            SummaryClause(query, analyzer, values, Occur.SHOULD);
            TagClause(query, analyzer, values, Occur.SHOULD);
            AuthorClause(query, analyzer, values, Occur.SHOULD);
        }

        static Query ExecuteAnalyzer(Analyzer analyzer, string field, string text)
        {
            TokenStream tokenStream = analyzer.TokenStream(field, new StringReader(text));

            ITermAttribute termAttribute = tokenStream.AddAttribute<ITermAttribute>();
            IPositionIncrementAttribute positionIncrementAttribute = tokenStream.AddAttribute<IPositionIncrementAttribute>();

            List<List<Term>> terms = new List<List<Term>>();
            List<Term> current = null;
            while (tokenStream.IncrementToken())
            {
                if (positionIncrementAttribute.PositionIncrement > 0)
                {
                    current = new List<Term>();
                    terms.Add(current);
                }
                if (current != null)
                {
                    current.Add(new Term(field, termAttribute.Term));
                }
            }

            if (terms.Count == 1 && terms[0].Count == 1)
            {
                return new TermQuery(terms[0][0]);
            }
            else if (terms.Select(l => l.Count).Sum() == terms.Count)
            {
                PhraseQuery phraseQuery = new PhraseQuery();
                foreach (var positionList in terms)
                {
                    phraseQuery.Add(positionList[0]);
                }
                return phraseQuery;
            }
            else
            {
                MultiPhraseQuery multiPhraseQuery = new MultiPhraseQuery();
                foreach (var positionList in terms)
                {
                    multiPhraseQuery.Add(positionList.ToArray());
                }
                return multiPhraseQuery;
            }
        }

        // NuGet query tokenizing code

        class Token
        {
            public enum TokenType { Value, Keyword }
            public TokenType Type { get; set; }
            public string Value { get; set; }
        }

        static IEnumerable<Token> Tokenize(string s)
        {
            StringBuilder buf = new StringBuilder();

            int state = 0;

            foreach (char ch in s)
            {
                switch (state)
                {
                    case 0:
                        if (Char.IsWhiteSpace(ch))
                        {
                            if (buf.Length > 0)
                            {
                                yield return new Token { Type = Token.TokenType.Value, Value = buf.ToString() };
                                buf.Clear();
                            }
                        }
                        else if (ch == '"')
                        {
                            state = 1;
                        }
                        else if (ch == ':')
                        {
                            if (buf.Length > 0)
                            {
                                yield return new Token { Type = Token.TokenType.Keyword, Value = buf.ToString() };
                                buf.Clear();
                            }
                        }
                        else
                        {
                            buf.Append(ch);
                        }
                        break;
                    case 1:
                        if (ch == '"')
                        {
                            if (buf.Length > 0)
                            {
                                yield return new Token { Type = Token.TokenType.Value, Value = buf.ToString() };
                                buf.Clear();
                            }
                            state = 0;
                        }
                        else
                        {
                            buf.Append(ch);
                        }
                        break;
                }
            }

            if (buf.Length > 0)
            {
                yield return new Token { Type = Token.TokenType.Value, Value = buf.ToString() };
            }

            yield break;
        }

        class Clause
        {
            public string Field { get; set; }
            public string Text { get; set; }
        }

        static IEnumerable<Clause> MakeClauses(IEnumerable<Token> tokens)
        {
            string field = null;

            foreach (Token token in tokens)
            {
                if (token.Type == Token.TokenType.Keyword)
                {
                    field = token.Value;
                }
                else if (token.Type == Token.TokenType.Value)
                {
                    if (token.Value.ToLowerInvariant() == "and" || token.Value.ToLowerInvariant() == "or")
                    {
                        continue;
                    }
                    if (field != null)
                    {
                        yield return new Clause { Field = field, Text = token.Value };
                    }
                    else
                    {
                        yield return new Clause { Field = "*", Text = token.Value };
                    }
                    field = null;
                }
            }

            yield break;
        }
    }
}
