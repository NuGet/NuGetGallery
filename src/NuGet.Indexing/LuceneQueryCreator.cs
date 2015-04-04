using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Util;
using Lucene.Net.Index;
using System.Text.RegularExpressions;
using System.Globalization;

namespace NuGet.Indexing
{
    public static class LuceneQueryCreator
    {
        private const string DefaultTermName = "__default";

        private static readonly ISet<string> AllowedNuGetFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "id",
            "packageid",
            "version",
            "author",
            "authors",
            "owner",
            "owners",
            "tag",
            "tags"
        };

        public static Query Parse(string inputQuery, bool rawLuceneQuery)
        {
            if (string.IsNullOrWhiteSpace(inputQuery))
            {
                return new MatchAllDocsQuery();
            }

            if (rawLuceneQuery)
            {
                return CreateRawQuery(inputQuery);
            }

            // Transform the query for Lucene parsing
            inputQuery = TransformQuery(inputQuery);

            // Parse the query our query parser
            PackageQueryParser parser = new PackageQueryParser(Lucene.Net.Util.Version.LUCENE_30, DefaultTermName, new PackageAnalyzer());
            Query query = parser.Parse(inputQuery);

            // Process the query into clauses
            List<BooleanClause> clausesCollector = new List<BooleanClause>();
            string nugetQuery = ExtractLuceneClauses(query, inputQuery, clausesCollector);

            // Rewrite Id clauses into "TokenizedId, ShingledId, Id" booleans
            IEnumerable<BooleanClause> luceneClauses = clausesCollector.Select(RewriteClauses(parser));

            // Now, take the nuget query, if there is one, and process it
            Query nugetParsedQuery = ParseNuGetQuery(nugetQuery);

            // Build the lucene clauses query
            BooleanQuery luceneQuery = BuildBooleanQuery(luceneClauses);

            // Build the final query
            return Combine(nugetParsedQuery, luceneQuery);
        }

        private static string TransformQuery(string inputQuery)
        {
            // Split on "Terms" and,
            // in each term, escape Lucene characters EXCEPT the first ":" in a field query
            return String.Join(" ", GetTerms(inputQuery, escape: false).Select(TransformTerm));
        }

        private static string TransformTerm(string term)
        {
            // If the term is not quoted and contains a colon that IS NOT at the immediate 
            // start or end, then it's a field query.
            //
            // For example:
            //  foo:bar - Field query
            //  :bar - Not a field query
            //  "foo bar" - Not a field query
            //  "foo:bar" - Not a field query
            //  bar: - Not a field query
            //  f:b - Field query.
            int colonIndex = term.IndexOf(':');

            // Quoted 
            if (term.Length >= 2 && term[0] == '\"' && term[term.Length - 1] == '\"')
            {
                // Leave the query entirely alone
                return term;
            }
            // Colon in the middle of the string
            else if(colonIndex > 0 && colonIndex < (term.Length - 1))
            {
                // Capture the field and value
                string field = term.Substring(0, colonIndex);
                string value = term.Substring(colonIndex + 1);
                if (!AllowedNuGetFields.Contains(field))
                {
                    // Field is invalid, discard it
                    return Escape(value);
                }
                else
                {
                    return
                        Escape(field) +
                        ":" +
                        Escape(value);
                }
            }
            // Leading/Trailing Colon or no colon, and unquoted.
            else
            {
                // Escape the whole dang thing
                return Escape(term);
            }
        }

        private static Func<BooleanClause, BooleanClause> RewriteClauses(QueryParser parser)
        {
            return clause =>
            {
                TermQuery tq = clause.Query as TermQuery;
                if (tq == null)
                {
                    // It's not a term query, leave it alone
                    return clause;
                }

                if (String.Equals(tq.Term.Field, "id", StringComparison.OrdinalIgnoreCase))
                {
                    // Users expect "id:foo" to be a substring search, so we need
                    // to rewrite that into "(Id:foo TokenizedId:foo ShingledId:foo)" in order
                    // to match our existing substring searching.
                    return new BooleanClause(
                        parser.Parse(
                            String.Format(
                                CultureInfo.InvariantCulture, 
                                "Id:{0} TokenizedId:{0} ShingledId:{0}", 
                                tq.Term.Text)), 
                            clause.Occur);
                }
                else if (String.Equals(tq.Term.Field, "packageid", StringComparison.OrdinalIgnoreCase))
                {
                    // PackageId is not a real field, it's just an alias for Id that bypasses
                    // the filter above and does a real exact-string match.
                    return new BooleanClause(new TermQuery(new Term("Id", tq.Term.Text)), clause.Occur);
                }
                else
                {
                    // All other fields pass through as-is
                    return clause;
                }
            };
        }

