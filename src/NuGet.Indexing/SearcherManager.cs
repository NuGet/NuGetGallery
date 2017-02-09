// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;

namespace NuGet.Indexing
{
    public class SearcherManager 
        : SearcherManager<IndexSearcher>
    {
        private readonly Directory _directory;

        public SearcherManager(Directory directory)
        {
            _directory = directory;
        }
        
        protected override Directory GetDirectory()
        {
            return _directory;
        }

        protected override IndexReader Reopen(IndexingConfiguration config, IndexSearcher searcher)
        {
            return searcher.IndexReader.Reopen();
        }
        
        protected override IndexSearcher CreateSearcher(IndexReader reader)
        {
            return new IndexSearcher(reader);
        }
    }
}
