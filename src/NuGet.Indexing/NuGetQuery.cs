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

            return ConstructQuery(grouping);
        }

        // Lucene Query creation logic

        static Query ConstructQuery(Dictionary<string, HashSet<string>> clauses)
        {
            Analyzer analyzer = new PackageAnalyzer();

            BooleanQuery query = new BooleanQuery();
            foreach (var clause in clauses)
            {
                switch (clause.Key.ToLowerInvariant())
                {
                    case "id":
                    case "packageid":
                        IdClause(query, analyzer, clause.Value);
                        break;
                    case "version":
                        VersionClause(query, analyzer, clause.Value);
                        break;
                    case "title":
                        TitleClause(query, analyzer, clause.Value);
                        break;
                    case "description":
                        DescriptionClause(query, analyzer, clause.Value);
                        break;
                    case "tag":
                    case "tags":
                        TagClause(query, analyzer, clause.Value);
                        break;
                    case "author":
                    case "authors":
                        AuthorClause(query, analyzer, clause.Value);
                        break;
                    case "summary":
                        SummaryClause(query, analyzer, clause.Value);
                        break;
                    case "owner":
                    case "owners":
                        OwnerClause(query, analyzer, clause.Value);
                        break;
                    default:
                        AnyClause(query, analyzer, clause.Value);
                        break;
                }
            }

            return query;
        }

        static Query ConstructClauseQuery(Analyzer analyzer, string field, IEnumerable<string> values, Occur occur = Occur.SHOULD, float queryBoost = 1.0f, float termBoost = 1.0f)
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

        static void IdClause(BooleanQuery query, Analyzer analyzer, IEnumerable<string> values)
        {
            query.Add(ConstructClauseQuery(analyzer, "Id", values), Occur.SHOULD);
            query.Add(ConstructClauseQuery(analyzer, "ShingledId", values), Occur.SHOULD);
            query.Add(ConstructClauseQuery(analyzer, "TokenizedId", values), Occur.SHOULD);
            if (values.Count() > 1)
            {
                query.Add(ConstructClauseQuery(analyzer, "TokenizedId", values, Occur.MUST, 4.0f), Occur.SHOULD);
            }
        }

        static void VersionClause(BooleanQuery query, Analyzer analyzer, IEnumerable<string> values)
        {
            query.Add(ConstructClauseQuery(analyzer, "Version", values), Occur.SHOULD);
        }

        static void TitleClause(BooleanQuery query, Analyzer analyzer, IEnumerable<string> values)
        {
            query.Add(ConstructClauseQuery(analyzer, "Title", values, Occur.SHOULD), Occur.SHOULD);
            if (values.Count() > 1)
            {
                query.Add(ConstructClauseQuery(analyzer, "Title", values, Occur.MUST, 4.0f), Occur.SHOULD);
            }
        }

        static void DescriptionClause(BooleanQuery query, Analyzer analyzer, IEnumerable<string> values)
        {
            query.Add(ConstructClauseQuery(analyzer, "Description", values), Occur.SHOULD);
        }
        static void SummaryClause(BooleanQuery query, Analyzer analyzer, IEnumerable<string> values)
        {
            query.Add(ConstructClauseQuery(analyzer, "Summary", values), Occur.SHOULD);
        }

        static void TagClause(BooleanQuery query, Analyzer analyzer, IEnumerable<string> values)
        {
            query.Add(ConstructClauseQuery(analyzer, "Tags", values), Occur.SHOULD);
        }

        static void AuthorClause(BooleanQuery query, Analyzer analyzer, IEnumerable<string> values)
        {
            query.Add(ConstructClauseQuery(analyzer, "Authors", values), Occur.SHOULD);
        }

        static void OwnerClause(BooleanQuery query, Analyzer analyzer, IEnumerable<string> values)
        {
            query.Add(ConstructClauseQuery(analyzer, "Owner", values), Occur.SHOULD);
        }

        static void AnyClause(BooleanQuery query, Analyzer analyzer, IEnumerable<string> values)
        {
            IdClause(query, analyzer, values);
            VersionClause(query, analyzer, values);
            TitleClause(query, analyzer, values);
            DescriptionClause(query, analyzer, values);
            SummaryClause(query, analyzer, values);
            TagClause(query, analyzer, values);
            AuthorClause(query, analyzer, values);
            OwnerClause(query, analyzer, values);
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
