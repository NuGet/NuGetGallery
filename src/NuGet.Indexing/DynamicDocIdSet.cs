// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Search;

namespace NuGet.Indexing
{
    public class DynamicDocIdSet : DocIdSet
    {
        public SortedSet<int> DocIds { get; private set; }

        public DynamicDocIdSet()
        {
            DocIds = new SortedSet<int>();
        }

        public override DocIdSetIterator Iterator()
        {
            return new DynamicDocIdSetIterator(DocIds);
        }

        public class DynamicDocIdSetIterator : DocIdSetIterator
        {
            private readonly int[] _docIds;
            private int _current = -1;

            public DynamicDocIdSetIterator(SortedSet<int> docIds)
            {
                _docIds = docIds.ToArray();
            }

            public override int DocID()
            {
                if (_current == -1)
                {
                    return -1; // start of the iteration
                }

                if (_current < _docIds.Length)
                {
                    return _docIds[_current];
                }

                return DocIdSetIterator.NO_MORE_DOCS;
            }

            public override int NextDoc()
            {
                ++_current;
                return DocID();
            }

            public override int Advance(int target)
            {
                while (_current < _docIds.Length && _docIds[_current] < target)
                {
                    _current++;
                }

                return DocID();
            }
        }
    }
}