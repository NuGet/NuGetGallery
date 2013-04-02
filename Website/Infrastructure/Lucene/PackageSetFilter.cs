using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;
using NuGet;

namespace NuGetGallery.Infrastructure.Lucene
{
    /// <summary>
    /// This filter contains a set of acceptable queryable package IDs, 
    /// it will look up all of the documents in the Lucene Index based on that complete set of package IDs
    /// and output a DocIdSet used to filter Lucene search results, restricting it to the input set.
    /// </summary>
    internal class PackageSetFilter : Filter
    {
        private Filter _innerFilter;
        private HashSet<int> _keys;
        private int[] _cachedResult;

        // filterTo needs to be passed as IQueryable for decent perf.
        public PackageSetFilter(IQueryable<Package> filterToPackageSet, Filter innerFilter)
        {
            _innerFilter = innerFilter;
            _keys = new HashSet<int>();
            _keys.AddRange(filterToPackageSet.Select(p => p.Key));
        }

        public override DocIdSet GetDocIdSet(IndexReader reader)
        {
            if (_cachedResult == null)
            {
                FieldSelector keySelector = new KeySelector();
                var set = _innerFilter.GetDocIdSet(reader).Iterator();
                var docId = set.NextDoc();
                var goodDocIds = new List<int>();
                while (docId != DocIdSetIterator.NO_MORE_DOCS)
                {
                    var document = reader.Document(docId, keySelector);
                    var key = document.GetField("Key").StringValue();
                    int keyVal = Int32.Parse(key, CultureInfo.InvariantCulture);
                    if (_keys.Contains(keyVal))
                    {
                        goodDocIds.Add(docId);
                    }

                    docId = set.NextDoc();
                }

                _cachedResult = goodDocIds.ToArray();
                Array.Sort(_cachedResult);
            }

            return new SortedVIntList(_cachedResult);
        }

        class KeySelector : FieldSelector
        {
            public FieldSelectorResult Accept(string fieldName)
            {
                return fieldName.Equals("Key", StringComparison.Ordinal) ? FieldSelectorResult.LOAD_AND_BREAK : FieldSelectorResult.NO_LOAD;
            }
        }
    }
}
