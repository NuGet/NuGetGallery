using System.Collections.Generic;
using System.Diagnostics;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;

namespace NuGetGallery.Infrastructure.Lucene
{
    /// <summary>
    /// Filter that returns only items in the intersection of the two input filters.
    /// </summary>
    public class IntersectionFilter : Filter
    {
        private Filter _filter2;
        private Filter _filter1;

        public IntersectionFilter(Filter filter1, Filter filter2)
        {
            _filter1 = filter1;
            _filter2 = filter2;
        }

        public override DocIdSet GetDocIdSet(IndexReader reader)
        {
            var set1 = _filter1.GetDocIdSet(reader).Iterator();
            var set2 = _filter2.GetDocIdSet(reader).Iterator();

            var intersection = new List<int>();
            int head1 = set1.NextDoc();
            int head2 = set2.Advance(head1);
            while (head1 != DocIdSetIterator.NO_MORE_DOCS && head2 != DocIdSetIterator.NO_MORE_DOCS)
            {
                if (head1 == head2)
                {
                    intersection.Add(head1);
                    head1 = set1.NextDoc();
                    head2 = set2.Advance(head1);
                }
                else if (head1 < head2)
                {
                    head1 = set1.Advance(head2);
                }
                else
                {
                    Debug.Assert(head1 > head2);
                    head2 = set2.Advance(head1);
                }
            }

            return new SortedVIntList(intersection.ToArray());
        }
    }
}