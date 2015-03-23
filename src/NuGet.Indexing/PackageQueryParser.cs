using Lucene.Net.Analysis;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using System.Collections.Generic;
using System;

namespace NuGet.Indexing
{
    public class PackageQueryParser : QueryParser
    {
        static IDictionary<string, string> Alternatives = new Dictionary<string, string>
        {
            { "id", "Id" },
            { "version", "Version" },
            { "tokenizedid", "TokenizedId" },
            { "shingledid", "ShingledId" },
            { "title", "Title" },
            { "description", "Description" },
            { "tag", "Tags" },
            { "tags", "Tags" },
            { "author", "Authors" },
            { "authors", "Authors" },
            { "owner", "Owners" },
            { "owners", "Owners" },
        };

        public PackageQueryParser(Lucene.Net.Util.Version matchVersion, string f, Analyzer a) :
            base(matchVersion, f, a)
        {
        }

        protected override Query GetPrefixQuery(string field, string termStr)
        {
            return BuildQuery(field, termStr, base.GetPrefixQuery);
        }

        protected override Query GetWildcardQuery(string field, string termStr)
        {
            return BuildQuery(field, termStr, base.GetWildcardQuery);
        }

        protected override Query GetFieldQuery(string field, string queryText, int slop)
        {
            return BuildQuery(field, queryText, (f, q) => base.GetFieldQuery(f, q, slop));
        }

        protected override Query GetFieldQuery(string field, string queryText)
        {
            return BuildQuery(field, queryText, base.GetFieldQuery);
        }

        private string Substitute(string fieldName)
        {
            string lowerCasedFieldName = fieldName.ToLowerInvariant();

            string subStitutedFieldName;
            if (Alternatives.TryGetValue(lowerCasedFieldName, out subStitutedFieldName))
            {
                return subStitutedFieldName;
            }
            return fieldName;
        }

        private Query BuildQuery(string field, string termStr, Func<string, string, Query> baseQueryBuilder)
        {
            // Just rewrite the field name
            return baseQueryBuilder(Substitute(field), termStr);
        }
    }
}

