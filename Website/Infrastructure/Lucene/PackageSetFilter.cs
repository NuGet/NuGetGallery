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
        private HashSet<int> _packageRegistrationKeys;
        private int[] _cachedResult;

        // filterTo needs to be passed as IQueryable for decent perf.
        public PackageSetFilter(IQueryable<PackageRegistration> filterToPackageSet, Filter innerFilter)
        {
            // Workaround: Distinct
            // For now, The curated feeds table has duplicate entries for feed, package registration pairs (we have a bug to fix it). Consequently
            // we have to apply a distinct on the results.

            _innerFilter = innerFilter;
            _packageRegistrationKeys = new HashSet<int>();
            _packageRegistrationKeys.AddRange(filterToPackageSet.Select(p => p.Key).Distinct());
        }

        public override DocIdSet GetDocIdSet(IndexReader reader)
        {
            if (_cachedResult == null)
            {
                FieldSelector keySelector = new PackageRegistrationKeySelector();
                var set = _innerFilter.GetDocIdSet(reader).Iterator();
                var docId = set.NextDoc();
                var goodDocIds = new List<int>();
                while (docId != DocIdSetIterator.NO_MORE_DOCS)
                {
                    var document = reader.Document(docId, keySelector);
                    var key = document.GetField("PackageRegistrationKey").StringValue();
                    int keyVal = Int32.Parse(key, CultureInfo.InvariantCulture);
                    if (_packageRegistrationKeys.Contains(keyVal))
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

        class PackageRegistrationKeySelector : FieldSelector
        {
            public FieldSelectorResult Accept(string fieldName)
            {
                return fieldName.Equals("PackageRegistrationKey", StringComparison.Ordinal) ? FieldSelectorResult.LOAD_AND_BREAK : FieldSelectorResult.NO_LOAD;
            }
        }
    }
}
