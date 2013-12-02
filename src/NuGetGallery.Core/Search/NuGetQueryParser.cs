using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public static class NuGetQueryParser
    {
        public static string Parse(string nugetQuery)
        {
            if (string.IsNullOrWhiteSpace(nugetQuery))
            {
                return string.Empty;
            }

            StringBuilder luceneQuery = new StringBuilder();

            CreateFieldClause(luceneQuery, "Id", nugetQuery);
            CreateFieldClause(luceneQuery, "Version", nugetQuery);
            CreateFieldClause(luceneQuery, "TokenizedId", nugetQuery);
            CreateFieldClauseAND(luceneQuery, "TokenizedId", nugetQuery, 4);
            CreateFieldClause(luceneQuery, "ShingledId", nugetQuery);
            CreateFieldClause(luceneQuery, "Title", nugetQuery);
            CreateFieldClauseAND(luceneQuery, "Title", nugetQuery, 4);
            CreateFieldClause(luceneQuery, "Tags", nugetQuery);
            CreateFieldClause(luceneQuery, "Description", nugetQuery);
            CreateFieldClause(luceneQuery, "Authors", nugetQuery);
            CreateFieldClause(luceneQuery, "Owners", nugetQuery);

            return luceneQuery.ToString();
        }

        static void CreateFieldClause(StringBuilder luceneQuery, string field, string query)
        {
            List<string> subterms = GetTerms(query);
            if (subterms.Count > 0)
            {
                if (subterms.Count == 1)
                {
                    luceneQuery.AppendFormat("{0}:{1} ", field, subterms[0]);
                }
                else
                {
                    luceneQuery.AppendFormat("({0}:{1}", field, subterms[0]);
                    for (int i = 1; i < subterms.Count; i += 1)
                    {
                        luceneQuery.AppendFormat(" OR {0}:{1}", field, subterms[i]);
                    }
                    luceneQuery.Append(") ");
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

        private static List<string> GetTerms(string query)
        {
            List<string> result = new List<string>();

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
                    if (ch == ' ')
                    {
                        string s = query.Substring(start, i - start);
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            result.Add(s);
                        }
                        start = i + 1;
                    }
                }
            }

            string t = query.Substring(start, query.Length - start);
            if (!string.IsNullOrWhiteSpace(t))
            {
                result.Add(t);
            }

            return result;
        }
    }
}
