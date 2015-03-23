using Lucene.Net.Index;
using Lucene.Net.Search;

namespace NuGet.Indexing
{
    public class PublicFilter : QueryWrapperFilter
    {
        public PublicFilter() : base(new TermQuery(new Term("Visibility", "Public")))
        {
        }
    }
}
