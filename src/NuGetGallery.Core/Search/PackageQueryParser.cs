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
            { "Author", "Authors" },
            { "Owner", "Owners" },
            { "Tag", "Tags" }
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
            string subStitutedFieldName;
            if (Alternatives.TryGetValue(fieldName, out subStitutedFieldName))
            {
                return subStitutedFieldName;
            }
            return fieldName;
        }
    }
}

