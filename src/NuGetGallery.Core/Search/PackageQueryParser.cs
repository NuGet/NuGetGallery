using Lucene.Net.Analysis;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class PackageQueryParser : QueryParser
    {
        static IDictionary<string, string> Alternatives = new Dictionary<string, string>
        {
            { "id", "Id" },
            { "version", "Version" },
            { "tokenizedid", "TokenizedId" },
            { "title", "Title" },
            { "description", "Description" },
            { "owner", "Owners" },
            { "tag", "Tags" },
            { "authors", "Authors" },
            { "owners", "Owners" },
            { "tags", "Tags" },
        };

        public PackageQueryParser(Lucene.Net.Util.Version matchVersion, string f, Analyzer a) :
            base(matchVersion, f, a)
        {
        }

        protected override Query GetPrefixQuery(string field, string termStr)
        {
            return base.GetPrefixQuery(Substitute(field), termStr);
        }

        protected override Query GetWildcardQuery(string field, string termStr)
        {
            return base.GetWildcardQuery(Substitute(field), termStr);
        }

        protected override Query GetFieldQuery(string field, string queryText, int slop)
        {
            return base.GetFieldQuery(Substitute(field), queryText, slop);
        }

        protected override Query GetFieldQuery(string field, string queryText)
        {
            return base.GetFieldQuery(Substitute(field), queryText);
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
    }
}