        public static Query CreateRawQuery(string q)
        {
            if (String.IsNullOrEmpty(q))
            {
                return null;
            }

            QueryParser parser = new PackageQueryParser(Lucene.Net.Util.Version.LUCENE_30, "Title", new PackageAnalyzer());
            Query query = parser.Parse(q);

            return query;
        }

        private static string ExtractLuceneClauses(Query query, string inputQuery, List<BooleanClause> luceneClauses)
        {
            BooleanQuery boolQuery = query as BooleanQuery;
            IEnumerable<Query> clauses = boolQuery == null ? new[] { query } : boolQuery.Clauses.Select(c => c.Query);

            // Process the query to extract the "nuget query" part and the "lucene filters" part
            StringBuilder nugetQuery = new StringBuilder();
            foreach (Query q in clauses)
            {
                TermQuery tq = q as TermQuery;
                bool handledQuery = false;
                if (tq != null && !AllowedNuGetFields.Contains(tq.Term.Field))
                {
                    // Ignore fields we don't accept in NuGet-style queries
                    // And, add in terms that aren't labelled with a field.
                    bool containsWhitespace = tq.Term.Text.Any(Char.IsWhiteSpace);
                    if (containsWhitespace)
                    {
                        nugetQuery.Append("\"");
                    }
                    nugetQuery.Append(tq.Term.Text);
                    if (containsWhitespace)
                    {
                        nugetQuery.Append("\"");
                    }
                    nugetQuery.Append(" ");
                    handledQuery = true;
                }
                else
                {
                    PhraseQuery pq = q as PhraseQuery;
                    if (pq != null)
                    {
                        Term[] terms = pq.GetTerms();
                        string[] fieldNames = terms
                            .Select(t => t.Field)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();
                        if (fieldNames.Length == 1 && !AllowedNuGetFields.Contains(fieldNames[0]))
                        {
                            // All the terms refer to the same field and it's 
                            // not a valid NuGet-style field
                            nugetQuery.AppendFormat("\"{0}\"", String.Join(" ", terms.Select(t => t.Text)));
                            nugetQuery.Append(" ");
                            handledQuery = true;
                        }
                    }
                }

                // Convert the clause to a MUST-have clause
                if (!handledQuery)
                {
                    luceneClauses.Add(new BooleanClause(q, Occur.MUST));
                }
            }

            if (nugetQuery.Length > 0)
            {
                nugetQuery.Length -= 1; // Strip trailing space.
            }
            return nugetQuery.ToString();
        }

        private static Query ParseNuGetQuery(string nugetQuery)
        {
            Query nugetParsedQuery = null;
            if (!String.IsNullOrEmpty(nugetQuery))
            {
                // Process the query
                StringBuilder luceneQuery = new StringBuilder();

                CreateFieldClause(luceneQuery, "Id", nugetQuery);
                CreateFieldClause(luceneQuery, "Version", nugetQuery);
                CreateFieldClause(luceneQuery, "TokenizedId", nugetQuery);
                CreateFieldClauseAND(luceneQuery, "TokenizedId", nugetQuery, 4);
                CreateFieldClause(luceneQuery, "ShingledId", nugetQuery);
                CreateFieldClause(luceneQuery, "Title", nugetQuery, 2);
                CreateFieldClauseAND(luceneQuery, "Title", nugetQuery, 4);
                CreateFieldClause(luceneQuery, "Tags", nugetQuery);
                CreateFieldClause(luceneQuery, "Description", nugetQuery);
                CreateFieldClause(luceneQuery, "Summary", nugetQuery);
                CreateFieldClause(luceneQuery, "Authors", nugetQuery);
                CreateFieldClause(luceneQuery, "Owners", nugetQuery);

                PackageQueryParser parser = new PackageQueryParser(Lucene.Net.Util.Version.LUCENE_30, "Title", new PackageAnalyzer());
                nugetParsedQuery = parser.Parse(luceneQuery.ToString());
            }
            return nugetParsedQuery;
        }

