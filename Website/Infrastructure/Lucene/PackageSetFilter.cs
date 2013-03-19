using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;

namespace NuGetGallery.Infrastructure.Lucene
{
    /// <summary>
    /// This filter contains a set of acceptable queryable package IDs, 
    /// it will look up all of the documents in the Lucene Index based on that complete set of package IDs
    /// and output a DocIdSet used to filter Lucene search results, restricting it to the input set.
    /// </summary>
    internal class PackageSetFilter : Filter
    {
        private readonly int[] _keys;

        // filterTo needs to be passed as IQueryable for decent perf.
        public PackageSetFilter(IQueryable<Package> filterTo)
        {
            if (filterTo == null)
            {
                throw new ArgumentNullException("filterTo");
            }

            _keys = filterTo.Select(p => p.Key).ToArray();
        }

        public override DocIdSet GetDocIdSet(IndexReader reader)
        {
            var docIds = new int[_keys.Length];

            for (int i = 0; i < _keys.Length; i++)
            {
                var docs = reader.TermDocs(new Term("Key", _keys[i].ToString(CultureInfo.InvariantCulture)));
                if (docs.Next())
                {
                    docIds[i] = docs.Doc();
                }
                else
                {
                    // We should nearly always be able to find a curated feed package in the lucene index, but it's possible the index is not up to date in which case we miss
                    docIds[i] = -1;
                }

                Debug.Assert(!docs.Next(), "There should not be multiple matching documents with the same package 'Key' in the index.");
            }

            var goodDocIds = docIds.Where(id => id != -1).ToArray();
            Array.Sort(goodDocIds);
            return new SortedVIntList(goodDocIds);
        }
    }
}
