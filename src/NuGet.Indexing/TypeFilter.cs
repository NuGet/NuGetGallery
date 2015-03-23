using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;
using System;

namespace NuGet.Indexing
{
    //  this class should be equivallent to:
    //
    //    new QueryWrapperFilter(new TermQuery(new Term("@type", type)))
    //
    //  an alternative implementation might be to use that inline or subclass from it

    public class TypeFilter : QueryWrapperFilter
    {
        public TypeFilter(string[] types) : base(MakeQuery(types))
        {
        }

        static Query MakeQuery(string[] types)
        {
            if (types.Length == 1)
            {
                return new TermQuery(new Term("@type", types[0]));
            }
            else
            {
                BooleanQuery query = new BooleanQuery();
                for (int i = 0; i < types.Length; i++)
                {
                    query.Add(new BooleanClause(new TermQuery(new Term("@type", types[i])), Occur.SHOULD));
                }
                return query;
            }
        }
    }
}