        private static BooleanQuery BuildBooleanQuery(IEnumerable<BooleanClause> luceneClauses)
        {
            BooleanQuery luceneQuery = null;
            if (luceneClauses.Any())
            {
                luceneQuery = new BooleanQuery();
                foreach (BooleanClause clause in luceneClauses)
                {
                    luceneQuery.Add(clause);
                }
            }
            return luceneQuery;
        }

        private static Query Combine(Query nugetParsedQuery, BooleanQuery luceneQuery)
        {
            if (nugetParsedQuery == null)
            {
                return luceneQuery;
            }
            else if (luceneQuery == null)
            {
                return nugetParsedQuery;
            }
            else
            {
                BooleanQuery q = new BooleanQuery();
                q.Clauses.Add(new BooleanClause(nugetParsedQuery, Occur.SHOULD));
                q.Clauses.AddRange(luceneQuery.Clauses);
                return q;
            }
        }

        static void CreateFieldClause(StringBuilder luceneQuery, string field, string query, float boost = 1.0f)
        {
            List<string> subterms = GetTerms(query);
            if (subterms.Count > 0)
            {
                if (subterms.Count == 1)
                {
                    luceneQuery.AppendFormat("{0}:{1}", field, subterms[0]);
                }
                else
                {
                    luceneQuery.AppendFormat("({0}:{1}", field, subterms[0]);
                    for (int i = 1; i < subterms.Count; i += 1)
                    {
                        luceneQuery.AppendFormat(" OR {0}:{1}", field, subterms[i]);
                    }
                    luceneQuery.Append(")");
                }

                if (boost != 1.0f)
                {
                    luceneQuery.AppendFormat("^{0} ", boost);
                }
                else
                {
                    luceneQuery.AppendFormat(" ");
                }
            }
        }

        private static void CreateFieldClauseAND(StringBuilder luceneQuery, string field, string query, float boost)
        {
            List<string> subterms = GetTerms(query);
            if (subterms.Count > 1)
            {
                luceneQuery.AppendFormat("({0}:{1}", field, subterms[0]);
                for (int i = 1; i < subterms.Count; i += 1)
                {
                    luceneQuery.AppendFormat(" AND {0}:{1}", field, subterms[i]);
                }
                luceneQuery.Append(')');
                if (boost != 1)
                {
                    luceneQuery.AppendFormat("^{0} ", boost);
                }
            }
        }

        private static List<string> GetTerms(string query, bool escape = true)
        {
            List<string> result = new List<string>();

            if (query.StartsWith("\"") && query.EndsWith("\""))
            {
                result.Add(Escape(query));
            }
            else
            {
                bool literal = false;
                int start = 0;
                for (int i = 0; i < query.Length; i++)
                {
                    char ch = query[i];
                    if (ch == '"')
                    {
                        literal = !literal;
                    }
                    if (!literal)
                    {
                        if (Char.IsWhiteSpace(ch))
                        {
                            string s = query.Substring(start, i - start);
                            if (!string.IsNullOrWhiteSpace(s))
                            {
                                result.Add(escape ? Escape(s) : s);
                            }
                            start = i + 1;
                        }
                    }
                }

                string t = query.Substring(start, query.Length - start);
                if (!string.IsNullOrWhiteSpace(t))
                {
                    result.Add(escape ? Escape(t) : t);
                }
            }

            return result;
        }

        private static string Quote(string term)
        {
            if (term.StartsWith("\"") && term.EndsWith("\""))
            {
                return term;
            }
            return string.Format("\"{0}\"", term);
        }

        private static string Escape(string term)
        {
            term = QueryParser.Escape(term);
            
            // Unescape quotes because we want them to be handled by Lucene
            term = term.Replace("\\\"", "\"");

            return term;
        }
    }
}
